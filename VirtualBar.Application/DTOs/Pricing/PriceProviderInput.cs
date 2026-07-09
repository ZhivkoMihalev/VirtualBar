using VirtualBar.Domain.Enums;

namespace VirtualBar.Application.DTOs.Pricing;

public sealed record PriceProviderInput(
    string Name,
    string? DistilleryName,
    SpiritCategory Category,
    int? Age,
    int? VintageYear,
    int? VolumeMl,
    string? Barcode,
    string ProductKey,
    Guid? BottleId = null);
