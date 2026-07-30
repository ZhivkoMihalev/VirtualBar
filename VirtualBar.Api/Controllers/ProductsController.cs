using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VirtualBar.Api.Extensions;
using VirtualBar.Application.Interfaces;
using VirtualBar.Domain.Enums;

namespace VirtualBar.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class ProductsController(
    IProductLookupService productLookupService,
    IProductCatalogService productCatalogService) : ControllerBase
{
    /// <summary>Looks up a product by EAN/UPC barcode and returns name, brand, and a locally cached image.</summary>
    /// <param name="barcode">The EAN or UPC barcode string.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Product found.</response>
    /// <response code="400">Barcode is empty.</response>
    /// <response code="404">Product not found in the barcode database.</response>
    [HttpGet("barcode/{barcode}")]
    public async Task<IActionResult> LookupBarcode(string barcode, CancellationToken cancellationToken)
    {
        var result = await productLookupService.LookupByBarcodeAsync(barcode, cancellationToken);
        return result.Success ? Ok(result.Data) : result.ToActionResult(this);
    }

    /// <summary>Searches the canonical product catalog by name, brand, or distillery name.</summary>
    /// <param name="search">Search term, at least 2 characters.</param>
    /// <param name="category">Optional spirit category filter.</param>
    /// <param name="limit">Maximum number of results, clamped to 1-50 (default 20).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Matching catalog products, name matches starting with the term first.</response>
    /// <response code="400">Search term is shorter than 2 characters.</response>
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Search([FromQuery] string search, [FromQuery] SpiritCategory? category, [FromQuery] int limit, CancellationToken cancellationToken)
    {
        var result = await productCatalogService.SearchAsync(search, category, limit, cancellationToken);
        return result.Success 
            ? Ok(result.Data) 
            : result.ToActionResult(this);
    }
}
