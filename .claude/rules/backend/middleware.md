---
paths:
  - "VirtualBar.Api/**"
---

# Middleware

Located in `VirtualBar.Api/Middleware/`. Registered in `Program.cs` in this exact order:

```
UseCors
→ RequestResponseLoggingMiddleware   (outermost logger)
  → GlobalExceptionMiddleware        (catches unhandled exceptions)
    → UseStaticFiles
    → UseRateLimiter                 (the auth rate limit, `RateLimiting:AuthPermitLimit`)
    → UseAuthentication
    → UseAuthorization
    → MapControllers
```

## RequestResponseLoggingMiddleware

Logs every HTTP request and response using Serilog (`ILogger<RequestResponseLoggingMiddleware>`):
- **`→`** on arrival — method, path, query string, remote IP
- **`←`** in `finally` — method, path, status code, elapsed ms, authenticated user ID

Log level varies by status code: `Information` (2xx/3xx), `Warning` (4xx), `Error` (5xx).

## GlobalExceptionMiddleware

Catches all unhandled exceptions that escape the controller/service layer:
- `OperationCanceledException` when client disconnected → `LogDebug`, no response written
- Any other `Exception` → `LogError` with full stack trace, returns `500` JSON:
  ```json
  { "status": 500, "title": "An unexpected error occurred.", "traceId": "..." }
  ```
- If response has already started streaming → does nothing (can't change headers)

**Order matters:** `GlobalExceptionMiddleware` is placed *inside* `RequestResponseLoggingMiddleware` so the logger reads the correct status code (500) after the exception middleware sets it.

## Serilog

Configured in `appsettings.json` under the `"Serilog"` key. Two sinks:
- **Console** — development output
- **File** — `logs/virtualbar-YYYYMMDD.log`, daily rotation, 7-day retention

Microsoft and EF Core namespaces are overridden to `Warning` to suppress noise.
`builder.Host.UseSerilog(...)` in `Program.cs` replaces the default .NET logging provider.
