using Microsoft.Extensions.Primitives;

namespace loans_service.Middleware;

public class RequestIdMiddleware
{
    private const string HeaderName = "x-request-id";
    private readonly ILogger<RequestIdMiddleware> _logger;
    private readonly RequestDelegate _next;

    public RequestIdMiddleware(RequestDelegate next, ILogger<RequestIdMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Headers.TryGetValue(HeaderName, out var requestId) ||
            StringValues.IsNullOrEmpty(requestId))
        {
            requestId = Guid.NewGuid().ToString();
        }

        context.Items[HeaderName] = requestId;
        context.Response.Headers[HeaderName] = requestId;

        RequestContext.RequestId = requestId;
        try
        {
            using (_logger.BeginScope(new Dictionary<string, object> { ["RequestId"] = requestId.ToString() }))
            {
                await _next(context);
            }
        }
        finally
        {
            RequestContext.RequestId = null;
        }
    }
}

public static class RequestIdMiddlewareExtensions
{
    public static IApplicationBuilder UseRequestId(this IApplicationBuilder app)
    {
        return app.UseMiddleware<RequestIdMiddleware>();
    }
}
