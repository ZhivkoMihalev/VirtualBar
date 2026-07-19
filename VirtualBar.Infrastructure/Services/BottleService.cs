using Microsoft.EntityFrameworkCore;
using VirtualBar.Application.Common;
using VirtualBar.Application.DTOs.Bottles;
using VirtualBar.Application.Interfaces;
using VirtualBar.Domain.Entities;
using VirtualBar.Domain.Enums;
using VirtualBar.Infrastructure.Persistence;

namespace VirtualBar.Infrastructure.Services;

public sealed class BottleService(
    AppDbContext db,
    ICurrentUser currentUser,
    INotificationService notificationService,
    IBadgeService badgeService) : IBottleService
{
    public async Task<Result<List<BottleDto>>> GetBottlesByUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        var bottles = await db.Bottles
            .Where(b => b.UserId == userId && !b.IsDeleted)
            .Include(b => b.Images.Where(i => !i.IsDeleted).OrderBy(i => i.SortOrder))
            .Include(b => b.Likes)
            .Include(b => b.Comments.Where(c => !c.IsDeleted))
            .Include(b => b.Reviews.Where(r => !r.IsDeleted))
            .Include(b => b.User)
            .Include(b => b.Distillery)
            .OrderBy(b => b.DisplayOrder)
            .ThenByDescending(b => b.CreatedAt)
            .ToListAsync(cancellationToken);

        return Result<List<BottleDto>>.Ok(
            bottles.Select(b => MapToDto(
                b,
                b.Likes.Count,
                b.Comments.Count,
                b.Reviews.Any() ? Math.Round(b.Reviews.Average(r => (double)r.Score), 1) : (double?)null,
                b.Reviews.Count,
                b.User.DisplayName)).ToList());
    }

    public async Task<Result<BottleDto>> GetBottleByIdAsync(Guid bottleId, CancellationToken cancellationToken)
    {
        var bottle = await db.Bottles
            .Include(b => b.Images.Where(i => !i.IsDeleted).OrderBy(i => i.SortOrder))
            .Include(b => b.Likes)
            .Include(b => b.Comments.Where(c => !c.IsDeleted))
            .Include(b => b.Reviews.Where(r => !r.IsDeleted))
            .Include(b => b.User)
            .Include(b => b.Distillery)
            .FirstOrDefaultAsync(b => b.Id == bottleId && !b.IsDeleted, cancellationToken);

        if (bottle is null)
            return Result<BottleDto>.NotFound("Bottle not found.");

        return Result<BottleDto>.Ok(MapToDto(
            bottle,
            bottle.Likes.Count,
            bottle.Comments.Count,
            bottle.Reviews.Any() ? Math.Round(bottle.Reviews.Average(r => (double)r.Score), 1) : (double?)null,
            bottle.Reviews.Count,
            bottle.User.DisplayName));
    }

    public async Task<Result<BottleDto>> AddBottleAsync(AddBottleRequest request, CancellationToken cancellationToken)
    {
        var bottle = new Bottle
        {
            UserId = currentUser.UserId,
            Name = request.Name,
            DistilleryId = request.DistilleryId,
            Region = request.Region,
            Country = request.Country,
            Category = request.Category,
            Age = request.Age,
            VintageYear = request.VintageYear,
            AbvPercent = request.AbvPercent,
            VolumeMl = request.VolumeMl,
            Condition = request.Condition,
            Description = request.Description,
            IsLimited = request.IsLimited
        };

        db.Bottles.Add(bottle);
        await db.SaveChangesAsync(cancellationToken);

        var followerIds = await db.UserFollows
            .Where(f => f.FollowedId == currentUser.UserId)
            .Select(f => f.FollowerId)
            .ToListAsync(cancellationToken);

        await notificationService.CreateBulkAsync(followerIds, NotificationType.NewBottleFromFollowing, bottle.Id, bottle.Name, cancellationToken);

        await badgeService.EvaluateAsync(currentUser.UserId, BadgeTrigger.BottleAdded, cancellationToken);

        return Result<BottleDto>.Ok(MapToDto(bottle));
    }

    public async Task<Result<BottleDto>> UpdateBottleAsync(Guid bottleId, UpdateBottleRequest request, CancellationToken cancellationToken)
    {
        var bottle = await db.Bottles
            .FirstOrDefaultAsync(b => b.Id == bottleId && !b.IsDeleted, cancellationToken);

        bottle!.Name = request.Name;
        bottle.DistilleryId = request.DistilleryId;
        bottle.Region = request.Region;
        bottle.Country = request.Country;
        bottle.Category = request.Category;
        bottle.Age = request.Age;
        bottle.VintageYear = request.VintageYear;
        bottle.AbvPercent = request.AbvPercent;
        bottle.VolumeMl = request.VolumeMl;
        bottle.Condition = request.Condition;
        bottle.Description = request.Description;
        bottle.IsLimited = request.IsLimited;
        bottle.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);

        var likesCount = await db.BottleLikes.CountAsync(l => l.BottleId == bottleId, cancellationToken);
        var commentsCount = await db.BottleComments.CountAsync(c => c.BottleId == bottleId && !c.IsDeleted, cancellationToken);
        var reviewsCount = await db.BottleReviews.CountAsync(r => r.BottleId == bottleId && !r.IsDeleted, cancellationToken);
        var averageScore = reviewsCount == 0
            ? (double?)null
            : Math.Round(await db.BottleReviews
                .Where(r => r.BottleId == bottleId && !r.IsDeleted)
                .AverageAsync(r => (double)r.Score, cancellationToken), 1);

        return Result<BottleDto>.Ok(MapToDto(bottle, likesCount, commentsCount, averageScore, reviewsCount));
    }

    public async Task<Result<bool>> RemoveBottleAsync(Guid bottleId, CancellationToken cancellationToken)
    {
        var bottle = await db.Bottles
            .FirstOrDefaultAsync(b => b.Id == bottleId && !b.IsDeleted, cancellationToken);

        bottle!.IsDeleted = true;
        bottle.DeletedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);

        return Result<bool>.Ok(true);
    }

    public async Task<Result<bool>> ReorderBottlesAsync(ReorderBottlesRequest request, CancellationToken cancellationToken)
    {
        var bottles = await db.Bottles
            .Where(b => b.UserId == currentUser.UserId && !b.IsDeleted)
            .ToListAsync(cancellationToken);

        var orderById = new Dictionary<Guid, int>(request.BottleIds.Count);
        for (var i = 0; i < request.BottleIds.Count; i++)
            orderById[request.BottleIds[i]] = i;

        // Bottles missing from the request (e.g. added from another tab mid-drag) keep their
        // relative order after the explicitly listed ones instead of colliding at position 0.
        var next = request.BottleIds.Count;
        var unlisted = bottles
            .Where(b => !orderById.ContainsKey(b.Id))
            .OrderBy(b => b.DisplayOrder)
            .ThenByDescending(b => b.CreatedAt);

        foreach (var bottle in unlisted)
            orderById[bottle.Id] = next++;

        var now = DateTime.UtcNow;
        foreach (var bottle in bottles)
        {
            bottle.DisplayOrder = orderById[bottle.Id];
            bottle.UpdatedAt = now;
        }

        await db.SaveChangesAsync(cancellationToken);

        return Result<bool>.Ok(true);
    }

    public async Task<Result<bool>> ListForSaleAsync(Guid bottleId, ListForSaleRequest request, CancellationToken cancellationToken)
    {
        var bottle = await db.Bottles
            .FirstOrDefaultAsync(b => b.Id == bottleId && !b.IsDeleted, cancellationToken);

        bottle!.IsForSale = true;
        bottle.AskingPrice = request.AskingPrice;
        bottle.Currency = request.Currency;
        bottle.ForSaleAt = DateTime.UtcNow;
        bottle.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);

        var followerIds = await db.UserFollows
            .Where(f => f.FollowedId == currentUser.UserId)
            .Select(f => f.FollowerId)
            .ToListAsync(cancellationToken);

        await notificationService.CreateBulkAsync(followerIds, NotificationType.BottleListedForSale, bottle.Id, bottle.Name, cancellationToken);

        var matchingUserIds = await db.WishListItems
            .Where(w => !w.IsDeleted
                && w.UserId != currentUser.UserId
                && (w.Category == null || w.Category == bottle.Category)
                && (w.DistilleryId == null || w.DistilleryId == bottle.DistilleryId))
            .Select(w => w.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);

        await notificationService.CreateBulkAsync(matchingUserIds, NotificationType.WishListMatch, bottle.Id, bottle.Name, cancellationToken);

        await badgeService.EvaluateAsync(currentUser.UserId, BadgeTrigger.BottleListed, cancellationToken);

        return Result<bool>.Ok(true);
    }

    public async Task<Result<bool>> UnlistFromSaleAsync(Guid bottleId, CancellationToken cancellationToken)
    {
        var bottle = await db.Bottles
            .FirstOrDefaultAsync(b => b.Id == bottleId && !b.IsDeleted, cancellationToken);

        bottle!.IsForSale = false;
        bottle.AskingPrice = null;
        bottle.Currency = null;
        bottle.ForSaleAt = null;
        bottle.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);

        return Result<bool>.Ok(true);
    }

    public async Task<Result<List<BottleDto>>> GetMarketplaceAsync(MarketplaceQuery query, CancellationToken cancellationToken)
    {
        var q = db.Bottles
            .Where(b => !b.IsDeleted && b.IsForSale)
            .Include(b => b.Images.Where(i => !i.IsDeleted).OrderBy(i => i.SortOrder))
            .Include(b => b.Likes)
            .Include(b => b.Comments.Where(c => !c.IsDeleted))
            .Include(b => b.Reviews.Where(r => !r.IsDeleted))
            .Include(b => b.User)
            .Include(b => b.Distillery)
            .AsQueryable();

        if (query.Search != null)
            q = q.Where(b => b.Name.Contains(query.Search)
                || (b.Distillery != null
                && b.Distillery.Name.Contains(query.Search)));

        if (query.Category != null)
            q = q.Where(b => b.Category == query.Category);

        q = query.Sort switch
        {
            "price_asc"  => q.OrderBy(b => b.AskingPrice),
            "price_desc" => q.OrderByDescending(b => b.AskingPrice),
            _            => q.OrderByDescending(b => b.CreatedAt),
        };

        var bottles = await q.ToListAsync(cancellationToken);

        return Result<List<BottleDto>>.Ok(
            bottles
            .Select(b => MapToDto(
                b,
                b.Likes.Count,
                b.Comments.Count,
                b.Reviews.Any() ? Math.Round(b.Reviews.Average(r => (double)r.Score), 1) : (double?)null,
                b.Reviews.Count,
                b.User.DisplayName))
            .ToList());
    }

    private static BottleDto MapToDto(Bottle bottle, int likesCount = 0, int commentsCount = 0, double? averageScore = null, int reviewsCount = 0, string userDisplayName = "") => new()
    {
        Id = bottle.Id,
        UserId = bottle.UserId,
        UserDisplayName = userDisplayName,
        Name = bottle.Name,
        DistilleryId = bottle.DistilleryId,
        DistilleryName = bottle.Distillery is { IsDeleted: false } ? bottle.Distillery.Name : null,
        Region = bottle.Region,
        Country = bottle.Country,
        Category = bottle.Category,
        Age = bottle.Age,
        VintageYear = bottle.VintageYear,
        AbvPercent = bottle.AbvPercent,
        VolumeMl = bottle.VolumeMl,
        Condition = bottle.Condition,
        Description = bottle.Description,
        IsLimited = bottle.IsLimited,
        IsForSale = bottle.IsForSale,
        AskingPrice = bottle.AskingPrice,
        Currency = bottle.Currency,
        LikesCount = likesCount,
        CommentsCount = commentsCount,
        AverageScore = averageScore,
        ReviewsCount = reviewsCount,
        CreatedAt = bottle.CreatedAt,
        Images = bottle.Images.Select(i => new BottleImageDto
        {
            Id = i.Id,
            Url = i.Url,
            IsPrimary = i.IsPrimary,
            SortOrder = i.SortOrder
        }).ToList()
    };
}
