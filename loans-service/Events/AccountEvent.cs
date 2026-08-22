namespace loans_service.Events;

public class AccountEventData
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public decimal Balance { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}

public class AccountEvent
{
    public const string ExchangeName = "lending.events";

    public string EventId { get; set; } = string.Empty;

    public string EventType { get; set; } = string.Empty;

    public DateTime OccurredAt { get; set; }

    public AccountEventData Data { get; set; } = new();
}
