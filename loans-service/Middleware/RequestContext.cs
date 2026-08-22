namespace loans_service.Middleware;

public static class RequestContext
{
    private static readonly AsyncLocal<string?> RequestIdAccessor = new();

    public static string? RequestId
    {
        get => RequestIdAccessor.Value;
        set => RequestIdAccessor.Value = value;
    }
}
