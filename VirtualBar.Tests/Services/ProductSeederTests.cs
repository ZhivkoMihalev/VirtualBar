using System.Text;
using Microsoft.EntityFrameworkCore;
using VirtualBar.Application.Common;
using VirtualBar.Domain.Entities;
using VirtualBar.Domain.Enums;
using VirtualBar.Infrastructure.Persistence;

namespace VirtualBar.Tests.Services;

public sealed class ProductSeederTests
{
    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static Distillery SeedDistillery(AppDbContext db, string name)
    {
        var distillery = new Distillery { Name = name, Country = "Scotland" };
        db.Distilleries.Add(distillery);
        db.SaveChanges();
        return distillery;
    }

    private static Product SeedProduct(AppDbContext db, string name = "Pre-existing Product")
    {
        var product = new Product
        {
            Name = name,
            Category = SpiritCategory.Whisky,
            CanonicalKey = ProductKey.For(null, name, SpiritCategory.Whisky, null, null, null),
            Origin = ProductOrigin.Approved
        };
        db.Products.Add(product);
        db.SaveChanges();
        return product;
    }

    private static Stream SeedStream(string json) => new MemoryStream(Encoding.UTF8.GetBytes(json));

    #region Embedded resource

    [Fact]
    public async Task SeedProductsAsync_WhenCatalogEmpty_SeedsCuratedProducts()
    {
        var db = CreateDbContext();
        var macallan = SeedDistillery(db, "Macallan");

        await ProductSeeder.SeedProductsAsync(db, cancellationToken: CancellationToken.None);

        var products = await db.Products.AsNoTracking().ToListAsync();
        Assert.True(products.Count >= 200, $"Expected a curated starter seed, got {products.Count} rows.");
        Assert.All(products, p => Assert.Equal(ProductOrigin.Seeded, p.Origin));
        Assert.All(products, p => Assert.False(string.IsNullOrWhiteSpace(p.CanonicalKey)));
        Assert.All(products, p => Assert.False(string.IsNullOrWhiteSpace(p.Name)));
        Assert.Equal(products.Count, products.Select(p => p.CanonicalKey).Distinct().Count());

        // The catalog carries the same expression in several bottle sizes, so pin the size too.
        var sherryOak = products.Single(p => p.Name == "Sherry Oak 12 Year Old" && p.VolumeMl == 700);
        Assert.Equal(macallan.Id, sherryOak.DistilleryId);

        // Brand (the label) and DistilleryId (the site) are separate facts; a single malt carries both.
        Assert.Equal("Macallan", sherryOak.Brand);

        // The key still resolves through the distillery, so populating Brand cannot move it.
        Assert.Equal(
            ProductKey.For("Macallan", "Sherry Oak 12 Year Old", SpiritCategory.Whisky, 12, null, 700),
            sherryOak.CanonicalKey);
    }

    [Fact]
    public async Task SeedProductsAsync_WhenCatalogPartiallySeeded_TopsUpOnlyMissing()
    {
        var db = CreateDbContext();
        var preExisting = SeedProduct(db);

        await ProductSeeder.SeedProductsAsync(db, cancellationToken: CancellationToken.None);

        var products = await db.Products.AsNoTracking().ToListAsync();
        Assert.True(products.Count > 1, "Expected the seed to top up a partially seeded catalog.");
        Assert.Equal(products.Count, products.Select(p => p.CanonicalKey).Distinct().Count());

        var kept = products.Single(p => p.Id == preExisting.Id);
        Assert.Equal("Pre-existing Product", kept.Name);
        Assert.Equal(ProductOrigin.Approved, kept.Origin);
    }

    [Fact]
    public async Task SeedProductsAsync_WhenRunTwice_DoesNotDuplicate()
    {
        var db = CreateDbContext();
        SeedDistillery(db, "Macallan");

        await ProductSeeder.SeedProductsAsync(db, cancellationToken: CancellationToken.None);
        var afterFirstRun = await db.Products.CountAsync();

        await ProductSeeder.SeedProductsAsync(db, cancellationToken: CancellationToken.None);

        Assert.Equal(afterFirstRun, await db.Products.CountAsync());
    }

