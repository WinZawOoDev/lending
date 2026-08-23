namespace loans_service.Middleware;

public static class CorrelationContext
{
    private static readonly AsyncLocal<string?> CorrelationIdAccessor = new();

    public static string? CorrelationId
    {
        get => CorrelationIdAccessor.Value;
        set => CorrelationIdAccessor.Value = value;
    }
}
