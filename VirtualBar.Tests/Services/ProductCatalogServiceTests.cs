using Microsoft.EntityFrameworkCore;
using VirtualBar.Application.Interfaces;
using VirtualBar.Domain.Entities;
using VirtualBar.Domain.Enums;
using VirtualBar.Infrastructure.Decorators;
using VirtualBar.Infrastructure.Persistence;
using VirtualBar.Infrastructure.Services;

namespace VirtualBar.Tests.Services;

// Case sensitivity note: the API's case-insensitive matching comes from the SQL Server collation, not
// from the code. EF InMemory evaluates string.Contains/StartsWith ordinally, so every term below uses
// an exact-case substring of the seeded data — case-insensitivity is deliberately NOT asserted.
public sealed class ProductCatalogServiceTests
{
    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static IProductCatalogService CreateService(AppDbContext db) =>
        new ProductCatalogValidationDecorator(new ProductCatalogService(db));

    private static Distillery SeedDistillery(AppDbContext db, string name = "Macallan", bool isDeleted = false)
    {
        var distillery = new Distillery
        {
            Name = name,
            IsDeleted = isDeleted,
            DeletedAt = isDeleted ? DateTime.UtcNow : null
        };
        db.Distilleries.Add(distillery);
        db.SaveChanges();
        return distillery;
    }

    private static Product SeedProduct(
        AppDbContext db,
        string name = "Sherry Oak 12 Year Old",
        string? brand = null,
        Guid? distilleryId = null,
        SpiritCategory category = SpiritCategory.Whisky,
        bool isDeleted = false,
        int? age = null,
        int? volumeMl = null,
        string? barcode = null,
        string? imageUrl = null)
    {
        var product = new Product
        {
            Name = name,
            Brand = brand,
            DistilleryId = distilleryId,
            Category = category,
            Country = "Scotland",
            Region = "Speyside",
            Age = age,
            AbvPercent = 43.0,
            VolumeMl = volumeMl,
            Barcode = barcode,
            ImageUrl = imageUrl,
            CanonicalKey = Guid.NewGuid().ToString(),
            Origin = ProductOrigin.Seeded,
            IsDeleted = isDeleted,
            DeletedAt = isDeleted ? DateTime.UtcNow : null
        };
        db.Products.Add(product);
        db.SaveChanges();
        return product;
    }

    #region Decorator guards

