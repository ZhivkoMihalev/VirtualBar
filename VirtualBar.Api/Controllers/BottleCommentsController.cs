using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VirtualBar.Api.Extensions;
using VirtualBar.Application.DTOs.Comments;
using VirtualBar.Application.Interfaces;

namespace VirtualBar.Api.Controllers;

[ApiController]
[Route("api/bottles")]
[Authorize]
public sealed class BottleCommentsController(IBottleCommentService bottleCommentService) : ControllerBase
{
    /// <summary>Returns all comments on a bottle.</summary>
    /// <param name="bottleId">The bottle identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Returns the list of comments.</response>
    /// <response code="404">Bottle not found.</response>
    [AllowAnonymous]
    [HttpGet("{bottleId:guid}/comments")]
    public async Task<IActionResult> GetComments(Guid bottleId, CancellationToken cancellationToken)
    {
        var result = await bottleCommentService.GetCommentsAsync(bottleId, cancellationToken);
        return result.Success
            ? Ok(result.Data)
            : result.ToActionResult(this);
    }

    /// <summary>Adds a comment to a bottle on behalf of the current collector.</summary>
    /// <param name="bottleId">The bottle identifier.</param>
    /// <param name="request">The comment content.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="201">Comment created; returns the created comment.</response>
    /// <response code="400">Validation failed.</response>
    /// <response code="404">Bottle not found.</response>
    [HttpPost("{bottleId:guid}/comments")]
    public async Task<IActionResult> AddComment(Guid bottleId, AddCommentRequest request, CancellationToken cancellationToken)
    {
        var result = await bottleCommentService.AddCommentAsync(bottleId, request, cancellationToken);
        return result.Success
            ? CreatedAtAction(nameof(GetComments), new { bottleId }, result.Data)
            : result.ToActionResult(this);
    }

    /// <summary>Deletes a comment owned by the current collector.</summary>
    /// <param name="bottleId">The bottle identifier.</param>
    /// <param name="commentId">The comment identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Comment deleted.</response>
    /// <response code="403">The comment belongs to another collector.</response>
    /// <response code="404">Comment not found.</response>
    [HttpDelete("{bottleId:guid}/comments/{commentId:guid}")]
    public async Task<IActionResult> DeleteComment(Guid bottleId, Guid commentId, CancellationToken cancellationToken)
    {
        var result = await bottleCommentService.DeleteCommentAsync(commentId, cancellationToken);
        return result.Success
            ? Ok()
            : result.ToActionResult(this);
    }
}
