using System.Security.Claims;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.IndexLifecycleManagement;
using Elastic.Clients.Elasticsearch.Mapping;
using Elastic.Clients.Elasticsearch.QueryDsl;
using loans_service.Middleware;

namespace loans_service.Audit;

public class AuditService
{
    public const string MutationIndex = "loan-audit";
    public const string ViewIlmPolicy = "loan-view-audit-retention";
    // ILM rollover requires a numbered backing index and a rollover_alias.
    public const string ViewBackingIndex = "loan-view-audit-000001";
    public const string ViewWriteAlias = "loan-view-audit-write";
    public const string ViewReadAlias = "loan-view-audit-read";

    private const int StartupAttempts = 8;
    private const int RetryBaseDelayMs = 1000;
    private const int RetryMaxDelayMs = 15000;

    private readonly ElasticsearchClient _client;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<AuditService> _logger;

    public AuditService(
        ElasticsearchClient client,
        IHttpContextAccessor httpContextAccessor,
        ILogger<AuditService> logger)
    {
        _client = client;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public async Task RecordAsync(
        string eventType,
        string aggregate,
        string aggregateId,
        object? before,
        object? after,
        CancellationToken cancellationToken)
    {
        var user = _httpContextAccessor.HttpContext?.User;

        var entry = new AuditEntry
        {
            EventType = eventType,
            Aggregate = aggregate,
            AggregateId = aggregateId,
            // The JWT handler maps "sub" to NameIdentifier by default, so check both.
            ActorId = user?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? user?.FindFirst("sub")?.Value,
            CorrelationId = CorrelationContext.CorrelationId,
            OccurredAt = DateTime.UtcNow,
            Before = before,
            After = after,
        };

        var isView = eventType is "loan.viewed" or "loan.view.denied";
        var index = isView ? ViewWriteAlias : MutationIndex;

        // Audit is fail-open: Elasticsearch problems are logged but never fail
        // the audited request, which has already committed its own state.
        if (await WriteAsync(entry, index, cancellationToken))
        {
            _logger.LogInformation(
                "Recorded {EventType} for {Aggregate} {AggregateId} (correlation {CorrelationId})",
                eventType, aggregate, aggregateId, entry.CorrelationId);
        }
        else
        {
            _logger.LogError(
                "Failed to record audit entry {EventType} for {Aggregate} {AggregateId} (correlation {CorrelationId}) — audit is degraded, the request continues",
                eventType, aggregate, aggregateId, entry.CorrelationId);
        }
    }

    private async Task<bool> WriteAsync(AuditEntry entry, string index, CancellationToken cancellationToken)
    {
        try
        {
            var response = await IndexAsync(entry, index, cancellationToken);
            if (response.IsSuccess())
            {
                return true;
            }

            // A missing index is self-healed by recreating it and retrying once.
            if (response.ElasticsearchServerError?.Error?.Type == "index_not_found_exception")
            {
                await EnsureIndexAsync(cancellationToken);

                response = await IndexAsync(entry, index, cancellationToken);
                if (response.IsSuccess())
                {
                    _logger.LogWarning(
                        "Audit index was missing — recreated it and flushed the pending entry");
                    return true;
                }
            }

            _logger.LogError(
                "Audit write to Elasticsearch failed: {DebugInformation}",
                response.DebugInformation);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Audit write to Elasticsearch failed: {EventType} for {Aggregate} {AggregateId}",
                entry.EventType, entry.Aggregate, entry.AggregateId);
            return false;
        }
    }

    private async Task<IndexResponse> IndexAsync(AuditEntry entry, string index, CancellationToken cancellationToken)
    {
        return await _client.IndexAsync(
            entry,
            i => i.Index(index).Id(Guid.NewGuid().ToString()),
            cancellationToken);
    }

    public async Task<(IReadOnlyList<AuditEntry> Hits, long Total)> SearchAsync(
        string? aggregateId,
        string? eventType,
        DateTime? from,
        DateTime? to,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var must = new List<Query>();

        if (!string.IsNullOrWhiteSpace(aggregateId))
        {
            must.Add(new TermQuery { Field = "aggregateId", Value = aggregateId });
        }

        if (!string.IsNullOrWhiteSpace(eventType))
        {
            must.Add(new TermQuery { Field = "eventType", Value = eventType });
        }

        if (from.HasValue || to.HasValue)
        {
            var range = new DateRangeQuery("occurredAt");
            if (from.HasValue) range.Gte = from.Value;
            if (to.HasValue) range.Lte = to.Value;
            must.Add(range);
        }

        Query query = must.Count > 0 ? new BoolQuery { Must = must } : new MatchAllQuery();

        var response = await _client.SearchAsync<AuditEntry>(s => s
            .Indices($"{MutationIndex},{ViewReadAlias}")
            .Query(query)
            .Sort(sort => sort
                .Field("occurredAt", f => f.Order(SortOrder.Desc)))
            .From((page - 1) * pageSize)
            .Size(pageSize),
            cancellationToken);

        if (!response.IsSuccess())
        {
            throw new InvalidOperationException(
                $"Failed to search audit trail: {response.DebugInformation}");
        }

        return (response.Hits.Select(h => h.Source).Where(h => h is not null).Cast<AuditEntry>().ToList(), response.Total);
    }

