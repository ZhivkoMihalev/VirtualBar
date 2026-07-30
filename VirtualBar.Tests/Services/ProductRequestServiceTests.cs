using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using VirtualBar.Application.Common;
using VirtualBar.Application.DTOs.ProductRequests;
using VirtualBar.Application.Interfaces;
using VirtualBar.Domain.Entities;
using VirtualBar.Domain.Enums;
using VirtualBar.Infrastructure.Decorators;
using VirtualBar.Infrastructure.Persistence;
using VirtualBar.Infrastructure.Services;

namespace VirtualBar.Tests.Services;

public sealed class ProductRequestServiceTests
{
    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    // SQLite in-memory is required wherever a filtered unique index must surface as DbUpdateException:
    // InMemory enforces neither the pending-request key index nor the catalog canonical-key index.
    private static AppDbContext CreateSqliteDbContext()
    {
        var conn = new SqliteConnection("DataSource=:memory:");
        conn.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(conn)
            .Options;
        var db = new AppDbContext(options);
        db.Database.EnsureCreated();
        return db;
    }

    private static ICurrentUser CreateCurrentUser(Guid userId, bool isAdmin = false)
    {
        var mock = new Mock<ICurrentUser>();
        mock.Setup(u => u.UserId).Returns(userId);
        mock.Setup(u => u.IsAuthenticated).Returns(true);
        mock.Setup(u => u.IsAdmin).Returns(isAdmin);
        return mock.Object;
    }

    private static ProductRequestService CreateInnerService(
        AppDbContext db,
        Guid currentUserId,
        INotificationService? notificationService = null) =>
        new(db, CreateCurrentUser(currentUserId), notificationService ?? Mock.Of<INotificationService>());

    private static IProductRequestService CreateService(
        AppDbContext db,
        Guid currentUserId,
        bool isAdmin = false,
        INotificationService? notificationService = null)
    {
        var currentUser = CreateCurrentUser(currentUserId, isAdmin);
        var inner = new ProductRequestService(db, currentUser, notificationService ?? Mock.Of<INotificationService>());
        return new ProductRequestValidationDecorator(inner, db, currentUser);
    }

