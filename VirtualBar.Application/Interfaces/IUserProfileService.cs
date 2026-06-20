using VirtualBar.Application.Common;
using VirtualBar.Application.DTOs.Users;

namespace VirtualBar.Application.Interfaces;

public interface IUserProfileService
{
    Task<Result<UserProfileDto>> GetProfileAsync(Guid userId, CancellationToken cancellationToken);

    Task<Result<List<UserSearchDto>>> SearchUsersAsync(string? query, CancellationToken cancellationToken);
}
