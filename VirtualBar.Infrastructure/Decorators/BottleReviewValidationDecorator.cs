using Microsoft.EntityFrameworkCore;
using VirtualBar.Application.Common;
using VirtualBar.Application.DTOs.Reviews;
using VirtualBar.Application.Interfaces;
using VirtualBar.Infrastructure.Persistence;
using VirtualBar.Infrastructure.Services;

namespace VirtualBar.Infrastructure.Decorators;

public sealed class BottleReviewValidationDecorator(
    BottleReviewService inner,
    AppDbContext db,
    ICurrentUser currentUser) : IBottleReviewService
{
    public async Task<Result<BottleReviewsSummaryDto>> GetReviewsAsync(Guid bottleId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var bottle = await db.Bottles.FirstOrDefaultAsync(b => b.Id == bottleId && !b.IsDeleted, cancellationToken);
        if (bottle is null)
            return Result<BottleReviewsSummaryDto>.NotFound("Bottle not found.");

        return await inner.GetReviewsAsync(bottleId, cancellationToken);
    }

    public async Task<Result<BottleReviewDto>> AddReviewAsync(Guid bottleId, AddReviewRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (request.Score < 0 || request.Score > 100)
            return Result<BottleReviewDto>.Fail("Score must be between 0 and 100.");

        if (request.Nose is { Length: > 2000 })
            return Result<BottleReviewDto>.Fail("Nose must be 2000 characters or fewer.");

        if (request.Palate is { Length: > 2000 })
            return Result<BottleReviewDto>.Fail("Palate must be 2000 characters or fewer.");

        if (request.Finish is { Length: > 2000 })
            return Result<BottleReviewDto>.Fail("Finish must be 2000 characters or fewer.");

        if (request.Summary is { Length: > 2000 })
            return Result<BottleReviewDto>.Fail("Summary must be 2000 characters or fewer.");

        if (request.Flavors is not null)
        {
            if (request.Flavors.Count > 5)
                return Result<BottleReviewDto>.Fail("A review can have at most 5 flavor tags.");

            if (request.Flavors.Distinct().Count() != request.Flavors.Count)
                return Result<BottleReviewDto>.Fail("Flavor tags must be distinct.");

            if (request.Flavors.Any(f => !Enum.IsDefined(f)))
                return Result<BottleReviewDto>.Fail("One or more flavor tags are invalid.");
        }

        var bottle = await db.Bottles.FirstOrDefaultAsync(b => b.Id == bottleId && !b.IsDeleted, cancellationToken);
        if (bottle is null)
            return Result<BottleReviewDto>.NotFound("Bottle not found.");

        var alreadyReviewed = await db.BottleReviews
            .AnyAsync(r => r.BottleId == bottleId && r.UserId == currentUser.UserId && !r.IsDeleted, cancellationToken);

        if (alreadyReviewed)
            return Result<BottleReviewDto>.Conflict("You have already reviewed this bottle.");

        return await inner.AddReviewAsync(bottleId, request, cancellationToken);
    }

    public async Task<Result<BottleReviewDto>> UpdateReviewAsync(Guid reviewId, UpdateReviewRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (request.Score < 0 || request.Score > 100)
            return Result<BottleReviewDto>.Fail("Score must be between 0 and 100.");

        if (request.Nose is { Length: > 2000 })
            return Result<BottleReviewDto>.Fail("Nose must be 2000 characters or fewer.");

        if (request.Palate is { Length: > 2000 })
            return Result<BottleReviewDto>.Fail("Palate must be 2000 characters or fewer.");

        if (request.Finish is { Length: > 2000 })
            return Result<BottleReviewDto>.Fail("Finish must be 2000 characters or fewer.");

        if (request.Summary is { Length: > 2000 })
            return Result<BottleReviewDto>.Fail("Summary must be 2000 characters or fewer.");

        if (request.Flavors is not null)
        {
            if (request.Flavors.Count > 5)
                return Result<BottleReviewDto>.Fail("A review can have at most 5 flavor tags.");

            if (request.Flavors.Distinct().Count() != request.Flavors.Count)
                return Result<BottleReviewDto>.Fail("Flavor tags must be distinct.");

            if (request.Flavors.Any(f => !Enum.IsDefined(f)))
                return Result<BottleReviewDto>.Fail("One or more flavor tags are invalid.");
        }

        var review = await db.BottleReviews
            .FirstOrDefaultAsync(r => r.Id == reviewId && !r.IsDeleted, cancellationToken);

        if (review is null)
            return Result<BottleReviewDto>.NotFound("Review not found.");

        if (review.UserId != currentUser.UserId)
            return Result<BottleReviewDto>.Forbidden("Forbidden.");

        return await inner.UpdateReviewAsync(reviewId, request, cancellationToken);
    }

    public async Task<Result<bool>> DeleteReviewAsync(Guid reviewId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var review = await db.BottleReviews
            .FirstOrDefaultAsync(r => r.Id == reviewId && !r.IsDeleted, cancellationToken);

        if (review is null)
            return Result<bool>.NotFound("Review not found.");

        if (review.UserId != currentUser.UserId)
            return Result<bool>.Forbidden("Forbidden.");

        return await inner.DeleteReviewAsync(reviewId, cancellationToken);
    }
}
