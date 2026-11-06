using VirtualBar.Application.Common;
using VirtualBar.Application.DTOs.Pricing;
using VirtualBar.Application.Interfaces;

namespace VirtualBar.Infrastructure.Decorators;

/// <summary>Validation guard for <see cref="IPriceSnapshotPruneService"/>: deleting snapshots is for admins only.</summary>
public sealed class PriceSnapshotPruneValidationDecorator(
    IPriceSnapshotPruneService inner,
    ICurrentUser currentUser) : IPriceSnapshotPruneService
{
    public async Task<Result<PriceSnapshotPruneResultDto>> PruneAsync(bool dryRun, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!currentUser.IsAdmin)
            return Result<PriceSnapshotPruneResultDto>.Forbidden("Only administrators can prune price snapshots.");

        return await inner.PruneAsync(dryRun, cancellationToken);
    }
}
