using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VirtualBar.Application.Options;

namespace VirtualBar.Infrastructure.Services.Pricing;

/// <summary>
/// Periodic pre-warm hosted service (slice 06). When <c>RefreshEnabled</c>, it runs once on startup and
/// then every <c>RefreshIntervalHours</c>, resolving a fresh scope and delegating to <see cref="PreWarmWorker"/>
/// to research the most-owned canonical bottles that have a missing or stale snapshot — within the Anthropic
/// daily call budget. This decouples price coverage from user traffic. A failed run is logged and never kills
/// the loop; the service is cancellation-aware and stops cleanly on shutdown.
/// </summary>
public sealed class PreWarmRefreshJob(
    IServiceScopeFactory scopeFactory,
    IOptions<PricingOptions> pricingOptions,
    ILogger<PreWarmRefreshJob> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var pricing = pricingOptions.Value;

        // The single on/off switch for the background job.
        if (!pricing.RefreshEnabled)
        {
            logger.LogInformation("Price pre-warm job is disabled (RefreshEnabled = false).");
            return;
        }

        var interval = pricing.RefreshIntervalHours > 0
            ? TimeSpan.FromHours(pricing.RefreshIntervalHours)
            : TimeSpan.FromHours(24);

        using var timer = new PeriodicTimer(interval);

        do
        {
            await RunOnceAsync(stoppingToken);
        }
        while (await WaitForNextTickAsync(timer, stoppingToken));
    }

    private async Task RunOnceAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var worker = scope.ServiceProvider.GetRequiredService<PreWarmWorker>();
            await worker.PreWarmAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // Shutdown requested mid-run — let the loop exit on the next tick wait.
        }
        catch (Exception ex)
        {
            // Never let a single failed run kill the background loop.
            logger.LogError(ex, "Price pre-warm run failed; will retry on the next interval.");
        }
    }

    private static async Task<bool> WaitForNextTickAsync(PeriodicTimer timer, CancellationToken stoppingToken)
    {
        try
        {
            return await timer.WaitForNextTickAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}
