using System.Diagnostics;
using System.Security.Claims;

namespace OMS.API.Infrastructure.Middlewares;

public sealed class RequestLoggingMiddleware(
    RequestDelegate next,
    ILogger<RequestLoggingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext httpContext)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            await next(httpContext);
        }
        finally
        {
            stopwatch.Stop();

            var userId = GetUserId(httpContext.User);

            logger.LogInformation(
                "HTTP {HttpMethod} {RequestPath} responded {StatusCode} in {DurationMilliseconds} ms for user {UserId}",
                httpContext.Request.Method,
                httpContext.Request.Path.Value,
                httpContext.Response.StatusCode,
                stopwatch.Elapsed.TotalMilliseconds,
                userId);
        }
    }

    private static string? GetUserId(ClaimsPrincipal user)
    {
        if (user.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        return user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? user.FindFirstValue("sub");
    }
}
