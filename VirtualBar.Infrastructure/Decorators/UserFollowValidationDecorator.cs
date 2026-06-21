using Microsoft.EntityFrameworkCore;
using VirtualBar.Application.Common;
using VirtualBar.Application.DTOs.Users;
using VirtualBar.Application.Interfaces;
using VirtualBar.Infrastructure.Persistence;
using VirtualBar.Infrastructure.Services;

namespace VirtualBar.Infrastructure.Decorators;

public sealed class UserFollowValidationDecorator(
    UserFollowService inner,
    AppDbContext db,
    ICurrentUser currentUser) : IUserFollowService
{
    public async Task<Result<bool>> FollowAsync(Guid targetUserId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (targetUserId == currentUser.UserId)
            return Result<bool>.Fail("Cannot follow yourself.");

        var target = await db.Users.FindAsync([targetUserId], cancellationToken);
        if (target is null)
            return Result<bool>.NotFound("User not found.");

        var alreadyFollowing = await db.UserFollows
            .AnyAsync(f => f.FollowerId == currentUser.UserId && f.FollowedId == targetUserId, cancellationToken);
        if (alreadyFollowing)
            return Result<bool>.Conflict("Already following this user.");

        return await inner.FollowAsync(targetUserId, cancellationToken);
    }

    public async Task<Result<bool>> UnfollowAsync(Guid targetUserId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (targetUserId == currentUser.UserId)
            return Result<bool>.Fail("Cannot unfollow yourself.");

        var target = await db.Users.FindAsync([targetUserId], cancellationToken);
        if (target is null)
            return Result<bool>.NotFound("User not found.");

        var following = await db.UserFollows
            .AnyAsync(f => f.FollowerId == currentUser.UserId && f.FollowedId == targetUserId, cancellationToken);
        if (!following)
            return Result<bool>.Fail("Not following this user.");

        return await inner.UnfollowAsync(targetUserId, cancellationToken);
    }

    public async Task<Result<List<UserSummaryDto>>> GetFollowersAsync(Guid userId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return await inner.GetFollowersAsync(userId, cancellationToken);
    }

    public async Task<Result<List<UserSummaryDto>>> GetFollowingAsync(Guid userId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return await inner.GetFollowingAsync(userId, cancellationToken);
    }
}
