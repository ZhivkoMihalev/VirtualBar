using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VirtualBar.Application.Common;
using VirtualBar.Application.DTOs.Pricing;
using VirtualBar.Application.Options;
using VirtualBar.Domain.Enums;
using VirtualBar.Infrastructure.Persistence;

namespace VirtualBar.Infrastructure.Services.Pricing;

/// <summary>
/// The community price signal: value derived from VirtualBar's own data — listings (<c>AskingPrice</c>)
/// and accepted <c>Offers</c> — for bottles whose canonical <see cref="ProductKey"/> matches the input.
/// 100% legal (it is our data), free, and grows with the marketplace. Confidence is sample-count driven.
/// </summary>
public sealed class InternalMarketPriceProvider(
    AppDbContext db,
    IOptions<InternalProviderOptions> internalOptions,
    IOptions<PricingOptions> pricingOptions,
    ILogger<InternalMarketPriceProvider> logger)
    : PriceProviderBase(pricingOptions, logger)
{
    public override PriceSource Source => PriceSource.Internal;

    protected override bool Enabled => internalOptions.Value.UseProviderStats;

    // Internal data is noisier than a researched range — narrow to the inter-quartile band.
    protected override (double Low, double High) PercentileBounds => (25d, 75d);

    protected override async Task<ProviderRawResult?> FetchAsync(PriceProviderInput input, CancellationToken cancellationToken)
    {
        var points = new List<PricePoint>();

        // Listings: any non-deleted bottle with an asking price, in the same category.
        var listings = await db.Bottles
            .AsNoTracking()
            .Where(b => !b.IsDeleted
                && b.Category == input.Category
                && b.AskingPrice != null
                && b.Currency != null)
            .Select(b => new PriceCandidate(
                b.AskingPrice!.Value,
                b.Currency!,
                b.Name,
                b.Distillery != null ? b.Distillery.Name : null,
                b.Category,
                b.Age,
                b.VintageYear,
                b.VolumeMl))
            .ToListAsync(cancellationToken);

        AddMatching(listings, input, points);

        // Accepted offers: a realized agreed price on a matching bottle.
        var acceptedOffers = await db.Offers
            .AsNoTracking()
            .Where(o => !o.IsDeleted
                && o.Status == OfferStatus.Accepted
                && !o.Bottle.IsDeleted
                && o.Bottle.Category == input.Category)
            .Select(o => new PriceCandidate(
                o.OfferedPrice,
                o.Currency,
                o.Bottle.Name,
                o.Bottle.Distillery != null ? o.Bottle.Distillery.Name : null,
                o.Bottle.Category,
                o.Bottle.Age,
                o.Bottle.VintageYear,
                o.Bottle.VolumeMl))
            .ToListAsync(cancellationToken);

        AddMatching(acceptedOffers, input, points);

        if (points.Count == 0)
            return null;

        var options = internalOptions.Value;
        var confidence = points.Count >= options.MinSamples
            ? PriceConfidence.High
            : points.Count >= options.MinApproxSamples
                ? PriceConfidence.Medium
                : PriceConfidence.Low;

        return new ProviderRawResult(points, confidence, DateTime.UtcNow, []);
    }

    private static void AddMatching(IEnumerable<PriceCandidate> candidates, PriceProviderInput input, List<PricePoint> points)
    {
        foreach (var c in candidates)
        {
            var key = ProductKey.For(c.DistilleryName, c.Name, c.Category, c.Age, c.VintageYear, c.VolumeMl);
            if (key == input.ProductKey)
                points.Add(new PricePoint(c.Amount, c.Currency));
        }
    }

    private sealed record PriceCandidate(
        decimal Amount,
        string Currency,
        string Name,
        string? DistilleryName,
        SpiritCategory Category,
        int? Age,
        int? VintageYear,
        int? VolumeMl);
}
