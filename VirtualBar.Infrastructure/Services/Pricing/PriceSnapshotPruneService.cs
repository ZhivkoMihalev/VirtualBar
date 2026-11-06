using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using VirtualBar.Application.Common;
using VirtualBar.Application.DTOs.Pricing;
using VirtualBar.Application.Interfaces;
using VirtualBar.Application.Options;
using VirtualBar.Infrastructure.Persistence;

namespace VirtualBar.Infrastructure.Services.Pricing;

/// <summary>
/// Deletes the price snapshots no path reads any more (#58): those written under a bottle key before #50 for a bottle
/// that is now linked, and those of deleted bottles and products. A snapshot is kept when:
/// <list type="bullet">
///   <item>it is the price key of a non-deleted bottle (<see cref="PriceKey.ForBottle"/>, as every pricing path computes it);</item>
///   <item>it is the canonical key of a non-deleted product, which the product page reads even with no bottle linked;</item>
///   <item>it is still within the TTL: a key that comes back later then costs one research call, never wrong data.</item>
/// </list>
/// </summary>
public sealed class PriceSnapshotPruneService(AppDbContext db, IOptions<PricingOptions> pricingOptions)
    : IPriceSnapshotPruneService
{
    private const int SampleSize = 20;

    /// <summary>Ids per delete statement, well under SQL Server's 2100 parameters.</summary>
    private const int DeleteBatchSize = 500;

    public async Task<Result<PriceSnapshotPruneResultDto>> PruneAsync(bool dryRun, CancellationToken cancellationToken)
    {
        var cutoff = DateTime.UtcNow - TimeSpan.FromDays(pricingOptions.Value.SnapshotTtlDays);
        var liveKeys = await LiveKeysAsync(cancellationToken);

        var staleSnapshots = await db.PriceSnapshots
            .AsNoTracking()
            .Where(s => s.FetchedAt < cutoff)
            .Select(s => new { s.Id, s.ProductKey })
            .ToListAsync(cancellationToken);

        var orphans = staleSnapshots
            .Where(s => !liveKeys.Contains(s.ProductKey))
            .OrderBy(s => s.ProductKey, StringComparer.Ordinal)
            .ToList();

        var result = new PriceSnapshotPruneResultDto
        {
            DryRun = dryRun,
            TotalCount = await db.PriceSnapshots.CountAsync(cancellationToken),
            OrphanCount = orphans.Count,
            SampleKeys = orphans.Take(SampleSize).Select(s => s.ProductKey).ToList()
        };

        if (!dryRun)
        {
            foreach (var batch in orphans.Select(s => s.Id).Chunk(DeleteBatchSize))
            {
                // The age check again: a snapshot refreshed since it was read is fresh, so it stays.
                result.DeletedCount += await db.PriceSnapshots
                    .Where(s => batch.Contains(s.Id) && s.FetchedAt < cutoff)
                    .ExecuteDeleteAsync(cancellationToken);
            }
        }

        return Result<PriceSnapshotPruneResultDto>.Ok(result);
    }

    /// <summary>
    /// Every key a pricing path can read: the price key of each non-deleted bottle, loaded and computed in C# exactly as
    /// <see cref="PreWarmWorker"/> does (the key cannot be computed in SQL), plus each non-deleted product's canonical key.
    /// </summary>
    private async Task<HashSet<string>> LiveKeysAsync(CancellationToken cancellationToken)
    {
        var bottles = await db.Bottles
            .AsNoTracking()
            .Where(b => !b.IsDeleted)
            .Select(b => new
            {
                b.Name,
                DistilleryName = b.Distillery != null ? b.Distillery.Name : null,
                b.Category,
                b.Age,
                b.VintageYear,
                b.VolumeMl,
                ProductCanonicalKey = b.Product != null && !b.Product.IsDeleted ? b.Product.CanonicalKey : null
            })
            .ToListAsync(cancellationToken);

        var productKeys = await db.Products
            .AsNoTracking()
            .Where(p => !p.IsDeleted)
            .Select(p => p.CanonicalKey)
            .ToListAsync(cancellationToken);

        var keys = new HashSet<string>(productKeys, StringComparer.Ordinal);
        keys.UnionWith(bottles.Select(b =>
            PriceKey.ForBottle(b.ProductCanonicalKey, b.DistilleryName, b.Name, b.Category, b.Age, b.VintageYear, b.VolumeMl)));
        return keys;
    }
}
