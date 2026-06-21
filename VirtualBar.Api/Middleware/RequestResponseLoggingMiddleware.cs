using System.Diagnostics;
using System.Security.Claims;

namespace VirtualBar.Api.Middleware;

public sealed class RequestResponseLoggingMiddleware(RequestDelegate next, ILogger<RequestResponseLoggingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var request = context.Request;
        var stopwatch = Stopwatch.StartNew();

        logger.LogInformation(
            "→ {Method} {Path}{QueryString} | IP {RemoteIp}",
            request.Method,
            request.Path,
            request.QueryString.HasValue ? request.QueryString.ToString() : string.Empty,
            context.Connection.RemoteIpAddress);

        try
        {
            await next(context);
        }
        finally
        {
            stopwatch.Stop();

            var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anonymous";
            var statusCode = context.Response.StatusCode;
            var level = statusCode >= 500 ? LogLevel.Error
                      : statusCode >= 400 ? LogLevel.Warning
                      : LogLevel.Information;

            logger.Log(
                level,
                "← {Method} {Path} {StatusCode} | {ElapsedMs}ms | user:{UserId}",
                request.Method,
                request.Path,
                statusCode,
                stopwatch.ElapsedMilliseconds,
                userId);
        }
    }
}