    [Fact]
    public async Task SeedProductsAsync_WhenCatalogEmpty_ReadsEveryCategorySeedFile()
    {
        var db = CreateDbContext();

        await ProductSeeder.SeedProductsAsync(db, cancellationToken: CancellationToken.None);

        var categories = await db.Products
            .AsNoTracking()
            .Select(p => p.Category)
            .Distinct()
            .ToListAsync();

        Assert.Contains(SpiritCategory.Whisky, categories);
        Assert.Contains(SpiritCategory.Rum, categories);
        Assert.Contains(SpiritCategory.Cognac, categories);
        Assert.Contains(SpiritCategory.Vodka, categories);
        Assert.Contains(SpiritCategory.Gin, categories);
        Assert.Contains(SpiritCategory.Tequila, categories);
        Assert.Contains(SpiritCategory.Brandy, categories);
        Assert.Contains(SpiritCategory.Other, categories);
    }

    #endregion

    #region Row rules

    [Fact]
    public async Task SeedFromStreamAsync_WhenDistilleryNameMatchesExactly_LinksDistillery()
    {
        var db = CreateDbContext();
        var macallan = SeedDistillery(db, "Macallan");
        const string json = """
        [{ "name": "Sherry Oak 12", "distillery": "Macallan", "category": "Whisky", "age": 12, "volumeMl": 700 }]
        """;

        await ProductSeeder.SeedFromStreamAsync(db, SeedStream(json), CancellationToken.None);

        var product = await db.Products.AsNoTracking().SingleAsync();
        Assert.Equal(macallan.Id, product.DistilleryId);
        Assert.Null(product.Brand);
        Assert.Equal(
            ProductKey.For("Macallan", "Sherry Oak 12", SpiritCategory.Whisky, 12, null, 700),
            product.CanonicalKey);
    }

    [Fact]
    public async Task SeedFromStreamAsync_WhenDistilleryNameDiffersInCasing_LinksDistillery()
    {
        var db = CreateDbContext();
        var macallan = SeedDistillery(db, "Macallan");
        const string json = """
        [{ "name": "Sherry Oak 12", "distillery": "mAcAlLaN", "category": "Whisky", "age": 12, "volumeMl": 700 }]
        """;

        await ProductSeeder.SeedFromStreamAsync(db, SeedStream(json), CancellationToken.None);

        var product = await db.Products.AsNoTracking().SingleAsync();
        Assert.Equal(macallan.Id, product.DistilleryId);
        Assert.Null(product.Brand);
    }

    /// <summary>
    /// With no brand of its own, an unresolved distillery name is the only label the row has, so it
    /// fills the gap. The companion case — a row that *does* carry a brand — is covered above; together
    /// they pin both sides of the fallback.
    /// </summary>
    [Fact]
    public async Task SeedFromStreamAsync_WhenDistilleryUnknownAndNoBrand_FallsBackToTheDistilleryName()
    {
        var db = CreateDbContext();
        SeedDistillery(db, "Macallan");
        const string json = """
        [{ "name": "Small Batch", "brand": null, "distillery": "Nowhere Distillery", "category": "Whisky", "volumeMl": 700 }]
        """;

        await ProductSeeder.SeedFromStreamAsync(db, SeedStream(json), CancellationToken.None);

        var product = await db.Products.AsNoTracking().SingleAsync();
        Assert.Null(product.DistilleryId);
        Assert.Equal("Nowhere Distillery", product.Brand);
        Assert.Equal(
            ProductKey.For("Nowhere Distillery", "Small Batch", SpiritCategory.Whisky, null, null, 700),
            product.CanonicalKey);
    }

    /// <summary>
    /// Regression: an unresolved distillery used to be copied straight over Brand, destroying the label
    /// the row actually carried. The canonical key still resolves through the distillery text, so the
    /// fix cannot move it — and it must not, ProductKey is frozen.
    /// </summary>
    [Fact]
    public async Task SeedFromStreamAsync_WhenDistilleryUnknownButBrandSet_KeepsTheBrand()
    {
        var db = CreateDbContext();
        const string json = """
            [{ "name": "Small Batch Gin", "brand": "Caorunn", "distillery": "Nowhere Distillery", "category": "Gin" }]
            """;

        await ProductSeeder.SeedFromStreamAsync(db, SeedStream(json), CancellationToken.None);

        var product = await db.Products.AsNoTracking().SingleAsync();

        Assert.Null(product.DistilleryId);
        Assert.Equal("Caorunn", product.Brand);
        Assert.Equal(
            ProductKey.For("Nowhere Distillery", "Small Batch Gin", SpiritCategory.Gin, null, null, null),
            product.CanonicalKey);
    }

