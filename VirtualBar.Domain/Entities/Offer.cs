using VirtualBar.Domain.Enums;

namespace VirtualBar.Domain.Entities;

public sealed class Offer : BaseEntity
{
    public Guid BottleId { get; set; }

    public Guid BuyerId { get; set; }

    public Guid SellerId { get; set; }

    public decimal OfferedPrice { get; set; }

    public string Currency { get; set; } = "";

    public string? Message { get; set; }

    public OfferStatus Status { get; set; } = OfferStatus.Pending;

    public DateTime? RespondedAt { get; set; }

    public Bottle Bottle { get; set; } = null!;

    public AppUser Buyer { get; set; } = null!;

    public AppUser Seller { get; set; } = null!;
}
