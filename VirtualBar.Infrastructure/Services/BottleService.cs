using Microsoft.EntityFrameworkCore;
using VirtualBar.Application.Common;
using VirtualBar.Application.DTOs.Bottles;
using VirtualBar.Application.Interfaces;
using VirtualBar.Domain.Entities;
using VirtualBar.Infrastructure.Persistence;

namespace VirtualBar.Infrastructure.Services;

public sealed class BottleService(
    AppDbContext db,
    ICurrentUser currentUser) : IBottleService
{
    public async Task<Result<List<BottleDto>>> GetBottlesByUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        var bottles = await db.Bottles
            .Where(b => b.UserId == userId && !b.IsDeleted)
            .Include(b => b.Images.OrderBy(i => i.SortOrder))
            .Include(b => b.Likes)
            .Include(b => b.Comments)
            .Include(b => b.User)
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync(cancellationToken);

        return Result<List<BottleDto>>.Ok(
            bottles.Select(b => MapToDto(b, b.Likes.Count, b.Comments.Count, b.User.DisplayName)).ToList());
    }

    public async Task<Result<BottleDto>> GetBottleByIdAsync(Guid bottleId, CancellationToken cancellationToken)
    {
        var bottle = await db.Bottles
            .Include(b => b.Images.OrderBy(i => i.SortOrder))
            .Include(b => b.Likes)
            .Include(b => b.Comments)
            .Include(b => b.User)
            .FirstOrDefaultAsync(b => b.Id == bottleId && !b.IsDeleted, cancellationToken);

        if (bottle is null)
            return Result<BottleDto>.NotFound("Bottle not found.");

        return Result<BottleDto>.Ok(MapToDto(bottle, bottle.Likes.Count, bottle.Comments.Count, bottle.User.DisplayName));
    }

    public async Task<Result<BottleDto>> AddBottleAsync(AddBottleRequest request, CancellationToken cancellationToken)
    {
        var bottle = new Bottle
        {
            UserId = currentUser.UserId,
            Name = request.Name,
            Distillery = request.Distillery,
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

        return Result<BottleDto>.Ok(MapToDto(bottle));
    }

    public async Task<Result<BottleDto>> UpdateBottleAsync(Guid bottleId, UpdateBottleRequest request, CancellationToken cancellationToken)
    {
        var bottle = await db.Bottles
            .FirstOrDefaultAsync(b => b.Id == bottleId && !b.IsDeleted, cancellationToken);

        bottle!.Name = request.Name;
        bottle.Distillery = request.Distillery;
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
        var commentsCount = await db.BottleComments.CountAsync(c => c.BottleId == bottleId, cancellationToken);

        return Result<BottleDto>.Ok(MapToDto(bottle, likesCount, commentsCount));
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

    public async Task<Result<bool>> ListForSaleAsync(Guid bottleId, ListForSaleRequest request, CancellationToken cancellationToken)
    {
        var bottle = await db.Bottles
            .FirstOrDefaultAsync(b => b.Id == bottleId && !b.IsDeleted, cancellationToken);

        bottle!.IsForSale = true;
        bottle.AskingPrice = request.AskingPrice;
        bottle.Currency = request.Currency;
        bottle.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);

        return Result<bool>.Ok(true);
    }

    public async Task<Result<bool>> UnlistFromSaleAsync(Guid bottleId, CancellationToken cancellationToken)
    {
        var bottle = await db.Bottles
            .FirstOrDefaultAsync(b => b.Id == bottleId && !b.IsDeleted, cancellationToken);

        bottle!.IsForSale = false;
        bottle.AskingPrice = null;
        bottle.Currency = null;
        bottle.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);

        return Result<bool>.Ok(true);
    }

    public async Task<Result<List<BottleDto>>> GetMarketplaceAsync(MarketplaceQuery query, CancellationToken cancellationToken)
    {
        var q = db.Bottles
            .Where(b => !b.IsDeleted && b.IsForSale)
            .Include(b => b.Images.OrderBy(i => i.SortOrder))
            .Include(b => b.Likes)
            .Include(b => b.Comments)
            .Include(b => b.User)
            .AsQueryable();

        if (query.Search != null)
            q = q.Where(b => b.Name.Contains(query.Search)
                || (b.Distillery != null 
                && b.Distillery.Contains(query.Search)));

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
            .Select(b => MapToDto(b, b.Likes.Count, b.Comments.Count, b.User.DisplayName))
            .ToList());
    }

    private static BottleDto MapToDto(Bottle bottle, int likesCount = 0, int commentsCount = 0, string userDisplayName = "") => new()
    {
        Id = bottle.Id,
        UserId = bottle.UserId,
        UserDisplayName = userDisplayName,
        Name = bottle.Name,
        Distillery = bottle.Distillery,
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
