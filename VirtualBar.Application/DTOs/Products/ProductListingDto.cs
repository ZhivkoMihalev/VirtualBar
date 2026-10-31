using VirtualBar.Domain.Enums;

namespace VirtualBar.Application.DTOs.Products;

/// <summary>A linked bottle that is currently listed for sale.</summary>
public sealed class ProductListingDto
{
    public Guid BottleId { get; set; }

    public Guid SellerId { get; set; }

    public string SellerDisplayName { get; set; } = string.Empty;

    public string? SellerAvatarUrl { get; set; }

    public int ForSaleQuantity { get; set; }

    /// <summary>Asking price per bottle.</summary>
    public decimal? AskingPrice { get; set; }

    public string? Currency { get; set; }

    public BottleCondition Condition { get; set; }

    public int? VintageYear { get; set; }

    public DateTime? ForSaleAt { get; set; }
}
