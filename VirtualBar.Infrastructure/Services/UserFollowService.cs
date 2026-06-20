using Microsoft.EntityFrameworkCore;
using VirtualBar.Application.Common;
using VirtualBar.Application.DTOs.Users;
using VirtualBar.Application.Interfaces;
using VirtualBar.Domain.Entities;
using VirtualBar.Infrastructure.Persistence;

namespace VirtualBar.Infrastructure.Services;

public sealed class UserFollowService(
    AppDbContext db,
    ICurrentUser currentUser) : IUserFollowService
{
    public async Task<Result<bool>> FollowAsync(Guid targetUserId, CancellationToken cancellationToken)
    {
        var follow = new UserFollow
        {
            FollowerId = currentUser.UserId,
            FollowedId = targetUserId,
            FollowedAt = DateTime.UtcNow
        };

        db.UserFollows.Add(follow);
        await db.SaveChangesAsync(cancellationToken);

        return Result<bool>.Ok(true);
    }

    public async Task<Result<bool>> UnfollowAsync(Guid targetUserId, CancellationToken cancellationToken)
    {
        var follow = await db.UserFollows
            .FirstOrDefaultAsync(f => f.FollowerId == currentUser.UserId && f.FollowedId == targetUserId, cancellationToken);

        db.UserFollows.Remove(follow!);
        await db.SaveChangesAsync(cancellationToken);

        return Result<bool>.Ok(true);
    }

    public async Task<Result<List<UserSummaryDto>>> GetFollowersAsync(Guid userId, CancellationToken cancellationToken)
    {
        var followers = await db.UserFollows
            .Where(f => f.FollowedId == userId)
            .Include(f => f.Follower)
            .OrderByDescending(f => f.FollowedAt)
            .Select(f => new UserSummaryDto
            {
                Id = f.Follower.Id,
                DisplayName = f.Follower.DisplayName,
                AvatarUrl = f.Follower.AvatarUrl
            })
            .ToListAsync(cancellationToken);

        return Result<List<UserSummaryDto>>.Ok(followers);
    }

    public async Task<Result<List<UserSummaryDto>>> GetFollowingAsync(Guid userId, CancellationToken cancellationToken)
    {
        var following = await db.UserFollows
            .Where(f => f.FollowerId == userId)
            .Include(f => f.Followed)
            .OrderByDescending(f => f.FollowedAt)
            .Select(f => new UserSummaryDto
            {
                Id = f.Followed.Id,
                DisplayName = f.Followed.DisplayName,
                AvatarUrl = f.Followed.AvatarUrl
            })
            .ToListAsync(cancellationToken);

        return Result<List<UserSummaryDto>>.Ok(following);
    }
}