    // Startup path: retry with backoff and continue booting when Elasticsearch
    // stays unavailable; audit writes self-heal the index once it returns.
    public async Task EnsureOnStartupAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= StartupAttempts; attempt++)
        {
            try
            {
                await EnsureIndexAsync(cancellationToken);
                return;
            }
            catch (Exception ex) when (attempt < StartupAttempts)
            {
                var delay = Math.Min(RetryBaseDelayMs * (int)Math.Pow(2, attempt - 1), RetryMaxDelayMs);
                _logger.LogWarning(
                    ex,
                    "Elasticsearch not ready (attempt {Attempt}/{MaxAttempts}), retrying in {Delay}ms",
                    attempt, StartupAttempts, delay);
                await Task.Delay(delay, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Elasticsearch unavailable after {MaxAttempts} attempts — continuing without the audit indexes. Audit entries will be dropped until the indexes exist.",
                    StartupAttempts);
                return;
            }
        }
    }

    public async Task EnsureIndexAsync(CancellationToken cancellationToken)
    {
        await EnsureMutationIndexAsync(cancellationToken);
        await EnsureViewIndexAsync(cancellationToken);
    }

    private async Task EnsureMutationIndexAsync(CancellationToken cancellationToken)
    {
        var exists = await _client.Indices.ExistsAsync(MutationIndex, cancellationToken);

        if (exists.Exists)
        {
            return;
        }

        var response = await _client.Indices.CreateAsync<AuditEntry>(MutationIndex,
            c => c.Mappings(m => m.Properties(Properties)));

        if (!response.IsSuccess())
        {
            throw new InvalidOperationException(
                $"Failed to create audit index {MutationIndex}: {response.DebugInformation}");
        }

        _logger.LogInformation("Created Elasticsearch index {Index}", MutationIndex);
    }

    private async Task EnsureViewIndexAsync(CancellationToken cancellationToken)
    {
        var policyResponse = await _client.IndexLifecycleManagement.PutLifecycleAsync(
            new PutLifecycleRequest(ViewIlmPolicy)
            {
                Policy = new IlmPolicy
                {
                    Phases = new Phases
                    {
                        Hot = new Phase
                        {
                            Actions = new Actions
                            {
                                Rollover = new RolloverAction
                                {
                                    MaxSize = "1gb",
                                    MaxAge = "7d",
                                },
                            },
                        },
                        Delete = new Phase
                        {
                            MinAge = "30d",
                            Actions = new Actions
                            {
                                Delete = new DeleteAction(),
                            },
                        },
                    },
                },
            },
            cancellationToken);

        if (!policyResponse.IsSuccess())
        {
            throw new InvalidOperationException(
                $"Failed to create ILM policy {ViewIlmPolicy}: {policyResponse.DebugInformation}");
        }

        // Checking the write alias also matches an existing backing index created by this code.
        var exists = await _client.Indices.ExistsAsync(ViewWriteAlias, cancellationToken);

        if (exists.Exists)
        {
            return;
        }

        var createResponse = await _client.Indices.CreateAsync<AuditEntry>(ViewBackingIndex,
            c => c
                .Mappings(m => m.Properties(Properties))
                .Settings(s => s
                    .NumberOfShards(1)
                    .NumberOfReplicas(0)
                    .Lifecycle(l => l
                        .Name(ViewIlmPolicy)
                        .RolloverAlias(ViewWriteAlias)))
                .Aliases(a => a
                    .Add(ViewWriteAlias, al => al.IsWriteIndex(true))
                    .Add(ViewReadAlias, _ => { })));

        if (!createResponse.IsSuccess())
        {
            throw new InvalidOperationException(
                $"Failed to create audit index {ViewBackingIndex}: {createResponse.DebugInformation}");
        }

        _logger.LogInformation("Created Elasticsearch index {Index}", ViewBackingIndex);
    }

    private static Action<PropertiesDescriptor<AuditEntry>> Properties => p => p
        .Keyword(k => k.EventType)
        .Keyword(k => k.Aggregate)
        .Keyword(k => k.AggregateId)
        .Keyword(k => k.ActorId)
        .Keyword(k => k.CorrelationId)
        .Date(d => d.OccurredAt);
}
