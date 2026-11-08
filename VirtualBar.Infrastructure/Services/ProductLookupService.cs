using Microsoft.EntityFrameworkCore;
using VirtualBar.Application.Common;
using VirtualBar.Application.DTOs.Products;
using VirtualBar.Application.Interfaces;
using VirtualBar.Infrastructure.Persistence;

namespace VirtualBar.Infrastructure.Services;

/// <summary>
/// Answers a barcode from the platform's own data only (#49): the catalog product's own barcode first, then the
/// barcodes collectors gave a product. There is no external barcode database any more: UPCitemdb answered with data
/// unfit to fill the add form (the owner dropped it on 2026-11-08). A hit is the whole catalog product, as the catalog
/// search returns it, so the form fills from it like a pick (#120).
/// </summary>
public sealed class ProductLookupService(AppDbContext db) : IProductLookupService
{
    public async Task<Result<ProductDto>> LookupByBarcodeAsync(string barcode, CancellationToken cancellationToken)
    {
        var product = await db.Products
            .AsNoTracking()
            .Where(p => !p.IsDeleted && p.Barcode == barcode)
            .OrderBy(p => p.CreatedAt)
            .ThenBy(p => p.Id)
            .Select(ProductMapping.ToDto)
            .FirstOrDefaultAsync(cancellationToken);

        if (product is not null)
            return Result<ProductDto>.Ok(product);

        // The first collector's link wins; several products for one barcode are rare (a mistaken scan).
        var linked = await db.ProductBarcodes
            .AsNoTracking()
            .Where(b => !b.IsDeleted && b.Barcode == barcode && !b.Product.IsDeleted)
            .OrderBy(b => b.CreatedAt)
            .ThenBy(b => b.Id)
            .Select(b => b.Product)
            .Select(ProductMapping.ToDto)
            .FirstOrDefaultAsync(cancellationToken);

        if (linked is not null)
            return Result<ProductDto>.Ok(linked);

        return Result<ProductDto>.NotFound("Product not found.");
    }
}