    [Fact]
    public async Task SeedFromStreamAsync_WhenRowHasNoDistillery_KeepsBrandAndUsesItForTheKey()
    {
        var db = CreateDbContext();
        const string json = """
        [{ "name": "Reserva Exclusiva", "brand": "Diplomatico", "category": "Rum", "volumeMl": 700 }]
        """;

        await ProductSeeder.SeedFromStreamAsync(db, SeedStream(json), CancellationToken.None);

        var product = await db.Products.AsNoTracking().SingleAsync();
        Assert.Null(product.DistilleryId);
        Assert.Equal("Diplomatico", product.Brand);
        Assert.Equal(
            ProductKey.For("Diplomatico", "Reserva Exclusiva", SpiritCategory.Rum, null, null, 700),
            product.CanonicalKey);
    }

    [Fact]
    public async Task SeedFromStreamAsync_WhenDistilleryBlank_KeepsBrand()
    {
        var db = CreateDbContext();
        const string json = """
        [{ "name": "Reserva Exclusiva", "brand": "Diplomatico", "distillery": "   ", "category": "Rum" }]
        """;

        await ProductSeeder.SeedFromStreamAsync(db, SeedStream(json), CancellationToken.None);

        var product = await db.Products.AsNoTracking().SingleAsync();
        Assert.Null(product.DistilleryId);
        Assert.Equal("Diplomatico", product.Brand);
    }

    [Fact]
    public async Task SeedFromStreamAsync_WhenRowHasNeitherDistilleryNorBrand_ComputesKeyWithoutProducer()
    {
        var db = CreateDbContext();
        const string json = """
        [{ "name": "House Blend", "category": "Other" }]
        """;

        await ProductSeeder.SeedFromStreamAsync(db, SeedStream(json), CancellationToken.None);

        var product = await db.Products.AsNoTracking().SingleAsync();
        Assert.Null(product.DistilleryId);
        Assert.Null(product.Brand);
        Assert.Equal(
            ProductKey.For(null, "House Blend", SpiritCategory.Other, null, null, null),
            product.CanonicalKey);
    }

    [Fact]
    public async Task SeedFromStreamAsync_WhenCategoryUnparseable_SkipsRow()
    {
        var db = CreateDbContext();
        const string json = """
        [
          { "name": "Absinthe Verte", "category": "Absinthe" },
          { "name": "Kept Row", "category": "Gin" }
        ]
        """;

        await ProductSeeder.SeedFromStreamAsync(db, SeedStream(json), CancellationToken.None);

        var product = await db.Products.AsNoTracking().SingleAsync();
        Assert.Equal("Kept Row", product.Name);
    }

    [Fact]
    public async Task SeedFromStreamAsync_WhenCategoryDiffersInCasing_ParsesIt()
    {
        var db = CreateDbContext();
        const string json = """
        [{ "name": "London Dry", "category": "gin" }]
        """;

        await ProductSeeder.SeedFromStreamAsync(db, SeedStream(json), CancellationToken.None);

        var product = await db.Products.AsNoTracking().SingleAsync();
        Assert.Equal(SpiritCategory.Gin, product.Category);
    }

    [Fact]
    public async Task SeedFromStreamAsync_WhenRowsShareCanonicalKey_KeepsOnlyTheFirst()
    {
        var db = CreateDbContext();
        const string json = """
        [
          { "name": "Sherry Oak 12", "brand": "Macallan", "category": "Whisky", "age": 12, "volumeMl": 700, "description": "first" },
          { "name": "The Sherry Oak 12", "brand": "Macallan", "category": "Whisky", "age": 12, "volumeMl": 700, "description": "second" }
        ]
        """;

        await ProductSeeder.SeedFromStreamAsync(db, SeedStream(json), CancellationToken.None);

        var product = await db.Products.AsNoTracking().SingleAsync();
        Assert.Equal("first", product.Description);
    }

