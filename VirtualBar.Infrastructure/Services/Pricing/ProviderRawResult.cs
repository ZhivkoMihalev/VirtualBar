using VirtualBar.Application.DTOs.Pricing;
using VirtualBar.Domain.Enums;

namespace VirtualBar.Infrastructure.Services.Pricing;

/// <summary>
/// The raw output of a provider's custom fetch step, before <see cref="PriceProviderBase"/> applies
/// FX conversion, aggregation, rounding, and DTO building. Each provider overrides only the fetch and
/// returns this; the shared plumbing lives in the base class.
/// </summary>
/// <param name="Points">The observed prices in their native currencies (one or more).</param>
/// <param name="Confidence">The confidence the provider assigns to this estimate.</param>
/// <param name="AsOf">The "as of" timestamp for the underlying data.</param>
/// <param name="Sources">Citations backing the estimate (empty for internal data).</param>
public sealed record ProviderRawResult(
    IReadOnlyList<PricePoint> Points,
    PriceConfidence Confidence,
    DateTime AsOf,
    IReadOnlyList<PriceCitation> Sources);
