using VirtualBar.Domain.Enums;

namespace VirtualBar.Application.DTOs.Bottles;

public sealed class MarketplaceQuery
{
    public string? Search { get; set; }

    public SpiritCategory? Category { get; set; }

    public string? Sort { get; set; }
}