    [Fact]
    public async Task SeedFromStreamAsync_MapsEveryOptionalField()
    {
        var db = CreateDbContext();
        const string json = """
        [{
          "name": "Sherry Oak 12",
          "brand": "Macallan",
          "category": "Whisky",
          "country": "Scotland",
          "region": "Speyside",
          "age": 12,
          "abvPercent": 40.0,
          "volumeMl": 700,
          "barcode": "12345678",
          "imageUrl": "/uploads/products/macallan.webp",
          "description": "Sherry-seasoned oak."
        }]
        """;

        await ProductSeeder.SeedFromStreamAsync(db, SeedStream(json), CancellationToken.None);

        var product = await db.Products.AsNoTracking().SingleAsync();
        Assert.Equal("Sherry Oak 12", product.Name);
        Assert.Equal("Macallan", product.Brand);
        Assert.Equal(SpiritCategory.Whisky, product.Category);
        Assert.Equal("Scotland", product.Country);
        Assert.Equal("Speyside", product.Region);
        Assert.Equal(12, product.Age);
        Assert.Equal(40.0, product.AbvPercent);
        Assert.Equal(700, product.VolumeMl);
        Assert.Equal("12345678", product.Barcode);
        Assert.Equal("/uploads/products/macallan.webp", product.ImageUrl);
        Assert.Equal("Sherry-seasoned oak.", product.Description);
        Assert.Equal(ProductOrigin.Seeded, product.Origin);
        Assert.NotEqual(default, product.CreatedAt);
        Assert.NotEqual(default, product.UpdatedAt);
        Assert.NotEqual(Guid.Empty, product.Id);
    }

    [Fact]
    public async Task SeedFromStreamAsync_WhenPayloadIsNull_Throws()
    {
        var db = CreateDbContext();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => ProductSeeder.SeedFromStreamAsync(db, SeedStream("null"), CancellationToken.None));
    }

    [Fact]
    public async Task SeedFromStreamAsync_WhenPayloadEmpty_SeedsNothing()
    {
        var db = CreateDbContext();

        await ProductSeeder.SeedFromStreamAsync(db, SeedStream("[]"), CancellationToken.None);

        Assert.Equal(0, await db.Products.CountAsync());
    }

    #endregion

    #region Top-up

    [Fact]
    public async Task SeedFromStreamAsync_WhenKeyAlreadyExists_SkipsRowWithoutOverwriting()
    {
        var db = CreateDbContext();
        var existing = new Product
        {
            Name = "Sherry Oak 12",
            Brand = "Macallan",
            Category = SpiritCategory.Whisky,
            Age = 12,
            VolumeMl = 700,
            Description = "kept",
            CanonicalKey = ProductKey.For("Macallan", "Sherry Oak 12", SpiritCategory.Whisky, 12, null, 700),
            Origin = ProductOrigin.Approved
        };
        db.Products.Add(existing);
        db.SaveChanges();
        const string json = """
        [
          { "name": "Sherry Oak 12", "brand": "Macallan", "category": "Whisky", "age": 12, "volumeMl": 700, "description": "incoming" },
          { "name": "Fine Oak 15", "brand": "Macallan", "category": "Whisky", "age": 15, "volumeMl": 700 }
        ]
        """;

        await ProductSeeder.SeedFromStreamAsync(db, SeedStream(json), CancellationToken.None);

        var products = await db.Products.AsNoTracking().ToListAsync();
        Assert.Equal(2, products.Count);

        var kept = products.Single(p => p.Id == existing.Id);
        Assert.Equal("kept", kept.Description);
        Assert.Equal(ProductOrigin.Approved, kept.Origin);

        Assert.Contains(products, p => p.Name == "Fine Oak 15" && p.Origin == ProductOrigin.Seeded);
    }

    [Fact]
    public async Task SeedFromStreamAsync_WhenExistingNameDiffersOnlyInCasing_SkipsRow()
    {
        var db = CreateDbContext();
        SeedProduct(db, "Pre-existing Product");
        const string json = """
        [{ "name": "PRE EXISTING PRODUCT", "category": "Whisky" }]
        """;

        await ProductSeeder.SeedFromStreamAsync(db, SeedStream(json), CancellationToken.None);

        Assert.Equal(1, await db.Products.CountAsync());
    }

    [Fact]
    public async Task SeedFromStreamAsync_WhenExistingProductSoftDeleted_DoesNotReinsert()
    {
        var db = CreateDbContext();
        var existing = SeedProduct(db);
        existing.IsDeleted = true;
        existing.DeletedAt = DateTime.UtcNow;
        db.SaveChanges();
        const string json = """
        [{ "name": "Pre-existing Product", "category": "Whisky" }]
        """;

        await ProductSeeder.SeedFromStreamAsync(db, SeedStream(json), CancellationToken.None);

        Assert.Equal(1, await db.Products.CountAsync());
    }

    #endregion
}
