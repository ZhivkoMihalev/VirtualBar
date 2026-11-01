using Microsoft.EntityFrameworkCore;
using VirtualBar.Application.Common;
using VirtualBar.Application.DTOs.Feed;
using VirtualBar.Application.Interfaces;
using VirtualBar.Domain.Enums;
using VirtualBar.Infrastructure.Persistence;

namespace VirtualBar.Infrastructure.Services;

public sealed class FeedService(AppDbContext db, ICurrentUser currentUser) : IFeedService
{
    /// <summary>How much of a review's summary a feed card shows; a longer one is cut at the last word that fits.</summary>
    internal const int ReviewSummaryMaxLength = 280;

    /// <summary>How many of a review's flavors a feed card shows.</summary>
    internal const int ReviewFlavorsShown = 3;

    public async Task<Result<List<FeedItemDto>>> GetFeedAsync(int skip, int take, string lang, CancellationToken cancellationToken)
    {
        // The page is cut from the merge of every source, so no followed-activity source can put more than its newest
        // skip + take items on it. Long arithmetic, so a huge skip cannot overflow into a negative Take.
        var perSource = (int)Math.Min((long)skip + take, int.MaxValue);

        var newsPosts = await db.NewsPosts
            .Where(p => !p.IsDeleted)
            .Include(p => p.Author)
            .Include(p => p.Translations)
            .OrderByDescending(p => p.CreatedAt)
            .Take(50)
            .ToListAsync(cancellationToken);

        var newsItems = newsPosts
            .Select(p =>
            {
                var translation = p.Translations.FirstOrDefault(t => t.LanguageCode == lang)
                    ?? p.Translations.FirstOrDefault(t => t.LanguageCode == "bg");

                return new FeedItemDto
                {
                    Type = FeedItemType.News,
                    Timestamp = p.CreatedAt,
                    PostId = p.Id,
                    PostTitle = translation?.Title ?? string.Empty,
                    PostContent = translation?.Content ?? string.Empty,
                    PostCoverImageUrl = p.CoverImageUrl,
                    PostAuthorDisplayName = p.Author.DisplayName,
                };
            })
            .ToList();

        var items = new List<FeedItemDto>(newsItems);

        if (currentUser.IsAuthenticated)
        {
            var followedIds = await db.UserFollows
                .Where(f => f.FollowerId == currentUser.UserId)
                .Select(f => f.FollowedId)
                .ToListAsync(cancellationToken);

            if (followedIds.Count > 0)
            {
                var newBottles = await db.Bottles
                    .Where(b => !b.IsDeleted && followedIds.Contains(b.UserId))
                    .OrderByDescending(b => b.CreatedAt)
                    .Take(perSource)
                    .Select(b => new FeedItemDto
                    {
                        Type = FeedItemType.NewBottle,
                        Timestamp = b.CreatedAt,
                        BottleId = b.Id,
                        BottleName = b.Name,
                        BottleCategory = b.Category.ToString(),
                        BottlePrimaryImageUrl = b.Images
                            .Where(i => i.IsPrimary && !i.IsDeleted)
                            .Select(i => i.Url)
                            .FirstOrDefault(),
                        BottleUserId = b.UserId,
                        BottleUserDisplayName = b.User.DisplayName,
                    })
                    .ToListAsync(cancellationToken);

                items.AddRange(newBottles);

                var forSaleItems = await db.Bottles
                    .Where(b => !b.IsDeleted && b.IsForSale && b.ForSaleAt != null && followedIds.Contains(b.UserId))
                    .OrderByDescending(b => b.ForSaleAt)
                    .Take(perSource)
                    .Select(b => new FeedItemDto
                    {
                        Type = FeedItemType.ForSale,
                        Timestamp = b.ForSaleAt!.Value,
                        BottleId = b.Id,
                        BottleName = b.Name,
                        BottleCategory = b.Category.ToString(),
                        BottlePrimaryImageUrl = b.Images
                            .Where(i => i.IsPrimary && !i.IsDeleted)
                            .Select(i => i.Url)
                            .FirstOrDefault(),
                        BottleUserId = b.UserId,
                        BottleUserDisplayName = b.User.DisplayName,
                        AskingPrice = b.AskingPrice,
                        Currency = b.Currency,
                    })
                    .ToListAsync(cancellationToken);

                items.AddRange(forSaleItems);

                // A review stays where it was first written: editing it does not bring it back to the top. A followed
                // collector's review of their own bottle shows too, as the same activity.
                var reviews = await db.BottleReviews
                    .Where(r => !r.IsDeleted && !r.Bottle.IsDeleted && followedIds.Contains(r.UserId))
                    .OrderByDescending(r => r.CreatedAt)
                    .Take(perSource)
                    .Select(r => new
                    {
                        r.Id,
                        r.CreatedAt,
                        r.Score,
                        r.Summary,
                        Flavors = r.Flavors.Select(f => f.Flavor).ToList(),
                        r.UserId,
                        ReviewerDisplayName = r.User.DisplayName,
                        ReviewerAvatarUrl = r.User.AvatarUrl,
                        r.BottleId,
                        BottleName = r.Bottle.Name,
                        BottleCategory = r.Bottle.Category,
                        BottlePrimaryImageUrl = r.Bottle.Images
                            .Where(i => i.IsPrimary && !i.IsDeleted)
                            .Select(i => i.Url)
                            .FirstOrDefault(),
                        OwnerId = r.Bottle.UserId,
                        OwnerDisplayName = r.Bottle.User.DisplayName,
                        ProductId = r.Bottle.Product != null && !r.Bottle.Product.IsDeleted
                            ? r.Bottle.ProductId
                            : null,
                        ProductName = r.Bottle.Product != null && !r.Bottle.Product.IsDeleted
                            ? r.Bottle.Product.Name
                            : null,
                        ProductBrand = r.Bottle.Product != null && !r.Bottle.Product.IsDeleted
                            ? r.Bottle.Product.Brand
                                ?? (r.Bottle.Product.Distillery != null && !r.Bottle.Product.Distillery.IsDeleted
                                    ? r.Bottle.Product.Distillery.Name
                                    : null)
                            : null,
                    })
                    .ToListAsync(cancellationToken);

                items.AddRange(reviews.Select(r => new FeedItemDto
                {
                    Type = FeedItemType.Reviewed,
                    Timestamp = r.CreatedAt,
                    ActorUserId = r.UserId,
                    ActorDisplayName = r.ReviewerDisplayName,
                    ActorAvatarUrl = r.ReviewerAvatarUrl,
                    ReviewId = r.Id,
                    ReviewScore = r.Score,
                    ReviewSummary = Excerpt(r.Summary),
                    ReviewFlavors = r.Flavors.OrderBy(f => f).Take(ReviewFlavorsShown).ToList(),
                    BottleId = r.BottleId,
                    BottleName = r.BottleName,
                    BottleCategory = r.BottleCategory.ToString(),
                    BottlePrimaryImageUrl = r.BottlePrimaryImageUrl,
                    BottleUserId = r.OwnerId,
                    BottleUserDisplayName = r.OwnerDisplayName,
                    ProductId = r.ProductId,
                    ProductName = r.ProductName,
                    ProductBrand = r.ProductBrand,
                }));
            }
        }

        return Result<List<FeedItemDto>>.Ok(
            items
                .OrderByDescending(i => i.Timestamp)
                .Skip(skip)
                .Take(take)
                .ToList());
    }

    /// <summary>
    /// The summary as a feed card shows it: whole up to <see cref="ReviewSummaryMaxLength"/>, else cut at the last
    /// space that fits (or hard at the limit when there is none) with an ellipsis. A blank summary shows nothing.
    /// </summary>
    private static string? Excerpt(string? summary)
    {
        if (string.IsNullOrWhiteSpace(summary))
            return null;

        if (summary.Length <= ReviewSummaryMaxLength)
            return summary;

        var lastSpace = summary.LastIndexOf(' ', ReviewSummaryMaxLength);
        var cut = lastSpace > 0 ? lastSpace : ReviewSummaryMaxLength;
        return summary[..cut].TrimEnd() + "…";
    }
}
