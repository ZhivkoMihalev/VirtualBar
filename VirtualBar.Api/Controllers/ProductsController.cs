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
    /// <param name="limit">Maximum number of results, clamped to 1-51 (default 20); asking for 51 tells "exactly 50" apart from "more".</param>
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

    /// <summary>One page of the public catalog, with each product's owner, review and listing counts.</summary>
    /// <param name="search">Optional search term (2-100 characters); omitted or blank lists the whole catalog by name.</param>
    /// <param name="category">Optional spirit category filter.</param>
    /// <param name="page">1-based page number, at most 1000 (default 1).</param>
    /// <param name="pageSize">Products per page, clamped to 1-48 (default 24).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">The page, with <c>hasMore</c> set when another page exists.</response>
    /// <response code="400">Search term too short or too long, or page out of range.</response>
    [HttpGet("browse")]
    [AllowAnonymous]
    public async Task<IActionResult> Browse([FromQuery] string? search, [FromQuery] SpiritCategory? category, [FromQuery] int page, [FromQuery] int pageSize, CancellationToken cancellationToken)
    {
        var result = await productCatalogService.BrowseAsync(search, category, page, pageSize, cancellationToken);
        return result.Success ? Ok(result.Data) : result.ToActionResult(this);
    }

    /// <summary>The product page: the product with the score, top flavors, owners, listings and newest reviews of all its bottles.</summary>
    /// <param name="id">The product id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">The product with its aggregates.</response>
    /// <response code="404">Product not found or deleted.</response>
    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await productCatalogService.GetProductAsync(id, cancellationToken);
        return result.Success ? Ok(result.Data) : result.ToActionResult(this);
    }

    /// <summary>One page of the reviews of every bottle linked to the product, newest first.</summary>
    /// <param name="id">The product id.</param>
    /// <param name="page">1-based page number, at most 1000 (default 1).</param>
    /// <param name="pageSize">Reviews per page, clamped to 1-50 (default 10).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">The page, with <c>hasMore</c> set when another page exists.</response>
    /// <response code="400">Page out of range.</response>
    /// <response code="404">Product not found or deleted.</response>
    [HttpGet("{id:guid}/reviews")]
    [AllowAnonymous]
    public async Task<IActionResult> GetReviews(Guid id, [FromQuery] int page, [FromQuery] int pageSize, CancellationToken cancellationToken)
    {
        var result = await productCatalogService.GetProductReviewsAsync(id, page, pageSize, cancellationToken);
        return result.Success ? Ok(result.Data) : result.ToActionResult(this);
    }
}
