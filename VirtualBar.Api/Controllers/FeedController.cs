using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VirtualBar.Api.Extensions;
using VirtualBar.Application.Interfaces;

namespace VirtualBar.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class FeedController(IFeedService feedService) : ControllerBase
{
    /// <summary>Get the activity feed (news + bottle events from followed users).</summary>
    /// <param name="skip">Number of items to skip.</param>
    /// <param name="take">Number of items to return (max 100).</param>
    /// <param name="lang">Language code for translated content (defaults to "bg").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Feed items sorted by date descending.</response>
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetFeed(
        [FromQuery] int skip = 0,
        [FromQuery] int take = 20,
        [FromQuery] string lang = "bg",
        CancellationToken cancellationToken = default)
    {
        var result = await feedService.GetFeedAsync(skip, take, lang, cancellationToken);
        return result.Success 
            ? Ok(result.Data) 
            : result.ToActionResult(this);
    }
}
