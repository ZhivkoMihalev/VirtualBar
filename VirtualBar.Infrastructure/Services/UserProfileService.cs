using Microsoft.EntityFrameworkCore;
using VirtualBar.Application.Common;
using VirtualBar.Application.DTOs.Users;
using VirtualBar.Application.Interfaces;
using VirtualBar.Infrastructure.Persistence;

namespace VirtualBar.Infrastructure.Services;

public sealed class UserProfileService(AppDbContext db, ICurrentUser currentUser) : IUserProfileService
{
    public async Task<Result<UserProfileDto>> GetProfileAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await db.Users.FindAsync([userId], cancellationToken);

        var bottleCount = await db.Bottles
            .Where(b => b.UserId == userId && !b.IsDeleted)
            .CountAsync(cancellationToken);

        var followerCount = await db.UserFollows
            .Where(f => f.FollowedId == userId)
            .CountAsync(cancellationToken);

        var followingCount = await db.UserFollows
            .Where(f => f.FollowerId == userId)
            .CountAsync(cancellationToken);

        var isFollowedByMe = currentUser.IsAuthenticated
            && await db.UserFollows.AnyAsync(
                f => f.FollowerId == currentUser.UserId && f.FollowedId == userId,
                cancellationToken);

        return Result<UserProfileDto>.Ok(new UserProfileDto
        {
            Id = user!.Id,
            DisplayName = user.DisplayName,
            Bio = user.Bio,
            AvatarUrl = user.AvatarUrl,
            Country = user.Country,
            City = user.City,
            BottleCount = bottleCount,
            FollowerCount = followerCount,
            FollowingCount = followingCount,
            IsFollowedByMe = isFollowedByMe,
        });
    }

    public async Task<Result<List<UserSearchDto>>> SearchUsersAsync(string? query, CancellationToken cancellationToken)
    {
        var q = db.Users.AsQueryable();

        if (query != null)
            q = q.Where(u => u.DisplayName.Contains(query));

        var users = await q
            .OrderBy(u => u.DisplayName)
            .Take(50)
            .Select(u => new UserSearchDto
            {
                Id = u.Id,
                DisplayName = u.DisplayName,
                AvatarUrl = u.AvatarUrl,
                Bio = u.Bio,
                Country = u.Country,
                BottleCount = db.Bottles.Count(b => b.UserId == u.Id && !b.IsDeleted),
                FollowerCount = db.UserFollows.Count(f => f.FollowedId == u.Id),
            })
            .ToListAsync(cancellationToken);

        return Result<List<UserSearchDto>>.Ok(users);
    }
}
