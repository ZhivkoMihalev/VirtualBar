using VirtualBar.Domain.Enums;

namespace VirtualBar.Application.DTOs.Pricing;

public sealed class PriceEstimateDto
{
    public decimal EstimatedPrice { get; set; }

    public decimal? LowEstimate { get; set; }

    public decimal? HighEstimate { get; set; }

    public string Currency { get; set; } = string.Empty;

    public int SampleSize { get; set; }

    public PriceSource Source { get; set; }

    public PriceConfidence Confidence { get; set; }

    public DateTime AsOf { get; set; }

    public IReadOnlyList<PriceCitation> Sources { get; set; } = [];
}
