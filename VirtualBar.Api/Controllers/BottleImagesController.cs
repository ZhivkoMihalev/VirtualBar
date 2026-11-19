using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VirtualBar.Api.Extensions;
using VirtualBar.Application.DTOs.Bottles;
using VirtualBar.Application.Interfaces;

namespace VirtualBar.Api.Controllers;

[ApiController]
[Route("api/bottles")]
[Authorize]
public sealed class BottleImagesController(IBottleImageService bottleImageService) : ControllerBase
{
    /// <summary>Uploads an image for a bottle.</summary>
    /// <param name="bottleId">Bottle identifier.</param>
    /// <param name="file">Image file (JPEG, PNG, WebP, GIF; max 10 MB).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Image uploaded; returns the image DTO.</response>
    /// <response code="400">Validation failed.</response>
    /// <response code="403">Bottle belongs to another collector.</response>
    /// <response code="404">Bottle not found.</response>
    [HttpPost("{bottleId:guid}/images")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> AddImage(Guid bottleId, IFormFile file, CancellationToken cancellationToken)
    {
        var result = await bottleImageService.AddImageAsync(bottleId, file, cancellationToken);
        return result.Success ? Ok(result.Data) : result.ToActionResult(this);
    }

    /// <summary>Deletes a bottle image.</summary>
    /// <param name="bottleId">Bottle identifier.</param>
    /// <param name="imageId">Image identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Image deleted.</response>
    /// <response code="403">Bottle belongs to another collector.</response>
    /// <response code="404">Image not found, or it belongs to another bottle.</response>
    [HttpDelete("{bottleId:guid}/images/{imageId:guid}")]
    public async Task<IActionResult> DeleteImage(Guid bottleId, Guid imageId, CancellationToken cancellationToken)
    {
        var result = await bottleImageService.DeleteImageAsync(bottleId, imageId, cancellationToken);
        return result.Success ? Ok() : result.ToActionResult(this);
    }

    /// <summary>Gives a bottle the image of a catalog product; the request names the product, never an address.</summary>
    /// <param name="bottleId">Bottle identifier.</param>
    /// <param name="request">The catalog product whose image the bottle takes.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Image linked; returns the image DTO with the product's image URL.</response>
    /// <response code="400">No product given, or the product has no image.</response>
    /// <response code="403">Bottle belongs to another collector.</response>
    /// <response code="404">Bottle or product not found.</response>
    [HttpPost("{bottleId:guid}/images/link")]
    public async Task<IActionResult> LinkImage(Guid bottleId, [FromBody] LinkImageRequest request, CancellationToken cancellationToken)
    {
        var result = await bottleImageService.LinkImageAsync(bottleId, request, cancellationToken);
        return result.Success ? Ok(result.Data) : result.ToActionResult(this);
    }
}
