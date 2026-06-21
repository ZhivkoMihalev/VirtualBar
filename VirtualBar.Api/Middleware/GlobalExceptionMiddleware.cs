using System.Net.Mime;
using System.Text.Json;

namespace VirtualBar.Api.Middleware;

public sealed class GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            logger.LogDebug(
                "Request cancelled by client: {Method} {Path}",
                context.Request.Method,
                context.Request.Path);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Unhandled exception for {Method} {Path}",
                context.Request.Method,
                context.Request.Path);

            await WriteErrorResponseAsync(context);
        }
    }

    private static async Task WriteErrorResponseAsync(HttpContext context)
    {
        if (context.Response.HasStarted)
            return;

        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = MediaTypeNames.Application.Json;

        var body = JsonSerializer.Serialize(new
        {
            status = 500,
            title = "An unexpected error occurred.",
            traceId = context.TraceIdentifier,
        });

        await context.Response.WriteAsync(body);
    }
}
