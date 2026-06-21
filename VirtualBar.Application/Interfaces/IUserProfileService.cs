using Microsoft.AspNetCore.Http;
using VirtualBar.Application.Common;
using VirtualBar.Application.DTOs.Users;

namespace VirtualBar.Application.Interfaces;

public interface IUserProfileService
{
    Task<Result<UserProfileDto>> GetProfileAsync(Guid userId, CancellationToken cancellationToken);

    Task<Result<List<UserSearchDto>>> SearchUsersAsync(string? query, CancellationToken cancellationToken);

    Task<Result<UpdatedProfileDto>> UpdateProfileAsync(UpdateProfileRequest request, CancellationToken cancellationToken);

    Task<Result<UpdatedProfileDto>> UploadAvatarAsync(IFormFile file, CancellationToken cancellationToken);
}