    [Fact]
    public async Task SearchAsync_WithCancelledToken_Throws()
    {
        var db = CreateDbContext();
        var service = CreateService(db);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => service.SearchAsync("Macallan", null, 20, cts.Token));
    }

    [Fact]
    public async Task SearchAsync_WhenSearchNull_ReturnsFail()
    {
        var db = CreateDbContext();
        var service = CreateService(db);

        var result = await service.SearchAsync(null!, null, 20, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Search term must be at least 2 characters.", result.Error);
    }

    [Fact]
    public async Task SearchAsync_WhenSearchWhitespace_ReturnsFail()
    {
        var db = CreateDbContext();
        var service = CreateService(db);

        var result = await service.SearchAsync("   ", null, 20, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Search term must be at least 2 characters.", result.Error);
    }

    [Fact]
    public async Task SearchAsync_WhenSearchSingleCharacter_ReturnsFail()
    {
        var db = CreateDbContext();
        var service = CreateService(db);

        var result = await service.SearchAsync("a", null, 20, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Search term must be at least 2 characters.", result.Error);
    }

    [Fact]
    public async Task SearchAsync_WhenSearchTooLong_ReturnsFail()
    {
        var db = CreateDbContext();
        var service = CreateService(db);

        var result = await service.SearchAsync(new string('a', 101), null, 20, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Search term is too long.", result.Error);
    }

    [Fact]
    public async Task SearchAsync_WhenSearchAtMaximumLength_PassesToInner()
    {
        var db = CreateDbContext();
        SeedProduct(db, name: new string('a', 100));
        var service = CreateService(db);

        var result = await service.SearchAsync(new string('a', 100), null, 20, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Single(result.Data!);
    }

    [Fact]
    public async Task SearchAsync_WhenTermPadded_TrimsBeforeMatching()
    {
        var db = CreateDbContext();
        SeedProduct(db, name: "Lagavulin 16");
        var service = CreateService(db);

        var result = await service.SearchAsync("  av  ", null, 20, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Single(result.Data!);
        Assert.Equal("Lagavulin 16", result.Data![0].Name);
    }

    [Fact]
    public async Task SearchAsync_WhenLimitZero_AppliesDefaultOfTwenty()
    {
        var db = CreateDbContext();
        for (var i = 0; i < 25; i++)
            SeedProduct(db, name: $"Glen Product {i:D2}");
        var service = CreateService(db);

        var result = await service.SearchAsync("Glen", null, 0, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(20, result.Data!.Count);
    }

    [Fact]
    public async Task SearchAsync_WhenLimitNegative_AppliesDefaultOfTwenty()
    {
        var db = CreateDbContext();
        for (var i = 0; i < 25; i++)
            SeedProduct(db, name: $"Glen Product {i:D2}");
        var service = CreateService(db);

        var result = await service.SearchAsync("Glen", null, -5, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(20, result.Data!.Count);
    }

    [Fact]
    public async Task SearchAsync_WhenLimitAboveMaximum_ClampsToFiftyOne()
    {
        var db = CreateDbContext();
        for (var i = 0; i < 60; i++)
            SeedProduct(db, name: $"Glen Product {i:D2}");
        var service = CreateService(db);

        var result = await service.SearchAsync("Glen", null, 999, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(51, result.Data!.Count);
    }

    /// <summary>
    /// The cap is one over what the UI renders on purpose: asking for 51 and getting 51 back is how the
    /// client knows there are more than 50 matches, instead of guessing from a full page.
    /// </summary>
    [Fact]
    public async Task SearchAsync_WhenExactlyFiftyMatchesAndProbeLimitRequested_ReturnsFifty()
    {
        var db = CreateDbContext();
        for (var i = 0; i < 50; i++)
            SeedProduct(db, name: $"Glen Product {i:D2}");
        var service = CreateService(db);

        var result = await service.SearchAsync("Glen", null, 51, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(50, result.Data!.Count);
    }

    [Fact]
    public async Task SearchAsync_WhenMoreThanFiftyMatchesAndProbeLimitRequested_ReturnsFiftyOne()
    {
        var db = CreateDbContext();
        for (var i = 0; i < 51; i++)
            SeedProduct(db, name: $"Glen Product {i:D2}");
        var service = CreateService(db);

        var result = await service.SearchAsync("Glen", null, 51, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(51, result.Data!.Count);
    }

    [Fact]
    public async Task SearchAsync_WhenLimitWithinRange_PassesItThrough()
    {
        var db = CreateDbContext();
        for (var i = 0; i < 5; i++)
            SeedProduct(db, name: $"Glen Product {i:D2}");
        var service = CreateService(db);

        var result = await service.SearchAsync("Glen", null, 3, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(3, result.Data!.Count);
    }

    #endregion

    #region Matching

    [Fact]
    public async Task SearchAsync_WhenTermMatchesName_ReturnsProduct()
    {
        var db = CreateDbContext();
        SeedProduct(db, name: "Sherry Oak 12 Year Old", brand: "Highland Brand");
        var service = CreateService(db);

        var result = await service.SearchAsync("herry", null, 20, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Single(result.Data!);
        Assert.Equal("Sherry Oak 12 Year Old", result.Data![0].Name);
    }

    [Fact]
    public async Task SearchAsync_WhenBrandNullAndNameMatches_ReturnsProduct()
    {
        var db = CreateDbContext();
        SeedProduct(db, name: "Sherry Oak 12 Year Old", brand: null);
        var service = CreateService(db);

        var result = await service.SearchAsync("herry", null, 20, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Single(result.Data!);
        Assert.Null(result.Data![0].Brand);
    }

    [Fact]
    public async Task SearchAsync_WhenTermMatchesBrandOnly_ReturnsProduct()
    {
        var db = CreateDbContext();
        SeedProduct(db, name: "Reserva Exclusiva", brand: "Diplomatico");
        var service = CreateService(db);

        var result = await service.SearchAsync("iplomatico", null, 20, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Single(result.Data!);
        Assert.Equal("Diplomatico", result.Data![0].Brand);
    }

    [Fact]
    public async Task SearchAsync_WhenTermMatchesDistilleryNameOnly_ReturnsProduct()
    {
        var db = CreateDbContext();
        var distillery = SeedDistillery(db, "Macallan");
        SeedProduct(db, name: "Sherry Oak 12 Year Old", brand: null, distilleryId: distillery.Id);
        var service = CreateService(db);

        var result = await service.SearchAsync("acallan", null, 20, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Single(result.Data!);
        Assert.Equal("Macallan", result.Data![0].DistilleryName);
        Assert.Equal(distillery.Id, result.Data[0].DistilleryId);
    }

    [Fact]
    public async Task SearchAsync_WhenBrandNullButDistilleryKnown_FallsBackToDistilleryName()
    {
        var db = CreateDbContext();
        var distillery = SeedDistillery(db, "Macallan");
        SeedProduct(db, name: "Sherry Oak 12 Year Old", brand: null, distilleryId: distillery.Id);
        var service = CreateService(db);

        var result = await service.SearchAsync("herry", null, 20, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Single(result.Data!);

        // Same contract as the barcode lookup: callers always get a brand when one can be derived.
        Assert.Equal("Macallan", result.Data![0].Brand);
    }

    [Fact]
    public async Task SearchAsync_WhenBrandNullAndDistillerySoftDeleted_LeavesBrandNull()
    {
        var db = CreateDbContext();
        var distillery = SeedDistillery(db, "Macallan", isDeleted: true);
        SeedProduct(db, name: "Sherry Oak 12 Year Old", brand: null, distilleryId: distillery.Id);
        var service = CreateService(db);

        var result = await service.SearchAsync("herry", null, 20, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Single(result.Data!);
        Assert.Null(result.Data![0].Brand);
        Assert.Null(result.Data[0].DistilleryName);
    }

    [Fact]
    public async Task SearchAsync_WhenBrandSetAndDistilleryDiffers_KeepsTheBrand()
    {
        var db = CreateDbContext();
        var distillery = SeedDistillery(db, "Balmenach");
        SeedProduct(db, name: "Small Batch Gin", brand: "Caorunn", distilleryId: distillery.Id);
        var service = CreateService(db);

        var result = await service.SearchAsync("Small", null, 20, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Single(result.Data!);

        // A gin distilled at someone else's site keeps its own label name.
        Assert.Equal("Caorunn", result.Data![0].Brand);
        Assert.Equal("Balmenach", result.Data[0].DistilleryName);
    }

    [Fact]
    public async Task SearchAsync_WhenNothingMatches_ReturnsEmptyList()
    {
        var db = CreateDbContext();
        var distillery = SeedDistillery(db, "Macallan");
        SeedProduct(db, name: "Sherry Oak 12 Year Old", brand: "Highland Brand", distilleryId: distillery.Id);
        var service = CreateService(db);

        var result = await service.SearchAsync("zzz", null, 20, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Empty(result.Data!);
    }

    [Fact]
    public async Task SearchAsync_WhenProductSoftDeleted_ExcludesIt()
    {
        var db = CreateDbContext();
        SeedProduct(db, name: "Glenfiddich 12", isDeleted: true);
        SeedProduct(db, name: "Glenlivet 12");
        var service = CreateService(db);

        var result = await service.SearchAsync("Glen", null, 20, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Single(result.Data!);
        Assert.Equal("Glenlivet 12", result.Data![0].Name);
    }

    [Fact]
    public async Task SearchAsync_WhenCategoryFilterSet_ReturnsOnlyThatCategory()
    {
        var db = CreateDbContext();
        SeedProduct(db, name: "Reserva Whisky", category: SpiritCategory.Whisky);
        SeedProduct(db, name: "Reserva Rum", category: SpiritCategory.Rum);
        var service = CreateService(db);

        var result = await service.SearchAsync("Reserva", SpiritCategory.Rum, 20, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Single(result.Data!);
        Assert.Equal("Reserva Rum", result.Data![0].Name);
        Assert.Equal(SpiritCategory.Rum, result.Data[0].Category);
    }

    [Fact]
    public async Task SearchAsync_WhenCategoryFilterNull_ReturnsAllCategories()
    {
        var db = CreateDbContext();
        SeedProduct(db, name: "Reserva Whisky", category: SpiritCategory.Whisky);
        SeedProduct(db, name: "Reserva Rum", category: SpiritCategory.Rum);
        var service = CreateService(db);

        var result = await service.SearchAsync("Reserva", null, 20, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(2, result.Data!.Count);
    }

    [Fact]
    public async Task SearchAsync_WhenCategoryFilterMatchesNothing_ReturnsEmptyList()
    {
        var db = CreateDbContext();
        SeedProduct(db, name: "Reserva Whisky", category: SpiritCategory.Whisky);
        var service = CreateService(db);

        var result = await service.SearchAsync("Reserva", SpiritCategory.Gin, 20, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Empty(result.Data!);
    }

    [Fact]
    public async Task SearchAsync_WhenBothStartsWithAndContainsMatch_RanksStartsWithFirst()
    {
        var db = CreateDbContext();
        SeedProduct(db, name: "Old Glenmorangie");
        SeedProduct(db, name: "Glenlivet 12");
        SeedProduct(db, name: "Glenfiddich 12");
        var service = CreateService(db);

        var result = await service.SearchAsync("Glen", null, 20, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(
            new[] { "Glenfiddich 12", "Glenlivet 12", "Old Glenmorangie" },
            result.Data!.Select(p => p.Name).ToArray());
    }

    #endregion

    #region Projection

    [Fact]
    public async Task SearchAsync_WhenDistillerySoftDeleted_ReturnsNullDistilleryName()
    {
        var db = CreateDbContext();
        var distillery = SeedDistillery(db, "Macallan", isDeleted: true);
        SeedProduct(db, name: "Sherry Oak 12 Year Old", distilleryId: distillery.Id);
        var service = CreateService(db);

        // The match itself deliberately ignores IsDeleted (only the projection is soft-delete aware).
        var result = await service.SearchAsync("acallan", null, 20, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Single(result.Data!);
        Assert.Null(result.Data![0].DistilleryName);
        Assert.Equal(distillery.Id, result.Data[0].DistilleryId);
    }

    [Fact]
    public async Task SearchAsync_WhenProductHasNoDistillery_ReturnsNullDistilleryName()
    {
        var db = CreateDbContext();
        SeedProduct(db, name: "Sherry Oak 12 Year Old", distilleryId: null);
        var service = CreateService(db);

        var result = await service.SearchAsync("herry", null, 20, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Null(result.Data![0].DistilleryName);
        Assert.Null(result.Data[0].DistilleryId);
    }

    [Fact]
    public async Task SearchAsync_WhenMatchFound_ProjectsAllFields()
    {
        var db = CreateDbContext();
        var distillery = SeedDistillery(db, "Macallan");
        var product = SeedProduct(
            db,
            name: "Sherry Oak 12 Year Old",
            brand: "The Macallan",
            distilleryId: distillery.Id,
            age: 12,
            volumeMl: 700,
            barcode: "12345678",
            imageUrl: "/uploads/bottles/macallan.webp");
        var service = CreateService(db);

        var result = await service.SearchAsync("herry", null, 20, CancellationToken.None);

        Assert.True(result.Success);
        var dto = Assert.Single(result.Data!);
        Assert.Equal(product.Id, dto.Id);
        Assert.Equal("Sherry Oak 12 Year Old", dto.Name);
        Assert.Equal("The Macallan", dto.Brand);
        Assert.Equal(distillery.Id, dto.DistilleryId);
        Assert.Equal("Macallan", dto.DistilleryName);
        Assert.Equal(SpiritCategory.Whisky, dto.Category);
        Assert.Equal("Scotland", dto.Country);
        Assert.Equal("Speyside", dto.Region);
        Assert.Equal(12, dto.Age);
        Assert.Equal(43.0, dto.AbvPercent);
        Assert.Equal(700, dto.VolumeMl);
        Assert.Equal("12345678", dto.Barcode);
        Assert.Equal("/uploads/bottles/macallan.webp", dto.ImageUrl);
    }

    #endregion
}
