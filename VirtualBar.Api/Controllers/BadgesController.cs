using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VirtualBar.Api.Extensions;
using VirtualBar.Application.Interfaces;

namespace VirtualBar.Api.Controllers;

[ApiController]
[Route("api/badges")]
[Authorize]
public sealed class BadgesController(IBadgeService badgeService) : ControllerBase
{
    /// <summary>Returns the badges a collector has already earned, newest first.</summary>
    /// <param name="userId">The collector's user identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Returns the earned badges.</response>
    /// <response code="404">The user does not exist.</response>
    [AllowAnonymous]
    [HttpGet("user/{userId:guid}")]
    public async Task<IActionResult> GetUserBadges(Guid userId, CancellationToken cancellationToken)
    {
        var result = await badgeService.GetUserBadgesAsync(userId, cancellationToken);
        return result.Success
            ? Ok(result.Data)
            : result.ToActionResult(this);
    }

    /// <summary>Returns the full badge catalog projected for the current collector — threshold, live count, earned state and award date per entry.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Returns the progress for every catalog entry.</response>
    [HttpGet("progress")]
    public async Task<IActionResult> GetMyProgress(CancellationToken cancellationToken)
    {
        var result = await badgeService.GetMyProgressAsync(cancellationToken);
        return result.Success
            ? Ok(result.Data)
            : result.ToActionResult(this);
    }
}
