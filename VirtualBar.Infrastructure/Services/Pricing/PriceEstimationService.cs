using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using VirtualBar.Application.Common;
using VirtualBar.Application.DTOs.Pricing;
using VirtualBar.Application.Interfaces;
using VirtualBar.Application.Options;
using VirtualBar.Domain.Entities;
using VirtualBar.Domain.Enums;
using VirtualBar.Infrastructure.Persistence;

namespace VirtualBar.Infrastructure.Services.Pricing;

/// <summary>
/// The orchestrator: a read-through <see cref="PriceSnapshot"/> cache (5-day TTL) plus a simple
/// source-priority choice between the providers. The cache is cost-critical — it caps how often we pay
/// for a Claude call. Collection value sums Sealed bottles only.
/// </summary>
public sealed class PriceEstimationService(
    AppDbContext db,
    InternalMarketPriceProvider internalProvider,
    ClaudeMarketResearchProvider claudeProvider,
    IOptions<PricingOptions> pricingOptions) : IPriceEstimationService
{
    public async Task<Result<PriceEstimateDto>> GetBottleEstimateAsync(Guid bottleId, CancellationToken cancellationToken)
    {
        var bottle = await db.Bottles
            .AsNoTracking()
            .Where(b => b.Id == bottleId && !b.IsDeleted)
            .Select(b => new BottleKeyInfo(
                b.Id,
                b.Name,
                b.Distillery != null ? b.Distillery.Name : null,
                b.Category,
                b.Age,
                b.VintageYear,
                b.VolumeMl,
                b.Barcode,
                b.Condition))
            .FirstOrDefaultAsync(cancellationToken);

        if (bottle is null)
            return Result<PriceEstimateDto>.NotFound("Bottle not found.");

        var productKey = ProductKey.For(bottle.DistilleryName, bottle.Name, bottle.Category, bottle.Age, bottle.VintageYear, bottle.VolumeMl);

        // 1. Cache first: a fresh snapshot returns immediately, without calling any external provider.
        var snapshot = await db.PriceSnapshots
            .FirstOrDefaultAsync(s => s.ProductKey == productKey, cancellationToken);

        var ttl = TimeSpan.FromDays(pricingOptions.Value.SnapshotTtlDays);
        if (snapshot is not null && DateTime.UtcNow - snapshot.FetchedAt <= ttl)
            return Result<PriceEstimateDto>.Ok(MapToDto(snapshot));

        // 2. Miss or stale: the external research is always the reference. Internal data is used only
        //    as a fallback when Claude returns nothing (and only if we have any of our own data).
        var input = new PriceProviderInput(
            bottle.Name,
            bottle.DistilleryName,
            bottle.Category,
            bottle.Age,
            bottle.VintageYear,
            bottle.VolumeMl,
            bottle.Barcode,
            productKey,
            bottle.Id);

        var estimate = await claudeProvider.TryEstimateAsync(input, cancellationToken)
            ?? await internalProvider.TryEstimateAsync(input, cancellationToken);

        if (estimate is null)
            return Result<PriceEstimateDto>.NotFound("No estimate available for this bottle.");

        // 3. Persist the chosen estimate to the read-through cache.
        await UpsertSnapshotAsync(snapshot, productKey, bottle.Barcode, bottle.Category, estimate, cancellationToken);

        return Result<PriceEstimateDto>.Ok(estimate);
    }

    public async Task<Result<PriceEstimateDto>> GetCachedBottleEstimateAsync(Guid bottleId, CancellationToken cancellationToken)
    {
        var bottle = await db.Bottles
            .AsNoTracking()
            .Where(b => b.Id == bottleId && !b.IsDeleted)
            .Select(b => new BottleKeyInfo(
                b.Id,
                b.Name,
                b.Distillery != null ? b.Distillery.Name : null,
                b.Category,
                b.Age,
                b.VintageYear,
                b.VolumeMl,
                b.Barcode,
                b.Condition))
            .FirstOrDefaultAsync(cancellationToken);

        if (bottle is null)
            return Result<PriceEstimateDto>.NotFound("Bottle not found.");

        var productKey = ProductKey.For(bottle.DistilleryName, bottle.Name, bottle.Category, bottle.Age, bottle.VintageYear, bottle.VolumeMl);

        // Cache-only: the request path never makes a billed Claude call. A missing snapshot returns a
        // successful result with null data — the controller renders 204 and the UI shows "—". The
        // pre-warm job (or a future async refresh) populates the cache out of band.
        var snapshot = await db.PriceSnapshots
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.ProductKey == productKey, cancellationToken);

        return snapshot is null
            ? Result<PriceEstimateDto>.Ok(null!)
            : Result<PriceEstimateDto>.Ok(MapToDto(snapshot));
    }

    public async Task<Result<CollectionValueDto>> GetCollectionValueAsync(Guid userId, CancellationToken cancellationToken)
    {
        var bottles = await db.Bottles
            .AsNoTracking()
            .Where(b => b.UserId == userId && !b.IsDeleted)
            .Select(b => new BottleKeyInfo(
                b.Id,
                b.Name,
                b.Distillery != null ? b.Distillery.Name : null,
                b.Category,
                b.Age,
                b.VintageYear,
                b.VolumeMl,
                b.Barcode,
                b.Condition))
            .ToListAsync(cancellationToken);

        var baseCurrency = pricingOptions.Value.BaseCurrency;

        if (bottles.Count == 0)
            return Result<CollectionValueDto>.Ok(new CollectionValueDto { Currency = baseCurrency });

        // Resolve each bottle's canonical key, then load the latest snapshot per key in one query.
        var keyByBottle = bottles.ToDictionary(
            b => b.Id,
            b => ProductKey.For(b.DistilleryName, b.Name, b.Category, b.Age, b.VintageYear, b.VolumeMl));

        var distinctKeys = keyByBottle.Values.Distinct().ToList();

        var snapshots = await db.PriceSnapshots
            .AsNoTracking()
            .Where(s => distinctKeys.Contains(s.ProductKey))
            .ToListAsync(cancellationToken);

        var snapshotByKey = snapshots.ToDictionary(s => s.ProductKey);

        var items = new List<BottlePriceLineDto>(bottles.Count);
        decimal total = 0m;
        var pricedCount = 0;

        foreach (var bottle in bottles)
        {
            snapshotByKey.TryGetValue(keyByBottle[bottle.Id], out var snapshot);

            // The total counts only Sealed bottles; per-bottle estimates are shown for all conditions.
            var countedInTotal = snapshot is not null && bottle.Condition == BottleCondition.Sealed;

            if (snapshot is not null)
                pricedCount++;

            if (countedInTotal)
                total += snapshot!.EstimatedPrice;

            items.Add(new BottlePriceLineDto
            {
                BottleId = bottle.Id,
                Name = bottle.Name,
                EstimatedPrice = snapshot?.EstimatedPrice,
                Currency = snapshot?.Currency ?? baseCurrency,
                Confidence = snapshot?.Confidence ?? PriceConfidence.Low,
                Source = snapshot?.Source ?? PriceSource.Internal,
                CountedInTotal = countedInTotal,
            });
        }

        return Result<CollectionValueDto>.Ok(new CollectionValueDto
        {
            TotalValue = decimal.Round(total, 2),
            Currency = baseCurrency,
            PricedCount = pricedCount,
            TotalCount = bottles.Count,
            Items = items,
        });
    }

    private async Task UpsertSnapshotAsync(
        PriceSnapshot? existing,
        string productKey,
        string? barcode,
        SpiritCategory category,
        PriceEstimateDto estimate,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var sourcesJson = JsonSerializer.Serialize(estimate.Sources);

        if (existing is null)
        {
            var snapshot = new PriceSnapshot { ProductKey = productKey };
            ApplyEstimate(snapshot, barcode, category, estimate, sourcesJson, now);
            db.PriceSnapshots.Add(snapshot);
        }
        else
        {
            ApplyEstimate(existing, barcode, category, estimate, sourcesJson, now);
            existing.UpdatedAt = now;
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Lost the upsert race: a concurrent run persisted a snapshot for the same ProductKey first
            // (unique index). The research is already paid for — re-apply it onto the winner's row so the
            // estimate is never dropped. The winner provably exists: the index just rejected our insert.
            db.ChangeTracker.Clear();

            var winner = await db.PriceSnapshots
                .FirstAsync(s => s.ProductKey == productKey, cancellationToken);

            ApplyEstimate(winner, barcode, category, estimate, sourcesJson, now);
            winner.UpdatedAt = now;

            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private static void ApplyEstimate(
        PriceSnapshot snapshot,
        string? barcode,
        SpiritCategory category,
        PriceEstimateDto estimate,
        string sourcesJson,
        DateTime now)
    {
        snapshot.Barcode = barcode ?? snapshot.Barcode;
        snapshot.Category = category;
        snapshot.EstimatedPrice = estimate.EstimatedPrice;
        snapshot.LowEstimate = estimate.LowEstimate;
        snapshot.HighEstimate = estimate.HighEstimate;
        snapshot.Currency = estimate.Currency;
        snapshot.SampleSize = estimate.SampleSize;
        snapshot.Source = estimate.Source;
        snapshot.Confidence = estimate.Confidence;
        snapshot.SourcesJson = sourcesJson;
        snapshot.AsOf = estimate.AsOf;
        snapshot.FetchedAt = now;
    }

    private static PriceEstimateDto MapToDto(PriceSnapshot snapshot) => new()
    {
        EstimatedPrice = snapshot.EstimatedPrice,
        LowEstimate = snapshot.LowEstimate,
        HighEstimate = snapshot.HighEstimate,
        Currency = snapshot.Currency,
        SampleSize = snapshot.SampleSize,
        Source = snapshot.Source,
        Confidence = snapshot.Confidence,
        AsOf = snapshot.AsOf,
        Sources = DeserializeSources(snapshot.SourcesJson),
    };

    private static IReadOnlyList<PriceCitation> DeserializeSources(string sourcesJson)
    {
        if (string.IsNullOrWhiteSpace(sourcesJson))
            return [];

        try
        {
            return JsonSerializer.Deserialize<List<PriceCitation>>(sourcesJson) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private sealed record BottleKeyInfo(
        Guid Id,
        string Name,
        string? DistilleryName,
        SpiritCategory Category,
        int? Age,
        int? VintageYear,
        int? VolumeMl,
        string? Barcode,
        BottleCondition Condition);
}
