namespace VirtualBar.Application.DTOs.Pricing;

public sealed class CollectionValueDto
{
    public decimal TotalValue { get; set; }

    public string Currency { get; set; } = string.Empty;

    public int PricedCount { get; set; }

    public int TotalCount { get; set; }

    public IReadOnlyList<BottlePriceLineDto> Items { get; set; } = [];
}
