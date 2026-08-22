using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using loans_service.Events;
using loans_service.Search;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace loans_service.Consumers;

public class AccountEventsConsumer : BackgroundService
{
    private const string QueueName = "loans-service.account-events";
    private const string RoutingKeyPattern = "account.*";

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
            ?? "amqp://lending:lending@localhost:5672";

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await StartConsumingAsync(uri, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "RabbitMQ consumer failed, retrying in 5s");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
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

        await _channel.QueueDeclareAsync(
            QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: stoppingToken);

        await _channel.QueueBindAsync(
            QueueName,
            AccountEvent.ExchangeName,
            RoutingKeyPattern,
            cancellationToken: stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += OnReceivedAsync;

        await _channel.BasicConsumeAsync(
            queue: QueueName,
            autoAck: false,
            consumer: consumer,
            cancellationToken: stoppingToken);

        _logger.LogInformation(
            "Subscribed to {Pattern} on exchange {Exchange} via queue {Queue}",
            RoutingKeyPattern,
            AccountEvent.ExchangeName,
            QueueName);

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private async Task OnReceivedAsync(object sender, BasicDeliverEventArgs ea)
    {
        try
        {
            var json = Encoding.UTF8.GetString(ea.Body.ToArray());
            var accountEvent = JsonSerializer.Deserialize<AccountEvent>(json, JsonOptions);

            if (accountEvent is not null)
            {
                switch (accountEvent.EventType)
                {
                    case "account.deleted":
                        await _indexer.DeleteAsync(accountEvent.Data.Id, CancellationToken.None);
                        break;
                    default:
                        await _indexer.IndexAsync(accountEvent.Data, CancellationToken.None);
                        break;
                }

                _logger.LogInformation(
                    "Processed {EventType} for account {AccountId} ({Email})",
                    accountEvent.EventType,
                    accountEvent.Data.Id,
                    accountEvent.Data.Email);
            }

            if (_channel is not null)
            {
                await _channel.BasicAckAsync(ea.DeliveryTag, false);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process message, discarding");
            if (_channel is not null)
            {
                await _channel.BasicNackAsync(ea.DeliveryTag, false, requeue: false);
            }
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_channel is not null)
        {
            await _channel.CloseAsync(cancellationToken);
        }

        if (_connection is not null)
        {
            await _connection.CloseAsync(cancellationToken);
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
