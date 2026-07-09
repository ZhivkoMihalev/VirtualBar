using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VirtualBar.Api.Extensions;
using VirtualBar.Application.Interfaces;

namespace VirtualBar.Api.Controllers;

[ApiController]
[Route("api/prices")]
[Authorize]
public sealed class PricesController(
    IPriceEstimationService priceEstimationService,
    ICurrentUser currentUser) : ControllerBase
{
    /// <summary>
    /// Returns the cached indicative market estimate for a bottle — range (low/high), confidence, source,
    /// mandatory citations and the "as of" date. Reads through the cache only; it never triggers a
    /// synchronous Claude call on the request path.
    /// </summary>
    /// <param name="bottleId">The bottle identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Returns the cached indicative price estimate.</response>
    /// <response code="204">No estimate is cached yet for this bottle; the UI renders "—".</response>
    /// <response code="404">The bottle does not exist.</response>
    [HttpGet("bottle/{bottleId:guid}")]
    public async Task<IActionResult> GetBottleEstimate(Guid bottleId, CancellationToken cancellationToken)
    {
        var result = await priceEstimationService.GetCachedBottleEstimateAsync(bottleId, cancellationToken);

        if (!result.Success)
            return result.ToActionResult(this);

        return result.Data is null 
            ? NoContent() 
            : Ok(result.Data);
    }

    /// <summary>
    /// Returns the current collector's total collection value (Sealed bottles only) together with the
    /// per-bottle estimate lines for their whole collection.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Returns the collection value summary.</response>
    [HttpGet("collection")]
    public async Task<IActionResult> GetCollectionValue(CancellationToken cancellationToken)
    {
        var result = await priceEstimationService.GetCollectionValueAsync(currentUser.UserId, cancellationToken);
        return result.Success
            ? Ok(result.Data)
            : result.ToActionResult(this);
    }
}
