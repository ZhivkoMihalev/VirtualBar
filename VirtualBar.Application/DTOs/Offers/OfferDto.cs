using VirtualBar.Domain.Enums;

namespace VirtualBar.Application.DTOs.Offers;

public sealed class OfferDto
{
    public Guid Id { get; set; }

    public Guid BottleId { get; set; }

    public string BottleName { get; set; } = "";

    public Guid BuyerId { get; set; }

    public string BuyerDisplayName { get; set; } = "";

    public Guid SellerId { get; set; }

    public string SellerDisplayName { get; set; } = "";

    public decimal OfferedPrice { get; set; }

    public string Currency { get; set; } = "";

    public string? Message { get; set; }

    public OfferStatus Status { get; set; }

    public DateTime? RespondedAt { get; set; }

    public DateTime CreatedAt { get; set; }
}
