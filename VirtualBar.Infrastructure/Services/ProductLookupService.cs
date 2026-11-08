using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using VirtualBar.Application.Common;
using VirtualBar.Application.DTOs.Products;
using VirtualBar.Application.Interfaces;
using VirtualBar.Domain.Entities;
using VirtualBar.Infrastructure.Persistence;

namespace VirtualBar.Infrastructure.Services;

/// <summary>
/// Answers a barcode from the platform's own data only (#49): the catalog product's own barcode first, then the
/// barcodes collectors gave a product. There is no external barcode database any more: UPCitemdb answered with data
/// unfit to fill the add form (the owner dropped it on 2026-11-08).
/// </summary>
public sealed class ProductLookupService(AppDbContext db) : IProductLookupService
{
    // Brand falls back to the non-deleted distillery name, exactly as the catalog search does.
    private static readonly Expression<Func<Product, BarcodeProductDto>> ToDto = p => new BarcodeProductDto
    {
        ProductId = p.Id,
        Name = p.Name,
        Brand = p.Brand ?? (p.Distillery != null && !p.Distillery.IsDeleted ? p.Distillery.Name : null),
        ImageUrl = p.ImageUrl,
        VolumeMl = p.VolumeMl,
        AbvPercent = p.AbvPercent,
    };

    public async Task<Result<BarcodeProductDto>> LookupByBarcodeAsync(string barcode, CancellationToken cancellationToken)
    {
        var product = await db.Products
            .AsNoTracking()
            .Where(p => !p.IsDeleted && p.Barcode == barcode)
            .OrderBy(p => p.CreatedAt)
            .ThenBy(p => p.Id)
            .Select(ToDto)
            .FirstOrDefaultAsync(cancellationToken);

        if (product is not null)
            return Result<BarcodeProductDto>.Ok(product);

        // The first collector's link wins; several products for one barcode are rare (a mistaken scan).
        var linked = await db.ProductBarcodes
            .AsNoTracking()
            .Where(b => !b.IsDeleted && b.Barcode == barcode && !b.Product.IsDeleted)
            .OrderBy(b => b.CreatedAt)
            .ThenBy(b => b.Id)
            .Select(b => b.Product)
            .Select(ToDto)
            .FirstOrDefaultAsync(cancellationToken);

        if (linked is not null)
            return Result<BarcodeProductDto>.Ok(linked);

        return Result<BarcodeProductDto>.NotFound("Product not found.");
    }
}
