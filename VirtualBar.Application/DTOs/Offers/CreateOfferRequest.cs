namespace VirtualBar.Application.DTOs.Offers;

public sealed class CreateOfferRequest
{
    public Guid BottleId { get; set; }

    public decimal OfferedPrice { get; set; }

    public string Currency { get; set; } = "";

    public string? Message { get; set; }
}
