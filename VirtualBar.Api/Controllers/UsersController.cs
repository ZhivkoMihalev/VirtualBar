using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using VirtualBar.Api.Extensions;
using VirtualBar.Application.DTOs.Users;
using VirtualBar.Application.Interfaces;

namespace VirtualBar.Api.Controllers;

[ApiController]
[Route("api/users")]
[Authorize]
public sealed class UsersController(IUserFollowService userFollowService, IUserProfileService userProfileService) : ControllerBase
{
    /// <summary>Returns a collector's public profile.</summary>
    /// <param name="userId">The collector's identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Returns the profile.</response>
    /// <response code="404">Collector not found.</response>
    [AllowAnonymous]
    [HttpGet("{userId:guid}")]
    public async Task<IActionResult> GetProfile(Guid userId, CancellationToken cancellationToken)
    {
        var result = await userProfileService.GetProfileAsync(userId, cancellationToken);
        return result.Success ? Ok(result.Data) : result.ToActionResult(this);
    }

    /// <summary>Searches collectors by display name.</summary>
    /// <param name="q">Optional search query.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Returns matching collectors.</response>
    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> SearchUsers([FromQuery] string? q, CancellationToken cancellationToken)
    {
        var result = await userProfileService.SearchUsersAsync(q, cancellationToken);
        return result.Success 
            ? Ok(result.Data) 
            : result.ToActionResult(this);
    }

    /// <summary>Updates the current collector's profile fields.</summary>
    /// <param name="request">The updated profile data.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Profile updated; returns updated profile data.</response>
    /// <response code="400">Validation failed.</response>
    [HttpPut("me")]
    public async Task<IActionResult> UpdateProfile(UpdateProfileRequest request, CancellationToken cancellationToken)
    {
        var result = await userProfileService.UpdateProfileAsync(request, cancellationToken);
        return result.Success 
            ? Ok(result.Data) 
            : result.ToActionResult(this);
    }

    /// <summary>Uploads a new avatar for the current collector.</summary>
    /// <param name="file">The image file to upload.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Avatar uploaded; returns updated profile data.</response>
    /// <response code="400">Validation failed (file too large, wrong type).</response>
    [HttpPost("me/avatar")]
    public async Task<IActionResult> UploadAvatar(IFormFile file, CancellationToken cancellationToken)
    {
        var result = await userProfileService.UploadAvatarAsync(file, cancellationToken);
        return result.Success 
            ? Ok(result.Data) 
            : result.ToActionResult(this);
    }

    /// <summary>Follows another collector on behalf of the current collector.</summary>
    /// <param name="userId">The identifier of the collector to follow.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Collector followed.</response>
    /// <response code="400">Cannot follow yourself.</response>
    /// <response code="404">Collector not found.</response>
    /// <response code="409">Already following this collector.</response>
    [HttpPost("{userId:guid}/follow")]
    public async Task<IActionResult> Follow(Guid userId, CancellationToken cancellationToken)
    {
        var result = await userFollowService.FollowAsync(userId, cancellationToken);
        return result.Success
            ? Ok()
            : result.ToActionResult(this);
    }

    /// <summary>Unfollows another collector on behalf of the current collector.</summary>
    /// <param name="userId">The identifier of the collector to unfollow.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Collector unfollowed.</response>
    /// <response code="400">Cannot unfollow yourself, or not following this collector.</response>
    /// <response code="404">Collector not found.</response>
    [HttpDelete("{userId:guid}/follow")]
    public async Task<IActionResult> Unfollow(Guid userId, CancellationToken cancellationToken)
    {
        var result = await userFollowService.UnfollowAsync(userId, cancellationToken);
        return result.Success
            ? Ok()
            : result.ToActionResult(this);
    }

    /// <summary>Returns the followers of a collector.</summary>
    /// <param name="userId">The collector's identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Returns the list of followers.</response>
    [AllowAnonymous]
    [HttpGet("{userId:guid}/followers")]
    public async Task<IActionResult> GetFollowers(Guid userId, CancellationToken cancellationToken)
    {
        var result = await userFollowService.GetFollowersAsync(userId, cancellationToken);
        return result.Success
            ? Ok(result.Data)
            : result.ToActionResult(this);
    }

    /// <summary>Returns the collectors a collector is following.</summary>
    /// <param name="userId">The collector's identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Returns the list of followed collectors.</response>
    [AllowAnonymous]
    [HttpGet("{userId:guid}/following")]
    public async Task<IActionResult> GetFollowing(Guid userId, CancellationToken cancellationToken)
    {
        var result = await userFollowService.GetFollowingAsync(userId, cancellationToken);
        return result.Success
            ? Ok(result.Data)
            : result.ToActionResult(this);
    }
}
