using Microsoft.EntityFrameworkCore;
using VirtualBar.Application.Common;
using VirtualBar.Application.DTOs.Feed;
using VirtualBar.Application.Interfaces;
using VirtualBar.Domain.Enums;
using VirtualBar.Infrastructure.Persistence;

namespace VirtualBar.Infrastructure.Services;

public sealed class FeedService(AppDbContext db, ICurrentUser currentUser) : IFeedService
{
    public async Task<Result<List<FeedItemDto>>> GetFeedAsync(int skip, int take, CancellationToken cancellationToken)
    {
        var newsItems = await db.NewsPosts
            .Where(p => !p.IsDeleted)
            .OrderByDescending(p => p.CreatedAt)
            .Take(50)
            .Select(p => new FeedItemDto
            {
                Type = FeedItemType.News,
                Timestamp = p.CreatedAt,
                PostId = p.Id,
                PostTitle = p.Title,
                PostExcerpt = p.Excerpt,
                PostCoverImageUrl = p.CoverImageUrl,
                PostAuthorDisplayName = p.Author.DisplayName,
            })
            .ToListAsync(cancellationToken);

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
                    .Select(b => new FeedItemDto
                    {
                        Type = FeedItemType.NewBottle,
                        Timestamp = b.CreatedAt,
                        BottleId = b.Id,
                        BottleName = b.Name,
                        BottleCategory = b.Category.ToString(),
                        BottlePrimaryImageUrl = b.Images
                            .Where(i => i.IsPrimary)
                            .Select(i => i.Url)
                            .FirstOrDefault(),
                        BottleUserId = b.UserId,
                        BottleUserDisplayName = b.User.DisplayName,
                    })
                    .ToListAsync(cancellationToken);

                items.AddRange(newBottles);

                var forSaleItems = await db.Bottles
                    .Where(b => !b.IsDeleted && b.IsForSale && b.ForSaleAt != null && followedIds.Contains(b.UserId))
                    .Select(b => new FeedItemDto
                    {
                        Type = FeedItemType.ForSale,
                        Timestamp = b.ForSaleAt!.Value,
                        BottleId = b.Id,
                        BottleName = b.Name,
                        BottleCategory = b.Category.ToString(),
                        BottlePrimaryImageUrl = b.Images
                            .Where(i => i.IsPrimary)
                            .Select(i => i.Url)
                            .FirstOrDefault(),
                        BottleUserId = b.UserId,
                        BottleUserDisplayName = b.User.DisplayName,
                        AskingPrice = b.AskingPrice,
                        Currency = b.Currency,
                    })
                    .ToListAsync(cancellationToken);

                items.AddRange(forSaleItems);
            }
        }

        return Result<List<FeedItemDto>>.Ok(
            items
                .OrderByDescending(i => i.Timestamp)
                .Skip(skip)
                .Take(take)
                .ToList());
    }
}
