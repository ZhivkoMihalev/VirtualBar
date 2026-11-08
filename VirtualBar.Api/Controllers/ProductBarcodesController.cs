using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VirtualBar.Api.Extensions;
using VirtualBar.Application.Interfaces;

namespace VirtualBar.Api.Controllers;

/// <summary>The barcodes collectors gave catalog products (#49), for an admin to check and correct.</summary>
[ApiController]
[Route("api/product-barcodes")]
[Authorize]
public sealed class ProductBarcodesController(IProductBarcodeService productBarcodeService) : ControllerBase
{
    /// <summary>Lists the barcodes collectors gave products, newest first, with who backs each and any conflict.</summary>
    /// <param name="search">Optional part of a barcode, or of a product's name or brand (at most 100 characters).</param>
    /// <param name="page">Page number from 1 (at most 1000).</param>
    /// <param name="pageSize">Rows per page, clamped to 1–50 (default 30).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">One page of pairs; <c>hasMore</c> says whether another follows.</response>
    /// <response code="400">The search term is too long, or the page is out of range.</response>
    /// <response code="403">The caller is not an administrator.</response>
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? search, [FromQuery] int page, [FromQuery] int pageSize, CancellationToken cancellationToken)
    {
        var result = await productBarcodeService.GetAllAsync(search, page, pageSize, cancellationToken);
        return result.Success ? Ok(result.Data) : result.ToActionResult(this);
    }

    /// <summary>Removes a wrong pair, in every form of its barcode: the lookup and new saves never give it again.</summary>
    /// <param name="id">The pair's id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="204">Removed.</response>
    /// <response code="403">The caller is not an administrator.</response>
    /// <response code="404">No live pair has this id.</response>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Remove(Guid id, CancellationToken cancellationToken)
    {
        var result = await productBarcodeService.RemoveAsync(id, cancellationToken);
        return result.Success ? NoContent() : result.ToActionResult(this);
    }
}
