using Microsoft.Extensions.Primitives;

namespace loans_service.Middleware;

public class CorrelationIdMiddleware
{
    private const string HeaderName = "x-correlation-id";
    private readonly ILogger<CorrelationIdMiddleware> _logger;
    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Headers.TryGetValue(HeaderName, out var correlationId) ||
            StringValues.IsNullOrEmpty(correlationId))
        {
            correlationId = Guid.NewGuid().ToString();
        }

        context.Items[HeaderName] = correlationId;
        context.Response.Headers[HeaderName] = correlationId;

        CorrelationContext.CorrelationId = correlationId;
        try
        {
            using (_logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId.ToString() }))
            {
                await _next(context);
            }
        }
        finally
        {
            CorrelationContext.CorrelationId = null;
        }
    }
}

public static class CorrelationIdMiddlewareExtensions
{
    public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder app)
    {
        return app.UseMiddleware<CorrelationIdMiddleware>();
    }
}
