using VirtualBar.Domain.Enums;

namespace VirtualBar.Application.DTOs.ProductRequests;

/// <summary>
/// Approval payload. Every field override is optional — a null override keeps the value proposed on the request row.
/// </summary>
public sealed class ResolveProductRequestRequest
{
    public Guid? ExistingProductId { get; set; }

    public string? Name { get; set; }

    public string? Brand { get; set; }

    public Guid? DistilleryId { get; set; }

    public SpiritCategory? Category { get; set; }

    public int? Age { get; set; }

    public double? AbvPercent { get; set; }

    public int? VolumeMl { get; set; }

    public string? Barcode { get; set; }

    public string? Country { get; set; }

    public string? Region { get; set; }

    public string? ImageUrl { get; set; }

    public string? Description { get; set; }

    public string? AdminNote { get; set; }

    /// <summary>
    /// Reuses the source bottle's primary image URL as the product image.
    /// </summary>
    public bool UseSourceBottleImage { get; set; }
}
