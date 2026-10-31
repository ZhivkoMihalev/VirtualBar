using Microsoft.EntityFrameworkCore;
using VirtualBar.Application.Common;
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
        new ProductCatalogValidationDecorator(new ProductCatalogService(db), db);

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

    #region Product page seed helpers

    private static AppUser SeedUser(AppDbContext db, string displayName = "Collector", string? avatarUrl = null)
    {
        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            UserName = $"{displayName}-{Guid.NewGuid():N}@example.com",
            Email = $"{displayName}-{Guid.NewGuid():N}@example.com",
            DisplayName = displayName,
            AvatarUrl = avatarUrl
        };
        db.Users.Add(user);
        db.SaveChanges();
        return user;
    }

    private static Bottle SeedLinkedBottle(
        AppDbContext db,
        Guid? productId,
        Guid userId,
        int quantity = 1,
        bool isForSale = false,
        int forSaleQuantity = 0,
        DateTime? forSaleAt = null,
        decimal? askingPrice = null,
        int? vintageYear = null,
        bool isDeleted = false)
    {
        var bottle = new Bottle
        {
            UserId = userId,
            ProductId = productId,
            Name = "Linked bottle",
            Category = SpiritCategory.Whisky,
            Condition = BottleCondition.Sealed,
            Quantity = quantity,
            IsForSale = isForSale,
            ForSaleQuantity = forSaleQuantity,
            ForSaleAt = forSaleAt,
            AskingPrice = askingPrice,
            Currency = askingPrice is null ? null : "EUR",
            VintageYear = vintageYear,
            IsDeleted = isDeleted,
            DeletedAt = isDeleted ? DateTime.UtcNow : null
        };
        db.Bottles.Add(bottle);
        db.SaveChanges();
        return bottle;
    }

    private static BottleReview SeedReview(
        AppDbContext db,
        Guid bottleId,
        Guid userId,
        int score = 90,
        bool isDeleted = false,
        DateTime? createdAt = null,
        IEnumerable<FlavorTag>? flavors = null)
    {
        var review = new BottleReview
        {
            BottleId = bottleId,
            UserId = userId,
            Score = score,
            Summary = $"Review scored {score}",
            IsDeleted = isDeleted,
            DeletedAt = isDeleted ? DateTime.UtcNow : null,
            Flavors = (flavors ?? []).Select(f => new BottleReviewFlavor { Flavor = f }).ToList()
        };
        if (createdAt is not null)
            review.CreatedAt = createdAt.Value;
        db.BottleReviews.Add(review);
        db.SaveChanges();
        return review;
    }

    #endregion

    #region BrowseAsync — decorator guards

    [Fact]
    public async Task BrowseAsync_WithCancelledToken_Throws()
    {
        var service = CreateService(CreateDbContext());
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => service.BrowseAsync(null, null, 1, 24, cts.Token));
    }

    [Fact]
    public async Task BrowseAsync_WhenSearchNull_ListsWholeCatalog()
    {
        var db = CreateDbContext();
        SeedProduct(db, name: "Glenlivet 12");
        SeedProduct(db, name: "Diplomatico Reserva");
        var service = CreateService(db);

        var result = await service.BrowseAsync(null, null, 1, 24, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(2, result.Data!.Items.Count);
    }

    [Fact]
    public async Task BrowseAsync_WhenSearchWhitespace_TreatsItAsNoFilter()
    {
        var db = CreateDbContext();
        SeedProduct(db, name: "Glenlivet 12");
        SeedProduct(db, name: "Diplomatico Reserva");
        var service = CreateService(db);

        var result = await service.BrowseAsync("   ", null, 1, 24, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(2, result.Data!.Items.Count);
    }

    [Fact]
    public async Task BrowseAsync_WhenSearchSingleCharacter_ReturnsFail()
    {
        var service = CreateService(CreateDbContext());

        var result = await service.BrowseAsync(" a ", null, 1, 24, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Search term must be at least 2 characters.", result.Error);
    }

    [Fact]
    public async Task BrowseAsync_WhenSearchTooLong_ReturnsFail()
    {
        var service = CreateService(CreateDbContext());

        var result = await service.BrowseAsync(new string('a', 101), null, 1, 24, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Search term is too long.", result.Error);
    }

    [Fact]
    public async Task BrowseAsync_WhenTermPadded_TrimsBeforeMatching()
    {
        var db = CreateDbContext();
        SeedProduct(db, name: "Lagavulin 16");
        SeedProduct(db, name: "Glenlivet 12");
        var service = CreateService(db);

        var result = await service.BrowseAsync("  av  ", null, 1, 24, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("Lagavulin 16", Assert.Single(result.Data!.Items).Name);
    }

    [Fact]
    public async Task BrowseAsync_WhenPageBelowOne_ReturnsFirstPage()
    {
        var db = CreateDbContext();
        SeedProduct(db, name: "Glenlivet 12");
        var service = CreateService(db);

        var result = await service.BrowseAsync(null, null, 0, 24, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Single(result.Data!.Items);
    }

    [Fact]
    public async Task BrowseAsync_WhenPageAboveMaximum_ReturnsFail()
    {
        var service = CreateService(CreateDbContext());

        var result = await service.BrowseAsync(null, null, 1001, 24, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Page is out of range.", result.Error);
    }

    [Fact]
    public async Task BrowseAsync_WhenPageAtMaximum_ReturnsEmptyPage()
    {
        var db = CreateDbContext();
        SeedProduct(db, name: "Glenlivet 12");
        var service = CreateService(db);

        var result = await service.BrowseAsync(null, null, 1000, 24, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Empty(result.Data!.Items);
        Assert.False(result.Data.HasMore);
    }

    [Fact]
    public async Task BrowseAsync_WhenPageSizeBelowOne_AppliesDefaultOfTwentyFour()
    {
        var db = CreateDbContext();
        for (var i = 0; i < 30; i++)
            SeedProduct(db, name: $"Glen Product {i:D2}");
        var service = CreateService(db);

        var result = await service.BrowseAsync(null, null, 1, 0, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(24, result.Data!.Items.Count);
        Assert.True(result.Data.HasMore);
    }

    [Fact]
    public async Task BrowseAsync_WhenPageSizeAboveMaximum_ClampsToFortyEight()
    {
        var db = CreateDbContext();
        for (var i = 0; i < 60; i++)
            SeedProduct(db, name: $"Glen Product {i:D2}");
        var service = CreateService(db);

        var result = await service.BrowseAsync(null, null, 1, 500, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(48, result.Data!.Items.Count);
        Assert.True(result.Data.HasMore);
    }

    #endregion

    #region BrowseAsync — listing

    [Fact]
    public async Task BrowseAsync_WithoutSearch_OrdersByNameAndExcludesDeleted()
    {
        var db = CreateDbContext();
        SeedProduct(db, name: "Lagavulin 16");
        SeedProduct(db, name: "Ardbeg 10");
        SeedProduct(db, name: "Bowmore 12", isDeleted: true);
        SeedProduct(db, name: "Caol Ila 12");
        var service = CreateService(db);

        var result = await service.BrowseAsync(null, null, 1, 24, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(
            new[] { "Ardbeg 10", "Caol Ila 12", "Lagavulin 16" },
            result.Data!.Items.Select(p => p.Name).ToArray());
        Assert.False(result.Data.HasMore);
    }

    [Fact]
    public async Task BrowseAsync_WithSearch_MatchesNameBrandAndDistilleryAndRanksPrefixFirst()
    {
        var db = CreateDbContext();
        var distillery = SeedDistillery(db, "Glenmorangie Distillery");
        SeedProduct(db, name: "Old Glen Reserve");
        SeedProduct(db, name: "Glenlivet 12");
        SeedProduct(db, name: "Signet", distilleryId: distillery.Id);
        SeedProduct(db, name: "Twelve", brand: "Glendronach");
        SeedProduct(db, name: "Lagavulin 16");
        var service = CreateService(db);

        var result = await service.BrowseAsync("Glen", null, 1, 24, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(
            new[] { "Glenlivet 12", "Old Glen Reserve", "Signet", "Twelve" },
            result.Data!.Items.Select(p => p.Name).ToArray());
    }

    [Fact]
    public async Task BrowseAsync_WhenCategorySet_ReturnsOnlyThatCategory()
    {
        var db = CreateDbContext();
        SeedProduct(db, name: "Reserva Whisky", category: SpiritCategory.Whisky);
        SeedProduct(db, name: "Reserva Rum", category: SpiritCategory.Rum);
        var service = CreateService(db);

        var result = await service.BrowseAsync(null, SpiritCategory.Rum, 1, 24, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("Reserva Rum", Assert.Single(result.Data!.Items).Name);
    }

    [Fact]
    public async Task BrowseAsync_WhenMoreRowsThanPageSize_PagesWithoutOverlap()
    {
        var db = CreateDbContext();
        SeedProduct(db, name: "Ardbeg 10");
        SeedProduct(db, name: "Ardbeg 10");
        SeedProduct(db, name: "Bowmore 12");
        var service = CreateService(db);

        var first = await service.BrowseAsync(null, null, 1, 2, CancellationToken.None);
        var second = await service.BrowseAsync(null, null, 2, 2, CancellationToken.None);

        Assert.Equal(2, first.Data!.Items.Count);
        Assert.True(first.Data.HasMore);
        Assert.Equal("Bowmore 12", Assert.Single(second.Data!.Items).Name);
        Assert.False(second.Data.HasMore);

        // Equal names are split by Id, so the two "Ardbeg 10" rows are distinct and neither repeats.
        Assert.Equal(3, first.Data.Items.Concat(second.Data.Items).Select(p => p.Id).Distinct().Count());
    }

    [Fact]
    public async Task BrowseAsync_WhenBrandNullAndDistilleryKnown_FallsBackToDistilleryName()
    {
        var db = CreateDbContext();
        var distillery = SeedDistillery(db, "Macallan");
        var deleted = SeedDistillery(db, "Closed Distillery", isDeleted: true);
        SeedProduct(db, name: "Sherry Oak 12", distilleryId: distillery.Id);
        SeedProduct(db, name: "Tribute", distilleryId: deleted.Id);
        var service = CreateService(db);

        var result = await service.BrowseAsync(null, null, 1, 24, CancellationToken.None);

        var sherry = result.Data!.Items.Single(p => p.Name == "Sherry Oak 12");
        Assert.Equal("Macallan", sherry.Brand);
        Assert.Equal("Macallan", sherry.DistilleryName);

        var tribute = result.Data.Items.Single(p => p.Name == "Tribute");
        Assert.Null(tribute.Brand);
        Assert.Null(tribute.DistilleryName);
    }

    [Fact]
    public async Task BrowseAsync_CountsOnlyLiveBottlesAndReviews()
    {
        var db = CreateDbContext();
        var product = SeedProduct(db, name: "Lagavulin 16");
        var other = SeedProduct(db, name: "Talisker 10");
        var alice = SeedUser(db, "Alice");
        var bob = SeedUser(db, "Bob");
        var carol = SeedUser(db, "Carol");

        var aliceFirst = SeedLinkedBottle(db, product.Id, alice.Id, isForSale: true, forSaleQuantity: 1);
        SeedLinkedBottle(db, product.Id, alice.Id);
        var bobBottle = SeedLinkedBottle(db, product.Id, bob.Id);
        var deletedBottle = SeedLinkedBottle(db, product.Id, carol.Id, isForSale: true, forSaleQuantity: 1, isDeleted: true);
        var otherBottle = SeedLinkedBottle(db, other.Id, carol.Id);

        SeedReview(db, aliceFirst.Id, alice.Id, score: 90);
        SeedReview(db, bobBottle.Id, bob.Id, score: 85);
        SeedReview(db, bobBottle.Id, carol.Id, score: 85);
        SeedReview(db, bobBottle.Id, alice.Id, score: 10, isDeleted: true);
        SeedReview(db, deletedBottle.Id, carol.Id, score: 10);
        SeedReview(db, otherBottle.Id, carol.Id, score: 10);
        var service = CreateService(db);

        var result = await service.BrowseAsync(null, null, 1, 24, CancellationToken.None);

        var card = result.Data!.Items.Single(p => p.Id == product.Id);
        Assert.Equal(2, card.OwnersCount);
        Assert.Equal(3, card.ReviewsCount);
        Assert.Equal(86.7, card.AverageScore);
        Assert.Equal(1, card.ListingsCount);
    }

    [Fact]
    public async Task BrowseAsync_WhenProductHasNoBottles_ReturnsZeroCountsAndNullScore()
    {
        var db = CreateDbContext();
        SeedProduct(db, name: "Lagavulin 16");
        var service = CreateService(db);

        var result = await service.BrowseAsync(null, null, 1, 24, CancellationToken.None);

        var card = Assert.Single(result.Data!.Items);
        Assert.Equal(0, card.OwnersCount);
        Assert.Equal(0, card.ReviewsCount);
        Assert.Null(card.AverageScore);
        Assert.Equal(0, card.ListingsCount);
    }

    #endregion

    #region GetProductAsync

    [Fact]
    public async Task GetProductAsync_WithCancelledToken_Throws()
    {
        var service = CreateService(CreateDbContext());
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => service.GetProductAsync(Guid.NewGuid(), cts.Token));
    }

    [Fact]
    public async Task GetProductAsync_WhenProductUnknown_ReturnsNotFound()
    {
        var service = CreateService(CreateDbContext());

        var result = await service.GetProductAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ErrorCode.NotFound, result.ErrorCode);
        Assert.Equal("Product not found.", result.Error);
    }

    [Fact]
    public async Task GetProductAsync_WhenProductDeleted_ReturnsNotFound()
    {
        var db = CreateDbContext();
        var product = SeedProduct(db, isDeleted: true);
        var service = CreateService(db);

        var result = await service.GetProductAsync(product.Id, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ErrorCode.NotFound, result.ErrorCode);
    }

    [Fact]
    public async Task GetProductAsync_WhenNothingLinked_ReturnsProductWithEmptyAggregates()
    {
        var db = CreateDbContext();
        var distillery = SeedDistillery(db, "Macallan");
        var product = SeedProduct(db, name: "Sherry Oak 12", distilleryId: distillery.Id, age: 12, volumeMl: 700,
            barcode: "12345678", imageUrl: "/uploads/bottles/macallan.webp");
        product.Description = "Rich and sherried.";
        db.SaveChanges();
        var service = CreateService(db);

        var result = await service.GetProductAsync(product.Id, CancellationToken.None);

        Assert.True(result.Success);
        var dto = result.Data!;
        Assert.Equal(product.Id, dto.Id);
        Assert.Equal("Sherry Oak 12", dto.Name);
        Assert.Equal("Macallan", dto.Brand);
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
        Assert.Equal("Rich and sherried.", dto.Description);
        Assert.Null(dto.AverageScore);
        Assert.Equal(0, dto.ReviewsCount);
        Assert.Empty(dto.TopFlavors);
        Assert.Equal(0, dto.OwnersCount);
        Assert.Empty(dto.Owners);
        Assert.Empty(dto.Listings);
        Assert.Empty(dto.LatestReviews);
    }

    [Fact]
    public async Task GetProductAsync_WhenDistilleryDeletedAndNoBrand_LeavesBrandNull()
    {
        var db = CreateDbContext();
        var distillery = SeedDistillery(db, "Closed Distillery", isDeleted: true);
        var product = SeedProduct(db, distilleryId: distillery.Id);
        var service = CreateService(db);

        var result = await service.GetProductAsync(product.Id, CancellationToken.None);

        Assert.Null(result.Data!.Brand);
        Assert.Null(result.Data.DistilleryName);
    }

    [Fact]
    public async Task GetProductAsync_AggregatesReviewsAcrossLiveLinkedBottlesOnly()
    {
        var db = CreateDbContext();
        var product = SeedProduct(db);
        var other = SeedProduct(db, name: "Other");
        var alice = SeedUser(db, "Alice");
        var bob = SeedUser(db, "Bob");

        var first = SeedLinkedBottle(db, product.Id, alice.Id);
        var second = SeedLinkedBottle(db, product.Id, bob.Id);
        var deletedBottle = SeedLinkedBottle(db, product.Id, bob.Id, isDeleted: true);
        var otherBottle = SeedLinkedBottle(db, other.Id, bob.Id);

        SeedReview(db, first.Id, alice.Id, score: 90);
        SeedReview(db, second.Id, bob.Id, score: 85);
        SeedReview(db, second.Id, alice.Id, score: 85);
        SeedReview(db, second.Id, bob.Id, score: 1, isDeleted: true, flavors: [FlavorTag.Leather]);
        SeedReview(db, deletedBottle.Id, bob.Id, score: 1, flavors: [FlavorTag.Leather]);
        SeedReview(db, otherBottle.Id, bob.Id, score: 1, flavors: [FlavorTag.Leather]);
        var service = CreateService(db);

        var result = await service.GetProductAsync(product.Id, CancellationToken.None);

        Assert.Equal(3, result.Data!.ReviewsCount);
        Assert.Equal(86.7, result.Data.AverageScore);
        Assert.Equal(3, result.Data.LatestReviews.Count);
        Assert.DoesNotContain(FlavorTag.Leather, result.Data.TopFlavors);
    }

    [Fact]
    public async Task GetProductAsync_TopFlavors_RanksByCountThenEnumOrderAndKeepsThree()
    {
        var db = CreateDbContext();
        var product = SeedProduct(db);
        var user = SeedUser(db);
        var first = SeedLinkedBottle(db, product.Id, user.Id);
        var second = SeedLinkedBottle(db, product.Id, user.Id);

        // Vanilla ×3; Peaty and Smoky ×2 (tie → enum order: Smoky before Peaty); Honey ×1 drops out.
        SeedReview(db, first.Id, user.Id, flavors: [FlavorTag.Vanilla, FlavorTag.Peaty, FlavorTag.Honey]);
        SeedReview(db, second.Id, user.Id, flavors: [FlavorTag.Vanilla, FlavorTag.Smoky, FlavorTag.Peaty]);
        SeedReview(db, second.Id, SeedUser(db).Id, flavors: [FlavorTag.Vanilla, FlavorTag.Smoky]);
        var service = CreateService(db);

        var result = await service.GetProductAsync(product.Id, CancellationToken.None);

        Assert.Equal(new[] { FlavorTag.Vanilla, FlavorTag.Smoky, FlavorTag.Peaty }, result.Data!.TopFlavors);
    }

    [Fact]
    public async Task GetProductAsync_Owners_SumsQuantityAndOrdersByCountThenName()
    {
        var db = CreateDbContext();
        var product = SeedProduct(db);
        var zed = SeedUser(db, "Zed", "/uploads/avatars/zed.webp");
        var amy = SeedUser(db, "Amy");
        var ben = SeedUser(db, "Ben");

        SeedLinkedBottle(db, product.Id, zed.Id, quantity: 2);
        SeedLinkedBottle(db, product.Id, zed.Id, quantity: 1);
        SeedLinkedBottle(db, product.Id, zed.Id, quantity: 5, isDeleted: true);
        SeedLinkedBottle(db, product.Id, ben.Id, quantity: 1);
        SeedLinkedBottle(db, product.Id, amy.Id, quantity: 1);
        SeedLinkedBottle(db, null, amy.Id, quantity: 9);
        var service = CreateService(db);

        var result = await service.GetProductAsync(product.Id, CancellationToken.None);

        Assert.Equal(3, result.Data!.OwnersCount);
        Assert.Equal(new[] { "Zed", "Amy", "Ben" }, result.Data.Owners.Select(o => o.DisplayName).ToArray());
        Assert.Equal(new[] { 3, 1, 1 }, result.Data.Owners.Select(o => o.BottleCount).ToArray());
        Assert.Equal(zed.Id, result.Data.Owners[0].UserId);
        Assert.Equal("/uploads/avatars/zed.webp", result.Data.Owners[0].AvatarUrl);
    }

    [Fact]
    public async Task GetProductAsync_Owners_CapsAtTwentyButCountsAll()
    {
        var db = CreateDbContext();
        var product = SeedProduct(db);
        for (var i = 0; i < 25; i++)
            SeedLinkedBottle(db, product.Id, SeedUser(db, $"Owner {i:D2}").Id);
        var service = CreateService(db);

        var result = await service.GetProductAsync(product.Id, CancellationToken.None);

        Assert.Equal(25, result.Data!.OwnersCount);
        Assert.Equal(20, result.Data.Owners.Count);
        Assert.Equal("Owner 00", result.Data.Owners[0].DisplayName);
    }

    [Fact]
    public async Task GetProductAsync_Listings_OnlyLiveForSaleBottlesNewestFirst()
    {
        var db = CreateDbContext();
        var product = SeedProduct(db);
        var seller = SeedUser(db, "Seller", "/uploads/avatars/seller.webp");
        var now = DateTime.UtcNow;

        var older = SeedLinkedBottle(db, product.Id, seller.Id, quantity: 3, isForSale: true, forSaleQuantity: 2,
            forSaleAt: now.AddDays(-3), askingPrice: 120m, vintageYear: 2008);
        var newer = SeedLinkedBottle(db, product.Id, seller.Id, isForSale: true, forSaleQuantity: 1,
            forSaleAt: now.AddDays(-1), askingPrice: 150m);
        SeedLinkedBottle(db, product.Id, seller.Id);
        SeedLinkedBottle(db, product.Id, seller.Id, isForSale: true, forSaleQuantity: 1, forSaleAt: now, isDeleted: true);
        var service = CreateService(db);

        var result = await service.GetProductAsync(product.Id, CancellationToken.None);

        Assert.Equal(new[] { newer.Id, older.Id }, result.Data!.Listings.Select(l => l.BottleId).ToArray());
        var listing = result.Data.Listings[1];
        Assert.Equal(seller.Id, listing.SellerId);
        Assert.Equal("Seller", listing.SellerDisplayName);
        Assert.Equal("/uploads/avatars/seller.webp", listing.SellerAvatarUrl);
        Assert.Equal(2, listing.ForSaleQuantity);
        Assert.Equal(120m, listing.AskingPrice);
        Assert.Equal("EUR", listing.Currency);
        Assert.Equal(BottleCondition.Sealed, listing.Condition);
        Assert.Equal(2008, listing.VintageYear);
        Assert.Equal(older.ForSaleAt, listing.ForSaleAt);
    }

    [Fact]
    public async Task GetProductAsync_LatestReviews_KeepsNewestFiveMappedLikeBottleReviews()
    {
        var db = CreateDbContext();
        var product = SeedProduct(db);
        var reviewer = SeedUser(db, "Reviewer", "/uploads/avatars/r.webp");
        var bottle = SeedLinkedBottle(db, product.Id, reviewer.Id);
        var now = DateTime.UtcNow;
        for (var i = 0; i < 7; i++)
            SeedReview(db, bottle.Id, SeedUser(db).Id, score: 80 + i, createdAt: now.AddDays(-i));
        var newest = SeedReview(db, bottle.Id, reviewer.Id, score: 99, createdAt: now.AddHours(1),
            flavors: [FlavorTag.Oak, FlavorTag.Smoky]);
        var service = CreateService(db);

        var result = await service.GetProductAsync(product.Id, CancellationToken.None);

        Assert.Equal(8, result.Data!.ReviewsCount);
        Assert.Equal(new[] { 99, 80, 81, 82, 83 }, result.Data.LatestReviews.Select(r => r.Score).ToArray());
        var top = result.Data.LatestReviews[0];
        Assert.Equal(newest.Id, top.Id);
        Assert.Equal(bottle.Id, top.BottleId);
        Assert.Equal(reviewer.Id, top.UserId);
        Assert.Equal("Reviewer", top.UserDisplayName);
        Assert.Equal("/uploads/avatars/r.webp", top.UserAvatarUrl);
        Assert.Equal("Review scored 99", top.Summary);
        Assert.Equal(new[] { FlavorTag.Smoky, FlavorTag.Oak }, top.Flavors);
    }

    #endregion

    #region GetProductReviewsAsync

    [Fact]
    public async Task GetProductReviewsAsync_WithCancelledToken_Throws()
    {
        var service = CreateService(CreateDbContext());
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => service.GetProductReviewsAsync(Guid.NewGuid(), 1, 10, cts.Token));
    }

    [Fact]
    public async Task GetProductReviewsAsync_WhenProductUnknown_ReturnsNotFound()
    {
        var service = CreateService(CreateDbContext());

        var result = await service.GetProductReviewsAsync(Guid.NewGuid(), 1, 10, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ErrorCode.NotFound, result.ErrorCode);
        Assert.Equal("Product not found.", result.Error);
    }

    [Fact]
    public async Task GetProductReviewsAsync_WhenProductDeleted_ReturnsNotFound()
    {
        var db = CreateDbContext();
        var product = SeedProduct(db, isDeleted: true);
        var service = CreateService(db);

        var result = await service.GetProductReviewsAsync(product.Id, 1, 10, CancellationToken.None);

        Assert.Equal(ErrorCode.NotFound, result.ErrorCode);
    }

    [Fact]
    public async Task GetProductReviewsAsync_WhenPageAboveMaximum_ReturnsFail()
    {
        var db = CreateDbContext();
        var product = SeedProduct(db);
        var service = CreateService(db);

        var result = await service.GetProductReviewsAsync(product.Id, 1001, 10, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ErrorCode.Validation, result.ErrorCode);
        Assert.Equal("Page is out of range.", result.Error);
    }

    private static (AppDbContext Db, Product Product) SeedReviewedProduct(int reviewCount)
    {
        var db = CreateDbContext();
        var product = SeedProduct(db);
        var owner = SeedUser(db);
        var bottle = SeedLinkedBottle(db, product.Id, owner.Id);
        var now = DateTime.UtcNow;
        for (var i = 0; i < reviewCount; i++)
            SeedReview(db, bottle.Id, SeedUser(db).Id, score: 50 + i, createdAt: now.AddMinutes(-i));
        return (db, product);
    }

    [Fact]
    public async Task GetProductReviewsAsync_WhenPageBelowOneAndSizeBelowOne_UsesFirstPageOfTen()
    {
        var (db, product) = SeedReviewedProduct(12);
        var service = CreateService(db);

        var result = await service.GetProductReviewsAsync(product.Id, 0, 0, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(10, result.Data!.Items.Count);
        Assert.Equal(50, result.Data.Items[0].Score);
        Assert.True(result.Data.HasMore);
    }

    [Fact]
    public async Task GetProductReviewsAsync_WhenPageSizeAboveMaximum_ClampsToFifty()
    {
        var (db, product) = SeedReviewedProduct(55);
        var service = CreateService(db);

        var result = await service.GetProductReviewsAsync(product.Id, 1, 500, CancellationToken.None);

        Assert.Equal(50, result.Data!.Items.Count);
        Assert.True(result.Data.HasMore);
    }

    [Fact]
    public async Task GetProductReviewsAsync_PagesNewestFirstAndSkipsDeadReviews()
    {
        var (db, product) = SeedReviewedProduct(3);
        var bob = SeedUser(db, "Bob");
        var deletedBottle = SeedLinkedBottle(db, product.Id, bob.Id, isDeleted: true);
        var otherBottle = SeedLinkedBottle(db, SeedProduct(db, name: "Other").Id, bob.Id);
        var liveBottle = db.Bottles.Single(b => b.ProductId == product.Id && !b.IsDeleted);
        SeedReview(db, liveBottle.Id, bob.Id, score: 1, isDeleted: true, createdAt: DateTime.UtcNow.AddDays(1));
        SeedReview(db, deletedBottle.Id, bob.Id, score: 1, createdAt: DateTime.UtcNow.AddDays(1));
        SeedReview(db, otherBottle.Id, bob.Id, score: 1, createdAt: DateTime.UtcNow.AddDays(1));
        var service = CreateService(db);

        var first = await service.GetProductReviewsAsync(product.Id, 1, 2, CancellationToken.None);
        var second = await service.GetProductReviewsAsync(product.Id, 2, 2, CancellationToken.None);

        Assert.Equal(new[] { 50, 51 }, first.Data!.Items.Select(r => r.Score).ToArray());
        Assert.True(first.Data.HasMore);
        Assert.Equal(new[] { 52 }, second.Data!.Items.Select(r => r.Score).ToArray());
        Assert.False(second.Data.HasMore);
    }

    #endregion
}
