using Microsoft.EntityFrameworkCore;
using VirtualBar.Application.Common;
using VirtualBar.Application.DTOs.Products;
using VirtualBar.Application.DTOs.Reviews;
using VirtualBar.Application.Interfaces;
using VirtualBar.Domain.Entities;
using VirtualBar.Domain.Enums;
using VirtualBar.Infrastructure.Persistence;

namespace VirtualBar.Infrastructure.Services;

public sealed class ProductCatalogService(AppDbContext db) : IProductCatalogService
{
    public async Task<Result<List<ProductDto>>> SearchAsync(string search, SpiritCategory? category, int limit, CancellationToken cancellationToken)
    {
        var q = db.Products
            .AsNoTracking()
            .Where(p => !p.IsDeleted);

        if (category.HasValue)
            q = q.Where(p => p.Category == category.Value);

        // Never ToLower() the column — it discards the collation and blocks any future index/full-text path on Name.
        var products = await q
            .Where(p => p.Name.Contains(search)
                || (p.Brand != null && p.Brand.Contains(search))
                || (p.Distillery != null && p.Distillery.Name.Contains(search)))
            .OrderByDescending(p => p.Name.StartsWith(search))
            .ThenBy(p => p.Name)
            .Take(limit)
            .Select(p => new ProductDto
            {
                Id = p.Id,
                Name = p.Name,
                // Brand is the label name and callers always want one. Seeded rows carry it, but an
                // admin-approved product may only have the distillery — fall back to it, exactly as
                // ProductLookupService does, so the two endpoints cannot disagree about the same product.
                Brand = p.Brand ?? (p.Distillery != null && !p.Distillery.IsDeleted ? p.Distillery.Name : null),
                DistilleryId = p.DistilleryId,
                DistilleryName = p.Distillery != null && !p.Distillery.IsDeleted ? p.Distillery.Name : null,
                Category = p.Category,
                Country = p.Country,
                Region = p.Region,
                Age = p.Age,
                AbvPercent = p.AbvPercent,
                VolumeMl = p.VolumeMl,
                Barcode = p.Barcode,
                ImageUrl = p.ImageUrl
            })
            .ToListAsync(cancellationToken);

        return Result<List<ProductDto>>.Ok(products);
    }

    public async Task<Result<PagedResult<ProductCardDto>>> BrowseAsync(string? search, SpiritCategory? category, int page, int pageSize, CancellationToken cancellationToken)
    {
        var q = db.Products
            .AsNoTracking()
            .Where(p => !p.IsDeleted);

        if (category.HasValue)
            q = q.Where(p => p.Category == category.Value);

        // Id breaks ties between equal names, so a row can never repeat or vanish between two pages.
        q = search is null
            ? q.OrderBy(p => p.Name).ThenBy(p => p.Id)
            : q.Where(p => p.Name.Contains(search)
                    || (p.Brand != null && p.Brand.Contains(search))
                    || (p.Distillery != null && p.Distillery.Name.Contains(search)))
                .OrderByDescending(p => p.Name.StartsWith(search))
                .ThenBy(p => p.Name)
                .ThenBy(p => p.Id);

        // One row over the page size tells whether another page exists, without a count query.
        var rows = await q
            .Skip((page - 1) * pageSize)
            .Take(pageSize + 1)
            .Select(p => new ProductCardDto
            {
                Id = p.Id,
                Name = p.Name,
                // Same brand fallback as SearchAsync, so a product reads the same on every catalog path.
                Brand = p.Brand ?? (p.Distillery != null && !p.Distillery.IsDeleted ? p.Distillery.Name : null),
                DistilleryId = p.DistilleryId,
                DistilleryName = p.Distillery != null && !p.Distillery.IsDeleted ? p.Distillery.Name : null,
                Category = p.Category,
                Country = p.Country,
                Region = p.Region,
                Age = p.Age,
                AbvPercent = p.AbvPercent,
                VolumeMl = p.VolumeMl,
                Barcode = p.Barcode,
                ImageUrl = p.ImageUrl,
                OwnersCount = p.Bottles.Where(b => !b.IsDeleted).Select(b => b.UserId).Distinct().Count(),
                ReviewsCount = p.Bottles.Where(b => !b.IsDeleted).SelectMany(b => b.Reviews).Count(r => !r.IsDeleted),
                AverageScore = p.Bottles.Where(b => !b.IsDeleted).SelectMany(b => b.Reviews).Where(r => !r.IsDeleted)
                    .Average(r => (double?)r.Score),
                ListingsCount = p.Bottles.Count(b => !b.IsDeleted && b.IsForSale)
            })
            .ToListAsync(cancellationToken);

        // Rounded here rather than in SQL: Math.Round over a nullable aggregate does not translate reliably.
        foreach (var card in rows)
            card.AverageScore = RoundScore(card.AverageScore);

        return Result<PagedResult<ProductCardDto>>.Ok(ToPage(rows, pageSize));
    }

