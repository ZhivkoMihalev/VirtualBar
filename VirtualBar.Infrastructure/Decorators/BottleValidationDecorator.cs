using Microsoft.EntityFrameworkCore;
using VirtualBar.Application.Common;
using VirtualBar.Application.DTOs.Bottles;
using VirtualBar.Application.Interfaces;
using VirtualBar.Infrastructure.Persistence;
using VirtualBar.Infrastructure.Services;

namespace VirtualBar.Infrastructure.Decorators;

public sealed class BottleValidationDecorator(
    BottleService inner,
    AppDbContext db,
    ICurrentUser currentUser) : IBottleService
{
    public async Task<Result<List<BottleDto>>> GetBottlesByUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (userId == Guid.Empty)
            return Result<List<BottleDto>>.Fail("User ID is required.");

        return await inner.GetBottlesByUserAsync(userId, cancellationToken);
    }

    public async Task<Result<BottleDto>> GetBottleByIdAsync(Guid bottleId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return await inner.GetBottleByIdAsync(bottleId, cancellationToken);
    }

    public async Task<Result<BottleDto>> AddBottleAsync(AddBottleRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(request.Name))
            return Result<BottleDto>.Fail("Name is required.");

        return await inner.AddBottleAsync(request, cancellationToken);
    }

    public async Task<Result<BottleDto>> UpdateBottleAsync(Guid bottleId, UpdateBottleRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(request.Name))
            return Result<BottleDto>.Fail("Name is required.");

        var bottle = await db.Bottles.FirstOrDefaultAsync(b => b.Id == bottleId && !b.IsDeleted, cancellationToken);
        if (bottle is null)
            return Result<BottleDto>.NotFound("Bottle not found.");

        if (bottle.UserId != currentUser.UserId)
            return Result<BottleDto>.Forbidden("Forbidden.");

        return await inner.UpdateBottleAsync(bottleId, request, cancellationToken);
    }

    public async Task<Result<bool>> RemoveBottleAsync(Guid bottleId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var bottle = await db.Bottles.FirstOrDefaultAsync(b => b.Id == bottleId && !b.IsDeleted, cancellationToken);
        if (bottle is null)
            return Result<bool>.NotFound("Bottle not found.");

        if (bottle.UserId != currentUser.UserId)
            return Result<bool>.Forbidden("Forbidden.");

        return await inner.RemoveBottleAsync(bottleId, cancellationToken);
    }

    public async Task<Result<bool>> ListForSaleAsync(Guid bottleId, ListForSaleRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var bottle = await db.Bottles.FirstOrDefaultAsync(b => b.Id == bottleId && !b.IsDeleted, cancellationToken);
        if (bottle is null)
            return Result<bool>.NotFound("Bottle not found.");

        if (bottle.UserId != currentUser.UserId)
            return Result<bool>.Forbidden("Forbidden.");

        return await inner.ListForSaleAsync(bottleId, request, cancellationToken);
    }

    public async Task<Result<bool>> UnlistFromSaleAsync(Guid bottleId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var bottle = await db.Bottles.FirstOrDefaultAsync(b => b.Id == bottleId && !b.IsDeleted, cancellationToken);
        if (bottle is null)
            return Result<bool>.NotFound("Bottle not found.");

        if (bottle.UserId != currentUser.UserId)
            return Result<bool>.Forbidden("Forbidden.");

        if (!bottle.IsForSale)
            return Result<bool>.Fail("Bottle is not listed for sale.");

        return await inner.UnlistFromSaleAsync(bottleId, cancellationToken);
    }

    public async Task<Result<List<BottleDto>>> GetMarketplaceAsync(MarketplaceQuery query, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var sanitized = new MarketplaceQuery
        {
            Search   = string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim(),
            Category = query.Category,
            Sort     = query.Sort is "price_asc" or "price_desc" or "newest" ? query.Sort : null,
        };

        return await inner.GetMarketplaceAsync(sanitized, cancellationToken);
    }
}
