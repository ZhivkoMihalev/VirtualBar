using System.Linq.Expressions;
using VirtualBar.Application.DTOs.Products;
using VirtualBar.Domain.Entities;

namespace VirtualBar.Infrastructure.Services;

/// <summary>
/// The one <see cref="Product"/> → <see cref="ProductDto"/> projection, shared by the catalog search
/// (<see cref="ProductCatalogService"/>) and the barcode lookup (<see cref="ProductLookupService"/>), so a product
/// reaches the add form the same way whichever way it was found (#120). An expression, so EF translates it to SQL.
/// </summary>
internal static class ProductMapping
{
    public static readonly Expression<Func<Product, ProductDto>> ToDto = p => new ProductDto
    {
        Id = p.Id,
        Name = p.Name,
        // Brand is the label name and callers always want one. Seeded rows carry it, but an admin-approved product
        // may only have the distillery: fall back to it.
        Brand = p.Brand ?? (p.Distillery != null && !p.Distillery.IsDeleted ? p.Distillery.Name : null),
        DistilleryId = p.DistilleryId,
        DistilleryName = p.Distillery != null && !p.Distillery.IsDeleted ? p.Distillery.Name : null,
        Category = p.Category,
        Country = p.Country,
        Region = p.Region,
        Age = p.Age,
        VintageYear = p.VintageYear,
        ReleaseYear = p.ReleaseYear,
        AbvPercent = p.AbvPercent,
        VolumeMl = p.VolumeMl,
        Barcode = p.Barcode,
        ImageUrl = p.ImageUrl
    };
}
