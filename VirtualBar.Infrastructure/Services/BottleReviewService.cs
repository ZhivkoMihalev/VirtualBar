using Microsoft.EntityFrameworkCore;
using VirtualBar.Application.Common;
using VirtualBar.Application.DTOs.Reviews;
using VirtualBar.Application.Interfaces;
using VirtualBar.Domain.Entities;
using VirtualBar.Infrastructure.Persistence;

namespace VirtualBar.Infrastructure.Services;

public sealed class BottleReviewService(
    AppDbContext db,
    ICurrentUser currentUser,
    INotificationService notificationService) : IBottleReviewService
{
    public async Task<Result<BottleReviewsSummaryDto>> GetReviewsAsync(Guid bottleId, CancellationToken cancellationToken)
    {
        var reviews = await db.BottleReviews
            .Where(r => r.BottleId == bottleId && !r.IsDeleted)
            .Include(r => r.User)
            .Include(r => r.Flavors)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(cancellationToken);

        var reviewDtos = reviews.Select(MapToDto).ToList();

        var summary = new BottleReviewsSummaryDto
        {
            AverageScore = reviews.Count == 0 ? null : Math.Round(reviews.Average(r => r.Score), 1),
            ReviewsCount = reviews.Count,
            TopFlavors = reviews
                .SelectMany(r => r.Flavors)
                .GroupBy(f => f.Flavor)
                .OrderByDescending(g => g.Count())
                .ThenBy(g => g.Key)
                .Take(3)
                .Select(g => g.Key)
                .ToList(),
            Reviews = reviewDtos,
            MyReview = reviewDtos.FirstOrDefault(r => r.UserId == currentUser.UserId),
        };

        return Result<BottleReviewsSummaryDto>.Ok(summary);
    }

    public async Task<Result<BottleReviewDto>> AddReviewAsync(Guid bottleId, AddReviewRequest request, CancellationToken cancellationToken)
    {
        var review = new BottleReview
        {
            BottleId = bottleId,
            UserId = currentUser.UserId,
            Score = request.Score,
            Nose = request.Nose?.Trim(),
            Palate = request.Palate?.Trim(),
            Finish = request.Finish?.Trim(),
            Summary = request.Summary?.Trim(),
            Flavors = (request.Flavors ?? [])
                .Distinct()
                .Select(f => new BottleReviewFlavor { Flavor = f })
                .ToList(),
        };

        db.BottleReviews.Add(review);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Lost the create race: a concurrent request slipped past the decorator's pre-check and the
            // filtered unique index (one review per user per bottle) rejected this insert.
            return Result<BottleReviewDto>.Conflict("You have already reviewed this bottle.");
        }

        await db.Entry(review).Reference(r => r.User).LoadAsync(cancellationToken);

        var bottleInfo = await db.Bottles
            .Where(b => b.Id == bottleId && !b.IsDeleted)
            .Select(b => new { b.UserId, b.Name })
            .FirstAsync(cancellationToken);

        await notificationService.CreateAsync(bottleInfo.UserId, NotificationType.BottleReviewed, bottleId, bottleInfo.Name, cancellationToken);

        return Result<BottleReviewDto>.Ok(MapToDto(review));
    }

    public async Task<Result<BottleReviewDto>> UpdateReviewAsync(Guid reviewId, UpdateReviewRequest request, CancellationToken cancellationToken)
    {
        var review = await db.BottleReviews
            .Include(r => r.Flavors)
            .Include(r => r.User)
            .FirstAsync(r => r.Id == reviewId && !r.IsDeleted, cancellationToken);

        review.Score = request.Score;
        review.Nose = request.Nose?.Trim();
        review.Palate = request.Palate?.Trim();
        review.Finish = request.Finish?.Trim();
        review.Summary = request.Summary?.Trim();
        review.UpdatedAt = DateTime.UtcNow;

        review.Flavors.Clear();
        foreach (var flavor in (request.Flavors ?? []).Distinct())
            review.Flavors.Add(new BottleReviewFlavor { ReviewId = review.Id, Flavor = flavor });

        await db.SaveChangesAsync(cancellationToken);

        return Result<BottleReviewDto>.Ok(MapToDto(review));
    }

    public async Task<Result<bool>> DeleteReviewAsync(Guid reviewId, CancellationToken cancellationToken)
    {
        var review = await db.BottleReviews
            .FirstOrDefaultAsync(r => r.Id == reviewId && !r.IsDeleted, cancellationToken);

        review!.IsDeleted = true;
        review.DeletedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);

        return Result<bool>.Ok(true);
    }

    private static BottleReviewDto MapToDto(BottleReview r) => new()
    {
        Id = r.Id,
        BottleId = r.BottleId,
        UserId = r.UserId,
        UserDisplayName = r.User.DisplayName,
        UserAvatarUrl = r.User.AvatarUrl,
        Score = r.Score,
        Nose = r.Nose,
        Palate = r.Palate,
        Finish = r.Finish,
        Summary = r.Summary,
        Flavors = r.Flavors
            .Select(f => f.Flavor)
            .OrderBy(f => f)
            .ToList(),
        CreatedAt = r.CreatedAt,
        UpdatedAt = r.UpdatedAt,
    };
}
