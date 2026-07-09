using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VirtualBar.Application.Common;
using VirtualBar.Application.Interfaces;
using VirtualBar.Application.Options;
using VirtualBar.Domain.Enums;
using VirtualBar.Infrastructure.Persistence;

namespace VirtualBar.Infrastructure.Services.Pricing;

/// <summary>
/// The pre-warm worker (slice 06). Computes the most-owned canonical bottles whose price snapshot is
/// missing or stale (&gt; TTL) and proactively researches the top-<c>PreWarmTopNBottles</c> of them through
/// the orchestrator (which caches each result), so estimates exist before users ask. Coverage is thereby
/// decoupled from user traffic — but kept on a tight leash: the run is strictly bounded by the Anthropic
/// <c>DailyCallBudget</c> and stops as soon as it is spent. Idempotent and cancellation-aware — fresh
/// snapshots are never re-researched.
/// </summary>
public sealed class PreWarmWorker(
    AppDbContext db,
    IPriceEstimationService orchestrator,
    AnthropicDailyCallBudget callBudget,
    IOptions<PricingOptions> pricingOptions,
    ILogger<PreWarmWorker> logger)
{
    /// <summary>
    /// Researches the top-N stale/missing canonical bottles, within the shared Anthropic daily call budget.
    /// </summary>
    /// <returns>The number of canonical bottles researched in this run.</returns>
    public async Task<int> PreWarmAsync(CancellationToken cancellationToken)
    {
        var pricing = pricingOptions.Value;
        var topN = pricing.PreWarmTopNBottles;

        // Nothing to do when pre-warm is disabled or the shared daily budget is already spent.
        if (topN <= 0 || callBudget.Remaining() <= 0)
            return 0;

        var selected = await SelectStaleTopBottlesAsync(topN, pricing.SnapshotTtlDays, cancellationToken);

        var researched = 0;
        foreach (var bottleId in selected)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            // Daily-budget stop: the provider consumes from the same shared counter, so once it is spent
            // continuing is pointless — further research calls would only short-circuit to null.
            if (callBudget.Remaining() <= 0)
            {
                logger.LogInformation("Pre-warm stopped early: the Anthropic daily call budget is spent.");
                break;
            }

            try
            {
                // The orchestrator researches on a miss/stale snapshot and writes the result to the cache.
                await orchestrator.GetBottleEstimateAsync(bottleId, cancellationToken);
                researched++;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                // One bottle failing (transient DB/HTTP error, a lost upsert race, …) must not abort the
                // whole run — log it and continue researching the remaining top-N.
                logger.LogError(ex, "Pre-warm failed to research bottle {BottleId}; continuing with the rest.", bottleId);
            }
        }

        logger.LogInformation("Pre-warm researched {Count} canonical bottle(s).", researched);
        return researched;
    }

    /// <summary>
    /// Ranks every owned canonical bottle by ownership count, drops those with a still-fresh snapshot,
    /// and returns a representative bottle id for the top-<paramref name="topN"/> remaining.
    /// </summary>
    private async Task<List<Guid>> SelectStaleTopBottlesAsync(int topN, int ttlDays, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var ttl = TimeSpan.FromDays(ttlDays);

        // Load the minimal fields needed to compute each bottle's canonical product key.
        var bottles = await db.Bottles
            .AsNoTracking()
            .Where(b => !b.IsDeleted)
            .Select(b => new BottleKeyFields(
                b.Id,
                b.Name,
                b.Distillery != null ? b.Distillery.Name : null,
                b.Category,
                b.Age,
                b.VintageYear,
                b.VolumeMl))
            .ToListAsync(cancellationToken);

        if (bottles.Count == 0)
            return [];

        // Group by canonical key → ownership count + a representative bottle id (ProductKey is computed
        // in C#, so the grouping cannot run in the database).
        var groups = bottles
            .GroupBy(b => ProductKey.For(b.DistilleryName, b.Name, b.Category, b.Age, b.VintageYear, b.VolumeMl))
            .Select(g => new CanonicalGroup(g.Key, g.Count(), g.First().Id))
            .ToList();

        var keys = groups.Select(g => g.ProductKey).ToList();

        // Keys whose snapshot is still within TTL are skipped (TTL respect — no wasted budget call).
        var freshKeys = (await db.PriceSnapshots
                .AsNoTracking()
                .Where(s => keys.Contains(s.ProductKey))
                .Select(s => new SnapshotAge(s.ProductKey, s.FetchedAt))
                .ToListAsync(cancellationToken))
            .Where(s => now - s.FetchedAt <= ttl)
            .Select(s => s.ProductKey)
            .ToHashSet();

        return groups
            .Where(g => !freshKeys.Contains(g.ProductKey))
            .OrderByDescending(g => g.OwnershipCount)
            .ThenBy(g => g.ProductKey, StringComparer.Ordinal)
            .Take(topN)
            .Select(g => g.RepresentativeBottleId)
            .ToList();
    }

    private sealed record BottleKeyFields(
        Guid Id,
        string Name,
        string? DistilleryName,
        SpiritCategory Category,
        int? Age,
        int? VintageYear,
        int? VolumeMl);

    private sealed record CanonicalGroup(string ProductKey, int OwnershipCount, Guid RepresentativeBottleId);

    private sealed record SnapshotAge(string ProductKey, DateTime FetchedAt);
}
