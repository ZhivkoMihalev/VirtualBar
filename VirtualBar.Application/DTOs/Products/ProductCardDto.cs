using VirtualBar.Domain.Enums;

namespace VirtualBar.Application.DTOs.Products;

/// <summary>A catalog product as shown on a <c>/catalog</c> card, with its community counts.</summary>
public sealed class ProductCardDto
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

    /// <summary>Distinct users who own at least one non-deleted bottle linked to the product.</summary>
    public int OwnersCount { get; set; }

    /// <summary>Average review score across every linked bottle, 1 decimal; null when there are no reviews.</summary>
    public double? AverageScore { get; set; }

    public int ReviewsCount { get; set; }

    /// <summary>Linked bottles currently listed for sale.</summary>
    public int ListingsCount { get; set; }
}
