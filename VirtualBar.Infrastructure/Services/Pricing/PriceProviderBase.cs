using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VirtualBar.Application.DTOs.Pricing;
using VirtualBar.Application.Interfaces;
using VirtualBar.Application.Options;
using VirtualBar.Domain.Enums;

namespace VirtualBar.Infrastructure.Services.Pricing;

/// <summary>
/// Shared base for every price provider. Owns the reusable plumbing — the on/off gate, FX→base
/// currency conversion, percentile aggregation, error swallowing (→ <c>null</c> + log), and final DTO
/// building. Each concrete provider overrides only its custom <see cref="FetchAsync"/>.
/// </summary>
public abstract class PriceProviderBase(IOptions<PricingOptions> pricingOptions, ILogger logger) : IPriceProvider
{
    /// <summary>Upper sanity bound for any single price point; values above it are treated as noise and dropped.</summary>
    protected const decimal MaxSanePrice = 1_000_000m;

    /// <summary>The configured pricing settings (base currency, FX table, TTL).</summary>
    protected PricingOptions Pricing => pricingOptions.Value;

    /// <summary>Shared logger for swallowed-error reporting.</summary>
    protected ILogger Logger => logger;

    /// <inheritdoc />
    public abstract PriceSource Source { get; }

    /// <summary>
    /// The single on/off switch for the provider, bound from its own <c>UseProviderStats</c> option.
    /// When <c>false</c> the provider short-circuits to <c>null</c> and contributes no data.
    /// </summary>
    protected abstract bool Enabled { get; }

    /// <summary>
    /// The (low, high) percentile bounds used to derive the estimate range from the observed points.
    /// Defaults to (0, 100) — the min and max. Volume-driven providers can narrow this.
    /// </summary>
    protected virtual (double Low, double High) PercentileBounds => (0d, 100d);

    /// <inheritdoc />
    public virtual bool Supports(SpiritCategory category) => true;

    /// <inheritdoc />
    public async Task<PriceEstimateDto?> TryEstimateAsync(PriceProviderInput input, CancellationToken cancellationToken)
    {
        if (!Enabled || !Supports(input.Category))
            return null;

        try
        {
            var raw = await FetchAsync(input, cancellationToken);
            if (raw is null || raw.Points.Count == 0)
                return null;

            return Build(raw);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Price provider {Source} failed for product key {ProductKey}.", Source, input.ProductKey);
            return null;
        }
    }

    /// <summary>
    /// The provider's custom fetch: a DB query for internal data, an HTTP call for external sources.
    /// Returns the raw observed prices, or <c>null</c> when there is no usable data.
    /// </summary>
    protected abstract Task<ProviderRawResult?> FetchAsync(PriceProviderInput input, CancellationToken cancellationToken);

    private PriceEstimateDto? Build(ProviderRawResult raw)
    {
        var converted = new List<decimal>(raw.Points.Count);
        foreach (var point in raw.Points)
        {
            var inBase = ConvertToBase(point.Amount, point.Currency);
            if (inBase is { } value && value > 0 && value <= MaxSanePrice)
                converted.Add(value);
        }

        if (converted.Count == 0)
            return null;

        converted.Sort();

        var (lowPct, highPct) = PercentileBounds;

        return new PriceEstimateDto
        {
            EstimatedPrice = decimal.Round(Percentile(converted, 50d), 2),
            LowEstimate = decimal.Round(Percentile(converted, lowPct), 2),
            HighEstimate = decimal.Round(Percentile(converted, highPct), 2),
            Currency = Pricing.BaseCurrency,
            SampleSize = converted.Count,
            Source = Source,
            Confidence = raw.Confidence,
            AsOf = raw.AsOf,
            Sources = raw.Sources,
        };
    }

    /// <summary>
    /// Converts an amount from its native currency to the configured base currency via the FX table.
    /// Returns <c>null</c> when the currency is unknown and cannot be converted reliably.
    /// </summary>
    protected decimal? ConvertToBase(decimal amount, string currency)
    {
        var code = string.IsNullOrWhiteSpace(currency)
            ? Pricing.BaseCurrency
            : currency.Trim().ToUpperInvariant();

        if (string.Equals(code, Pricing.BaseCurrency, StringComparison.OrdinalIgnoreCase))
            return amount;

        if (Pricing.FxToBase.TryGetValue(code, out var rate) && rate > 0)
            return amount * rate;

        return null;
    }

    private static decimal Percentile(IReadOnlyList<decimal> sortedAscending, double percentile)
    {
        if (sortedAscending.Count == 1)
            return sortedAscending[0];

        var rank = percentile / 100d * (sortedAscending.Count - 1);
        var lowerIndex = (int)Math.Floor(rank);
        var upperIndex = (int)Math.Ceiling(rank);

        if (lowerIndex == upperIndex)
            return sortedAscending[lowerIndex];

        var weight = (decimal)(rank - lowerIndex);
        return sortedAscending[lowerIndex] + (sortedAscending[upperIndex] - sortedAscending[lowerIndex]) * weight;
    }
}
