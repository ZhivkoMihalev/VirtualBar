using Microsoft.EntityFrameworkCore;
using VirtualBar.Application.Common;
using VirtualBar.Application.DTOs.Products;
using VirtualBar.Application.Interfaces;
using VirtualBar.Domain.Enums;
using VirtualBar.Infrastructure.Persistence;

namespace VirtualBar.Infrastructure.Services;

public sealed class ProductCatalogService(AppDbContext db) : IProductCatalogService
{
    public async Task<Result<List<ProductDto>>> SearchAsync(string search, SpiritCategory? category, int limit, CancellationToken cancellationToken)
    {
        var q = db.Products
            .AsNoTracking()
            .Where(p => !p.IsDeleted);

        if (category.HasValue)
            q = q.Where(p => p.Category == category.Value);

        // Never ToLower() the column — it discards the collation and blocks any future index/full-text path on Name.
        var products = await q
            .Where(p => p.Name.Contains(search)
                || (p.Brand != null && p.Brand.Contains(search))
                || (p.Distillery != null && p.Distillery.Name.Contains(search)))
            .OrderByDescending(p => p.Name.StartsWith(search))
            .ThenBy(p => p.Name)
            .Take(limit)
            .Select(p => new ProductDto
            {
                Id = p.Id,
                Name = p.Name,
                // Brand is the label name and callers always want one. Seeded rows carry it, but an
                // admin-approved product may only have the distillery — fall back to it, exactly as
                // ProductLookupService does, so the two endpoints cannot disagree about the same product.
                Brand = p.Brand ?? (p.Distillery != null && !p.Distillery.IsDeleted ? p.Distillery.Name : null),
                DistilleryId = p.DistilleryId,
                DistilleryName = p.Distillery != null && !p.Distillery.IsDeleted ? p.Distillery.Name : null,
                Category = p.Category,
                Country = p.Country,
                Region = p.Region,
                Age = p.Age,
                AbvPercent = p.AbvPercent,
                VolumeMl = p.VolumeMl,
                Barcode = p.Barcode,
                ImageUrl = p.ImageUrl
            })
            .ToListAsync(cancellationToken);

        return Result<List<ProductDto>>.Ok(products);
    }
}
