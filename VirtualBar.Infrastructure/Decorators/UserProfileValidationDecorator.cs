using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using VirtualBar.Application.Common;
using VirtualBar.Application.DTOs.Users;
using VirtualBar.Application.Interfaces;
using VirtualBar.Infrastructure.Persistence;
using VirtualBar.Infrastructure.Storage;
using VirtualBar.Infrastructure.Services;

namespace VirtualBar.Infrastructure.Decorators;

public sealed class UserProfileValidationDecorator(
    UserProfileService inner,
    AppDbContext db,
    ICurrentUser currentUser) : IUserProfileService
{
    public async Task<Result<UserProfileDto>> GetProfileAsync(Guid userId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (userId == Guid.Empty)
            return Result<UserProfileDto>.Fail("User ID is required.");

        var exists = await db.Users.AnyAsync(u => u.Id == userId, cancellationToken);
        if (!exists)
            return Result<UserProfileDto>.NotFound("User not found.");

        return await inner.GetProfileAsync(userId, cancellationToken);
    }

    public async Task<Result<List<UserSearchDto>>> SearchUsersAsync(string? query, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var sanitized = string.IsNullOrWhiteSpace(query) ? null : query.Trim();
        return await inner.SearchUsersAsync(sanitized, cancellationToken);
    }

    public async Task<Result<UpdatedProfileDto>> UpdateProfileAsync(UpdateProfileRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(request.DisplayName))
            return Result<UpdatedProfileDto>.Fail("Display name is required.");

        if (request.DisplayName.Trim().Length < 2)
            return Result<UpdatedProfileDto>.Fail("Display name must be at least 2 characters.");

        if (request.DisplayName.Trim().Length > 100)
            return Result<UpdatedProfileDto>.Fail("Display name must be at most 100 characters.");

        if (request.Bio != null && request.Bio.Length > 500)
            return Result<UpdatedProfileDto>.Fail("Bio must be at most 500 characters.");

        var exists = await db.Users.AnyAsync(u => u.Id == currentUser.UserId, cancellationToken);
        if (!exists)
            return Result<UpdatedProfileDto>.NotFound("User not found.");

        return await inner.UpdateProfileAsync(request, cancellationToken);
    }

    public async Task<Result<UpdatedProfileDto>> UploadAvatarAsync(IFormFile file, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (file.Length == 0)
            return Result<UpdatedProfileDto>.Fail("File is empty.");

        if (file.Length > 5 * 1024 * 1024)
            return Result<UpdatedProfileDto>.Fail("File size must not exceed 5 MB.");

        if (!ImageUploadTypes.IsAllowed(file.ContentType))
            return Result<UpdatedProfileDto>.Fail($"Only {ImageUploadTypes.AllowedFormatsLabel} images are allowed.");

        var exists = await db.Users.AnyAsync(u => u.Id == currentUser.UserId, cancellationToken);
        if (!exists)
            return Result<UpdatedProfileDto>.NotFound("User not found.");

        return await inner.UploadAvatarAsync(file, cancellationToken);
    }
}
