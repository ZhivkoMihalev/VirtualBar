using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VirtualBar.Api.Extensions;
using VirtualBar.Application.DTOs.News;
using VirtualBar.Application.Interfaces;

namespace VirtualBar.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class NewsController(INewsService newsService, IWebHostEnvironment env) : ControllerBase
{
    /// <summary>Get paginated news posts.</summary>
    /// <param name="skip">Number of posts to skip.</param>
    /// <param name="take">Number of posts to return (max 100).</param>
    /// <param name="lang">Language code for translated content (defaults to "bg").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">List of news posts.</response>
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetAll(
        [FromQuery] int skip = 0,
        [FromQuery] int take = 20,
        [FromQuery] string lang = "bg",
        CancellationToken cancellationToken = default)
    {
        var result = await newsService.GetAllAsync(skip, take, lang, cancellationToken);
        return result.Success 
            ? Ok(result.Data) 
            : result.ToActionResult(this);
    }

    /// <summary>Get a single news post by ID.</summary>
    /// <param name="id">News post ID.</param>
    /// <param name="lang">Language code for translated content (defaults to "bg").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">News post details.</response>
    /// <response code="404">Post not found.</response>
    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetById(
        Guid id,
        [FromQuery] string lang = "bg",
        CancellationToken cancellationToken = default)
    {
        var result = await newsService.GetByIdAsync(id, lang, cancellationToken);
        return result.Success 
            ? Ok(result.Data) 
            : result.ToActionResult(this);
    }

    /// <summary>Create a new news post (admin only).</summary>
    /// <param name="request">Post data.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="201">Post created.</response>
    /// <response code="400">Validation error.</response>
    /// <response code="403">Not an administrator.</response>
    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateNewsPostRequest request,
        CancellationToken cancellationToken)
    {
        var result = await newsService.CreateAsync(request, cancellationToken);
        return result.Success
            ? Created($"api/news/{result.Data!.Id}", result.Data)
            : result.ToActionResult(this);
    }

    /// <summary>Update an existing news post (admin only).</summary>
    /// <param name="id">News post ID.</param>
    /// <param name="request">Fields to update.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Updated post.</response>
    /// <response code="403">Not an administrator.</response>
    /// <response code="404">Post not found.</response>
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateNewsPostRequest request,
        CancellationToken cancellationToken)
    {
        var result = await newsService.UpdateAsync(id, request, cancellationToken);
        return result.Success 
            ? Ok(result.Data) 
            : result.ToActionResult(this);
    }

    /// <summary>Delete a news post (admin only, soft delete).</summary>
    /// <param name="id">News post ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="204">Deleted.</response>
    /// <response code="403">Not an administrator.</response>
    /// <response code="404">Post not found.</response>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await newsService.DeleteAsync(id, cancellationToken);
        return result.Success ? NoContent() : result.ToActionResult(this);
    }

    /// <summary>Upload a cover image for a news post.</summary>
    /// <param name="file">The image file to upload.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Returns the relative URL of the uploaded image.</response>
    /// <response code="400">Validation error.</response>
    /// <response code="403">Not an administrator.</response>
    [HttpPost("upload-cover")]
    public async Task<IActionResult> UploadCover(IFormFile file, CancellationToken cancellationToken)
    {
        var saveDirectory = Path.Combine(
            env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot"),
            "uploads", "news");

        Directory.CreateDirectory(saveDirectory);

        var result = await newsService.UploadCoverAsync(file, saveDirectory, cancellationToken);
        return result.Success ? Ok(new { url = result.Data }) : result.ToActionResult(this);
    }
}
