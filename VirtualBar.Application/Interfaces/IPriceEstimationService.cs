using VirtualBar.Application.Common;
using VirtualBar.Application.DTOs.Pricing;

namespace VirtualBar.Application.Interfaces;

public interface IPriceEstimationService
{
    Task<Result<PriceEstimateDto>> GetBottleEstimateAsync(Guid bottleId, CancellationToken cancellationToken);

    /// <summary>
    /// Cache-only read for the request path: returns the cached estimate for the bottle, or a successful
    /// result with <c>null</c> data when no snapshot exists yet. Never triggers a synchronous (billed)
    /// Claude call — pre-warm or an async refresh populates the cache.
    /// </summary>
    Task<Result<PriceEstimateDto>> GetCachedBottleEstimateAsync(Guid bottleId, CancellationToken cancellationToken);

    /// <summary>
    /// Cache-only read of a catalog product's estimate: the snapshot stored under the product's canonical
    /// key, which every linked bottle shares. Successful result with <c>null</c> data when none is cached;
    /// <c>NotFound</c> for an unknown or deleted product. Never triggers a Claude call.
    /// </summary>
    Task<Result<PriceEstimateDto>> GetCachedProductEstimateAsync(Guid productId, CancellationToken cancellationToken);

    Task<Result<CollectionValueDto>> GetCollectionValueAsync(Guid userId, CancellationToken cancellationToken);
}
