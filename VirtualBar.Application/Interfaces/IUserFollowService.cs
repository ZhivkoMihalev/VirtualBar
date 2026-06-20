using VirtualBar.Application.Common;
using VirtualBar.Application.DTOs.Users;

namespace VirtualBar.Application.Interfaces;

public interface IUserFollowService
{
    Task<Result<bool>> FollowAsync(Guid targetUserId, CancellationToken cancellationToken);

    Task<Result<bool>> UnfollowAsync(Guid targetUserId, CancellationToken cancellationToken);

    Task<Result<List<UserSummaryDto>>> GetFollowersAsync(Guid userId, CancellationToken cancellationToken);

    Task<Result<List<UserSummaryDto>>> GetFollowingAsync(Guid userId, CancellationToken cancellationToken);
}
