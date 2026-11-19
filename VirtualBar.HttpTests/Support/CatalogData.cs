using Microsoft.Extensions.DependencyInjection;
using VirtualBar.Domain.Entities;
using VirtualBar.Domain.Enums;
using VirtualBar.Infrastructure.Persistence;

namespace VirtualBar.HttpTests.Support;

/// <summary>Catalog products for the tests that need one with an image of its own.</summary>
internal static class CatalogData
{
    /// <summary>The image the products seeded here carry: an address on a test domain that nothing serves.</summary>
    public const string ProductImageUrl = "https://images.example.test/product.png";

    /// <summary>
    /// A new catalog product with <paramref name="imageUrl"/> (none when null). It goes straight into the app's
    /// database: there is no endpoint that creates a product without the admin request flow.
    /// </summary>
    public static async Task<Guid> SeedProductAsync(ApiFactory api, string? imageUrl = ProductImageUrl)
    {
        var name = CollectorApi.UniqueName("product");
        var product = new Product
        {
            Name = name,
            Category = SpiritCategory.Rum,
            CanonicalKey = name,
            Origin = ProductOrigin.Approved,
            ImageUrl = imageUrl,
        };

        await using var scope = api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Products.Add(product);
        await db.SaveChangesAsync();

        return product.Id;
    }
}
