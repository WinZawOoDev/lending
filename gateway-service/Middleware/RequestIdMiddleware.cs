using Microsoft.AspNetCore.Http;

namespace gateway_service.Middleware;

public class RequestIdMiddleware
{
    private const string HeaderName = "x-request-id";
    private readonly RequestDelegate _next;

    public RequestIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Headers.ContainsKey(HeaderName))
        {
            context.Request.Headers[HeaderName] = Guid.NewGuid().ToString();
        }

        await _next(context);
    }
}

public static class RequestIdMiddlewareExtensions
{
    public static IApplicationBuilder UseRequestId(this IApplicationBuilder app)
    {
        return app.UseMiddleware<RequestIdMiddleware>();
    }
}
