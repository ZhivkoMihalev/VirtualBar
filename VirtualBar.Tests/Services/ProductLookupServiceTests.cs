using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Moq;
using VirtualBar.Application.Common;
using VirtualBar.Application.Interfaces;
using VirtualBar.Domain.Entities;
using VirtualBar.Domain.Enums;
using VirtualBar.Infrastructure.Decorators;
using VirtualBar.Infrastructure.Persistence;
using VirtualBar.Infrastructure.Services;

namespace VirtualBar.Tests.Services;

/// <summary>
/// The barcode lookup answers from the platform's own data only (#49), in every form of the barcode: the product's own
/// barcode, then the barcode collectors gave a product (most collectors win), then a suggestion from collectors'
/// bottles not yet in the catalog. The external UPC database and its tests were removed with it.
/// </summary>
public sealed class ProductLookupServiceTests
{
    private const string Barcode = "5010314000011";

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    /// <summary>
    /// A database whose every query fails with <paramref name="exception"/> when EF compiles it. Service provider
    /// caching is off, so this context compiles its queries itself instead of reusing another test's.
    /// </summary>
    private static AppDbContext CreateFailingDbContext(Exception exception)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .EnableServiceProviderCaching(false)
            .AddInterceptors(new ThrowOnQueryInterceptor(exception))
            .Options;
        return new AppDbContext(options);
    }

    private sealed class ThrowOnQueryInterceptor(Exception exception) : IQueryExpressionInterceptor
    {
        public Expression QueryCompilationStarting(Expression queryExpression, QueryExpressionEventData eventData) =>
            throw exception;
    }

    private static IProductLookupService CreateService(AppDbContext db) =>
        new ProductValidationDecorator(new ProductLookupService(db), Mock.Of<ILogger<ProductValidationDecorator>>());

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
        string? barcode = Barcode,
        string name = "Sherry Oak 12",
        string? brand = null,
        Guid? distilleryId = null,
        string? imageUrl = null,
        int? volumeMl = 700,
        double? abvPercent = 43.0,
        bool isDeleted = false,
        DateTime? createdAt = null)
    {
        var product = new Product
        {
            Name = name,
            Brand = brand,
            DistilleryId = distilleryId,
            Category = SpiritCategory.Whisky,
            Barcode = barcode,
            ImageUrl = imageUrl,
            VolumeMl = volumeMl,
            AbvPercent = abvPercent,
            CanonicalKey = Guid.NewGuid().ToString(),
            Origin = ProductOrigin.Seeded,
            IsDeleted = isDeleted,
            DeletedAt = isDeleted ? DateTime.UtcNow : null
        };
        if (createdAt is not null)
            product.CreatedAt = createdAt.Value;
        db.Products.Add(product);
        db.SaveChanges();
        return product;
    }

    /// <summary>A barcode a collector gave <paramref name="product"/> by saving a bottle of it.</summary>
    private static ProductBarcode SeedLink(
        AppDbContext db,
        Product product,
        string barcode = Barcode,
        bool isDeleted = false,
        DateTime? createdAt = null,
        Guid? addedBy = null)
    {
        var link = new ProductBarcode
        {
            ProductId = product.Id,
            Barcode = barcode,
            AddedByUserId = addedBy ?? Guid.NewGuid(),
            IsDeleted = isDeleted,
            DeletedAt = isDeleted ? DateTime.UtcNow : null
        };
        if (createdAt is not null)
            link.CreatedAt = createdAt.Value;
        db.ProductBarcodes.Add(link);
        db.SaveChanges();
        return link;
    }

    /// <summary>A collector's bottle with <paramref name="barcode"/>, linked to <paramref name="product"/> or not.</summary>
    private static Bottle SeedBottle(
        AppDbContext db,
        string barcode = Barcode,
        Product? product = null,
        Guid? userId = null,
        string name = "Unknown Single Cask",
        SpiritCategory category = SpiritCategory.Rum,
        Distillery? distillery = null,
        int? age = null,
        int? vintageYear = null,
        double? abvPercent = null,
        int? volumeMl = null,
        string? country = null,
        string? region = null,
        bool isDeleted = false,
        DateTime? createdAt = null)
    {
        var bottle = new Bottle
        {
            UserId = userId ?? Guid.NewGuid(),
            Name = name,
            Category = category,
            DistilleryId = distillery?.Id,
            ProductId = product?.Id,
            Barcode = barcode,
            Age = age,
            VintageYear = vintageYear,
            AbvPercent = abvPercent,
            VolumeMl = volumeMl,
            Country = country,
            Region = region,
            IsDeleted = isDeleted,
            DeletedAt = isDeleted ? DateTime.UtcNow : null
        };
        if (createdAt is not null)
            bottle.CreatedAt = createdAt.Value;
        db.Bottles.Add(bottle);
        db.SaveChanges();
        return bottle;
    }

    private static readonly DateTime BaseTime = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    #region Validation

    [Fact]
    public async Task LookupByBarcodeAsync_WhenBarcodeEmpty_ReturnsFail()
    {
        var service = CreateService(CreateDbContext());

        var result = await service.LookupByBarcodeAsync("", CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ErrorCode.Validation, result.ErrorCode);
        Assert.Equal("Barcode is required.", result.Error);
    }

    [Fact]
    public async Task LookupByBarcodeAsync_WhenBarcodeWhitespace_ReturnsFail()
    {
        var service = CreateService(CreateDbContext());

        var result = await service.LookupByBarcodeAsync("   ", CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Barcode is required.", result.Error);
    }

    [Theory]
    [InlineData("1234567")]
    [InlineData("123456789012345")]
    [InlineData("50103140000AB")]
    public async Task LookupByBarcodeAsync_WhenNotEightToFourteenDigits_ReturnsFail(string barcode)
    {
        var service = CreateService(CreateDbContext());

        var result = await service.LookupByBarcodeAsync(barcode, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ErrorCode.Validation, result.ErrorCode);
        Assert.Equal("Barcode must be 8 to 14 digits.", result.Error);
    }

    [Fact]
    public async Task LookupByBarcodeAsync_WhenInnerThrowsNonCancellation_ReturnsFail()
    {
        var service = CreateService(CreateFailingDbContext(new InvalidOperationException("boom")));

        var result = await service.LookupByBarcodeAsync(Barcode, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Barcode lookup failed. Please try again.", result.Error);
    }

    [Fact]
    public async Task LookupByBarcodeAsync_WhenInnerThrowsCancellation_Rethrows()
    {
        var service = CreateService(CreateFailingDbContext(new OperationCanceledException()));

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => service.LookupByBarcodeAsync(Barcode, CancellationToken.None));
    }

    [Fact]
    public async Task LookupByBarcodeAsync_WhenTokenCancelled_Throws()
    {
        var service = CreateService(CreateDbContext());
        var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => service.LookupByBarcodeAsync(Barcode, cts.Token));
    }

    [Fact]
    public async Task LookupByBarcodeAsync_WhenBarcodePadded_TrimsBeforeLookup()
    {
        var db = CreateDbContext();
        var product = SeedProduct(db);
        var service = CreateService(db);

        var result = await service.LookupByBarcodeAsync($"  {Barcode}  ", CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(product.Id, result.Data!.Product!.Id);
    }

    [Fact]
    public async Task LookupByBarcodeAsync_WhenNobodyHasTheBarcode_ReturnsNotFound()
    {
        var db = CreateDbContext();
        var product = SeedProduct(db, barcode: "0000000000000");
        SeedLink(db, product, barcode: "1111111111111");
        SeedBottle(db, barcode: "2222222222222");
        var service = CreateService(db);

        var result = await service.LookupByBarcodeAsync(Barcode, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ErrorCode.NotFound, result.ErrorCode);
        Assert.Equal("Product not found.", result.Error);
    }

    #endregion

    #region The product's own barcode

    /// <summary>
    /// A hit is the whole catalog product, so the add form fills the bottle from it like a catalog pick: it used to
    /// carry only the name, brand, image, volume and ABV, and the bottle kept the form's category (#120).
    /// </summary>
    [Fact]
    public async Task LookupByBarcodeAsync_ReturnsEveryFieldTheAddFormFillsFromAProduct()
    {
        var db = CreateDbContext();
        var distillery = SeedDistillery(db, "Foursquare");
        var product = SeedProduct(db, name: "Foursquare 2005 Vintage", brand: "Foursquare", distilleryId: distillery.Id);
        product.Category = SpiritCategory.Rum;
        product.Country = "Barbados";
        product.Region = "Caribbean";
        product.Age = 12;
        product.VintageYear = 2005;
        db.SaveChanges();
        var service = CreateService(db);

        var result = await service.LookupByBarcodeAsync(Barcode, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(BarcodeSource.Catalog, result.Data!.Source);
        Assert.Equal(0, result.Data.Collectors);
        Assert.Null(result.Data.Suggestion);
        var found = result.Data.Product!;
        Assert.Equal(product.Id, found.Id);
        Assert.Equal(SpiritCategory.Rum, found.Category);
        Assert.Equal(distillery.Id, found.DistilleryId);
        Assert.Equal("Foursquare", found.DistilleryName);
        Assert.Equal("Barbados", found.Country);
        Assert.Equal("Caribbean", found.Region);
        Assert.Equal(12, found.Age);
        Assert.Equal(2005, found.VintageYear);
        Assert.Equal(Barcode, found.Barcode);
    }

    [Fact]
    public async Task LookupByBarcodeAsync_WhenBarcodeIsTheProductsOwn_ReturnsTheProduct()
    {
        var db = CreateDbContext();
        var product = SeedProduct(db, name: "Sherry Oak 12", brand: "Macallan", imageUrl: "/uploads/products/macallan.webp");
        var service = CreateService(db);

        var result = await service.LookupByBarcodeAsync(Barcode, CancellationToken.None);

        Assert.True(result.Success);
        var found = result.Data!.Product!;
        Assert.Equal(product.Id, found.Id);
        Assert.Equal("Sherry Oak 12", found.Name);
        Assert.Equal("Macallan", found.Brand);
        Assert.Equal("/uploads/products/macallan.webp", found.ImageUrl);
        Assert.Equal(700, found.VolumeMl);
        Assert.Equal(43.0, found.AbvPercent);
    }

    /// <summary>A UPC-A on the label and the EAN-13 it also is are one barcode (#49).</summary>
    [Theory]
    [InlineData("082184090466", "0082184090466")]
    [InlineData("0082184090466", "082184090466")]
    [InlineData("082184090466", "00082184090466")]
    public async Task LookupByBarcodeAsync_FindsTheProductsOwnBarcodeInAnotherForm(string stored, string scanned)
    {
        var db = CreateDbContext();
        var product = SeedProduct(db, barcode: stored);
        var service = CreateService(db);

        var result = await service.LookupByBarcodeAsync(scanned, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(product.Id, result.Data!.Product!.Id);
    }

    [Fact]
    public async Task LookupByBarcodeAsync_WhenProductHasNoBrand_UsesDistilleryName()
    {
        var db = CreateDbContext();
        var distillery = SeedDistillery(db, "Macallan");
        SeedProduct(db, brand: null, distilleryId: distillery.Id);
        var service = CreateService(db);

        var result = await service.LookupByBarcodeAsync(Barcode, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("Macallan", result.Data!.Product!.Brand);
    }

    [Fact]
    public async Task LookupByBarcodeAsync_WhenDistillerySoftDeleted_ReturnsNullBrand()
    {
        var db = CreateDbContext();
        var distillery = SeedDistillery(db, "Macallan", isDeleted: true);
        SeedProduct(db, brand: null, distilleryId: distillery.Id);
        var service = CreateService(db);

        var result = await service.LookupByBarcodeAsync(Barcode, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Null(result.Data!.Product!.Brand);
    }

    [Fact]
    public async Task LookupByBarcodeAsync_WhenProductHasNoBrandAndNoDistillery_ReturnsNullBrand()
    {
        var db = CreateDbContext();
        SeedProduct(db, brand: null, distilleryId: null);
        var service = CreateService(db);

        var result = await service.LookupByBarcodeAsync(Barcode, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Null(result.Data!.Product!.Brand);
    }

    [Fact]
    public async Task LookupByBarcodeAsync_WhenSeveralProductsShareTheBarcode_ReturnsTheOldest()
    {
        var db = CreateDbContext();
        SeedProduct(db, name: "Newer Row", createdAt: BaseTime.AddDays(1));
        var oldest = SeedProduct(db, name: "Older Row", createdAt: BaseTime);
        var service = CreateService(db);

        var result = await service.LookupByBarcodeAsync(Barcode, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(oldest.Id, result.Data!.Product!.Id);
    }

    [Fact]
    public async Task LookupByBarcodeAsync_WhenProductWithTheBarcodeSoftDeleted_ReturnsNotFound()
    {
        var db = CreateDbContext();
        SeedProduct(db, name: "Deleted Row", isDeleted: true);
        var service = CreateService(db);

        var result = await service.LookupByBarcodeAsync(Barcode, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ErrorCode.NotFound, result.ErrorCode);
    }

    #endregion

    #region Barcodes collectors gave a product

    [Fact]
    public async Task LookupByBarcodeAsync_WhenOnlyACollectorGaveTheBarcode_ReturnsThatProduct()
    {
        var db = CreateDbContext();
        var distillery = SeedDistillery(db, "Macallan");
        var product = SeedProduct(db, barcode: null, name: "Sherry Oak 12", brand: null, distilleryId: distillery.Id);
        SeedLink(db, product);
        var service = CreateService(db);

        var result = await service.LookupByBarcodeAsync(Barcode, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(BarcodeSource.Collectors, result.Data!.Source);
        Assert.Equal(1, result.Data.Collectors);
        Assert.Equal(product.Id, result.Data.Product!.Id);
        Assert.Equal("Macallan", result.Data.Product.Brand);
    }

    [Fact]
    public async Task LookupByBarcodeAsync_WhenAProductHasTheBarcodeAndACollectorGaveItToAnother_ReturnsTheProductsOwn()
    {
        var db = CreateDbContext();
        var own = SeedProduct(db, name: "Own Barcode");
        var other = SeedProduct(db, barcode: null, name: "Collector's Pick");
        SeedLink(db, other, createdAt: BaseTime);
        var service = CreateService(db);

        var result = await service.LookupByBarcodeAsync(Barcode, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(BarcodeSource.Catalog, result.Data!.Source);
        Assert.Equal(own.Id, result.Data.Product!.Id);
    }

    [Fact]
    public async Task LookupByBarcodeAsync_WhenCollectorsGaveTheBarcodeToSeveralProducts_ReturnsTheOneMostOfThemBack()
    {
        var db = CreateDbContext();
        var mistaken = SeedProduct(db, barcode: null, name: "Mistaken Pick");
        var right = SeedProduct(db, barcode: null, name: "Right Pick");
        SeedLink(db, mistaken, createdAt: BaseTime);
        var first = Guid.NewGuid();
        SeedLink(db, right, createdAt: BaseTime.AddDays(1), addedBy: first);
        SeedBottle(db, product: right, userId: first);
        SeedBottle(db, product: right, userId: Guid.NewGuid());
        var service = CreateService(db);

        var result = await service.LookupByBarcodeAsync(Barcode, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(right.Id, result.Data!.Product!.Id);
        // The collector who recorded the pair also owns one of the bottles: counted once.
        Assert.Equal(2, result.Data.Collectors);
    }

    [Fact]
    public async Task LookupByBarcodeAsync_WhenAsManyCollectorsBackTwoProducts_ReturnsTheOldestPair()
    {
        var db = CreateDbContext();
        var first = SeedProduct(db, barcode: null, name: "First Link");
        var second = SeedProduct(db, barcode: null, name: "Second Link");
        SeedLink(db, second, createdAt: BaseTime.AddDays(1));
        SeedLink(db, first, createdAt: BaseTime);
        var service = CreateService(db);

        var result = await service.LookupByBarcodeAsync(Barcode, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(first.Id, result.Data!.Product!.Id);
    }

    [Fact]
    public async Task LookupByBarcodeAsync_CountsOnlyLiveBottlesWithTheBarcodeOfThatProduct()
    {
        var db = CreateDbContext();
        var product = SeedProduct(db, barcode: null);
        var other = SeedProduct(db, barcode: null, name: "Other Product");
        SeedLink(db, product);
        SeedBottle(db, product: product, isDeleted: true);
        SeedBottle(db, product: product, barcode: "1111111111111");
        SeedBottle(db, product: other);
        var service = CreateService(db);

        var result = await service.LookupByBarcodeAsync(Barcode, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(product.Id, result.Data!.Product!.Id);
        Assert.Equal(1, result.Data.Collectors);
    }

    [Fact]
    public async Task LookupByBarcodeAsync_FindsACollectorsBarcodeInAnotherForm()
    {
        var db = CreateDbContext();
        var product = SeedProduct(db, barcode: null);
        SeedLink(db, product, barcode: "082184090466");
        var service = CreateService(db);

        var result = await service.LookupByBarcodeAsync("0082184090466", CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(product.Id, result.Data!.Product!.Id);
    }

    /// <summary>An admin removed the pair: the product is ruled out, even while collectors' bottles still carry it.</summary>
    [Fact]
    public async Task LookupByBarcodeAsync_WhenAnAdminRemovedThePair_RulesThatProductOut()
    {
        var db = CreateDbContext();
        var removed = SeedProduct(db, barcode: null, name: "Wrong Product");
        var other = SeedProduct(db, barcode: null, name: "Right Product");
        SeedLink(db, removed, createdAt: BaseTime);
        SeedLink(db, removed, isDeleted: true, createdAt: BaseTime.AddDays(1));
        SeedBottle(db, product: removed);
        SeedBottle(db, product: removed);
        SeedLink(db, other, createdAt: BaseTime.AddDays(2));
        var service = CreateService(db);

        var result = await service.LookupByBarcodeAsync(Barcode, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(other.Id, result.Data!.Product!.Id);
    }

    [Fact]
    public async Task LookupByBarcodeAsync_WhenEveryPairWasRemoved_ReturnsNotFound()
    {
        var db = CreateDbContext();
        var product = SeedProduct(db, barcode: null);
        SeedLink(db, product, isDeleted: true);
        var service = CreateService(db);

        var result = await service.LookupByBarcodeAsync(Barcode, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ErrorCode.NotFound, result.ErrorCode);
    }

    [Fact]
    public async Task LookupByBarcodeAsync_WhenTheLinkedProductIsDeleted_SkipsIt()
    {
        var db = CreateDbContext();
        var deleted = SeedProduct(db, barcode: null, name: "Deleted Product", isDeleted: true);
        var live = SeedProduct(db, barcode: null, name: "Live Product");
        SeedLink(db, deleted, createdAt: BaseTime);
        SeedLink(db, live, createdAt: BaseTime.AddDays(1));
        var service = CreateService(db);

        var result = await service.LookupByBarcodeAsync(Barcode, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(live.Id, result.Data!.Product!.Id);
    }

    #endregion

    #region Collectors' bottles not in the catalog

    [Fact]
    public async Task LookupByBarcodeAsync_WhenOnlyBottlesNotInTheCatalogHaveIt_SuggestsWhatMostOfThemSay()
    {
        var db = CreateDbContext();
        var distillery = SeedDistillery(db, "Foursquare");
        var collector = Guid.NewGuid();
        SeedBottle(db, userId: collector, name: "Foursquare Exceptional Cask", distillery: distillery, age: 12,
            vintageYear: 2008, abvPercent: 62, volumeMl: 700, country: "Barbados", region: "Caribbean");
        SeedBottle(db, userId: collector, name: " Foursquare Exceptional Cask ", distillery: distillery, age: 12,
            volumeMl: 700, country: "Barbados");
        SeedBottle(db, name: "Foursquare Cask", category: SpiritCategory.Whisky, age: 10, volumeMl: 750);
        var service = CreateService(db);

        var result = await service.LookupByBarcodeAsync(Barcode, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(BarcodeSource.Bottles, result.Data!.Source);
        Assert.Null(result.Data.Product);
        Assert.Equal(2, result.Data.Collectors);
        var suggestion = result.Data.Suggestion!;
        Assert.Equal("Foursquare Exceptional Cask", suggestion.Name);
        Assert.Equal(SpiritCategory.Rum, suggestion.Category);
        Assert.Equal(distillery.Id, suggestion.DistilleryId);
        Assert.Equal("Foursquare", suggestion.DistilleryName);
        Assert.Equal(12, suggestion.Age);
        Assert.Equal(700, suggestion.VolumeMl);
        Assert.Equal("Barbados", suggestion.Country);
        // A field only one bottle carries is still taken: the others say nothing against it.
        Assert.Equal(2008, suggestion.VintageYear);
        Assert.Equal(62, suggestion.AbvPercent);
        Assert.Equal("Caribbean", suggestion.Region);
    }

    [Fact]
    public async Task LookupByBarcodeAsync_WhenTheBottlesAreEvenlySplit_TakesTheMostRecentBottles()
    {
        var db = CreateDbContext();
        SeedBottle(db, name: "Older Name", age: 10, createdAt: BaseTime);
        SeedBottle(db, name: "Newer Name", age: 12, createdAt: BaseTime.AddDays(1));
        var service = CreateService(db);

        var result = await service.LookupByBarcodeAsync(Barcode, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("Newer Name", result.Data!.Suggestion!.Name);
        Assert.Equal(12, result.Data.Suggestion.Age);
    }

    [Fact]
    public async Task LookupByBarcodeAsync_SuggestsNoDistilleryThatWasDeletedNorABlankText()
    {
        var db = CreateDbContext();
        var deleted = SeedDistillery(db, "Gone", isDeleted: true);
        SeedBottle(db, distillery: deleted, country: "  ", region: "");
        var service = CreateService(db);

        var result = await service.LookupByBarcodeAsync(Barcode, CancellationToken.None);

        Assert.True(result.Success);
        var suggestion = result.Data!.Suggestion!;
        Assert.Null(suggestion.DistilleryId);
        Assert.Null(suggestion.DistilleryName);
        Assert.Null(suggestion.Country);
        Assert.Null(suggestion.Region);
    }

    [Fact]
    public async Task LookupByBarcodeAsync_SuggestsFromNeitherDeletedNorLinkedBottles()
    {
        var db = CreateDbContext();
        var product = SeedProduct(db, barcode: null);
        SeedBottle(db, isDeleted: true);
        // Linked, but no pair recorded: what it says belongs to its product, never to a suggestion.
        SeedBottle(db, product: product);
        var service = CreateService(db);

        var result = await service.LookupByBarcodeAsync(Barcode, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ErrorCode.NotFound, result.ErrorCode);
    }

    [Fact]
    public async Task LookupByBarcodeAsync_FindsBottlesWithTheBarcodeInAnotherForm()
    {
        var db = CreateDbContext();
        SeedBottle(db, barcode: "0082184090466", name: "Old No. 7");
        var service = CreateService(db);

        var result = await service.LookupByBarcodeAsync("082184090466", CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("Old No. 7", result.Data!.Suggestion!.Name);
    }

    #endregion
}
