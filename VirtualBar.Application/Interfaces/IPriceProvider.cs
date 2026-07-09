using VirtualBar.Application.DTOs.Pricing;
using VirtualBar.Domain.Enums;

namespace VirtualBar.Application.Interfaces;

public interface IPriceProvider
{
    PriceSource Source { get; }

    bool Supports(SpiritCategory category);

    Task<PriceEstimateDto?> TryEstimateAsync(PriceProviderInput input, CancellationToken cancellationToken);
}
