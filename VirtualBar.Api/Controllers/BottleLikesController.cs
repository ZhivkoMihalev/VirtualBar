using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VirtualBar.Api.Extensions;
using VirtualBar.Application.Interfaces;

namespace VirtualBar.Api.Controllers;

[ApiController]
[Route("api/bottles")]
[Authorize]
public sealed class BottleLikesController(IBottleLikeService bottleLikeService) : ControllerBase
{
    /// <summary>Likes a bottle on behalf of the current collector.</summary>
    /// <param name="bottleId">The bottle identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Bottle liked.</response>
    /// <response code="404">Bottle not found.</response>
    /// <response code="409">Already liked.</response>
    [HttpPost("{bottleId:guid}/like")]
    public async Task<IActionResult> Like(Guid bottleId, CancellationToken cancellationToken)
    {
        var result = await bottleLikeService.LikeAsync(bottleId, cancellationToken);
        return result.Success
            ? Ok()
            : result.ToActionResult(this);
    }

    /// <summary>Removes the current collector's like from a bottle.</summary>
    /// <param name="bottleId">The bottle identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Like removed.</response>
    /// <response code="400">Bottle is not liked.</response>
    /// <response code="404">Bottle not found.</response>
    [HttpDelete("{bottleId:guid}/like")]
    public async Task<IActionResult> Unlike(Guid bottleId, CancellationToken cancellationToken)
    {
        var result = await bottleLikeService.UnlikeAsync(bottleId, cancellationToken);
        return result.Success
            ? Ok()
            : result.ToActionResult(this);
    }
}
