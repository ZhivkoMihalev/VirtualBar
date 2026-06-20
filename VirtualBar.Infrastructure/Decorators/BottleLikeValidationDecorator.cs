using Microsoft.EntityFrameworkCore;
using VirtualBar.Application.Common;
using VirtualBar.Application.Interfaces;
using VirtualBar.Infrastructure.Persistence;
using VirtualBar.Infrastructure.Services;

namespace VirtualBar.Infrastructure.Decorators;

public sealed class BottleLikeValidationDecorator(
    BottleLikeService inner,
    AppDbContext db,
    ICurrentUser currentUser) : IBottleLikeService
{
    public async Task<Result<bool>> LikeAsync(Guid bottleId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var bottle = await db.Bottles.FirstOrDefaultAsync(b => b.Id == bottleId && !b.IsDeleted, cancellationToken);
        if (bottle is null)
            return Result<bool>.NotFound("Bottle not found.");

        var alreadyLiked = await db.BottleLikes
            .AnyAsync(l => l.BottleId == bottleId && l.UserId == currentUser.UserId, cancellationToken);
        if (alreadyLiked)
            return Result<bool>.Conflict("Bottle already liked.");

        return await inner.LikeAsync(bottleId, cancellationToken);
    }

    public async Task<Result<bool>> UnlikeAsync(Guid bottleId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var bottle = await db.Bottles.FirstOrDefaultAsync(b => b.Id == bottleId && !b.IsDeleted, cancellationToken);
        if (bottle is null)
            return Result<bool>.NotFound("Bottle not found.");

        var liked = await db.BottleLikes
            .AnyAsync(l => l.BottleId == bottleId && l.UserId == currentUser.UserId, cancellationToken);
        if (!liked)
            return Result<bool>.Fail("Bottle is not liked.");

        return await inner.UnlikeAsync(bottleId, cancellationToken);
    }
}
