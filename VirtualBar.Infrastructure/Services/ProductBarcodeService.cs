using Microsoft.EntityFrameworkCore;
using VirtualBar.Application.Common;
using VirtualBar.Application.DTOs.ProductBarcodes;
using VirtualBar.Application.Interfaces;
using VirtualBar.Infrastructure.Persistence;

namespace VirtualBar.Infrastructure.Services;

public sealed class ProductBarcodeService(AppDbContext db) : IProductBarcodeService
{
    public async Task<Result<PagedResult<ProductBarcodeDto>>> GetAllAsync(
        string? search, int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = db.ProductBarcodes
            .AsNoTracking()
            .Where(b => !b.IsDeleted && !b.Product.IsDeleted);

        if (search is not null)
            query = query.Where(b => b.Barcode.Contains(search)
                || b.Product.Name.Contains(search)
                || (b.Product.Brand != null && b.Product.Brand.Contains(search)));

        var rows = await query
            .OrderByDescending(b => b.CreatedAt)
            .ThenBy(b => b.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize + 1)
            .Select(b => new ProductBarcodeDto
            {
                Id = b.Id,
                Barcode = b.Barcode,
                ProductId = b.ProductId,
                ProductName = b.Product.Name,
                ProductBrand = b.Product.Brand
                    ?? (b.Product.Distillery != null && !b.Product.Distillery.IsDeleted ? b.Product.Distillery.Name : null),
                AddedByUserId = b.AddedByUserId,
                AddedByDisplayName = b.AddedByUser.DisplayName,
                CreatedAt = b.CreatedAt,
            })
            .ToListAsync(cancellationToken);

        await FillCollectorsAndConflictsAsync(rows, cancellationToken);

        return Result<PagedResult<ProductBarcodeDto>>.Ok(new PagedResult<ProductBarcodeDto>
        {
            Items = rows.Take(pageSize).ToList(),
            HasMore = rows.Count > pageSize,
        });
    }

    public async Task<Result<bool>> RemoveAsync(Guid id, CancellationToken cancellationToken)
    {
        var pair = await db.ProductBarcodes.FirstAsync(b => b.Id == id, cancellationToken);
        string[] codes = [.. BarcodeFormat.Equivalents(pair.Barcode)];

        // The same pair may have been recorded twice, or in another form: removing it removes them all.
        var rows = await db.ProductBarcodes
            .Where(b => b.ProductId == pair.ProductId && !b.IsDeleted && codes.Contains(b.Barcode))
            .ToListAsync(cancellationToken);

        var now = DateTime.UtcNow;

        foreach (var row in rows)
        {
            row.IsDeleted = true;
            row.DeletedAt = now;
            row.UpdatedAt = now;
        }

        await db.SaveChangesAsync(cancellationToken);

        return Result<bool>.Ok(true);
    }

    /// <summary>
    /// For the rows of one page: how many collectors back each pair, and whether its barcode, in any form, also belongs
    /// to another live product, through another collector or as that product's own barcode.
    /// </summary>
    private async Task FillCollectorsAndConflictsAsync(List<ProductBarcodeDto> rows, CancellationToken cancellationToken)
    {
        string[] codes = [.. rows.SelectMany(r => BarcodeFormat.Equivalents(r.Barcode)).Distinct()];

        var pairs = await db.ProductBarcodes
            .AsNoTracking()
            .Where(b => !b.IsDeleted && !b.Product.IsDeleted && codes.Contains(b.Barcode))
            .Select(b => new { b.Barcode, b.ProductId, CollectorId = b.AddedByUserId })
            .ToListAsync(cancellationToken);

        var bottles = await db.Bottles
            .AsNoTracking()
            .Where(b => !b.IsDeleted && b.ProductId != null && b.Barcode != null && codes.Contains(b.Barcode))
            .Select(b => new { Barcode = b.Barcode!, ProductId = b.ProductId!.Value, CollectorId = b.UserId })
            .ToListAsync(cancellationToken);

        var owners = await db.Products
            .AsNoTracking()
            .Where(p => !p.IsDeleted && p.Barcode != null && codes.Contains(p.Barcode))
            .Select(p => new { Barcode = p.Barcode!, ProductId = p.Id })
            .ToListAsync(cancellationToken);

        foreach (var row in rows)
        {
            var forms = BarcodeFormat.Equivalents(row.Barcode);

            row.Collectors = pairs.Where(p => p.ProductId == row.ProductId && forms.Contains(p.Barcode)).Select(p => p.CollectorId)
                .Concat(bottles.Where(b => b.ProductId == row.ProductId && forms.Contains(b.Barcode)).Select(b => b.CollectorId))
                .Distinct()
                .Count();

            row.Conflict = pairs.Any(p => p.ProductId != row.ProductId && forms.Contains(p.Barcode))
                || owners.Any(o => o.ProductId != row.ProductId && forms.Contains(o.Barcode));
        }
    }
}
