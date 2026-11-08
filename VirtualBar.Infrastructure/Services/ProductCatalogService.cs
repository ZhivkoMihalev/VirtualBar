using Microsoft.EntityFrameworkCore;
using VirtualBar.Application.Common;
using VirtualBar.Application.DTOs.Products;
using VirtualBar.Application.DTOs.Reviews;
using VirtualBar.Application.Interfaces;
using VirtualBar.Domain.Entities;
using VirtualBar.Domain.Enums;
using VirtualBar.Infrastructure.Persistence;

namespace VirtualBar.Infrastructure.Services;

public sealed class ProductCatalogService(AppDbContext db, ICurrentUser currentUser) : IProductCatalogService
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
            .Select(ProductMapping.ToDto)
            .ToListAsync(cancellationToken);

        return Result<List<ProductDto>>.Ok(products);
    }

    public async Task<Result<PagedResult<ProductCardDto>>> BrowseAsync(string? search, SpiritCategory? category, ProductSort? sort, int page, int pageSize, CancellationToken cancellationToken)
    {
        var q = db.Products
            .AsNoTracking()
            .Where(p => !p.IsDeleted);

        if (category.HasValue)
            q = q.Where(p => p.Category == category.Value);

        if (search is not null)
            q = q.Where(p => p.Name.Contains(search)
                || (p.Brand != null && p.Brand.Contains(search))
                || (p.Distillery != null && p.Distillery.Name.Contains(search)));

        // Every product's live reviews, aggregated in one pass over the reviews and joined to the products. Ranking by
        // a review subquery per product took about 2.5 s over the whole catalog on SQL Server (#56).
        var reviewStats = db.BottleReviews
            .Where(r => !r.IsDeleted && !r.Bottle.IsDeleted && r.Bottle.ProductId != null)
            .GroupBy(r => r.Bottle.ProductId)
            // Nullable, so a product with no reviews reads null rather than failing on a missing row (EF InMemory).
            .Select(g => new { ProductId = g.Key, Count = (int?)g.Count(), Average = (double?)g.Average(r => r.Score) });

        // The card counts, computed once in the query so the order and the card read the same numbers.
        var counted =
            from p in q
            join s in reviewStats on (Guid?)p.Id equals s.ProductId into stats
            from s in stats.DefaultIfEmpty()
            select new
            {
                Product = p,
                OwnersCount = p.Bottles.Where(b => !b.IsDeleted).Select(b => b.UserId).Distinct().Count(),
                // A product with no reviews has no row in the join: its columns are NULL in SQL, never a null object,
                // and a conditional on the row would not translate inside ORDER BY.
                ReviewsCount = s!.Count ?? 0,
                AverageScore = s!.Average,
                ListingsCount = p.Bottles.Count(b => !b.IsDeleted && b.IsForSale)
            };

        // The decorator has resolved the default order; null cannot reach here.
        var ordered = sort switch
        {
            ProductSort.Relevance => counted
                .OrderByDescending(x => x.Product.Name.StartsWith(search!))
                .ThenBy(x => x.Product.Name),
            // Few reviews must not outrank many: products with enough reviews come first, then the reviewed rest,
            // then the unreviewed, each by score. The card still shows the plain average.
            ProductSort.TopRated => counted
                .OrderByDescending(x => x.ReviewsCount >= MinReviewsToRankFirst)
                .ThenByDescending(x => x.ReviewsCount > 0)
                .ThenByDescending(x => x.AverageScore)
                .ThenByDescending(x => x.ReviewsCount)
                .ThenBy(x => x.Product.Name),
            ProductSort.MostReviewed => counted.OrderByDescending(x => x.ReviewsCount).ThenBy(x => x.Product.Name),
            ProductSort.Popular => counted.OrderByDescending(x => x.OwnersCount).ThenBy(x => x.Product.Name),
            ProductSort.ForSale => counted.OrderByDescending(x => x.ListingsCount).ThenBy(x => x.Product.Name),
            _ => counted.OrderBy(x => x.Product.Name)
        };

        // One row over the page size tells whether another page exists, without a count query. Id breaks the
        // last ties, so a row can never repeat or vanish between two pages.
        var rows = await ordered
            .ThenBy(x => x.Product.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize + 1)
            .Select(x => new ProductCardDto
            {
                Id = x.Product.Id,
                Name = x.Product.Name,
                // Same brand fallback as SearchAsync, so a product reads the same on every catalog path.
                Brand = x.Product.Brand
                    ?? (x.Product.Distillery != null && !x.Product.Distillery.IsDeleted ? x.Product.Distillery.Name : null),
                DistilleryId = x.Product.DistilleryId,
                DistilleryName = x.Product.Distillery != null && !x.Product.Distillery.IsDeleted
                    ? x.Product.Distillery.Name
                    : null,
                Category = x.Product.Category,
                Country = x.Product.Country,
                Region = x.Product.Region,
                Age = x.Product.Age,
                AbvPercent = x.Product.AbvPercent,
                VolumeMl = x.Product.VolumeMl,
                Barcode = x.Product.Barcode,
                ImageUrl = x.Product.ImageUrl,
                OwnersCount = x.OwnersCount,
                ReviewsCount = x.ReviewsCount,
                AverageScore = x.AverageScore,
                ListingsCount = x.ListingsCount
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
                VintageYear = p.VintageYear,
                ReleaseYear = p.ReleaseYear,
                AbvPercent = p.AbvPercent,
                VolumeMl = p.VolumeMl,
                Barcode = p.Barcode,
                ImageUrl = p.ImageUrl,
                Description = p.Description
            })
            .FirstAsync(cancellationToken);

        detail.OtherYears = await OtherYearsAsync(detail, cancellationToken);

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

        var listings = bottles.Where(b => b.IsForSale);

        // The same count as ProductCardDto.ListingsCount, returned to everyone.
        detail.ListingsCount = await listings.CountAsync(cancellationToken);

        // The listings themselves (seller, price) are for logged-in collectors only, like the marketplace (#102).
        if (currentUser.IsAuthenticated)
            detail.Listings = await listings
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

    /// <summary>
    /// The other vintages, or the other editions, of a dated product's line (#59), oldest first. The database narrows
    /// to the live products with the same category, maker and kind of year; the line's name is compared in memory,
    /// since taking the year out of a name does not translate to SQL. A dated line holds a handful of products.
    /// </summary>
    private async Task<List<ProductYearDto>> OtherYearsAsync(ProductDetailDto product, CancellationToken cancellationToken)
    {
        if ((product.VintageYear ?? product.ReleaseYear) is not int year)
            return [];

        var sameMaker = db.Products
            .AsNoTracking()
            .Where(p => !p.IsDeleted && p.Id != product.Id && p.Category == product.Category);

        // The maker is the distillery when there is one; without one, the brand is the label's own name (no fallback).
        sameMaker = product.DistilleryId is Guid distilleryId
            ? sameMaker.Where(p => p.DistilleryId == distilleryId)
            : sameMaker.Where(p => p.DistilleryId == null && p.Brand == product.Brand);

        var candidates = product.VintageYear is not null
            ? await sameMaker.Where(p => p.VintageYear != null)
                .Select(p => new { p.Id, p.Name, Year = p.VintageYear!.Value })
                .ToListAsync(cancellationToken)
            : await sameMaker.Where(p => p.ReleaseYear != null)
                .Select(p => new { p.Id, p.Name, Year = p.ReleaseYear!.Value })
                .ToListAsync(cancellationToken);

        var line = ProductYearLine.Of(product.Name, year);
        var siblings = candidates.Where(c => ProductYearLine.Of(c.Name, c.Year) == line).ToList();

        if (siblings.Count == 0)
            return [];

        var ids = siblings.Select(s => s.Id).ToList();
        var stats = await db.Products
            .AsNoTracking()
            .Where(p => ids.Contains(p.Id))
            .Select(p => new
            {
                p.Id,
                AverageScore = p.Bottles.Where(b => !b.IsDeleted).SelectMany(b => b.Reviews).Where(r => !r.IsDeleted)
                    .Average(r => (double?)r.Score),
                ListingsCount = p.Bottles.Count(b => !b.IsDeleted && b.IsForSale)
            })
            .ToDictionaryAsync(s => s.Id, cancellationToken);

        return siblings
            .OrderBy(s => s.Year)
            .ThenBy(s => s.Name)
            .Select(s => new ProductYearDto
            {
                Id = s.Id,
                Name = s.Name,
                Year = s.Year,
                AverageScore = RoundScore(stats[s.Id].AverageScore),
                ListingsCount = stats[s.Id].ListingsCount
            })
            .ToList();
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

    /// <summary>How many reviews a product needs to rank among the top of <see cref="ProductSort.TopRated"/> (#56).</summary>
    private const int MinReviewsToRankFirst = 3;

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
