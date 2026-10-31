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

    public List<ProductListingDto> Listings { get; set; } = [];

    /// <summary>The newest 5 reviews; the rest come from <c>GET /api/products/{id}/reviews</c>.</summary>
    public List<BottleReviewDto> LatestReviews { get; set; } = [];
}
