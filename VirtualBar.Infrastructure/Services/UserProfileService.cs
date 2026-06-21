using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using VirtualBar.Application.Common;
using VirtualBar.Application.DTOs.Users;
using VirtualBar.Application.Interfaces;
using VirtualBar.Infrastructure.Persistence;

namespace VirtualBar.Infrastructure.Services;

public sealed class UserProfileService(AppDbContext db, ICurrentUser currentUser, IWebHostEnvironment env) : IUserProfileService
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

    public async Task<Result<UpdatedProfileDto>> UpdateProfileAsync(UpdateProfileRequest request, CancellationToken cancellationToken)
    {
        var user = await db.Users.FindAsync([currentUser.UserId], cancellationToken);

        user!.DisplayName = request.DisplayName.Trim();
        user.Bio = string.IsNullOrWhiteSpace(request.Bio) ? null : request.Bio.Trim();
        user.Country = string.IsNullOrWhiteSpace(request.Country) ? null : request.Country.Trim();
        user.City = string.IsNullOrWhiteSpace(request.City) ? null : request.City.Trim();

        await db.SaveChangesAsync(cancellationToken);

        return Result<UpdatedProfileDto>.Ok(new UpdatedProfileDto
        {
            DisplayName = user.DisplayName,
            Bio = user.Bio,
            AvatarUrl = user.AvatarUrl,
            Country = user.Country,
            City = user.City,
        });
    }

    public async Task<Result<UpdatedProfileDto>> UploadAvatarAsync(IFormFile file, CancellationToken cancellationToken)
    {
        var uploadsDir = Path.Combine(env.WebRootPath, "uploads", "avatars");
        Directory.CreateDirectory(uploadsDir);

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        var fileName = $"{Guid.NewGuid()}{ext}";
        var filePath = Path.Combine(uploadsDir, fileName);

        await using var stream = File.Create(filePath);
        await file.CopyToAsync(stream, cancellationToken);

        var relativeUrl = $"/uploads/avatars/{fileName}";

        var user = await db.Users.FindAsync([currentUser.UserId], cancellationToken);

        user!.AvatarUrl = relativeUrl;

        await db.SaveChangesAsync(cancellationToken);

        return Result<UpdatedProfileDto>.Ok(new UpdatedProfileDto
        {
            DisplayName = user.DisplayName,
            Bio = user.Bio,
            AvatarUrl = relativeUrl,
            Country = user.Country,
            City = user.City,
        });
    }
}
