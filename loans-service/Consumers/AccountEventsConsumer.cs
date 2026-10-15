using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using loans_service.Events;
using loans_service.Middleware;
using loans_service.Search;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;

namespace loans_service.Consumers;

/// <summary>
/// Thrown for messages that can never succeed (malformed payload, unknown
/// event type). They bypass retries and go straight to the dead-letter queue.
/// </summary>
public class PoisonMessageException(string message) : Exception(message);

public class AccountEventsConsumer : BackgroundService
{
    private const string QueueName = "loans-service.account-events";
    private const string RoutingKeyPattern = "account.*";
    private const string DlxExchangeName = "lending.events.dlx";
    private const string DlqName = "loans-service.account-events-dlq";
    private const int MaxDeliveryAttempts = 4;
    private const ushort PrefetchCount = 10;

    private static readonly HashSet<string> KnownEventTypes = new(StringComparer.Ordinal)
    {
        "account.created",
        "account.updated",
        "account.deleted",
    };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    private readonly ILogger<AccountEventsConsumer> _logger;
    private readonly IConfiguration _configuration;
    private readonly AccountIndexer _indexer;
    private IConnection? _connection;
    private IChannel? _channel;
    private CancellationToken _shutdownToken = CancellationToken.None;

    public AccountEventsConsumer(
        ILogger<AccountEventsConsumer> logger,
        IConfiguration configuration,
        AccountIndexer indexer)
    {
        _logger = logger;
        _configuration = configuration;
        _indexer = indexer;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var uri = _configuration["RabbitMQ:Uri"]
            ?? throw new InvalidOperationException("RabbitMQ:Uri is missing.");

        using var shutdownCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        _shutdownToken = shutdownCts.Token;

        var reconnectAttempt = 0;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await StartConsumingAsync(uri, stoppingToken);
                reconnectAttempt = 0;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                reconnectAttempt++;
                var delay = TimeSpan.FromSeconds(Math.Min(60, 5 * Math.Pow(2, reconnectAttempt - 1)));
                _logger.LogError(ex, "RabbitMQ consumer failed, retrying in {Delay}", delay);
                try
                {
                    await Task.Delay(delay, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }
    }

    private async Task StartConsumingAsync(string uri, CancellationToken stoppingToken)
    {
        var factory = new ConnectionFactory
        {
            Uri = new Uri(uri),
            AutomaticRecoveryEnabled = true,
        };

        _connection = await factory.CreateConnectionAsync(stoppingToken);
        _channel = await _connection.CreateChannelAsync(cancellationToken: stoppingToken);

        await _channel.ExchangeDeclareAsync(
            AccountEvent.ExchangeName,
            ExchangeType.Topic,
            durable: true,
            autoDelete: false,
            cancellationToken: stoppingToken);

        await DeclareDeadLetterTopologyAsync(stoppingToken);

        try
        {
            await _indexer.EnsureIndexAsync(stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to ensure Elasticsearch index; will retry on next reconnect");
        }

        await _channel.QueueDeclareAsync(
            QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: new Dictionary<string, object?>
            {
                ["x-dead-letter-exchange"] = DlxExchangeName,
            },
            cancellationToken: stoppingToken);

        await _channel.QueueBindAsync(
            QueueName,
            AccountEvent.ExchangeName,
            RoutingKeyPattern,
            cancellationToken: stoppingToken);

        // Bound in-flight deliveries so a slow Elasticsearch does not make the
        // client buffer unbounded amounts of unacknowledged messages.
        await _channel.BasicQosAsync(
            prefetchSize: 0,
            prefetchCount: PrefetchCount,
            global: false,
            cancellationToken: stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += OnReceivedAsync;

        await _channel.BasicConsumeAsync(
            queue: QueueName,
            autoAck: false,
            consumer: consumer,
            cancellationToken: stoppingToken);

        _logger.LogInformation(
            "Subscribed to {Pattern} on exchange {Exchange} via queue {Queue} (DLQ: {Dlq})",
            RoutingKeyPattern,
            AccountEvent.ExchangeName,
            QueueName,
            DlqName);

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private async Task DeclareDeadLetterTopologyAsync(CancellationToken stoppingToken)
    {
        await _channel!.ExchangeDeclareAsync(
            DlxExchangeName,
            ExchangeType.Topic,
            durable: true,
            autoDelete: false,
            cancellationToken: stoppingToken);

        await _channel.QueueDeclareAsync(
            DlqName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: stoppingToken);

        await _channel.QueueBindAsync(
            DlqName,
            DlxExchangeName,
            RoutingKeyPattern,
            cancellationToken: stoppingToken);
    }

    private async Task OnReceivedAsync(object sender, BasicDeliverEventArgs ea)
    {
        var channel = _channel;
        if (channel is null)
        {
            return;
        }

        var attempt = 0;
        while (true)
        {
            try
            {
                await ProcessMessageAsync(ea);
                await channel.BasicAckAsync(ea.DeliveryTag, false);
                return;
            }
            catch (PoisonMessageException ex)
            {
                _logger.LogError(ex, "Malformed or unsupported message; dead-lettering it");
                await SafeNackAsync(channel, ea, requeue: false);
                return;
            }
            catch (OperationCanceledException) when (_shutdownToken.IsCancellationRequested)
            {
                // Shutting down mid-processing: requeue so the message is not lost.
                await SafeNackAsync(channel, ea, requeue: true);
                return;
            }
            catch (Exception ex)
            {
                attempt++;
                if (attempt >= MaxDeliveryAttempts)
                {
                    _logger.LogError(
                        ex,
                        "Failed to process message after {Attempts} attempts; dead-lettering it",
                        attempt);
                    await SafeNackAsync(channel, ea, requeue: false);
                    return;
                }

                var delay = TimeSpan.FromMilliseconds(500 * Math.Pow(2, attempt - 1));
                _logger.LogWarning(
                    ex,
                    "Failed to process message (attempt {Attempt}/{MaxAttempts}); retrying in {Delay}",
                    attempt,
                    MaxDeliveryAttempts,
                    delay);
                try
                {
                    await Task.Delay(delay, _shutdownToken);
                }
                catch (OperationCanceledException)
                {
                    await SafeNackAsync(channel, ea, requeue: true);
                    return;
                }
            }
        }
    }

    private async Task ProcessMessageAsync(BasicDeliverEventArgs ea)
    {
        var json = Encoding.UTF8.GetString(ea.Body.ToArray());
        var accountEvent = JsonSerializer.Deserialize<AccountEvent>(json, JsonOptions);

        if (accountEvent is null || string.IsNullOrWhiteSpace(accountEvent.Data.Id))
        {
            throw new PoisonMessageException("Event payload is missing an account id.");
        }

        if (!KnownEventTypes.Contains(accountEvent.EventType))
        {
            throw new PoisonMessageException(
                $"Unknown event type '{accountEvent.EventType}'.");
        }

        CorrelationContext.CorrelationId = accountEvent.CorrelationId;
        try
        {
            switch (accountEvent.EventType)
            {
                case "account.deleted":
                    await _indexer.DeleteAsync(accountEvent.Data.Id, _shutdownToken);
                    break;
                default:
                    await _indexer.IndexAsync(accountEvent.Data, _shutdownToken);
                    break;
            }

            _logger.LogInformation(
                "Processed {EventType} for account {AccountId}",
                accountEvent.EventType,
                accountEvent.Data.Id);
        }
        finally
        {
            CorrelationContext.CorrelationId = null;
        }
    }

    private async Task SafeNackAsync(IChannel channel, BasicDeliverEventArgs ea, bool requeue)
    {
        try
        {
            await channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: requeue);
        }
        catch (Exception ex) when (ex is AlreadyClosedException or OperationInterruptedException)
        {
            _logger.LogWarning(
                "Could not nack delivery tag {DeliveryTag}; channel already closed. Message will be redelivered by the broker.",
                ea.DeliveryTag);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (_channel is not null)
            {
                await _channel.CloseAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Channel already closed during shutdown.");
        }

        try
        {
            if (_connection is not null)
            {
                await _connection.CloseAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Connection already closed during shutdown.");
        }

        await base.StopAsync(cancellationToken);
    }

    public override void Dispose()
    {
        _channel?.Dispose();
        _connection?.Dispose();
        base.Dispose();
    }
}