    private static AppUser SeedUser(AppDbContext db, string displayName = "Test User")
    {
        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            UserName = $"{displayName}-{Guid.NewGuid():N}@example.com",
            Email = $"{displayName}-{Guid.NewGuid():N}@example.com",
            DisplayName = displayName
        };
        db.Users.Add(user);
        db.SaveChanges();
        return user;
    }

    private static Distillery SeedDistillery(AppDbContext db, string name = "Macallan", bool isDeleted = false)
    {
        var distillery = new Distillery
        {
            Name = name,
            Country = "Scotland",
            IsDeleted = isDeleted,
            DeletedAt = isDeleted ? DateTime.UtcNow : null
        };
        db.Distilleries.Add(distillery);
        db.SaveChanges();
        return distillery;
    }

    private static Bottle SeedBottle(
        AppDbContext db,
        Guid userId,
        string name = "Sherry Oak 12",
        SpiritCategory category = SpiritCategory.Whisky,
        int? age = 12,
        int? volumeMl = 700,
        Guid? distilleryId = null,
        Guid? productId = null,
        bool isDeleted = false)
    {
        var bottle = new Bottle
        {
            UserId = userId,
            Name = name,
            Category = category,
            Condition = BottleCondition.Sealed,
            Age = age,
            VolumeMl = volumeMl,
            DistilleryId = distilleryId,
            ProductId = productId,
            IsDeleted = isDeleted,
            DeletedAt = isDeleted ? DateTime.UtcNow : null
        };
        db.Bottles.Add(bottle);
        db.SaveChanges();
        return bottle;
    }

    private static BottleImage SeedBottleImage(
        AppDbContext db,
        Guid bottleId,
        string url,
        bool isPrimary = false,
        int sortOrder = 0,
        bool isDeleted = false)
    {
        var image = new BottleImage
        {
            BottleId = bottleId,
            Url = url,
            IsPrimary = isPrimary,
            SortOrder = sortOrder,
            IsDeleted = isDeleted,
            DeletedAt = isDeleted ? DateTime.UtcNow : null
        };
        db.BottleImages.Add(image);
        db.SaveChanges();
        return image;
    }

    private static Product SeedProduct(
        AppDbContext db,
        string name = "Sherry Oak 12",
        SpiritCategory category = SpiritCategory.Whisky,
        string? distilleryOrBrand = null,
        int? age = 12,
        int? volumeMl = 700,
        string? canonicalKey = null,
        string? imageUrl = null,
        bool isDeleted = false)
    {
        var product = new Product
        {
            Name = name,
            Brand = distilleryOrBrand,
            Category = category,
            Age = age,
            VolumeMl = volumeMl,
            ImageUrl = imageUrl,
            CanonicalKey = canonicalKey ?? ProductKey.For(distilleryOrBrand, name, category, age, null, volumeMl),
            Origin = ProductOrigin.Seeded,
            IsDeleted = isDeleted,
            DeletedAt = isDeleted ? DateTime.UtcNow : null
        };
        db.Products.Add(product);
        db.SaveChanges();
        return product;
    }

    private static ProductRequest SeedRequest(
        AppDbContext db,
        Guid userId,
        string name = "Sherry Oak 12",
        SpiritCategory category = SpiritCategory.Whisky,
        ProductRequestStatus status = ProductRequestStatus.Pending,
        string? brand = null,
        Guid? distilleryId = null,
        string? distilleryName = null,
        int? age = 12,
        double? abvPercent = 43.0,
        int? volumeMl = 700,
        string? barcode = null,
        string? country = "Scotland",
        string? region = "Speyside",
        string? userNote = null,
        string? adminNote = null,
        Guid? sourceBottleId = null,
        bool isDeleted = false,
        DateTime? createdAt = null,
        string? canonicalKey = null)
    {
        var request = new ProductRequest
        {
            UserId = userId,
            Name = name,
            Brand = brand,
            DistilleryId = distilleryId,
            Category = category,
            Age = age,
            AbvPercent = abvPercent,
            VolumeMl = volumeMl,
            Barcode = barcode,
            Country = country,
            Region = region,
            UserNote = userNote,
            AdminNote = adminNote,
            SourceBottleId = sourceBottleId,
            Status = status,
            CanonicalKey = canonicalKey
                ?? ProductKey.For(distilleryName ?? brand, name, category, age, null, volumeMl),
            IsDeleted = isDeleted,
            DeletedAt = isDeleted ? DateTime.UtcNow : null
        };
        if (createdAt is not null)
            request.CreatedAt = createdAt.Value;
        db.ProductRequests.Add(request);
        db.SaveChanges();
        return request;
    }

    private static CreateProductRequestRequest ValidCreateRequest(
        string name = "Sherry Oak 12",
        SpiritCategory category = SpiritCategory.Whisky) => new()
        {
            Name = name,
            Category = category,
            Age = 12,
            AbvPercent = 43.0,
            VolumeMl = 700
        };

    #region CreateAsync guards

    [Fact]
    public async Task CreateAsync_WithCancelledToken_Throws()
    {
        var db = CreateDbContext();
        var service = CreateService(db, Guid.NewGuid());
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => service.CreateAsync(ValidCreateRequest(), cts.Token));
    }

    [Fact]
    public async Task CreateAsync_WhenNameEmpty_ReturnsFail()
    {
        var db = CreateDbContext();
        var user = SeedUser(db);
        var service = CreateService(db, user.Id);

        var result = await service.CreateAsync(ValidCreateRequest(name: ""), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Name is required.", result.Error);
    }

    [Fact]
    public async Task CreateAsync_WhenNameWhitespace_ReturnsFail()
    {
        var db = CreateDbContext();
        var user = SeedUser(db);
        var service = CreateService(db, user.Id);

        var result = await service.CreateAsync(ValidCreateRequest(name: "   "), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Name is required.", result.Error);
    }

    [Fact]
    public async Task CreateAsync_WhenCategoryUndefined_ReturnsFail()
    {
        var db = CreateDbContext();
        var user = SeedUser(db);
        var service = CreateService(db, user.Id);

        var result = await service.CreateAsync(
            ValidCreateRequest(category: (SpiritCategory)99), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Category is invalid.", result.Error);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("1234567")]
    [InlineData("123456789012345")]
    [InlineData("1234567a")]
    public async Task CreateAsync_WhenBarcodeInvalid_ReturnsFail(string barcode)
    {
        var db = CreateDbContext();
        var user = SeedUser(db);
        var service = CreateService(db, user.Id);
        var request = ValidCreateRequest();
        request.Barcode = barcode;

        var result = await service.CreateAsync(request, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Invalid barcode.", result.Error);
    }

    [Theory]
    [InlineData("12345678")]
    [InlineData("12345678901234")]
    public async Task CreateAsync_WhenBarcodeValid_CreatesRequest(string barcode)
    {
        var db = CreateDbContext();
        var user = SeedUser(db);
        var service = CreateService(db, user.Id);
        var request = ValidCreateRequest();
        request.Barcode = barcode;

        var result = await service.CreateAsync(request, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(barcode, result.Data!.Barcode);
    }

    [Fact]
    public async Task CreateAsync_WhenBarcodeWhitespace_SkipsBarcodeValidation()
    {
        var db = CreateDbContext();
        var user = SeedUser(db);
        var service = CreateService(db, user.Id);
        var request = ValidCreateRequest();
        request.Barcode = "   ";

        var result = await service.CreateAsync(request, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Null(result.Data!.Barcode);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public async Task CreateAsync_WhenAgeOutOfRange_ReturnsFail(int age)
    {
        var db = CreateDbContext();
        var user = SeedUser(db);
        var service = CreateService(db, user.Id);
        var request = ValidCreateRequest();
        request.Age = age;

        var result = await service.CreateAsync(request, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Age must be between 1 and 100 years.", result.Error);
    }

    [Theory]
    [InlineData(0.5)]
    [InlineData(97)]
    public async Task CreateAsync_WhenAbvOutOfRange_ReturnsFail(double abv)
    {
        var db = CreateDbContext();
        var user = SeedUser(db);
        var service = CreateService(db, user.Id);
        var request = ValidCreateRequest();
        request.AbvPercent = abv;

        var result = await service.CreateAsync(request, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("ABV must be between 1 and 96 percent.", result.Error);
    }

    [Theory]
    [InlineData(19)]
    [InlineData(6001)]
    public async Task CreateAsync_WhenVolumeOutOfRange_ReturnsFail(int volumeMl)
    {
        var db = CreateDbContext();
        var user = SeedUser(db);
        var service = CreateService(db, user.Id);
        var request = ValidCreateRequest();
        request.VolumeMl = volumeMl;

        var result = await service.CreateAsync(request, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Volume must be between 20 and 6000 millilitres.", result.Error);
    }

    [Fact]
    public async Task CreateAsync_WhenNumericFieldsNull_SkipsRangeChecks()
    {
        var db = CreateDbContext();
        var user = SeedUser(db);
        var service = CreateService(db, user.Id);
        var request = new CreateProductRequestRequest { Name = "No Numbers", Category = SpiritCategory.Gin };

        var result = await service.CreateAsync(request, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Null(result.Data!.Age);
        Assert.Null(result.Data.AbvPercent);
        Assert.Null(result.Data.VolumeMl);
    }

    [Fact]
    public async Task CreateAsync_WhenBrandTooLong_ReturnsFail()
    {
        var db = CreateDbContext();
        var user = SeedUser(db);
        var service = CreateService(db, user.Id);
        var request = ValidCreateRequest();
        request.Brand = new string('b', 201);

        var result = await service.CreateAsync(request, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Brand must be 200 characters or fewer.", result.Error);
    }

    [Fact]
    public async Task CreateAsync_WhenUserNoteTooLong_ReturnsFail()
    {
        var db = CreateDbContext();
        var user = SeedUser(db);
        var service = CreateService(db, user.Id);
        var request = ValidCreateRequest();
        request.UserNote = new string('n', 501);

        var result = await service.CreateAsync(request, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Note must be 500 characters or fewer.", result.Error);
    }

    [Fact]
    public async Task CreateAsync_WhenDistilleryNotFound_ReturnsFail()
    {
        var db = CreateDbContext();
        var user = SeedUser(db);
        var service = CreateService(db, user.Id);
        var request = ValidCreateRequest();
        request.DistilleryId = Guid.NewGuid();

        var result = await service.CreateAsync(request, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Distillery not found.", result.Error);
    }

    [Fact]
    public async Task CreateAsync_WhenDistillerySoftDeleted_ReturnsFail()
    {
        var db = CreateDbContext();
        var user = SeedUser(db);
        var distillery = SeedDistillery(db, isDeleted: true);
        var service = CreateService(db, user.Id);
        var request = ValidCreateRequest();
        request.DistilleryId = distillery.Id;

        var result = await service.CreateAsync(request, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Distillery not found.", result.Error);
    }

    [Fact]
    public async Task CreateAsync_WhenSourceBottleNotFound_ReturnsFail()
    {
        var db = CreateDbContext();
        var user = SeedUser(db);
        var service = CreateService(db, user.Id);
        var request = ValidCreateRequest();
        request.SourceBottleId = Guid.NewGuid();

        var result = await service.CreateAsync(request, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Source bottle not found.", result.Error);
    }

    [Fact]
    public async Task CreateAsync_WhenSourceBottleSoftDeleted_ReturnsFail()
    {
        var db = CreateDbContext();
        var user = SeedUser(db);
        var bottle = SeedBottle(db, user.Id, isDeleted: true);
        var service = CreateService(db, user.Id);
        var request = ValidCreateRequest();
        request.SourceBottleId = bottle.Id;

        var result = await service.CreateAsync(request, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Source bottle not found.", result.Error);
    }

    [Fact]
    public async Task CreateAsync_WhenSourceBottleOwnedByAnotherUser_ReturnsForbidden()
    {
        var db = CreateDbContext();
        var owner = SeedUser(db, "Owner");
        var other = SeedUser(db, "Other");
        var bottle = SeedBottle(db, owner.Id);
        var service = CreateService(db, other.Id);
        var request = ValidCreateRequest();
        request.SourceBottleId = bottle.Id;

        var result = await service.CreateAsync(request, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ErrorCode.Forbidden, result.ErrorCode);
        Assert.Equal("Forbidden.", result.Error);
    }

    [Fact]
    public async Task CreateAsync_WhenOpenRequestCapReached_ReturnsFail()
    {
        var db = CreateDbContext();
        var user = SeedUser(db);
        for (var i = 0; i < 25; i++)
            SeedRequest(db, user.Id, name: $"Pending {i:D2}");
        var service = CreateService(db, user.Id);

        var result = await service.CreateAsync(ValidCreateRequest(name: "One More"), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("You have too many open catalog requests.", result.Error);
    }

    [Fact]
    public async Task CreateAsync_WhenBelowOpenRequestCap_CreatesRequest()
    {
        var db = CreateDbContext();
        var user = SeedUser(db);
        for (var i = 0; i < 24; i++)
            SeedRequest(db, user.Id, name: $"Pending {i:D2}");
        var service = CreateService(db, user.Id);

        var result = await service.CreateAsync(ValidCreateRequest(name: "One More"), CancellationToken.None);

        Assert.True(result.Success);
    }

    [Fact]
    public async Task CreateAsync_WhenProductAlreadyInCatalog_ReturnsConflict()
    {
        var db = CreateDbContext();
        var user = SeedUser(db);
        SeedProduct(db, name: "Sherry Oak 12", age: 12, volumeMl: 700);
        var service = CreateService(db, user.Id);

        var result = await service.CreateAsync(ValidCreateRequest(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ErrorCode.Conflict, result.ErrorCode);
        Assert.Equal("This product already exists in the catalog.", result.Error);
    }

    [Fact]
    public async Task CreateAsync_WhenCatalogProductSoftDeleted_CreatesRequest()
    {
        var db = CreateDbContext();
        var user = SeedUser(db);
        SeedProduct(db, name: "Sherry Oak 12", age: 12, volumeMl: 700, isDeleted: true);
        var service = CreateService(db, user.Id);

        var result = await service.CreateAsync(ValidCreateRequest(), CancellationToken.None);

        Assert.True(result.Success);
    }

    [Fact]
    public async Task CreateAsync_WhenPendingRequestWithSameKeyExists_ReturnsConflict()
    {
        var db = CreateDbContext();
        var user = SeedUser(db);
        var other = SeedUser(db, "Other");
        SeedRequest(db, other.Id, name: "Sherry Oak 12", age: 12, volumeMl: 700);
        var service = CreateService(db, user.Id);

        var result = await service.CreateAsync(ValidCreateRequest(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ErrorCode.Conflict, result.ErrorCode);
        Assert.Equal("This product has already been requested.", result.Error);
    }

    [Fact]
    public async Task CreateAsync_WhenExistingRequestWithSameKeyIsResolved_CreatesRequest()
    {
        var db = CreateDbContext();
        var user = SeedUser(db);
        var other = SeedUser(db, "Other");
        SeedRequest(db, other.Id, name: "Sherry Oak 12", age: 12, volumeMl: 700,
            status: ProductRequestStatus.Rejected);
        var service = CreateService(db, user.Id);

        var result = await service.CreateAsync(ValidCreateRequest(), CancellationToken.None);

        Assert.True(result.Success);
    }

    #endregion

    #region CreateAsync inner

    [Fact]
    public async Task CreateAsync_WhenValidWithDistillery_PersistsTrimmedFieldsAndDistilleryKey()
    {
        var db = CreateDbContext();
        var user = SeedUser(db, "Collector");
        var distillery = SeedDistillery(db, "Macallan");
        var service = CreateService(db, user.Id);
        var request = new CreateProductRequestRequest
        {
            Name = "  Sherry Oak 12  ",
            Brand = "  The Macallan  ",
            DistilleryId = distillery.Id,
            Category = SpiritCategory.Whisky,
            Age = 12,
            AbvPercent = 43.0,
            VolumeMl = 700,
            Barcode = "  12345678  ",
            Country = "  Scotland  ",
            Region = "  Speyside  ",
            UserNote = "  please add  "
        };

        var result = await service.CreateAsync(request, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("Sherry Oak 12", result.Data!.Name);
        Assert.Equal("The Macallan", result.Data.Brand);
        Assert.Equal("12345678", result.Data.Barcode);
        Assert.Equal("Scotland", result.Data.Country);
        Assert.Equal("Speyside", result.Data.Region);
        Assert.Equal("please add", result.Data.UserNote);
        Assert.Equal(ProductRequestStatus.Pending, result.Data.Status);
        Assert.Equal(user.Id, result.Data.RequesterId);
        Assert.Equal("Collector", result.Data.RequesterDisplayName);
        Assert.Equal("Macallan", result.Data.DistilleryName);

        var entity = await db.ProductRequests.AsNoTracking().SingleAsync();
        Assert.Equal(
            ProductKey.For("Macallan", "Sherry Oak 12", SpiritCategory.Whisky, 12, null, 700),
            entity.CanonicalKey);
        Assert.Equal(ProductRequestStatus.Pending, entity.Status);
    }

    [Fact]
    public async Task CreateAsync_WhenNoDistillery_UsesBrandForCanonicalKey()
    {
        var db = CreateDbContext();
        var user = SeedUser(db);
        var service = CreateService(db, user.Id);
        var request = new CreateProductRequestRequest
        {
            Name = "Reserva Exclusiva",
            Brand = "Diplomatico",
            Category = SpiritCategory.Rum,
            VolumeMl = 700
        };

        var result = await service.CreateAsync(request, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Null(result.Data!.DistilleryName);

        var entity = await db.ProductRequests.AsNoTracking().SingleAsync();
        Assert.Equal(
            ProductKey.For("Diplomatico", "Reserva Exclusiva", SpiritCategory.Rum, null, null, 700),
            entity.CanonicalKey);
    }

    [Fact]
    public async Task CreateAsync_WhenRequesterRowMissing_ReturnsEmptyDisplayName()
    {
        var db = CreateDbContext();
        var orphanUserId = Guid.NewGuid();
        var service = CreateService(db, orphanUserId);

        var result = await service.CreateAsync(ValidCreateRequest(), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(string.Empty, result.Data!.RequesterDisplayName);
        Assert.Equal(orphanUserId, result.Data.RequesterId);
    }

    [Fact]
    public async Task CreateAsync_WhenSourceBottleOwned_StoresSourceBottleId()
    {
        var db = CreateDbContext();
        var user = SeedUser(db);
        var bottle = SeedBottle(db, user.Id);
        var service = CreateService(db, user.Id);
        var request = ValidCreateRequest();
        request.SourceBottleId = bottle.Id;

        var result = await service.CreateAsync(request, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(bottle.Id, result.Data!.SourceBottleId);
    }

    [Fact]
    public async Task CreateAsync_WhenDuplicatePendingSlipsPastValidation_ReturnsConflict()
    {
        var db = CreateSqliteDbContext();
        var user = SeedUser(db);
        // Pre-inserted straight into the DB: the decorator's friendly pre-check never runs, so the
        // filtered unique index on CanonicalKey is what rejects the second pending row.
        SeedRequest(db, user.Id, name: "Sherry Oak 12", age: 12, volumeMl: 700);
        var inner = CreateInnerService(db, user.Id);

        var result = await inner.CreateAsync(ValidCreateRequest(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ErrorCode.Conflict, result.ErrorCode);
        Assert.Equal("This product has already been requested.", result.Error);
        Assert.DoesNotContain(
            db.ChangeTracker.Entries<ProductRequest>(),
            e => e.State == EntityState.Added);

        db.ChangeTracker.Clear();
        Assert.Equal(1, await db.ProductRequests.CountAsync());
    }

    #endregion

    #region GetMineAsync

    [Fact]
    public async Task GetMineAsync_WithCancelledToken_Throws()
    {
        var db = CreateDbContext();
        var service = CreateService(db, Guid.NewGuid());
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => service.GetMineAsync(cts.Token));
    }

    [Fact]
    public async Task GetMineAsync_ReturnsOwnNonDeletedRequestsNewestFirst()
    {
        var db = CreateDbContext();
        var user = SeedUser(db);
        var other = SeedUser(db, "Other");
        var baseTime = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        SeedRequest(db, user.Id, name: "Older", createdAt: baseTime);
        SeedRequest(db, user.Id, name: "Newer", createdAt: baseTime.AddDays(1));
        SeedRequest(db, user.Id, name: "Withdrawn", isDeleted: true);
        SeedRequest(db, other.Id, name: "Foreign");
        var service = CreateService(db, user.Id);

        var result = await service.GetMineAsync(CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(new[] { "Newer", "Older" }, result.Data!.Select(r => r.Name).ToArray());
    }

    [Fact]
    public async Task GetMineAsync_WhenDistilleryActive_ReturnsDistilleryName()
    {
        var db = CreateDbContext();
        var user = SeedUser(db);
        var distillery = SeedDistillery(db, "Macallan");
        SeedRequest(db, user.Id, distilleryId: distillery.Id, distilleryName: distillery.Name);
        var service = CreateService(db, user.Id);

        var result = await service.GetMineAsync(CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("Macallan", Assert.Single(result.Data!).DistilleryName);
    }

    [Fact]
    public async Task GetMineAsync_WhenDistillerySoftDeleted_ReturnsNullDistilleryName()
    {
        var db = CreateDbContext();
        var user = SeedUser(db);
        var distillery = SeedDistillery(db, "Macallan", isDeleted: true);
        SeedRequest(db, user.Id, distilleryId: distillery.Id, distilleryName: distillery.Name);
        var service = CreateService(db, user.Id);

        var result = await service.GetMineAsync(CancellationToken.None);

        Assert.True(result.Success);
        Assert.Null(Assert.Single(result.Data!).DistilleryName);
    }

    #endregion

    #region WithdrawAsync

    [Fact]
    public async Task WithdrawAsync_WithCancelledToken_Throws()
    {
        var db = CreateDbContext();
        var service = CreateService(db, Guid.NewGuid());
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => service.WithdrawAsync(Guid.NewGuid(), cts.Token));
    }

    [Fact]
    public async Task WithdrawAsync_WhenRequestNotFound_ReturnsNotFound()
    {
        var db = CreateDbContext();
        var user = SeedUser(db);
        var service = CreateService(db, user.Id);

        var result = await service.WithdrawAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ErrorCode.NotFound, result.ErrorCode);
        Assert.Equal("Product request not found.", result.Error);
    }

    [Fact]
    public async Task WithdrawAsync_WhenRequestAlreadyWithdrawn_ReturnsNotFound()
    {
        var db = CreateDbContext();
        var user = SeedUser(db);
        var request = SeedRequest(db, user.Id, isDeleted: true);
        var service = CreateService(db, user.Id);

        var result = await service.WithdrawAsync(request.Id, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ErrorCode.NotFound, result.ErrorCode);
    }

    [Fact]
    public async Task WithdrawAsync_WhenNotOwner_ReturnsForbidden()
    {
        var db = CreateDbContext();
        var owner = SeedUser(db, "Owner");
        var other = SeedUser(db, "Other");
        var request = SeedRequest(db, owner.Id);
        var service = CreateService(db, other.Id);

        var result = await service.WithdrawAsync(request.Id, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ErrorCode.Forbidden, result.ErrorCode);
        Assert.Equal("Forbidden.", result.Error);
    }

    [Theory]
    [InlineData(ProductRequestStatus.Approved)]
    [InlineData(ProductRequestStatus.Rejected)]
    public async Task WithdrawAsync_WhenAlreadyResolved_ReturnsConflict(ProductRequestStatus status)
    {
        var db = CreateDbContext();
        var user = SeedUser(db);
        var request = SeedRequest(db, user.Id, status: status);
        var service = CreateService(db, user.Id);

        var result = await service.WithdrawAsync(request.Id, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ErrorCode.Conflict, result.ErrorCode);
        Assert.Equal("Only pending requests can be withdrawn.", result.Error);
    }

    [Fact]
    public async Task WithdrawAsync_WhenPendingAndOwned_SoftDeletesRequest()
    {
        var db = CreateDbContext();
        var user = SeedUser(db);
        var request = SeedRequest(db, user.Id);
        var service = CreateService(db, user.Id);

        var result = await service.WithdrawAsync(request.Id, CancellationToken.None);

        Assert.True(result.Success);
        Assert.True(result.Data);

        var stored = await db.ProductRequests.AsNoTracking().SingleAsync(r => r.Id == request.Id);
        Assert.True(stored.IsDeleted);
        Assert.NotNull(stored.DeletedAt);
        Assert.Equal(ProductRequestStatus.Pending, stored.Status);
    }

    #endregion

    #region GetAllAsync

    [Fact]
    public async Task GetAllAsync_WithCancelledToken_Throws()
    {
        var db = CreateDbContext();
        var service = CreateService(db, Guid.NewGuid(), isAdmin: true);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => service.GetAllAsync(null, cts.Token));
    }

    [Fact]
    public async Task GetAllAsync_WhenNotAdmin_ReturnsForbidden()
    {
        var db = CreateDbContext();
        var user = SeedUser(db);
        var service = CreateService(db, user.Id);

        var result = await service.GetAllAsync(null, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ErrorCode.Forbidden, result.ErrorCode);
        Assert.Equal("Only administrators can manage product requests.", result.Error);
    }

    [Fact]
    public async Task GetAllAsync_WhenStatusNull_ReturnsAllNonDeletedNewestFirst()
    {
        var db = CreateDbContext();
        var admin = SeedUser(db, "Admin");
        var collector = SeedUser(db, "Collector");
        var baseTime = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        SeedRequest(db, collector.Id, name: "Older", createdAt: baseTime);
        SeedRequest(db, collector.Id, name: "Newer", createdAt: baseTime.AddDays(1),
            status: ProductRequestStatus.Approved);
        SeedRequest(db, collector.Id, name: "Deleted", isDeleted: true);
        var service = CreateService(db, admin.Id, isAdmin: true);

        var result = await service.GetAllAsync(null, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(new[] { "Newer", "Older" }, result.Data!.Select(r => r.Name).ToArray());
        Assert.All(result.Data!, r => Assert.Equal("Collector", r.RequesterDisplayName));
    }

    [Theory]
    [InlineData(ProductRequestStatus.Pending, "Pending Request")]
    [InlineData(ProductRequestStatus.Approved, "Approved Request")]
    [InlineData(ProductRequestStatus.Rejected, "Rejected Request")]
    public async Task GetAllAsync_WhenStatusFilterSet_ReturnsOnlyThatStatus(
        ProductRequestStatus status, string expectedName)
    {
        var db = CreateDbContext();
        var admin = SeedUser(db, "Admin");
        var collector = SeedUser(db, "Collector");
        SeedRequest(db, collector.Id, name: "Pending Request", status: ProductRequestStatus.Pending);
        SeedRequest(db, collector.Id, name: "Approved Request", status: ProductRequestStatus.Approved);
        SeedRequest(db, collector.Id, name: "Rejected Request", status: ProductRequestStatus.Rejected);
        var service = CreateService(db, admin.Id, isAdmin: true);

        var result = await service.GetAllAsync(status, CancellationToken.None);

        Assert.True(result.Success);
        var dto = Assert.Single(result.Data!);
        Assert.Equal(expectedName, dto.Name);
        Assert.Equal(status, dto.Status);
    }

    #endregion

    #region ApproveAsync guards

    [Fact]
    public async Task ApproveAsync_WithCancelledToken_Throws()
    {
        var db = CreateDbContext();
        var service = CreateService(db, Guid.NewGuid(), isAdmin: true);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => service.ApproveAsync(Guid.NewGuid(), new ResolveProductRequestRequest(), cts.Token));
    }

    [Fact]
    public async Task ApproveAsync_WhenNotAdmin_ReturnsForbidden()
    {
        var db = CreateDbContext();
        var user = SeedUser(db);
        var request = SeedRequest(db, user.Id);
        var service = CreateService(db, user.Id);

        var result = await service.ApproveAsync(request.Id, new ResolveProductRequestRequest(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ErrorCode.Forbidden, result.ErrorCode);
        Assert.Equal("Only administrators can manage product requests.", result.Error);
    }

    [Fact]
    public async Task ApproveAsync_WhenRequestNotFound_ReturnsNotFound()
    {
        var db = CreateDbContext();
        var admin = SeedUser(db, "Admin");
        var service = CreateService(db, admin.Id, isAdmin: true);

        var result = await service.ApproveAsync(Guid.NewGuid(), new ResolveProductRequestRequest(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ErrorCode.NotFound, result.ErrorCode);
        Assert.Equal("Product request not found.", result.Error);
    }

    [Fact]
    public async Task ApproveAsync_WhenRequestSoftDeleted_ReturnsNotFound()
    {
        var db = CreateDbContext();
        var admin = SeedUser(db, "Admin");
        var collector = SeedUser(db, "Collector");
        var request = SeedRequest(db, collector.Id, isDeleted: true);
        var service = CreateService(db, admin.Id, isAdmin: true);

        var result = await service.ApproveAsync(request.Id, new ResolveProductRequestRequest(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ErrorCode.NotFound, result.ErrorCode);
    }

    [Theory]
    [InlineData(ProductRequestStatus.Approved)]
    [InlineData(ProductRequestStatus.Rejected)]
    public async Task ApproveAsync_WhenAlreadyResolved_ReturnsConflict(ProductRequestStatus status)
    {
        var db = CreateDbContext();
        var admin = SeedUser(db, "Admin");
        var collector = SeedUser(db, "Collector");
        var request = SeedRequest(db, collector.Id, status: status);
        var service = CreateService(db, admin.Id, isAdmin: true);

        var result = await service.ApproveAsync(request.Id, new ResolveProductRequestRequest(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ErrorCode.Conflict, result.ErrorCode);
        Assert.Equal("Request already resolved.", result.Error);
    }

    [Fact]
    public async Task ApproveAsync_WhenExistingProductIdUnknown_ReturnsFail()
    {
        var db = CreateDbContext();
        var admin = SeedUser(db, "Admin");
        var collector = SeedUser(db, "Collector");
        var request = SeedRequest(db, collector.Id);
        var service = CreateService(db, admin.Id, isAdmin: true);
        var payload = new ResolveProductRequestRequest { ExistingProductId = Guid.NewGuid() };

        var result = await service.ApproveAsync(request.Id, payload, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ErrorCode.Validation, result.ErrorCode);
        Assert.Equal("Product not found.", result.Error);
    }

    [Fact]
    public async Task ApproveAsync_WhenExistingProductSoftDeleted_ReturnsFail()
    {
        var db = CreateDbContext();
        var admin = SeedUser(db, "Admin");
        var collector = SeedUser(db, "Collector");
        var request = SeedRequest(db, collector.Id);
        var product = SeedProduct(db, name: "Deleted Catalog Row", isDeleted: true);
        var service = CreateService(db, admin.Id, isAdmin: true);
        var payload = new ResolveProductRequestRequest { ExistingProductId = product.Id };

        var result = await service.ApproveAsync(request.Id, payload, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Product not found.", result.Error);
    }

    [Fact]
    public async Task ApproveAsync_WhenEffectiveNameBlank_ReturnsFail()
    {
        var db = CreateDbContext();
        var admin = SeedUser(db, "Admin");
        var collector = SeedUser(db, "Collector");
        var request = SeedRequest(db, collector.Id);
        var service = CreateService(db, admin.Id, isAdmin: true);
        var payload = new ResolveProductRequestRequest { Name = "   " };

        var result = await service.ApproveAsync(request.Id, payload, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Name is required.", result.Error);
    }

    [Fact]
    public async Task ApproveAsync_WhenEffectiveCategoryUndefined_ReturnsFail()
    {
        var db = CreateDbContext();
        var admin = SeedUser(db, "Admin");
        var collector = SeedUser(db, "Collector");
        var request = SeedRequest(db, collector.Id);
        var service = CreateService(db, admin.Id, isAdmin: true);
        var payload = new ResolveProductRequestRequest { Category = (SpiritCategory)99 };

        var result = await service.ApproveAsync(request.Id, payload, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Category is invalid.", result.Error);
    }

    [Fact]
    public async Task ApproveAsync_WhenEffectiveBarcodeInvalid_ReturnsFail()
    {
        var db = CreateDbContext();
        var admin = SeedUser(db, "Admin");
        var collector = SeedUser(db, "Collector");
        var request = SeedRequest(db, collector.Id);
        var service = CreateService(db, admin.Id, isAdmin: true);
        var payload = new ResolveProductRequestRequest { Barcode = "abc" };

        var result = await service.ApproveAsync(request.Id, payload, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Invalid barcode.", result.Error);
    }

    [Fact]
    public async Task ApproveAsync_WhenRequestBarcodeInvalidAndNoOverride_ReturnsFail()
    {
        var db = CreateDbContext();
        var admin = SeedUser(db, "Admin");
        var collector = SeedUser(db, "Collector");
        var request = SeedRequest(db, collector.Id, barcode: "123");
        var service = CreateService(db, admin.Id, isAdmin: true);

        var result = await service.ApproveAsync(request.Id, new ResolveProductRequestRequest(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Invalid barcode.", result.Error);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public async Task ApproveAsync_WhenEffectiveAgeOutOfRange_ReturnsFail(int age)
    {
        var db = CreateDbContext();
        var admin = SeedUser(db, "Admin");
        var collector = SeedUser(db, "Collector");
        var request = SeedRequest(db, collector.Id);
        var service = CreateService(db, admin.Id, isAdmin: true);
        var payload = new ResolveProductRequestRequest { Age = age };

        var result = await service.ApproveAsync(request.Id, payload, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Age must be between 1 and 100 years.", result.Error);
    }

    [Theory]
    [InlineData(0.5)]
    [InlineData(97)]
    public async Task ApproveAsync_WhenEffectiveAbvOutOfRange_ReturnsFail(double abv)
    {
        var db = CreateDbContext();
        var admin = SeedUser(db, "Admin");
        var collector = SeedUser(db, "Collector");
        var request = SeedRequest(db, collector.Id);
        var service = CreateService(db, admin.Id, isAdmin: true);
        var payload = new ResolveProductRequestRequest { AbvPercent = abv };

        var result = await service.ApproveAsync(request.Id, payload, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("ABV must be between 1 and 96 percent.", result.Error);
    }

    [Theory]
    [InlineData(19)]
    [InlineData(6001)]
    public async Task ApproveAsync_WhenEffectiveVolumeOutOfRange_ReturnsFail(int volumeMl)
    {
        var db = CreateDbContext();
        var admin = SeedUser(db, "Admin");
        var collector = SeedUser(db, "Collector");
        var request = SeedRequest(db, collector.Id);
        var service = CreateService(db, admin.Id, isAdmin: true);
        var payload = new ResolveProductRequestRequest { VolumeMl = volumeMl };

        var result = await service.ApproveAsync(request.Id, payload, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Volume must be between 20 and 6000 millilitres.", result.Error);
    }

    [Fact]
    public async Task ApproveAsync_WhenEffectiveDistilleryUnknown_ReturnsFail()
    {
        var db = CreateDbContext();
        var admin = SeedUser(db, "Admin");
        var collector = SeedUser(db, "Collector");
        var request = SeedRequest(db, collector.Id);
        var service = CreateService(db, admin.Id, isAdmin: true);
        var payload = new ResolveProductRequestRequest { DistilleryId = Guid.NewGuid() };

        var result = await service.ApproveAsync(request.Id, payload, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Distillery not found.", result.Error);
    }

    [Fact]
    public async Task ApproveAsync_WhenEffectiveDistillerySoftDeleted_ReturnsFail()
    {
        var db = CreateDbContext();
        var admin = SeedUser(db, "Admin");
        var collector = SeedUser(db, "Collector");
        var request = SeedRequest(db, collector.Id);
        var distillery = SeedDistillery(db, "Gone", isDeleted: true);
        var service = CreateService(db, admin.Id, isAdmin: true);
        var payload = new ResolveProductRequestRequest { DistilleryId = distillery.Id };

        var result = await service.ApproveAsync(request.Id, payload, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Distillery not found.", result.Error);
    }

    [Fact]
    public async Task ApproveAsync_WhenRequestDistilleryUnknownAndNoOverride_ReturnsFail()
    {
        var db = CreateDbContext();
        var admin = SeedUser(db, "Admin");
        var collector = SeedUser(db, "Collector");
        var request = SeedRequest(db, collector.Id, distilleryId: Guid.NewGuid());
        var service = CreateService(db, admin.Id, isAdmin: true);

        var result = await service.ApproveAsync(request.Id, new ResolveProductRequestRequest(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Distillery not found.", result.Error);
    }

    #endregion

    #region ApproveAsync new product

    [Fact]
    public async Task ApproveAsync_WhenNoOverrides_CreatesApprovedProductFromRequestValues()
    {
        var db = CreateDbContext();
        var admin = SeedUser(db, "Admin");
        var collector = SeedUser(db, "Collector");
        var request = SeedRequest(db, collector.Id, brand: "Diplomatico", name: "Reserva Exclusiva",
            category: SpiritCategory.Rum, age: null, volumeMl: 700, barcode: "12345678", userNote: "please");
        var notificationMock = new Mock<INotificationService>();
        var service = CreateService(db, admin.Id, isAdmin: true, notificationService: notificationMock.Object);

        var result = await service.ApproveAsync(request.Id, new ResolveProductRequestRequest(), CancellationToken.None);

        Assert.True(result.Success);

        var product = await db.Products.AsNoTracking().SingleAsync();
        Assert.Equal("Reserva Exclusiva", product.Name);
        Assert.Equal("Diplomatico", product.Brand);
        Assert.Null(product.DistilleryId);
        Assert.Equal(SpiritCategory.Rum, product.Category);
        Assert.Equal("Scotland", product.Country);
        Assert.Equal("Speyside", product.Region);
        Assert.Null(product.Age);
        Assert.Equal(43.0, product.AbvPercent);
        Assert.Equal(700, product.VolumeMl);
        Assert.Equal("12345678", product.Barcode);
        Assert.Null(product.Description);
        Assert.Null(product.ImageUrl);
        Assert.Equal(ProductOrigin.Approved, product.Origin);
        Assert.Equal(
            ProductKey.For("Diplomatico", "Reserva Exclusiva", SpiritCategory.Rum, null, null, 700),
            product.CanonicalKey);

        var stored = await db.ProductRequests.AsNoTracking().SingleAsync();
        Assert.Equal(ProductRequestStatus.Approved, stored.Status);
        Assert.Equal(product.Id, stored.ResolvedProductId);
        Assert.NotNull(stored.RespondedAt);
        Assert.Equal(product.Id, result.Data!.ResolvedProductId);

        notificationMock.Verify(n => n.CreateAsync(
            collector.Id,
            NotificationType.ProductRequestApproved,
            product.Id,
            product.Name,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ApproveAsync_WhenOverridesProvided_AppliesThemAndRecomputesKey()
    {
        var db = CreateDbContext();
        var admin = SeedUser(db, "Admin");
        var collector = SeedUser(db, "Collector");
        var distillery = SeedDistillery(db, "Macallan");
        var request = SeedRequest(db, collector.Id, name: "Old Name", brand: "Old Brand",
            category: SpiritCategory.Whisky, age: 12, abvPercent: 40.0, volumeMl: 700,
            country: "Old Country", region: "Old Region", adminNote: "old note");
        var service = CreateService(db, admin.Id, isAdmin: true);
        var payload = new ResolveProductRequestRequest
        {
            Name = "  Sherry Oak 18  ",
            Brand = "  The Macallan  ",
            DistilleryId = distillery.Id,
            Category = SpiritCategory.Rum,
            Age = 18,
            AbvPercent = 46.0,
            VolumeMl = 750,
            Barcode = "  12345678  ",
            Country = "  Scotland  ",
            Region = "  Speyside  ",
            Description = "  A fine dram  ",
            AdminNote = "  approved by admin  "
        };

        var result = await service.ApproveAsync(request.Id, payload, CancellationToken.None);

        Assert.True(result.Success);

        var product = await db.Products.AsNoTracking().SingleAsync();
        Assert.Equal("Sherry Oak 18", product.Name);
        Assert.Equal("The Macallan", product.Brand);
        Assert.Equal(distillery.Id, product.DistilleryId);
        Assert.Equal(SpiritCategory.Rum, product.Category);
        Assert.Equal(18, product.Age);
        Assert.Equal(46.0, product.AbvPercent);
        Assert.Equal(750, product.VolumeMl);
        Assert.Equal("12345678", product.Barcode);
        Assert.Equal("Scotland", product.Country);
        Assert.Equal("Speyside", product.Region);
        Assert.Equal("A fine dram", product.Description);
        Assert.Equal(
            ProductKey.For("Macallan", "Sherry Oak 18", SpiritCategory.Rum, 18, null, 750),
            product.CanonicalKey);

        var stored = await db.ProductRequests.AsNoTracking().SingleAsync();
        Assert.Equal("approved by admin", stored.AdminNote);
    }

    [Fact]
    public async Task ApproveAsync_WhenAdminNoteOmitted_KeepsExistingNote()
    {
        var db = CreateDbContext();
        var admin = SeedUser(db, "Admin");
        var collector = SeedUser(db, "Collector");
        var request = SeedRequest(db, collector.Id, adminNote: "old note");
        var service = CreateService(db, admin.Id, isAdmin: true);

        var result = await service.ApproveAsync(
            request.Id, new ResolveProductRequestRequest { AdminNote = "   " }, CancellationToken.None);

        Assert.True(result.Success);

        var stored = await db.ProductRequests.AsNoTracking().SingleAsync();
        Assert.Equal("old note", stored.AdminNote);
    }

    [Fact]
    public async Task ApproveAsync_WhenRequestCarriesDistillery_UsesDistilleryNameForKey()
    {
        var db = CreateDbContext();
        var admin = SeedUser(db, "Admin");
        var collector = SeedUser(db, "Collector");
        var distillery = SeedDistillery(db, "Macallan");
        var request = SeedRequest(db, collector.Id, name: "Sherry Oak 12", brand: "Ignored Brand",
            distilleryId: distillery.Id, distilleryName: distillery.Name, age: 12, volumeMl: 700);
        var service = CreateService(db, admin.Id, isAdmin: true);

        var result = await service.ApproveAsync(request.Id, new ResolveProductRequestRequest(), CancellationToken.None);

        Assert.True(result.Success);

        var product = await db.Products.AsNoTracking().SingleAsync();
        Assert.Equal(distillery.Id, product.DistilleryId);
        Assert.Equal(
            ProductKey.For("Macallan", "Sherry Oak 12", SpiritCategory.Whisky, 12, null, 700),
            product.CanonicalKey);
    }

    #endregion

    #region ApproveAsync existing product

    [Fact]
    public async Task ApproveAsync_WhenExistingProductId_LinksWithoutCreatingProduct()
    {
        var db = CreateDbContext();
        var admin = SeedUser(db, "Admin");
        var collector = SeedUser(db, "Collector");
        var product = SeedProduct(db, name: "Sherry Oak 12", age: 12, volumeMl: 700);
        var request = SeedRequest(db, collector.Id, name: "sherry oak twelve");
        var notificationMock = new Mock<INotificationService>();
        var service = CreateService(db, admin.Id, isAdmin: true, notificationService: notificationMock.Object);
        var payload = new ResolveProductRequestRequest { ExistingProductId = product.Id };

        var result = await service.ApproveAsync(request.Id, payload, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(1, await db.Products.CountAsync());
        Assert.Equal(product.Id, result.Data!.ResolvedProductId);

        notificationMock.Verify(n => n.CreateAsync(
            collector.Id,
            NotificationType.ProductRequestApproved,
            product.Id,
            product.Name,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ApproveAsync_WhenExistingProductId_IgnoresUseSourceBottleImage()
    {
        var db = CreateDbContext();
        var admin = SeedUser(db, "Admin");
        var collector = SeedUser(db, "Collector");
        var bottle = SeedBottle(db, collector.Id);
        SeedBottleImage(db, bottle.Id, "/uploads/bottles/source.webp", isPrimary: true);
        var product = SeedProduct(db, name: "Sherry Oak 12", imageUrl: null);
        var request = SeedRequest(db, collector.Id, sourceBottleId: bottle.Id);
        var service = CreateService(db, admin.Id, isAdmin: true);
        var payload = new ResolveProductRequestRequest
        {
            ExistingProductId = product.Id,
            UseSourceBottleImage = true
        };

        var result = await service.ApproveAsync(request.Id, payload, CancellationToken.None);

        Assert.True(result.Success);

        var stored = await db.Products.AsNoTracking().SingleAsync();
        Assert.Null(stored.ImageUrl);
    }

    #endregion

    #region ApproveAsync source bottle

    [Fact]
    public async Task ApproveAsync_WhenSourceBottleUnlinked_LinksItToTheProduct()
    {
        var db = CreateDbContext();
        var admin = SeedUser(db, "Admin");
        var collector = SeedUser(db, "Collector");
        var bottle = SeedBottle(db, collector.Id, name: "Sherry Oak 12", age: 12, volumeMl: 700);
        var request = SeedRequest(db, collector.Id, name: "Sherry Oak 12", age: 12, volumeMl: 700,
            sourceBottleId: bottle.Id);
        var service = CreateService(db, admin.Id, isAdmin: true);

        var result = await service.ApproveAsync(request.Id, new ResolveProductRequestRequest(), CancellationToken.None);

        Assert.True(result.Success);

        var product = await db.Products.AsNoTracking().SingleAsync();
        var stored = await db.Bottles.AsNoTracking().SingleAsync(b => b.Id == bottle.Id);
        Assert.Equal(product.Id, stored.ProductId);
    }

    [Fact]
    public async Task ApproveAsync_WhenSourceBottleAlreadyLinked_LeavesItUntouched()
    {
        var db = CreateDbContext();
        var admin = SeedUser(db, "Admin");
        var collector = SeedUser(db, "Collector");
        var otherProduct = SeedProduct(db, name: "Some Other Product", age: 25, volumeMl: 500);
        var bottle = SeedBottle(db, collector.Id, name: "Sherry Oak 12", age: 12, volumeMl: 700,
            productId: otherProduct.Id);
        var request = SeedRequest(db, collector.Id, name: "Sherry Oak 12", age: 12, volumeMl: 700,
            sourceBottleId: bottle.Id);
        var service = CreateService(db, admin.Id, isAdmin: true);

        var result = await service.ApproveAsync(request.Id, new ResolveProductRequestRequest(), CancellationToken.None);

        Assert.True(result.Success);

        var stored = await db.Bottles.AsNoTracking().SingleAsync(b => b.Id == bottle.Id);
        Assert.Equal(otherProduct.Id, stored.ProductId);
    }

    [Fact]
    public async Task ApproveAsync_WhenSourceBottleSoftDeleted_SkipsLinking()
    {
        var db = CreateDbContext();
        var admin = SeedUser(db, "Admin");
        var collector = SeedUser(db, "Collector");
        var bottle = SeedBottle(db, collector.Id, name: "Sherry Oak 12", age: 12, volumeMl: 700,
            isDeleted: true);
        var request = SeedRequest(db, collector.Id, name: "Sherry Oak 12", age: 12, volumeMl: 700,
            sourceBottleId: bottle.Id);
        var service = CreateService(db, admin.Id, isAdmin: true);

        var result = await service.ApproveAsync(request.Id, new ResolveProductRequestRequest(), CancellationToken.None);

        Assert.True(result.Success);

        var stored = await db.Bottles.AsNoTracking().SingleAsync(b => b.Id == bottle.Id);
        Assert.Null(stored.ProductId);
    }

    [Fact]
    public async Task ApproveAsync_WhenNoSourceBottle_ApprovesWithoutLinking()
    {
        var db = CreateDbContext();
        var admin = SeedUser(db, "Admin");
        var collector = SeedUser(db, "Collector");
        var request = SeedRequest(db, collector.Id, sourceBottleId: null);
        var service = CreateService(db, admin.Id, isAdmin: true);

        var result = await service.ApproveAsync(request.Id, new ResolveProductRequestRequest(), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Null(result.Data!.SourceBottleId);
        Assert.Equal(1, await db.Products.CountAsync());
    }

    #endregion

    #region ApproveAsync image reuse

    [Fact]
    public async Task ApproveAsync_WhenUseSourceBottleImageAndPrimaryImage_CopiesPrimaryUrl()
    {
        var db = CreateDbContext();
        var admin = SeedUser(db, "Admin");
        var collector = SeedUser(db, "Collector");
        var bottle = SeedBottle(db, collector.Id);
        SeedBottleImage(db, bottle.Id, "/uploads/bottles/gallery.webp", isPrimary: false, sortOrder: 0);
        SeedBottleImage(db, bottle.Id, "/uploads/bottles/primary.webp", isPrimary: true, sortOrder: 5);
        var request = SeedRequest(db, collector.Id, sourceBottleId: bottle.Id);
        var service = CreateService(db, admin.Id, isAdmin: true);
        var payload = new ResolveProductRequestRequest { UseSourceBottleImage = true };

        var result = await service.ApproveAsync(request.Id, payload, CancellationToken.None);

        Assert.True(result.Success);

        var product = await db.Products.AsNoTracking().SingleAsync();
        Assert.Equal("/uploads/bottles/primary.webp", product.ImageUrl);
    }

    [Fact]
    public async Task ApproveAsync_WhenUseSourceBottleImageAndNoPrimary_CopiesLowestSortOrderUrl()
    {
        var db = CreateDbContext();
        var admin = SeedUser(db, "Admin");
        var collector = SeedUser(db, "Collector");
        var bottle = SeedBottle(db, collector.Id);
        SeedBottleImage(db, bottle.Id, "/uploads/bottles/second.webp", sortOrder: 2);
        SeedBottleImage(db, bottle.Id, "/uploads/bottles/first.webp", sortOrder: 1);
        SeedBottleImage(db, bottle.Id, "/uploads/bottles/deleted.webp", sortOrder: 0, isDeleted: true);
        var request = SeedRequest(db, collector.Id, sourceBottleId: bottle.Id);
        var service = CreateService(db, admin.Id, isAdmin: true);
        var payload = new ResolveProductRequestRequest { UseSourceBottleImage = true };

        var result = await service.ApproveAsync(request.Id, payload, CancellationToken.None);

        Assert.True(result.Success);

        var product = await db.Products.AsNoTracking().SingleAsync();
        Assert.Equal("/uploads/bottles/first.webp", product.ImageUrl);
    }

    [Fact]
    public async Task ApproveAsync_WhenUseSourceBottleImageAndNoImages_LeavesImageNull()
    {
        var db = CreateDbContext();
        var admin = SeedUser(db, "Admin");
        var collector = SeedUser(db, "Collector");
        var bottle = SeedBottle(db, collector.Id);
        var request = SeedRequest(db, collector.Id, sourceBottleId: bottle.Id);
        var service = CreateService(db, admin.Id, isAdmin: true);
        var payload = new ResolveProductRequestRequest { UseSourceBottleImage = true };

        var result = await service.ApproveAsync(request.Id, payload, CancellationToken.None);

        Assert.True(result.Success);

        var product = await db.Products.AsNoTracking().SingleAsync();
        Assert.Null(product.ImageUrl);
    }

    [Fact]
    public async Task ApproveAsync_WhenUseSourceBottleImageAndSourceBottleDeleted_LeavesImageNull()
    {
        var db = CreateDbContext();
        var admin = SeedUser(db, "Admin");
        var collector = SeedUser(db, "Collector");
        var bottle = SeedBottle(db, collector.Id, isDeleted: true);
        SeedBottleImage(db, bottle.Id, "/uploads/bottles/primary.webp", isPrimary: true);
        var request = SeedRequest(db, collector.Id, sourceBottleId: bottle.Id);
        var service = CreateService(db, admin.Id, isAdmin: true);
        var payload = new ResolveProductRequestRequest { UseSourceBottleImage = true };

        var result = await service.ApproveAsync(request.Id, payload, CancellationToken.None);

        Assert.True(result.Success);

        var product = await db.Products.AsNoTracking().SingleAsync();
        Assert.Null(product.ImageUrl);
    }

    [Fact]
    public async Task ApproveAsync_WhenUseSourceBottleImageWithoutSourceBottle_LeavesImageNull()
    {
        var db = CreateDbContext();
        var admin = SeedUser(db, "Admin");
        var collector = SeedUser(db, "Collector");
        var request = SeedRequest(db, collector.Id, sourceBottleId: null);
        var service = CreateService(db, admin.Id, isAdmin: true);
        var payload = new ResolveProductRequestRequest { UseSourceBottleImage = true };

        var result = await service.ApproveAsync(request.Id, payload, CancellationToken.None);

        Assert.True(result.Success);

        var product = await db.Products.AsNoTracking().SingleAsync();
        Assert.Null(product.ImageUrl);
    }

    [Fact]
    public async Task ApproveAsync_WhenUseSourceBottleImageFalse_LeavesImageNull()
    {
        var db = CreateDbContext();
        var admin = SeedUser(db, "Admin");
        var collector = SeedUser(db, "Collector");
        var bottle = SeedBottle(db, collector.Id);
        SeedBottleImage(db, bottle.Id, "/uploads/bottles/primary.webp", isPrimary: true);
        var request = SeedRequest(db, collector.Id, sourceBottleId: bottle.Id);
        var service = CreateService(db, admin.Id, isAdmin: true);

        var result = await service.ApproveAsync(
            request.Id, new ResolveProductRequestRequest { UseSourceBottleImage = false }, CancellationToken.None);

        Assert.True(result.Success);

        var product = await db.Products.AsNoTracking().SingleAsync();
        Assert.Null(product.ImageUrl);
    }

    [Fact]
    public async Task ApproveAsync_WhenImageUrlOverrideProvided_WinsOverSourceBottleImage()
    {
        var db = CreateDbContext();
        var admin = SeedUser(db, "Admin");
        var collector = SeedUser(db, "Collector");
        var bottle = SeedBottle(db, collector.Id);
        SeedBottleImage(db, bottle.Id, "/uploads/bottles/primary.webp", isPrimary: true);
        var request = SeedRequest(db, collector.Id, sourceBottleId: bottle.Id);
        var service = CreateService(db, admin.Id, isAdmin: true);
        var payload = new ResolveProductRequestRequest
        {
            ImageUrl = "  /uploads/products/official.webp  ",
            UseSourceBottleImage = true
        };

        var result = await service.ApproveAsync(request.Id, payload, CancellationToken.None);

        Assert.True(result.Success);

        var product = await db.Products.AsNoTracking().SingleAsync();
        Assert.Equal("/uploads/products/official.webp", product.ImageUrl);
    }

    #endregion

    #region ApproveAsync retro-link

    [Fact]
    public async Task ApproveAsync_WhenUnlinkedBottleMatchesKey_RetroLinksIt()
    {
        var db = CreateDbContext();
        var admin = SeedUser(db, "Admin");
        var collector = SeedUser(db, "Collector");
        var stranger = SeedUser(db, "Stranger");
        var distillery = SeedDistillery(db, "Macallan");
        var twin = SeedBottle(db, stranger.Id, name: "The Sherry Oak 12", age: 12, volumeMl: 700,
            distilleryId: distillery.Id);
        var request = SeedRequest(db, collector.Id, name: "Sherry Oak 12", distilleryId: distillery.Id,
            distilleryName: distillery.Name, age: 12, volumeMl: 700);
        var notificationMock = new Mock<INotificationService>();
        var service = CreateService(db, admin.Id, isAdmin: true, notificationService: notificationMock.Object);

        var result = await service.ApproveAsync(request.Id, new ResolveProductRequestRequest(), CancellationToken.None);

        Assert.True(result.Success);

        var product = await db.Products.AsNoTracking().SingleAsync();
        var stored = await db.Bottles.AsNoTracking().SingleAsync(b => b.Id == twin.Id);
        Assert.Equal(product.Id, stored.ProductId);

        // Retro-linked owners are enriched silently — only the requester is notified.
        notificationMock.Verify(n => n.CreateAsync(
            It.IsAny<Guid>(), It.IsAny<NotificationType>(), It.IsAny<Guid?>(), It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Regression: the candidate key used to read the distillery name straight off the navigation, while
    /// every other key builder (BottleService.AddBottleAsync, BuildProductAsync) treats a soft-deleted
    /// distillery as absent. The two disagreed, so a bottle whose distillery had been retired never
    /// retro-linked.
    /// </summary>
    [Fact]
    public async Task ApproveAsync_WhenBottleDistillerySoftDeleted_StillRetroLinksIt()
    {
        var db = CreateDbContext();
        var admin = SeedUser(db, "Admin");
        var collector = SeedUser(db, "Collector");
        var stranger = SeedUser(db, "Stranger");
        var retired = SeedDistillery(db, "Macallan", isDeleted: true);
        var twin = SeedBottle(db, stranger.Id, name: "Sherry Oak 12", age: 12, volumeMl: 700,
            distilleryId: retired.Id);
        var request = SeedRequest(db, collector.Id, name: "Sherry Oak 12", age: 12, volumeMl: 700);
        var service = CreateService(db, admin.Id, isAdmin: true);

        var result = await service.ApproveAsync(request.Id, new ResolveProductRequestRequest(), CancellationToken.None);

        Assert.True(result.Success);

        var product = await db.Products.AsNoTracking().SingleAsync();
        var stored = await db.Bottles.AsNoTracking().SingleAsync(b => b.Id == twin.Id);
        Assert.Equal(product.Id, stored.ProductId);
    }

    [Fact]
    public async Task ApproveAsync_WhenBottleNameDiffers_DoesNotRetroLink()
    {
        var db = CreateDbContext();
        var admin = SeedUser(db, "Admin");
        var collector = SeedUser(db, "Collector");
        var stranger = SeedUser(db, "Stranger");
        var different = SeedBottle(db, stranger.Id, name: "Double Cask 12", age: 12, volumeMl: 700);
        var request = SeedRequest(db, collector.Id, name: "Sherry Oak 12", age: 12, volumeMl: 700);
        var service = CreateService(db, admin.Id, isAdmin: true);

        var result = await service.ApproveAsync(request.Id, new ResolveProductRequestRequest(), CancellationToken.None);

        Assert.True(result.Success);

        var stored = await db.Bottles.AsNoTracking().SingleAsync(b => b.Id == different.Id);
        Assert.Null(stored.ProductId);
    }

    [Fact]
    public async Task ApproveAsync_WhenBottleCategoryDiffers_DoesNotRetroLink()
    {
        var db = CreateDbContext();
        var admin = SeedUser(db, "Admin");
        var collector = SeedUser(db, "Collector");
        var stranger = SeedUser(db, "Stranger");
        var otherCategory = SeedBottle(db, stranger.Id, name: "Sherry Oak 12", category: SpiritCategory.Rum,
            age: 12, volumeMl: 700);
        var request = SeedRequest(db, collector.Id, name: "Sherry Oak 12", category: SpiritCategory.Whisky,
            age: 12, volumeMl: 700);
        var service = CreateService(db, admin.Id, isAdmin: true);

        var result = await service.ApproveAsync(request.Id, new ResolveProductRequestRequest(), CancellationToken.None);

        Assert.True(result.Success);

        var stored = await db.Bottles.AsNoTracking().SingleAsync(b => b.Id == otherCategory.Id);
        Assert.Null(stored.ProductId);
    }

    [Fact]
    public async Task ApproveAsync_WhenAgeAndVolumeNull_RetroLinksOnNullEquality()
    {
        var db = CreateDbContext();
        var admin = SeedUser(db, "Admin");
        var collector = SeedUser(db, "Collector");
        var stranger = SeedUser(db, "Stranger");
        var twin = SeedBottle(db, stranger.Id, name: "Blue Label", age: null, volumeMl: null);
        var aged = SeedBottle(db, stranger.Id, name: "Blue Label", age: 12, volumeMl: null);
        var request = SeedRequest(db, collector.Id, name: "Blue Label", age: null, volumeMl: null);
        var service = CreateService(db, admin.Id, isAdmin: true);

        var result = await service.ApproveAsync(request.Id, new ResolveProductRequestRequest(), CancellationToken.None);

        Assert.True(result.Success);

        var product = await db.Products.AsNoTracking().SingleAsync();
        Assert.Equal(product.Id, (await db.Bottles.AsNoTracking().SingleAsync(b => b.Id == twin.Id)).ProductId);
        Assert.Null((await db.Bottles.AsNoTracking().SingleAsync(b => b.Id == aged.Id)).ProductId);
    }

    [Fact]
    public async Task ApproveAsync_WhenMatchingBottleIsDeleted_DoesNotRetroLink()
    {
        var db = CreateDbContext();
        var admin = SeedUser(db, "Admin");
        var collector = SeedUser(db, "Collector");
        var stranger = SeedUser(db, "Stranger");
        var deleted = SeedBottle(db, stranger.Id, name: "Sherry Oak 12", age: 12, volumeMl: 700,
            isDeleted: true);
        var request = SeedRequest(db, collector.Id, name: "Sherry Oak 12", age: 12, volumeMl: 700);
        var service = CreateService(db, admin.Id, isAdmin: true);

        var result = await service.ApproveAsync(request.Id, new ResolveProductRequestRequest(), CancellationToken.None);

        Assert.True(result.Success);

        var stored = await db.Bottles.AsNoTracking().SingleAsync(b => b.Id == deleted.Id);
        Assert.Null(stored.ProductId);
    }

    #endregion

    #region ApproveAsync duplicate product race

    [Fact]
    public async Task ApproveAsync_WhenProductKeyAlreadyExists_ReturnsConflictAndSkipsNotification()
    {
        var db = CreateSqliteDbContext();
        var admin = SeedUser(db, "Admin");
        var collector = SeedUser(db, "Collector");
        SeedProduct(db, name: "Sherry Oak 12", age: 12, volumeMl: 700);
        var request = SeedRequest(db, collector.Id, name: "Sherry Oak 12", age: 12, volumeMl: 700);
        var notificationMock = new Mock<INotificationService>();
        var service = CreateService(db, admin.Id, isAdmin: true, notificationService: notificationMock.Object);

        var result = await service.ApproveAsync(request.Id, new ResolveProductRequestRequest(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ErrorCode.Conflict, result.ErrorCode);
        Assert.Equal("A product with the same canonical identity already exists.", result.Error);
        Assert.DoesNotContain(db.ChangeTracker.Entries<Product>(), e => e.State == EntityState.Added);

        notificationMock.Verify(n => n.CreateAsync(
            It.IsAny<Guid>(), It.IsAny<NotificationType>(), It.IsAny<Guid?>(), It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Never);

        db.ChangeTracker.Clear();
        Assert.Equal(1, await db.Products.CountAsync());
        Assert.Equal(ProductRequestStatus.Pending,
            (await db.ProductRequests.AsNoTracking().SingleAsync()).Status);
    }

    #endregion

    #region RejectAsync

    [Fact]
    public async Task RejectAsync_WithCancelledToken_Throws()
    {
        var db = CreateDbContext();
        var service = CreateService(db, Guid.NewGuid(), isAdmin: true);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => service.RejectAsync(Guid.NewGuid(), new RejectProductRequestRequest(), cts.Token));
    }

    [Fact]
    public async Task RejectAsync_WhenNotAdmin_ReturnsForbidden()
    {
        var db = CreateDbContext();
        var user = SeedUser(db);
        var request = SeedRequest(db, user.Id);
        var service = CreateService(db, user.Id);

        var result = await service.RejectAsync(request.Id, new RejectProductRequestRequest(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ErrorCode.Forbidden, result.ErrorCode);
        Assert.Equal("Only administrators can manage product requests.", result.Error);
    }

    [Fact]
    public async Task RejectAsync_WhenRequestNotFound_ReturnsNotFound()
    {
        var db = CreateDbContext();
        var admin = SeedUser(db, "Admin");
        var service = CreateService(db, admin.Id, isAdmin: true);

        var result = await service.RejectAsync(Guid.NewGuid(), new RejectProductRequestRequest(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ErrorCode.NotFound, result.ErrorCode);
        Assert.Equal("Product request not found.", result.Error);
    }

    [Fact]
    public async Task RejectAsync_WhenRequestSoftDeleted_ReturnsNotFound()
    {
        var db = CreateDbContext();
        var admin = SeedUser(db, "Admin");
        var collector = SeedUser(db, "Collector");
        var request = SeedRequest(db, collector.Id, isDeleted: true);
        var service = CreateService(db, admin.Id, isAdmin: true);

        var result = await service.RejectAsync(request.Id, new RejectProductRequestRequest(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ErrorCode.NotFound, result.ErrorCode);
    }

    [Theory]
    [InlineData(ProductRequestStatus.Approved)]
    [InlineData(ProductRequestStatus.Rejected)]
    public async Task RejectAsync_WhenAlreadyResolved_ReturnsConflict(ProductRequestStatus status)
    {
        var db = CreateDbContext();
        var admin = SeedUser(db, "Admin");
        var collector = SeedUser(db, "Collector");
        var request = SeedRequest(db, collector.Id, status: status);
        var service = CreateService(db, admin.Id, isAdmin: true);

        var result = await service.RejectAsync(request.Id, new RejectProductRequestRequest(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ErrorCode.Conflict, result.ErrorCode);
        Assert.Equal("Request already resolved.", result.Error);
    }

    [Fact]
    public async Task RejectAsync_WhenValid_SetsRejectedAndNotifiesRequester()
    {
        var db = CreateDbContext();
        var admin = SeedUser(db, "Admin");
        var collector = SeedUser(db, "Collector");
        var request = SeedRequest(db, collector.Id, name: "Sherry Oak 12");
        var notificationMock = new Mock<INotificationService>();
        var service = CreateService(db, admin.Id, isAdmin: true, notificationService: notificationMock.Object);

        var result = await service.RejectAsync(
            request.Id, new RejectProductRequestRequest { AdminNote = "  not a real product  " }, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(ProductRequestStatus.Rejected, result.Data!.Status);

        var stored = await db.ProductRequests.AsNoTracking().SingleAsync();
        Assert.Equal(ProductRequestStatus.Rejected, stored.Status);
        Assert.Equal("not a real product", stored.AdminNote);
        Assert.NotNull(stored.RespondedAt);
        Assert.Null(stored.ResolvedProductId);

        notificationMock.Verify(n => n.CreateAsync(
            collector.Id,
            NotificationType.ProductRequestRejected,
            request.Id,
            "Sherry Oak 12",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RejectAsync_WhenAdminNoteOmitted_KeepsExistingNote()
    {
        var db = CreateDbContext();
        var admin = SeedUser(db, "Admin");
        var collector = SeedUser(db, "Collector");
        var request = SeedRequest(db, collector.Id, adminNote: "old note");
        var service = CreateService(db, admin.Id, isAdmin: true);

        var result = await service.RejectAsync(request.Id, new RejectProductRequestRequest(), CancellationToken.None);

        Assert.True(result.Success);

        var stored = await db.ProductRequests.AsNoTracking().SingleAsync();
        Assert.Equal("old note", stored.AdminNote);
    }

    #endregion
}
