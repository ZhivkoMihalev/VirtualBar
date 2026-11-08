using Microsoft.EntityFrameworkCore;
using VirtualBar.Application.Common;
using VirtualBar.Application.DTOs.Products;
using VirtualBar.Application.Interfaces;
using VirtualBar.Domain.Enums;
using VirtualBar.Infrastructure.Persistence;

namespace VirtualBar.Infrastructure.Services;

/// <summary>
/// Answers a barcode from the platform's own data only (#49), in any of the forms the same number can be written
/// (<see cref="BarcodeFormat.Equivalents"/>):
/// <list type="number">
/// <item>the catalog product's own barcode;</item>
/// <item>else the barcode collectors gave a product, the product most collectors back winning;</item>
/// <item>else what collectors' bottles that are not in the catalog say about it, as a suggestion.</item>
/// </list>
/// There is no external barcode database: UPCitemdb answered with data unfit to fill the add form (the owner dropped it
/// on 2026-11-08). A product is returned whole, as the catalog search returns it, so the form fills from it like a pick
/// (#120).
/// </summary>
public sealed class ProductLookupService(AppDbContext db) : IProductLookupService
{
    public async Task<Result<BarcodeLookupDto>> LookupByBarcodeAsync(string barcode, CancellationToken cancellationToken)
    {
        string[] codes = [.. BarcodeFormat.Equivalents(barcode)];

        var own = await db.Products
            .AsNoTracking()
            .Where(p => !p.IsDeleted && p.Barcode != null && codes.Contains(p.Barcode))
            .OrderBy(p => p.CreatedAt)
            .ThenBy(p => p.Id)
            .Select(ProductMapping.ToDto)
            .FirstOrDefaultAsync(cancellationToken);

        if (own is not null)
            return Result<BarcodeLookupDto>.Ok(new BarcodeLookupDto { Source = BarcodeSource.Catalog, Product = own });

        var given = await GivenByCollectorsAsync(codes, cancellationToken);

        if (given is not null)
            return Result<BarcodeLookupDto>.Ok(given);

        var suggested = await SuggestedByBottlesAsync(codes, cancellationToken);

        if (suggested is not null)
            return Result<BarcodeLookupDto>.Ok(suggested);

        return Result<BarcodeLookupDto>.NotFound("Product not found.");
    }

    /// <summary>
    /// The product collectors gave the barcode to. Several products for one barcode come from a mistaken scan, so the
    /// one most collectors back wins (those who recorded the pair and those whose live bottles carry it), then the
    /// oldest. A pair an admin removed rules its product out, whatever the bottles say.
    /// </summary>
    private async Task<BarcodeLookupDto?> GivenByCollectorsAsync(string[] codes, CancellationToken cancellationToken)
    {
        var pairs = await db.ProductBarcodes
            .AsNoTracking()
            .Where(b => codes.Contains(b.Barcode) && !b.Product.IsDeleted)
            .Select(b => new { b.ProductId, b.AddedByUserId, b.IsDeleted, b.CreatedAt })
            .ToListAsync(cancellationToken);

        var removed = pairs.Where(p => p.IsDeleted).Select(p => p.ProductId).ToHashSet();
        var live = pairs.Where(p => !p.IsDeleted && !removed.Contains(p.ProductId)).ToList();

        if (live.Count == 0)
            return null;

        var productIds = live.Select(p => p.ProductId).Distinct().ToList();
        var owners = await db.Bottles
            .AsNoTracking()
            .Where(b => !b.IsDeleted
                && b.Barcode != null
                && codes.Contains(b.Barcode)
                && b.ProductId != null
                && productIds.Contains(b.ProductId.Value))
            .Select(b => new { ProductId = b.ProductId!.Value, b.UserId })
            .ToListAsync(cancellationToken);

        var best = live
            .GroupBy(p => p.ProductId)
            .Select(g => new
            {
                ProductId = g.Key,
                Collectors = g.Select(p => p.AddedByUserId)
                    .Concat(owners.Where(o => o.ProductId == g.Key).Select(o => o.UserId))
                    .Distinct()
                    .Count(),
                Since = g.Min(p => p.CreatedAt),
            })
            .OrderByDescending(c => c.Collectors)
            .ThenBy(c => c.Since)
            .ThenBy(c => c.ProductId)
            .First();

        var product = await db.Products
            .AsNoTracking()
            .Where(p => p.Id == best.ProductId)
            .Select(ProductMapping.ToDto)
            .FirstAsync(cancellationToken);

        return new BarcodeLookupDto { Source = BarcodeSource.Collectors, Product = product, Collectors = best.Collectors };
    }

    /// <summary>
    /// What the live bottles with the barcode that are not in the catalog say: each field is the value most of them
    /// carry, the most recent bottle's breaking a tie. They are bottles whose product is waiting for an admin.
    /// </summary>
    private async Task<BarcodeLookupDto?> SuggestedByBottlesAsync(string[] codes, CancellationToken cancellationToken)
    {
        var bottles = await db.Bottles
            .AsNoTracking()
            .Where(b => !b.IsDeleted && b.ProductId == null && b.Barcode != null && codes.Contains(b.Barcode))
            .Select(b => new BottleFacts(
                b.UserId,
                b.CreatedAt,
                b.Name,
                b.Category,
                b.Distillery != null && !b.Distillery.IsDeleted ? b.DistilleryId : null,
                b.Distillery != null && !b.Distillery.IsDeleted ? b.Distillery.Name : null,
                b.Age,
                b.VintageYear,
                b.AbvPercent,
                b.VolumeMl,
                b.Country,
                b.Region))
            .ToListAsync(cancellationToken);

        if (bottles.Count == 0)
            return null;

        var distillery = Vote(bottles, b => b.DistilleryId is Guid id ? new DistilleryChoice(id, b.DistilleryName!) : null);

        return new BarcodeLookupDto
        {
            Source = BarcodeSource.Bottles,
            Collectors = bottles.Select(b => b.UserId).Distinct().Count(),
            Suggestion = new BarcodeSuggestionDto
            {
                Name = Vote(bottles, b => b.Name.Trim())!,
                Category = Vote(bottles, b => (SpiritCategory?)b.Category)!.Value,
                DistilleryId = distillery?.Id,
                DistilleryName = distillery?.Name,
                Age = Vote(bottles, b => b.Age),
                VintageYear = Vote(bottles, b => b.VintageYear),
                AbvPercent = Vote(bottles, b => b.AbvPercent),
                VolumeMl = Vote(bottles, b => b.VolumeMl),
                Country = Vote(bottles, b => Text(b.Country)),
                Region = Vote(bottles, b => Text(b.Region)),
            },
        };
    }

    /// <summary>The value most bottles carry, ignoring the ones without; a tie goes to the most recent bottle's.</summary>
    private static T? Vote<T>(List<BottleFacts> bottles, Func<BottleFacts, T?> field) =>
        bottles
            .Select(b => (Value: field(b), b.CreatedAt))
            .Where(v => v.Value is not null)
            .GroupBy(v => v.Value)
            .OrderByDescending(g => g.Count())
            .ThenByDescending(g => g.Max(v => v.CreatedAt))
            .Select(g => g.Key)
            .FirstOrDefault();

    private static string? Text(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed record DistilleryChoice(Guid Id, string Name);

    private sealed record BottleFacts(
        Guid UserId,
        DateTime CreatedAt,
        string Name,
        SpiritCategory Category,
        Guid? DistilleryId,
        string? DistilleryName,
        int? Age,
        int? VintageYear,
        double? AbvPercent,
        int? VolumeMl,
        string? Country,
        string? Region);
}