    public async Task<Result<ProductDetailDto>> GetProductAsync(Guid productId, CancellationToken cancellationToken)
    {
        // The decorator has already checked that the product exists and is not deleted.
        var detail = await db.Products
            .AsNoTracking()
            .Where(p => p.Id == productId)
            .Select(p => new ProductDetailDto
            {
                Id = p.Id,
                Name = p.Name,
                Brand = p.Brand ?? (p.Distillery != null && !p.Distillery.IsDeleted ? p.Distillery.Name : null),
                DistilleryId = p.DistilleryId,
                DistilleryName = p.Distillery != null && !p.Distillery.IsDeleted ? p.Distillery.Name : null,
                Category = p.Category,
                Country = p.Country,
                Region = p.Region,
                Age = p.Age,
                AbvPercent = p.AbvPercent,
                VolumeMl = p.VolumeMl,
                Barcode = p.Barcode,
                ImageUrl = p.ImageUrl,
                Description = p.Description
            })
            .FirstAsync(cancellationToken);

        var reviews = LinkedReviews(productId);

        detail.ReviewsCount = await reviews.CountAsync(cancellationToken);
        detail.AverageScore = RoundScore(await reviews.AverageAsync(r => (double?)r.Score, cancellationToken));

        // Same ranking and tie-break as a single bottle's top flavors in BottleReviewService.
        detail.TopFlavors = await db.BottleReviewFlavors
            .Where(f => !f.Review.IsDeleted && !f.Review.Bottle.IsDeleted && f.Review.Bottle.ProductId == productId)
            .GroupBy(f => f.Flavor)
            .Select(g => new { Flavor = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ThenBy(x => x.Flavor)
            .Take(TopFlavorsCount)
            .Select(x => x.Flavor)
            .ToListAsync(cancellationToken);

        var bottles = LinkedBottles(productId);

        detail.OwnersCount = await bottles.Select(b => b.UserId).Distinct().CountAsync(cancellationToken);

        // Every total counts physical bottles, so one ×3 entry outweighs two single ones.
        detail.Owners = await bottles
            .GroupBy(b => new { b.UserId, b.User.DisplayName, b.User.AvatarUrl })
            .Select(g => new ProductOwnerDto
            {
                UserId = g.Key.UserId,
                DisplayName = g.Key.DisplayName,
                AvatarUrl = g.Key.AvatarUrl,
                BottleCount = g.Sum(b => b.Quantity)
            })
            .OrderByDescending(o => o.BottleCount)
            .ThenBy(o => o.DisplayName)
            .ThenBy(o => o.UserId)
            .Take(MaxOwners)
            .ToListAsync(cancellationToken);

        detail.Listings = await bottles
            .Where(b => b.IsForSale)
            .OrderByDescending(b => b.ForSaleAt)
            .ThenBy(b => b.Id)
            .Select(b => new ProductListingDto
            {
                BottleId = b.Id,
                SellerId = b.UserId,
                SellerDisplayName = b.User.DisplayName,
                SellerAvatarUrl = b.User.AvatarUrl,
                ForSaleQuantity = b.ForSaleQuantity,
                AskingPrice = b.AskingPrice,
                Currency = b.Currency,
                Condition = b.Condition,
                VintageYear = b.VintageYear,
                ForSaleAt = b.ForSaleAt
            })
            .ToListAsync(cancellationToken);

        var latest = await NewestFirst(reviews)
            .Take(LatestReviewsCount)
            .ToListAsync(cancellationToken);

        detail.LatestReviews = latest.Select(ReviewMapping.ToDto).ToList();

        return Result<ProductDetailDto>.Ok(detail);
    }

    public async Task<Result<PagedResult<BottleReviewDto>>> GetProductReviewsAsync(Guid productId, int page, int pageSize, CancellationToken cancellationToken)
    {
        // The decorator has already checked that the product exists and is not deleted.
        var rows = await NewestFirst(LinkedReviews(productId))
            .Skip((page - 1) * pageSize)
            .Take(pageSize + 1)
            .ToListAsync(cancellationToken);

        return Result<PagedResult<BottleReviewDto>>.Ok(ToPage(rows.Select(ReviewMapping.ToDto).ToList(), pageSize));
    }

    private const int TopFlavorsCount = 3;

    private const int MaxOwners = 20;

    private const int LatestReviewsCount = 5;

    /// <summary>Non-deleted bottles linked to the product.</summary>
    private IQueryable<Bottle> LinkedBottles(Guid productId) => db.Bottles
        .AsNoTracking()
        .Where(b => b.ProductId == productId && !b.IsDeleted);

    /// <summary>Non-deleted reviews on non-deleted bottles linked to the product.</summary>
    private IQueryable<BottleReview> LinkedReviews(Guid productId) => db.BottleReviews
        .AsNoTracking()
        .Where(r => !r.IsDeleted && !r.Bottle.IsDeleted && r.Bottle.ProductId == productId);

    /// <summary>Loads what <see cref="ReviewMapping.ToDto"/> needs, newest first; Id keeps paging stable.</summary>
    private static IQueryable<BottleReview> NewestFirst(IQueryable<BottleReview> reviews) => reviews
        .Include(r => r.User)
        .Include(r => r.Flavors)
        .OrderByDescending(r => r.CreatedAt)
        .ThenBy(r => r.Id);

    private static double? RoundScore(double? average) => average is { } value ? Math.Round(value, 1) : null;

    /// <summary>Splits a page fetched with one extra row into the page itself and whether more exist.</summary>
    private static PagedResult<T> ToPage<T>(List<T> rows, int pageSize) => new()
    {
        Items = rows.Take(pageSize).ToList(),
        HasMore = rows.Count > pageSize
    };
}
