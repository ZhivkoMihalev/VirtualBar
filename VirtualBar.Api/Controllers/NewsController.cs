using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VirtualBar.Api.Extensions;
using VirtualBar.Application.DTOs.News;
using VirtualBar.Application.Interfaces;

namespace VirtualBar.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class NewsController(INewsService newsService) : ControllerBase
{
    /// <summary>Get paginated news posts.</summary>
    /// <param name="skip">Number of posts to skip.</param>
    /// <param name="take">Number of posts to return (max 100).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">List of news posts.</response>
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetAll(
        [FromQuery] int skip = 0,
        [FromQuery] int take = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await newsService.GetAllAsync(skip, take, cancellationToken);
        return result.Success 
            ? Ok(result.Data) 
            : result.ToActionResult(this);
    }

    /// <summary>Get a single news post by ID.</summary>
    /// <param name="id">News post ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">News post details.</response>
    /// <response code="404">Post not found.</response>
    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await newsService.GetByIdAsync(id, cancellationToken);
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
}
