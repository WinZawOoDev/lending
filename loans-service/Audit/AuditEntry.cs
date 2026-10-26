namespace loans_service.Audit;

public class AuditEntry
{
    public string EventType { get; set; } = string.Empty;

    public string Aggregate { get; set; } = string.Empty;

    public string AggregateId { get; set; } = string.Empty;

    public string? ActorId { get; set; }

    public string? CorrelationId { get; set; }

    public DateTime OccurredAt { get; set; }

    public object? Before { get; set; }

    public object? After { get; set; }
}
