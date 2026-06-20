using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VirtualBar.Api.Extensions;
using VirtualBar.Application.Interfaces;

namespace VirtualBar.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class ProductsController(IProductLookupService productLookupService) : ControllerBase
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
}
