using Microsoft.AspNetCore.Http;
using VirtualBar.Application.Common;
using VirtualBar.Application.DTOs.Bottles;

namespace VirtualBar.Application.Interfaces;

public interface IBottleImageService
{
    Task<Result<BottleImageDto>> AddImageAsync(Guid bottleId, IFormFile file, CancellationToken cancellationToken);

    Task<Result<bool>> DeleteImageAsync(Guid imageId, CancellationToken cancellationToken);

    Task<Result<BottleImageDto>> LinkImageAsync(Guid bottleId, LinkImageRequest request, CancellationToken cancellationToken);
}
