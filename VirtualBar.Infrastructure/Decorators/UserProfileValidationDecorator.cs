using VirtualBar.Application.Common;
using VirtualBar.Application.DTOs.Users;
using VirtualBar.Application.Interfaces;
using VirtualBar.Infrastructure.Services;

namespace VirtualBar.Infrastructure.Decorators;

public sealed class UserProfileValidationDecorator(UserProfileService inner) : IUserProfileService
{
    public async Task<Result<UserProfileDto>> GetProfileAsync(Guid userId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (userId == Guid.Empty)
            return Result<UserProfileDto>.Fail("User ID is required.");

        return await inner.GetProfileAsync(userId, cancellationToken);
    }

    public async Task<Result<List<UserSearchDto>>> SearchUsersAsync(string? query, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await inner.SearchUsersAsync(query, cancellationToken);
    }
}
