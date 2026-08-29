using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.QueryDsl;
using loans_service.Middleware;
using System.Security.Claims;

namespace loans_service.Audit;

public class AuditService
{
    public const string IndexName = "loan-audit";

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
        var entry = new AuditEntry
        {
            EventType = eventType,
            Aggregate = aggregate,
            AggregateId = aggregateId,
            ActorId = _httpContextAccessor.HttpContext?.User.FindFirst("sub")?.Value,
            CorrelationId = CorrelationContext.CorrelationId,
            OccurredAt = DateTime.UtcNow,
            Before = before,
            After = after,
        };

        var response = await _client.IndexAsync(
            entry,
            i => i.Index(IndexName).Id(Guid.NewGuid().ToString()),
            cancellationToken);

        if (!response.IsSuccess())
        {
            throw new InvalidOperationException(
                $"Failed to record audit entry for {aggregate} {aggregateId}: {response.DebugInformation}");
        }

        _logger.LogInformation(
            "Recorded {EventType} for {Aggregate} {AggregateId} (correlation {CorrelationId})",
            eventType, aggregate, aggregateId, entry.CorrelationId);
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
            .Indices(IndexName)
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

    public async Task EnsureIndexAsync(CancellationToken cancellationToken)
    {
        var exists = await _client.Indices.ExistsAsync(IndexName, cancellationToken);

        if (exists.Exists)
        {
            return;
        }

        var response = await _client.Indices.CreateAsync<AuditEntry>(IndexName,
            c => c.Mappings(m => m
                .Properties(p => p
                    .Keyword(k => k.EventType)
                    .Keyword(k => k.Aggregate)
                    .Keyword(k => k.AggregateId)
                    .Keyword(k => k.ActorId)
                    .Keyword(k => k.CorrelationId)
                    .Date(d => d.OccurredAt))),
            cancellationToken);

        if (!response.IsSuccess())
        {
            throw new InvalidOperationException(
                $"Failed to create audit index {IndexName}: {response.DebugInformation}");
        }

        _logger.LogInformation("Created Elasticsearch index {Index}", IndexName);
    }
}
