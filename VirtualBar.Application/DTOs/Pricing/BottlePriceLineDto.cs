using VirtualBar.Domain.Enums;

namespace VirtualBar.Application.DTOs.Pricing;

public sealed class BottlePriceLineDto
{
    public Guid BottleId { get; set; }

    public string Name { get; set; } = string.Empty;

    public decimal? EstimatedPrice { get; set; }

    public string Currency { get; set; } = string.Empty;

    public PriceConfidence Confidence { get; set; }

    public PriceSource Source { get; set; }

    public bool CountedInTotal { get; set; }
}
