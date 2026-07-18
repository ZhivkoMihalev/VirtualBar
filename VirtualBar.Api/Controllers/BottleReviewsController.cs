using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VirtualBar.Api.Extensions;
using VirtualBar.Application.DTOs.Reviews;
using VirtualBar.Application.Interfaces;

namespace VirtualBar.Api.Controllers;

[ApiController]
[Route("api/bottles")]
[Authorize]
public sealed class BottleReviewsController(IBottleReviewService bottleReviewService) : ControllerBase
{
    /// <summary>Returns the review summary for a bottle: aggregate, all reviews, and the current collector's own review.</summary>
    /// <param name="bottleId">The bottle identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Returns the review summary.</response>
    /// <response code="404">Bottle not found.</response>
    [AllowAnonymous]
    [HttpGet("{bottleId:guid}/reviews")]
    public async Task<IActionResult> GetReviews(Guid bottleId, CancellationToken cancellationToken)
    {
        var result = await bottleReviewService.GetReviewsAsync(bottleId, cancellationToken);
        return result.Success
            ? Ok(result.Data)
            : result.ToActionResult(this);
    }

    /// <summary>Adds a review to a bottle on behalf of the current collector.</summary>
    /// <param name="bottleId">The bottle identifier.</param>
    /// <param name="request">The score, optional tasting notes and flavor tags.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="201">Review created; returns the created review.</response>
    /// <response code="400">Validation failed.</response>
    /// <response code="404">Bottle not found.</response>
    /// <response code="409">The current collector has already reviewed this bottle.</response>
    [HttpPost("{bottleId:guid}/reviews")]
    public async Task<IActionResult> AddReview(Guid bottleId, AddReviewRequest request, CancellationToken cancellationToken)
    {
        var result = await bottleReviewService.AddReviewAsync(bottleId, request, cancellationToken);
        return result.Success
            ? CreatedAtAction(nameof(GetReviews), new { bottleId }, result.Data)
            : result.ToActionResult(this);
    }

    /// <summary>Updates a review owned by the current collector.</summary>
    /// <param name="bottleId">The bottle identifier.</param>
    /// <param name="reviewId">The review identifier.</param>
    /// <param name="request">The updated score, optional tasting notes and flavor tags.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Review updated; returns the updated review.</response>
    /// <response code="400">Validation failed.</response>
    /// <response code="403">The review belongs to another collector.</response>
    /// <response code="404">Review not found.</response>
    [HttpPut("{bottleId:guid}/reviews/{reviewId:guid}")]
    public async Task<IActionResult> UpdateReview(Guid bottleId, Guid reviewId, UpdateReviewRequest request, CancellationToken cancellationToken)
    {
        var result = await bottleReviewService.UpdateReviewAsync(reviewId, request, cancellationToken);
        return result.Success
            ? Ok(result.Data)
            : result.ToActionResult(this);
    }

    /// <summary>Deletes a review owned by the current collector.</summary>
    /// <param name="bottleId">The bottle identifier.</param>
    /// <param name="reviewId">The review identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Review deleted.</response>
    /// <response code="403">The review belongs to another collector.</response>
    /// <response code="404">Review not found.</response>
    [HttpDelete("{bottleId:guid}/reviews/{reviewId:guid}")]
    public async Task<IActionResult> DeleteReview(Guid bottleId, Guid reviewId, CancellationToken cancellationToken)
    {
        var result = await bottleReviewService.DeleteReviewAsync(reviewId, cancellationToken);
        return result.Success
            ? Ok()
            : result.ToActionResult(this);
    }
}
