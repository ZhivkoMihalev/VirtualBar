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
        var dto = await db.Users
            .Where(u => u.Id == userId)
            .Select(u => new UserProfileDto
            {
                Id = u.Id,
                DisplayName = u.DisplayName,
                Bio = u.Bio,
                AvatarUrl = u.AvatarUrl,
                Country = u.Country,
                City = u.City,
                BottleCount = u.Bottles.Count(b => !b.IsDeleted),
                FollowerCount = u.Followers.Count,
                FollowingCount = u.Following.Count,
                IsFollowedByMe = u.Followers.Any(f => f.FollowerId == currentUser.UserId),
            })
            .FirstAsync(cancellationToken);

        dto.IsFollowedByMe = currentUser.IsAuthenticated && dto.IsFollowedByMe;

        return Result<UserProfileDto>.Ok(dto);
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
                BottleCount = u.Bottles.Count(b => !b.IsDeleted),
                FollowerCount = u.Followers.Count,
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
