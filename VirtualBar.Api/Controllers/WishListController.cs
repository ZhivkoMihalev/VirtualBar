using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VirtualBar.Api.Extensions;
using VirtualBar.Application.DTOs.WishList;
using VirtualBar.Application.Interfaces;

namespace VirtualBar.Api.Controllers;

[ApiController]
[Route("api/wishlist")]
[Authorize]
public sealed class WishListController(IWishListService wishListService) : ControllerBase
{
    /// <summary>Returns the current collector's wish list items.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Returns the wish list items.</response>
    [HttpGet]
    public async Task<IActionResult> GetWishList(CancellationToken cancellationToken)
    {
        var result = await wishListService.GetWishListAsync(cancellationToken);
        return result.Success
            ? Ok(result.Data)
            : result.ToActionResult(this);
    }

    /// <summary>Get all users' wish list items.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Success.</response>
    [HttpGet("all")]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var result = await wishListService.GetAllAsync(cancellationToken);
        return result.Success
            ? Ok(result.Data)
            : result.ToActionResult(this);
    }

    /// <summary>Adds an item to the current collector's wish list.</summary>
    /// <param name="request">The wish list item details. At least one of distillery or category is required.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Item added; returns the created item.</response>
    /// <response code="400">No matching criterion provided.</response>
    [HttpPost]
    public async Task<IActionResult> AddItem(AddWishListItemRequest request, CancellationToken cancellationToken)
    {
        var result = await wishListService.AddItemAsync(request, cancellationToken);
        return result.Success
            ? Ok(result.Data)
            : result.ToActionResult(this);
    }

    /// <summary>Upload an image for a wish list item.</summary>
    /// <param name="file">The image file (jpg/png/webp, max 5 MB).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Returns the relative URL of the uploaded image.</response>
    /// <response code="400">Invalid file.</response>
    [HttpPost("image")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadImage(IFormFile file, CancellationToken cancellationToken)
    {
        var result = await wishListService.UploadImageAsync(file, cancellationToken);
        return result.Success ? Ok(new { url = result.Data }) : result.ToActionResult(this);
    }

    /// <summary>Removes an item from the current collector's wish list.</summary>
    /// <param name="id">The wish list item identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Item removed.</response>
    /// <response code="403">The item belongs to another collector.</response>
    /// <response code="404">The item does not exist.</response>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> RemoveItem(Guid id, CancellationToken cancellationToken)
    {
        var result = await wishListService.RemoveItemAsync(id, cancellationToken);
        return result.Success
            ? Ok()
            : result.ToActionResult(this);
    }
}
