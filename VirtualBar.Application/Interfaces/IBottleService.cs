using VirtualBar.Application.Common;
using VirtualBar.Application.DTOs.Bottles;

namespace VirtualBar.Application.Interfaces;

public interface IBottleService
{
    Task<Result<List<BottleDto>>> GetBottlesByUserAsync(Guid userId, CancellationToken cancellationToken);

    Task<Result<BottleDto>> GetBottleByIdAsync(Guid bottleId, CancellationToken cancellationToken);

    Task<Result<BottleDto>> AddBottleAsync(AddBottleRequest request, CancellationToken cancellationToken);

    Task<Result<BottleDto>> UpdateBottleAsync(Guid bottleId, UpdateBottleRequest request, CancellationToken cancellationToken);

    Task<Result<bool>> RemoveBottleAsync(Guid bottleId, CancellationToken cancellationToken);

    Task<Result<bool>> ReorderBottlesAsync(ReorderBottlesRequest request, CancellationToken cancellationToken);

    Task<Result<bool>> ListForSaleAsync(Guid bottleId, ListForSaleRequest request, CancellationToken cancellationToken);

    Task<Result<bool>> UnlistFromSaleAsync(Guid bottleId, CancellationToken cancellationToken);

    Task<Result<List<BottleDto>>> GetMarketplaceAsync(MarketplaceQuery query, CancellationToken cancellationToken);
}
