using VirtualBar.Application.Common;

namespace VirtualBar.Application.Interfaces;

public interface IBottleLikeService
{
    Task<Result<bool>> LikeAsync(Guid bottleId, CancellationToken cancellationToken);

    Task<Result<bool>> UnlikeAsync(Guid bottleId, CancellationToken cancellationToken);
}
