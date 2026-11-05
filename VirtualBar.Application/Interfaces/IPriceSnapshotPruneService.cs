using VirtualBar.Application.Common;
using VirtualBar.Application.DTOs.Pricing;

namespace VirtualBar.Application.Interfaces;

/// <summary>Removes price snapshots that no pricing path reads any more (#58). Admin only.</summary>
public interface IPriceSnapshotPruneService
{
    /// <summary>
    /// Finds the orphaned snapshots: older than the TTL, and under a key that is neither the price key of a non-deleted
    /// bottle nor the canonical key of a non-deleted product. A dry run only reports them; otherwise they are deleted.
    /// </summary>
    Task<Result<PriceSnapshotPruneResultDto>> PruneAsync(bool dryRun, CancellationToken cancellationToken);
}
