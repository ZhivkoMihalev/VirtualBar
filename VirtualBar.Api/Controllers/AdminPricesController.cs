using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VirtualBar.Api.Extensions;
using VirtualBar.Application.Interfaces;

namespace VirtualBar.Api.Controllers;

[ApiController]
[Route("api/admin/prices")]
[Authorize]
public sealed class AdminPricesController(IPriceSnapshotPruneService priceSnapshotPruneService) : ControllerBase
{
    /// <summary>
    /// Finds the price snapshots no pricing path reads any more: older than the TTL, and under a key that is neither the
    /// price key of a non-deleted bottle nor the canonical key of a non-deleted product. A dry run, the default, only
    /// reports them; <c>dryRun=false</c> deletes them. Run the dry run first and get its numbers approved (#58).
    /// </summary>
    /// <param name="dryRun">True (the default) to only report; false to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">The total, the orphans found, how many were deleted and a sample of their keys.</response>
    /// <response code="403">The caller is not an administrator.</response>
    [HttpPost("prune")]
    public async Task<IActionResult> Prune([FromQuery] bool dryRun = true, CancellationToken cancellationToken = default)
    {
        var result = await priceSnapshotPruneService.PruneAsync(dryRun, cancellationToken);
        return result.Success ? Ok(result.Data) : result.ToActionResult(this);
    }
}
