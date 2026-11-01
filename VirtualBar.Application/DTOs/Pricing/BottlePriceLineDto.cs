using VirtualBar.Domain.Enums;

namespace VirtualBar.Application.DTOs.Pricing;

public sealed class BottlePriceLineDto
{
    public Guid BottleId { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Indicative value of a single bottle — multiply by <see cref="Quantity"/> for the line total.
    /// </summary>
    public decimal? EstimatedPrice { get; set; }

    /// <summary>
    /// How many identical bottles this entry stands for.
    /// </summary>
    public int Quantity { get; set; } = 1;

    public string Currency { get; set; } = string.Empty;

    public PriceConfidence Confidence { get; set; }

    public PriceSource Source { get; set; }

    public bool CountedInTotal { get; set; }
}
