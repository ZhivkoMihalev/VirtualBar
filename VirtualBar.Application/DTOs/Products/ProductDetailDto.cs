using VirtualBar.Application.DTOs.Reviews;
using VirtualBar.Domain.Enums;

namespace VirtualBar.Application.DTOs.Products;

/// <summary>
/// The <c>/product/:id</c> page: the product plus everything aggregated over its non-deleted linked
/// bottles. Every aggregate is computed on read, never stored.
/// </summary>
public sealed class ProductDetailDto
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Brand { get; set; }

    public Guid? DistilleryId { get; set; }

    public string? DistilleryName { get; set; }

    public SpiritCategory Category { get; set; }

    public string? Country { get; set; }

    public string? Region { get; set; }

    public int? Age { get; set; }

    /// <summary>The year of distillation of a vintage-dated product; null otherwise (#59).</summary>
    public int? VintageYear { get; set; }

    /// <summary>The year of a yearly edition; null otherwise. Never set together with <see cref="VintageYear"/>.</summary>
    public int? ReleaseYear { get; set; }

    /// <summary>
    /// The other vintages, or the other editions, of the product's line, oldest first: the live products with the same
    /// category, maker, kind of year and name without the year (<c>ProductYearLine</c>). Empty for an undated product.
    /// </summary>
    public List<ProductYearDto> OtherYears { get; set; } = [];

    public double? AbvPercent { get; set; }

    public int? VolumeMl { get; set; }

    public string? Barcode { get; set; }

    public string? ImageUrl { get; set; }

    public string? Description { get; set; }

    /// <summary>Average over all reviews of all linked bottles, 1 decimal; null when there are none.</summary>
    public double? AverageScore { get; set; }

    /// <summary>All reviews of all linked bottles, not only <see cref="LatestReviews"/>.</summary>
    public int ReviewsCount { get; set; }

    public List<FlavorTag> TopFlavors { get; set; } = [];

    /// <summary>All distinct owners; <see cref="Owners"/> holds at most the top 20.</summary>
    public int OwnersCount { get; set; }

    public List<ProductOwnerDto> Owners { get; set; } = [];

    /// <summary>
    /// How many current listings (non-deleted linked bottles for sale) the product has, the same count as
    /// <see cref="ProductCardDto.ListingsCount"/>. Returned to everyone, including a caller who is not logged in.
    /// </summary>
    public int ListingsCount { get; set; }

    /// <summary>The current listings, newest first; always empty for a caller who is not logged in (#102).</summary>
    public List<ProductListingDto> Listings { get; set; } = [];

    /// <summary>The newest 5 reviews; the rest come from <c>GET /api/products/{id}/reviews</c>.</summary>
    public List<BottleReviewDto> LatestReviews { get; set; } = [];
}
