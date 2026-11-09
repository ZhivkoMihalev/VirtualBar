using Microsoft.EntityFrameworkCore;
using Moq;
using VirtualBar.Application.Common;
using VirtualBar.Application.Interfaces;
using VirtualBar.Domain.Entities;
using VirtualBar.Domain.Enums;
using VirtualBar.Infrastructure.Decorators;
using VirtualBar.Infrastructure.Persistence;
using VirtualBar.Infrastructure.Services;

namespace VirtualBar.Tests.Services;

/// <summary>The admin's list of the barcodes collectors gave catalog products, and removing a wrong one (#49).</summary>
public sealed class ProductBarcodeServiceTests
{
    private const string Barcode = "5000267014203";

    private static readonly DateTime BaseTime = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static IProductBarcodeService CreateService(AppDbContext db, bool isAdmin = true)
    {
        var currentUser = new Mock<ICurrentUser>();
        currentUser.Setup(u => u.UserId).Returns(Guid.NewGuid());
        currentUser.Setup(u => u.IsAdmin).Returns(isAdmin);
        return new ProductBarcodeValidationDecorator(new ProductBarcodeService(db), db, currentUser.Object);
    }

    private static AppUser SeedUser(AppDbContext db, string displayName = "Collector")
    {
        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            UserName = $"{Guid.NewGuid():N}@example.com",
            Email = $"{Guid.NewGuid():N}@example.com",
            DisplayName = displayName
        };
        db.Users.Add(user);
        db.SaveChanges();
        return user;
    }

    private static Product SeedProduct(
        AppDbContext db,
        string name = "Red Label",
        string? brand = "Johnnie Walker",
        Distillery? distillery = null,
        string? barcode = null,
        bool isDeleted = false)
    {
        var product = new Product
        {
            Name = name,
            Brand = brand,
            DistilleryId = distillery?.Id,
            Category = SpiritCategory.Whisky,
            Barcode = barcode,
            CanonicalKey = Guid.NewGuid().ToString(),
            Origin = ProductOrigin.Seeded,
            IsDeleted = isDeleted,
            DeletedAt = isDeleted ? DateTime.UtcNow : null
        };
        db.Products.Add(product);
        db.SaveChanges();
        return product;
    }

    private static ProductBarcode SeedPair(
        AppDbContext db,
        Product product,
        AppUser addedBy,
        string barcode = Barcode,
        bool isDeleted = false,
        DateTime? createdAt = null)
    {
        var pair = new ProductBarcode
        {
            ProductId = product.Id,
            Barcode = barcode,
            AddedByUserId = addedBy.Id,
            IsDeleted = isDeleted,
            DeletedAt = isDeleted ? DateTime.UtcNow : null
        };
        if (createdAt is not null)
            pair.CreatedAt = createdAt.Value;
        db.ProductBarcodes.Add(pair);
        db.SaveChanges();
        return pair;
    }

    private static void SeedBottle(AppDbContext db, AppUser owner, Product product, string barcode = Barcode, bool isDeleted = false)
    {
        db.Bottles.Add(new Bottle
        {
            UserId = owner.Id,
            Name = product.Name,
            ProductId = product.Id,
            Barcode = barcode,
            IsDeleted = isDeleted,
            DeletedAt = isDeleted ? DateTime.UtcNow : null
        });
        db.SaveChanges();
    }

    #region GetAllAsync guards

    [Fact]
    public async Task GetAllAsync_WhenTheCallerIsNotAnAdmin_ReturnsForbidden()
    {
        var service = CreateService(CreateDbContext(), isAdmin: false);

        var result = await service.GetAllAsync(null, 1, 30, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ErrorCode.Forbidden, result.ErrorCode);
    }

    [Fact]
    public async Task GetAllAsync_WithCancelledToken_Throws()
    {
        var service = CreateService(CreateDbContext());
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => service.GetAllAsync(null, 1, 30, cts.Token));
    }

    [Fact]
    public async Task GetAllAsync_WhenTheSearchTermIsTooLong_ReturnsFail()
    {
        var service = CreateService(CreateDbContext());

        var result = await service.GetAllAsync(new string('x', 101), 1, 30, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ErrorCode.Validation, result.ErrorCode);
    }

    [Fact]
    public async Task GetAllAsync_WhenThePageIsOutOfRange_ReturnsFail()
    {
        var service = CreateService(CreateDbContext());

        var result = await service.GetAllAsync(null, 1001, 30, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Page is out of range.", result.Error);
    }

    [Theory]
    [InlineData(0, 0, 30)]
    [InlineData(-3, 100, 50)]
    public async Task GetAllAsync_ClampsThePageAndItsSize(int page, int pageSize, int expectedItems)
    {
        var db = CreateDbContext();
        var collector = SeedUser(db);
        var product = SeedProduct(db);
        for (var i = 0; i < 51; i++)
            SeedPair(db, product, collector, barcode: $"50000000{i:D5}");
        var service = CreateService(db);

        var result = await service.GetAllAsync(null, page, pageSize, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(expectedItems, result.Data!.Items.Count);
        Assert.True(result.Data.HasMore);
    }

    #endregion

    #region GetAllAsync

    [Fact]
    public async Task GetAllAsync_ListsLivePairsNewestFirst_WithTheirProductAndCollector()
    {
        var db = CreateDbContext();
        var collector = SeedUser(db, "Ivan");
        var product = SeedProduct(db, name: "Red Label", brand: "Johnnie Walker");
        var older = SeedPair(db, product, collector, barcode: "5000267014210", createdAt: BaseTime);
        var newer = SeedPair(db, product, collector, createdAt: BaseTime.AddDays(1));
        SeedPair(db, product, collector, barcode: "5000267014227", isDeleted: true);
        SeedPair(db, SeedProduct(db, name: "Deleted", isDeleted: true), collector, barcode: "5000267014234");
        var service = CreateService(db);

        var result = await service.GetAllAsync(null, 1, 30, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal([newer.Id, older.Id], result.Data!.Items.Select(i => i.Id));
        Assert.False(result.Data.HasMore);
        var row = result.Data.Items[0];
        Assert.Equal(Barcode, row.Barcode);
        Assert.Equal(product.Id, row.ProductId);
        Assert.Equal("Red Label", row.ProductName);
        Assert.Equal("Johnnie Walker", row.ProductBrand);
        Assert.Equal(collector.Id, row.AddedByUserId);
        Assert.Equal("Ivan", row.AddedByDisplayName);
    }

    [Fact]
    public async Task GetAllAsync_PagesThroughThePairs()
    {
        var db = CreateDbContext();
        var collector = SeedUser(db);
        var product = SeedProduct(db);
        SeedPair(db, product, collector, barcode: "5000267014210", createdAt: BaseTime);
        SeedPair(db, product, collector, barcode: "5000267014227", createdAt: BaseTime.AddDays(1));
        var service = CreateService(db);

        var result = await service.GetAllAsync(null, 2, 1, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("5000267014210", Assert.Single(result.Data!.Items).Barcode);
        Assert.False(result.Data.HasMore);
    }

    [Theory]
    [InlineData("70142", "Red Label")]
    [InlineData(" Black ", "Black Label")]
    [InlineData("Ardbeg", "Uigeadail")]
    public async Task GetAllAsync_FindsPairsByPartOfTheBarcodeOrOfTheProductsNameOrBrand(string search, string expected)
    {
        var db = CreateDbContext();
        var collector = SeedUser(db);
        SeedPair(db, SeedProduct(db, name: "Red Label"), collector, barcode: Barcode);
        SeedPair(db, SeedProduct(db, name: "Black Label"), collector, barcode: "5000267024202");
        SeedPair(db, SeedProduct(db, name: "Uigeadail", brand: "Ardbeg"), collector, barcode: "5010494195286");
        SeedPair(db, SeedProduct(db, name: "No Brand", brand: null), collector, barcode: "5010494195293");
        var service = CreateService(db);

        var result = await service.GetAllAsync(search, 1, 30, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(expected, Assert.Single(result.Data!.Items).ProductName);
    }

    [Fact]
    public async Task GetAllAsync_WhenTheProductHasNoBrand_ShowsItsLiveDistillery()
    {
        var db = CreateDbContext();
        var collector = SeedUser(db);
        var distillery = new Distillery { Name = "Lagavulin" };
        var gone = new Distillery { Name = "Gone", IsDeleted = true, DeletedAt = DateTime.UtcNow };
        db.Distilleries.AddRange(distillery, gone);
        db.SaveChanges();
        SeedPair(db, SeedProduct(db, name: "16", brand: null, distillery: distillery), collector, createdAt: BaseTime.AddDays(1));
        SeedPair(db, SeedProduct(db, name: "Old", brand: null, distillery: gone), collector, barcode: "5000267014210", createdAt: BaseTime);
        var service = CreateService(db);

        var result = await service.GetAllAsync(null, 1, 30, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(["Lagavulin", null], result.Data!.Items.Select(i => i.ProductBrand));
    }

    [Fact]
    public async Task GetAllAsync_CountsTheCollectorsWhoBackThePair_InEveryFormOfItsBarcode()
    {
        var db = CreateDbContext();
        var first = SeedUser(db, "First");
        var second = SeedUser(db, "Second");
        var third = SeedUser(db, "Third");
        var product = SeedProduct(db);
        var other = SeedProduct(db, name: "Other");
        SeedPair(db, product, first, barcode: "082184090466");
        SeedBottle(db, first, product, barcode: "082184090466");
        SeedBottle(db, second, product, barcode: "0082184090466");
        SeedBottle(db, third, product, barcode: "082184090466", isDeleted: true);
        SeedBottle(db, third, other, barcode: "082184090466");
        SeedBottle(db, third, product, barcode: "5000267014203");
        var service = CreateService(db);

        var result = await service.GetAllAsync(null, 1, 30, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(2, Assert.Single(result.Data!.Items).Collectors);
    }

    [Fact]
    public async Task GetAllAsync_FlagsABarcodeAnotherProductHasThroughACollector()
    {
        var db = CreateDbContext();
        var collector = SeedUser(db);
        var product = SeedProduct(db);
        var other = SeedProduct(db, name: "Other");
        SeedPair(db, product, collector, barcode: "082184090466", createdAt: BaseTime);
        SeedPair(db, other, collector, barcode: "0082184090466", createdAt: BaseTime.AddDays(1));
        var service = CreateService(db);

        var result = await service.GetAllAsync(null, 1, 30, CancellationToken.None);

        Assert.True(result.Success);
        Assert.All(result.Data!.Items, i => Assert.True(i.Conflict));
    }

    [Fact]
    public async Task GetAllAsync_FlagsABarcodeThatIsAnotherProductsOwn()
    {
        var db = CreateDbContext();
        var collector = SeedUser(db);
        SeedProduct(db, name: "Owner", barcode: "0082184090466");
        SeedPair(db, SeedProduct(db), collector, barcode: "082184090466");
        var service = CreateService(db);

        var result = await service.GetAllAsync(null, 1, 30, CancellationToken.None);

        Assert.True(result.Success);
        Assert.True(Assert.Single(result.Data!.Items).Conflict);
    }

    [Fact]
    public async Task GetAllAsync_DoesNotFlagTheSameProductNorARemovedPairNorADeletedProduct()
    {
        var db = CreateDbContext();
        var collector = SeedUser(db);
        var product = SeedProduct(db, barcode: Barcode);
        SeedPair(db, product, collector);
        SeedPair(db, SeedProduct(db, name: "Removed"), collector, isDeleted: true);
        SeedProduct(db, name: "Deleted Owner", barcode: Barcode, isDeleted: true);
        var service = CreateService(db);

        var result = await service.GetAllAsync(null, 1, 30, CancellationToken.None);

        Assert.True(result.Success);
        Assert.False(Assert.Single(result.Data!.Items).Conflict);
    }

    #endregion

    #region RemoveAsync

    [Fact]
    public async Task RemoveAsync_WhenTheCallerIsNotAnAdmin_ReturnsForbidden()
    {
        var db = CreateDbContext();
        var pair = SeedPair(db, SeedProduct(db), SeedUser(db));
        var service = CreateService(db, isAdmin: false);

        var result = await service.RemoveAsync(pair.Id, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ErrorCode.Forbidden, result.ErrorCode);
        Assert.False((await db.ProductBarcodes.AsNoTracking().SingleAsync()).IsDeleted);
    }

    [Fact]
    public async Task RemoveAsync_WithCancelledToken_Throws()
    {
        var service = CreateService(CreateDbContext());
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => service.RemoveAsync(Guid.NewGuid(), cts.Token));
    }

    [Fact]
    public async Task RemoveAsync_WhenThePairIsUnknownOrAlreadyRemoved_ReturnsNotFound()
    {
        var db = CreateDbContext();
        var removed = SeedPair(db, SeedProduct(db), SeedUser(db), isDeleted: true);
        var service = CreateService(db);

        var unknown = await service.RemoveAsync(Guid.NewGuid(), CancellationToken.None);
        var again = await service.RemoveAsync(removed.Id, CancellationToken.None);

        Assert.Equal(ErrorCode.NotFound, unknown.ErrorCode);
        Assert.Equal(ErrorCode.NotFound, again.ErrorCode);
    }

    [Fact]
    public async Task RemoveAsync_RemovesThePairInEveryFormOfItsBarcode_AndNothingElse()
    {
        var db = CreateDbContext();
        var collector = SeedUser(db);
        var product = SeedProduct(db);
        var other = SeedProduct(db, name: "Other");
        var pair = SeedPair(db, product, collector, barcode: "082184090466");
        SeedPair(db, product, SeedUser(db), barcode: "0082184090466");
        var kept = SeedPair(db, product, collector, barcode: "5000267014203");
        var otherProducts = SeedPair(db, other, collector, barcode: "082184090466");
        var service = CreateService(db);

        var result = await service.RemoveAsync(pair.Id, CancellationToken.None);

        Assert.True(result.Success);
        var live = await db.ProductBarcodes.AsNoTracking().Where(b => !b.IsDeleted).Select(b => b.Id).ToListAsync();
        Assert.Equal(new[] { kept.Id, otherProducts.Id }.Order(), live.Order());
        var removed = await db.ProductBarcodes.AsNoTracking().Where(b => b.IsDeleted).ToListAsync();
        Assert.Equal(2, removed.Count);
        Assert.All(removed, b => Assert.NotNull(b.DeletedAt));
    }

    #endregion
}
