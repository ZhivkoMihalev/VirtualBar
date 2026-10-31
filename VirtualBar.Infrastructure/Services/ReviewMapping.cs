using VirtualBar.Application.DTOs.Reviews;
using VirtualBar.Domain.Entities;

namespace VirtualBar.Infrastructure.Services;

/// <summary>
/// The one <see cref="BottleReview"/> → <see cref="BottleReviewDto"/> mapping, shared by the bottle reviews
/// (<see cref="BottleReviewService"/>) and the product page (<see cref="ProductCatalogService"/>) so the two
/// cannot drift. Expects <c>User</c> and <c>Flavors</c> to be loaded.
/// </summary>
internal static class ReviewMapping
{
    public static BottleReviewDto ToDto(BottleReview review) => new()
    {
        Id = review.Id,
        BottleId = review.BottleId,
        UserId = review.UserId,
        UserDisplayName = review.User.DisplayName,
        UserAvatarUrl = review.User.AvatarUrl,
        Score = review.Score,
        Nose = review.Nose,
        Palate = review.Palate,
        Finish = review.Finish,
        Summary = review.Summary,
        Flavors = review.Flavors
            .Select(f => f.Flavor)
            .OrderBy(f => f)
            .ToList(),
        CreatedAt = review.CreatedAt,
        UpdatedAt = review.UpdatedAt,
    };
}
