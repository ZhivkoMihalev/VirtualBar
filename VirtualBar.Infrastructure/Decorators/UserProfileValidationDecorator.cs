using Microsoft.EntityFrameworkCore;
using VirtualBar.Application.Common;
using VirtualBar.Application.DTOs.Users;
using VirtualBar.Application.Interfaces;
using VirtualBar.Infrastructure.Persistence;
using VirtualBar.Infrastructure.Services;

namespace VirtualBar.Infrastructure.Decorators;

public sealed class UserProfileValidationDecorator(
    UserProfileService inner,
    AppDbContext db) : IUserProfileService
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
}
