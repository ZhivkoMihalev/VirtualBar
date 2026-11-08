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
/// The barcode lookup answers from the platform's own data only (#49): the product's own barcode, then the barcodes
/// collectors gave a product. The external UPC database and its tests were removed with it.
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
        DateTime? createdAt = null)
    {
        var link = new ProductBarcode
        {
            ProductId = product.Id,
            Barcode = barcode,
            AddedByUserId = Guid.NewGuid(),
            IsDeleted = isDeleted,
            DeletedAt = isDeleted ? DateTime.UtcNow : null
        };
        if (createdAt is not null)
            link.CreatedAt = createdAt.Value;
        db.ProductBarcodes.Add(link);
        db.SaveChanges();
        return link;
    }

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
        Assert.Equal(product.Id, result.Data!.ProductId);
    }

    #endregion

    #region The product's own barcode

    [Fact]
    public async Task LookupByBarcodeAsync_WhenBarcodeIsTheProductsOwn_ReturnsTheProduct()
    {
        var db = CreateDbContext();
        var product = SeedProduct(db, name: "Sherry Oak 12", brand: "Macallan", imageUrl: "/uploads/products/macallan.webp");
        var service = CreateService(db);

        var result = await service.LookupByBarcodeAsync(Barcode, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(product.Id, result.Data!.ProductId);
        Assert.Equal("Sherry Oak 12", result.Data.Name);
        Assert.Equal("Macallan", result.Data.Brand);
        Assert.Equal("/uploads/products/macallan.webp", result.Data.ImageUrl);
        Assert.Equal(700, result.Data.VolumeMl);
        Assert.Equal(43.0, result.Data.AbvPercent);
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
        Assert.Equal("Macallan", result.Data!.Brand);
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
        Assert.Null(result.Data!.Brand);
    }

    [Fact]
    public async Task LookupByBarcodeAsync_WhenProductHasNoBrandAndNoDistillery_ReturnsNullBrand()
    {
        var db = CreateDbContext();
        SeedProduct(db, brand: null, distilleryId: null);
        var service = CreateService(db);

        var result = await service.LookupByBarcodeAsync(Barcode, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Null(result.Data!.Brand);
    }

    [Fact]
    public async Task LookupByBarcodeAsync_WhenSeveralProductsShareTheBarcode_ReturnsTheOldest()
    {
        var db = CreateDbContext();
        var baseTime = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        SeedProduct(db, name: "Newer Row", createdAt: baseTime.AddDays(1));
        var oldest = SeedProduct(db, name: "Older Row", createdAt: baseTime);
        var service = CreateService(db);

        var result = await service.LookupByBarcodeAsync(Barcode, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(oldest.Id, result.Data!.ProductId);
        Assert.Equal("Older Row", result.Data.Name);
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
        var product = SeedProduct(db, barcode: null, name: "Sherry Oak 12", brand: null, distilleryId: distillery.Id,
            imageUrl: "/uploads/products/macallan.webp");
        SeedLink(db, product);
        var service = CreateService(db);

        var result = await service.LookupByBarcodeAsync(Barcode, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(product.Id, result.Data!.ProductId);
        Assert.Equal("Sherry Oak 12", result.Data.Name);
        Assert.Equal("Macallan", result.Data.Brand);
        Assert.Equal("/uploads/products/macallan.webp", result.Data.ImageUrl);
        Assert.Equal(700, result.Data.VolumeMl);
        Assert.Equal(43.0, result.Data.AbvPercent);
    }

    [Fact]
    public async Task LookupByBarcodeAsync_WhenProductHasTheBarcodeAndACollectorGaveItToAnother_ReturnsTheProductsOwn()
    {
        var db = CreateDbContext();
        var own = SeedProduct(db, name: "Own Barcode");
        var other = SeedProduct(db, barcode: null, name: "Collector's Pick");
        SeedLink(db, other, createdAt: new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var service = CreateService(db);

        var result = await service.LookupByBarcodeAsync(Barcode, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(own.Id, result.Data!.ProductId);
    }

    [Fact]
    public async Task LookupByBarcodeAsync_WhenCollectorsGaveTheBarcodeToSeveralProducts_ReturnsTheOldestLink()
    {
        var db = CreateDbContext();
        var baseTime = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var first = SeedProduct(db, barcode: null, name: "First Link");
        var second = SeedProduct(db, barcode: null, name: "Second Link");
        SeedLink(db, second, createdAt: baseTime.AddDays(1));
        SeedLink(db, first, createdAt: baseTime);
        var service = CreateService(db);

        var result = await service.LookupByBarcodeAsync(Barcode, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(first.Id, result.Data!.ProductId);
    }

    [Fact]
    public async Task LookupByBarcodeAsync_WhenTheLinkIsDeleted_ReturnsNotFound()
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
        var baseTime = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var deleted = SeedProduct(db, barcode: null, name: "Deleted Product", isDeleted: true);
        var live = SeedProduct(db, barcode: null, name: "Live Product");
        SeedLink(db, deleted, createdAt: baseTime);
        SeedLink(db, live, createdAt: baseTime.AddDays(1));
        var service = CreateService(db);

        var result = await service.LookupByBarcodeAsync(Barcode, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(live.Id, result.Data!.ProductId);
    }

    [Fact]
    public async Task LookupByBarcodeAsync_WhenNobodyHasTheBarcode_ReturnsNotFound()
    {
        var db = CreateDbContext();
        var product = SeedProduct(db, barcode: "0000000000000");
        SeedLink(db, product, barcode: "1111111111111");
        var service = CreateService(db);

        var result = await service.LookupByBarcodeAsync(Barcode, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ErrorCode.NotFound, result.ErrorCode);
        Assert.Equal("Product not found.", result.Error);
    }

    #endregion
}
