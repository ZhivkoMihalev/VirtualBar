using Microsoft.EntityFrameworkCore;
using VirtualBar.Application.Common;
using VirtualBar.Application.Interfaces;
using VirtualBar.Domain.Entities;
using VirtualBar.Infrastructure.Persistence;

namespace VirtualBar.Infrastructure.Services;

public sealed class BottleLikeService(
    AppDbContext db,
    ICurrentUser currentUser) : IBottleLikeService
{
    public async Task<Result<bool>> LikeAsync(Guid bottleId, CancellationToken cancellationToken)
    {
        var like = new BottleLike
        {
            BottleId = bottleId,
            UserId = currentUser.UserId,
            LikedAt = DateTime.UtcNow
        };

        db.BottleLikes.Add(like);
        await db.SaveChangesAsync(cancellationToken);

        return Result<bool>.Ok(true);
    }

    public async Task<Result<bool>> UnlikeAsync(Guid bottleId, CancellationToken cancellationToken)
    {
        var like = await db.BottleLikes
            .FirstOrDefaultAsync(l => l.BottleId == bottleId && l.UserId == currentUser.UserId, cancellationToken);

        db.BottleLikes.Remove(like!);
        await db.SaveChangesAsync(cancellationToken);

        return Result<bool>.Ok(true);
    }
}
