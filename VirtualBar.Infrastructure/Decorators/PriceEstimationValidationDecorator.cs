using VirtualBar.Application.Common;
using VirtualBar.Application.DTOs.Pricing;
using VirtualBar.Application.Interfaces;

namespace VirtualBar.Infrastructure.Decorators;

/// <summary>
/// Validation guard for <see cref="IPriceEstimationService"/>. Honors cancellation up front and enforces
/// that a user can only request their own collection value (MVP: owner Dashboard + bottle detail panel).
/// </summary>
public sealed class PriceEstimationValidationDecorator(
    IPriceEstimationService inner,
    ICurrentUser currentUser) : IPriceEstimationService
{
    public async Task<Result<PriceEstimateDto>> GetBottleEstimateAsync(Guid bottleId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await inner.GetBottleEstimateAsync(bottleId, cancellationToken);
    }

    public async Task<Result<PriceEstimateDto>> GetCachedBottleEstimateAsync(Guid bottleId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await inner.GetCachedBottleEstimateAsync(bottleId, cancellationToken);
    }

    public async Task<Result<CollectionValueDto>> GetCollectionValueAsync(Guid userId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (userId != currentUser.UserId)
            return Result<CollectionValueDto>.Forbidden("You can only view your own collection value.");

        return await inner.GetCollectionValueAsync(userId, cancellationToken);
    }
}
