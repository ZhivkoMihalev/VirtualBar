using VirtualBar.Domain.Enums;

namespace VirtualBar.Domain.Entities;

public sealed class PriceSnapshot : BaseEntity
{
    public string ProductKey { get; set; } = string.Empty;

    public string? Barcode { get; set; }

    public SpiritCategory Category { get; set; }

    public decimal EstimatedPrice { get; set; }

    public decimal? LowEstimate { get; set; }

    public decimal? HighEstimate { get; set; }

    public string Currency { get; set; } = string.Empty;

    public int SampleSize { get; set; }

    public PriceSource Source { get; set; }

    public PriceConfidence Confidence { get; set; }

    public string SourcesJson { get; set; } = "[]";

    public DateTime AsOf { get; set; }

    public DateTime FetchedAt { get; set; }
}
