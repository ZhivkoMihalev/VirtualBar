namespace VirtualBar.Application.Options;

public sealed class PricingOptions
{
    public const string SectionName = "Pricing";

    public string BaseCurrency { get; set; } = string.Empty;

    public Dictionary<string, decimal> FxToBase { get; set; } = new();

    public int SnapshotTtlDays { get; set; }

    public int RefreshIntervalHours { get; set; }

    public int PreWarmTopNBottles { get; set; }

    public bool RefreshEnabled { get; set; }
}
