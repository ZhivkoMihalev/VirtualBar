using Microsoft.EntityFrameworkCore;
using Moq;
using VirtualBar.Application.Common;
using VirtualBar.Application.DTOs.Bottles;
using VirtualBar.Application.Interfaces;
using VirtualBar.Domain.Entities;
using VirtualBar.Domain.Enums;
using VirtualBar.Infrastructure.Decorators;
using VirtualBar.Infrastructure.Persistence;
using VirtualBar.Infrastructure.Services;

namespace VirtualBar.Tests.Services;

public sealed class BottleServiceTests
{
    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static ICurrentUser CreateCurrentUser(Guid userId)
    {
        var mock = new Mock<ICurrentUser>();
        mock.Setup(u => u.UserId).Returns(userId);
        mock.Setup(u => u.IsAuthenticated).Returns(true);
        return mock.Object;
    }

    private static IBottleService CreateBottleService(AppDbContext db, Guid currentUserId, INotificationService? notificationService = null)
    {
        var currentUser = CreateCurrentUser(currentUserId);
        var inner = new BottleService(db, currentUser, notificationService ?? Mock.Of<INotificationService>());
        return new BottleValidationDecorator(inner, db, currentUser);
    }

    private static AppUser SeedUser(AppDbContext db, string displayName = "Test User")
    {
        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            UserName = $"{displayName}@example.com",
            Email = $"{displayName}@example.com",
            DisplayName = displayName
        };
        db.Users.Add(user);
        db.SaveChanges();
        return user;
    }

    private static Bottle SeedBottle(
        AppDbContext db,
        Guid userId,
        string name = "Lagavulin 16",
        bool isForSale = false,
        bool isDeleted = false,
        decimal? askingPrice = null,
        string? currency = null,
        Guid? distilleryId = null)
    {
        var bottle = new Bottle
        {
            UserId = userId,
            Name = name,
            DistilleryId = distilleryId,
            Category = SpiritCategory.Whisky,
            Condition = BottleCondition.Sealed,
            IsForSale = isForSale,
            IsDeleted = isDeleted,
            DeletedAt = isDeleted ? DateTime.UtcNow : null,
            AskingPrice = askingPrice,
            Currency = currency
        };
        db.Bottles.Add(bottle);
        db.SaveChanges();
        return bottle;
    }

    private static Distillery SeedDistillery(AppDbContext db, string name = "Lagavulin")
    {
        var distillery = new Distillery { Name = name };
        db.Distilleries.Add(distillery);
        db.SaveChanges();
        return distillery;
    }

    #region GetBottlesByUserAsync

    [Fact]
    public async Task GetBottlesByUserAsync_WhenUserIdEmpty_ReturnsFail()
    {
        var db = CreateDbContext();
        var service = CreateBottleService(db, Guid.NewGuid());

        var result = await service.GetBottlesByUserAsync(Guid.Empty, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("User ID is required.", result.Error);
    }

    [Fact]
    public async Task GetBottlesByUserAsync_WhenNoBottles_ReturnsEmptyList()
    {
        var db = CreateDbContext();
        var user = SeedUser(db);
        var service = CreateBottleService(db, user.Id);

        var result = await service.GetBottlesByUserAsync(user.Id, CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Empty(result.Data);
    }

    [Fact]
    public async Task GetBottlesByUserAsync_DoesNotReturnDeletedBottles()
    {
        var db = CreateDbContext();
        var user = SeedUser(db);
        SeedBottle(db, user.Id, "Visible");
        SeedBottle(db, user.Id, "Deleted", isDeleted: true);
        var service = CreateBottleService(db, user.Id);

        var result = await service.GetBottlesByUserAsync(user.Id, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Single(result.Data!);
        Assert.Equal("Visible", result.Data![0].Name);
    }

    [Fact]
    public async Task GetBottlesByUserAsync_ReturnsOnlyRequestedUsersBottles()
    {
        var db = CreateDbContext();
        var userA = SeedUser(db, "User A");
        var userB = SeedUser(db, "User B");
        SeedBottle(db, userA.Id, "A Bottle");
        SeedBottle(db, userB.Id, "B Bottle");
        var service = CreateBottleService(db, userA.Id);

        var result = await service.GetBottlesByUserAsync(userA.Id, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Single(result.Data!);
        Assert.Equal("A Bottle", result.Data![0].Name);
    }

    [Fact]
    public async Task GetBottlesByUserAsync_WithCancelledToken_Throws()
    {
        var db = CreateDbContext();
        var service = CreateBottleService(db, Guid.NewGuid());
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => service.GetBottlesByUserAsync(Guid.NewGuid(), cts.Token));
    }

    #endregion

    #region GetBottleByIdAsync

    [Fact]
    public async Task GetBottleByIdAsync_WhenNotFound_ReturnsNotFound()
    {
        var db = CreateDbContext();
        var service = CreateBottleService(db, Guid.NewGuid());

        var result = await service.GetBottleByIdAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Bottle not found.", result.Error);
    }

    [Fact]
    public async Task GetBottleByIdAsync_WhenDeleted_ReturnsNotFound()
    {
        var db = CreateDbContext();
        var user = SeedUser(db);
        var bottle = SeedBottle(db, user.Id, isDeleted: true);
        var service = CreateBottleService(db, user.Id);

        var result = await service.GetBottleByIdAsync(bottle.Id, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Bottle not found.", result.Error);
    }

    [Fact]
    public async Task GetBottleByIdAsync_WhenFound_ReturnsBottle()
    {
        var db = CreateDbContext();
        var user = SeedUser(db, "Collector");
        var bottle = SeedBottle(db, user.Id, "Ardbeg 10");
        var service = CreateBottleService(db, user.Id);

        var result = await service.GetBottleByIdAsync(bottle.Id, CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(bottle.Id, result.Data.Id);
        Assert.Equal("Ardbeg 10", result.Data.Name);
        Assert.Equal("Collector", result.Data.UserDisplayName);
    }

    [Fact]
    public async Task GetBottleByIdAsync_ExcludesSoftDeletedImagesAndComments()
    {
        var db = CreateDbContext();
        var user = SeedUser(db, "Collector");
        var bottle = SeedBottle(db, user.Id, "Ardbeg 10");

        db.BottleImages.AddRange(
            new BottleImage { BottleId = bottle.Id, Url = "/uploads/bottles/live.jpg", IsPrimary = true, SortOrder = 0 },
            new BottleImage { BottleId = bottle.Id, Url = "/uploads/bottles/deleted.jpg", IsPrimary = false, SortOrder = 1, IsDeleted = true, DeletedAt = DateTime.UtcNow });
        db.BottleComments.AddRange(
            new BottleComment { BottleId = bottle.Id, UserId = user.Id, Content = "live" },
            new BottleComment { BottleId = bottle.Id, UserId = user.Id, Content = "gone", IsDeleted = true, DeletedAt = DateTime.UtcNow });
        db.SaveChanges();
        db.ChangeTracker.Clear(); // mirror a fresh per-request context so the filtered Include isn't bypassed by fixup

        var service = CreateBottleService(db, user.Id);

        var result = await service.GetBottleByIdAsync(bottle.Id, CancellationToken.None);

        Assert.True(result.Success);
        var image = Assert.Single(result.Data!.Images);
        Assert.Equal("/uploads/bottles/live.jpg", image.Url);
        Assert.Equal(1, result.Data.CommentsCount);
    }

    [Fact]
    public async Task GetBottleByIdAsync_WithCancelledToken_Throws()
    {
        var db = CreateDbContext();
        var service = CreateBottleService(db, Guid.NewGuid());
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => service.GetBottleByIdAsync(Guid.NewGuid(), cts.Token));
    }

    #endregion

    #region AddBottleAsync

    [Fact]
    public async Task AddBottleAsync_WhenNameEmpty_ReturnsFail()
    {
        var db = CreateDbContext();
        var service = CreateBottleService(db, Guid.NewGuid());
        var request = new AddBottleRequest { Name = "" };

        var result = await service.AddBottleAsync(request, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Name is required.", result.Error);
    }

    [Fact]
    public async Task AddBottleAsync_WhenNameWhitespace_ReturnsFail()
    {
        var db = CreateDbContext();
        var service = CreateBottleService(db, Guid.NewGuid());
        var request = new AddBottleRequest { Name = "   " };

        var result = await service.AddBottleAsync(request, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Name is required.", result.Error);
    }

    [Fact]
    public async Task AddBottleAsync_WhenDistilleryIdNotFound_ReturnsNotFound()
    {
        var db = CreateDbContext();
        var service = CreateBottleService(db, Guid.NewGuid());
        var request = new AddBottleRequest { Name = "Valid", DistilleryId = Guid.NewGuid() };

        var result = await service.AddBottleAsync(request, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Distillery not found.", result.Error);
    }

    [Fact]
    public async Task AddBottleAsync_WhenValid_ReturnsCreatedBottle()
    {
        var db = CreateDbContext();
        var userId = Guid.NewGuid();
        var service = CreateBottleService(db, userId);
        var request = new AddBottleRequest
        {
            Name = "Glenfiddich 18",
            Category = SpiritCategory.Whisky,
            Condition = BottleCondition.Sealed,
            IsLimited = true
        };

        var result = await service.AddBottleAsync(request, CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal("Glenfiddich 18", result.Data.Name);
        Assert.Equal(userId, result.Data.UserId);
        Assert.True(result.Data.IsLimited);
        Assert.NotEqual(Guid.Empty, result.Data.Id);
    }

    [Fact]
    public async Task AddBottleAsync_WithCancelledToken_Throws()
    {
        var db = CreateDbContext();
        var service = CreateBottleService(db, Guid.NewGuid());
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => service.AddBottleAsync(new AddBottleRequest { Name = "Valid" }, cts.Token));
    }

    #endregion

    #region UpdateBottleAsync

    [Fact]
    public async Task UpdateBottleAsync_WhenNameEmpty_ReturnsFail()
    {
        var db = CreateDbContext();
        var service = CreateBottleService(db, Guid.NewGuid());
        var request = new UpdateBottleRequest { Name = "" };

        var result = await service.UpdateBottleAsync(Guid.NewGuid(), request, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Name is required.", result.Error);
    }

    [Fact]
    public async Task UpdateBottleAsync_WhenNotFound_ReturnsNotFound()
    {
        var db = CreateDbContext();
        var service = CreateBottleService(db, Guid.NewGuid());
        var request = new UpdateBottleRequest { Name = "Valid Name" };

        var result = await service.UpdateBottleAsync(Guid.NewGuid(), request, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Bottle not found.", result.Error);
    }

    [Fact]
    public async Task UpdateBottleAsync_WhenForbidden_ReturnsForbidden()
    {
        var db = CreateDbContext();
        var owner = SeedUser(db);
        var bottle = SeedBottle(db, owner.Id);
        var service = CreateBottleService(db, Guid.NewGuid());
        var request = new UpdateBottleRequest { Name = "New Name" };

        var result = await service.UpdateBottleAsync(bottle.Id, request, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Forbidden.", result.Error);
    }

    [Fact]
    public async Task UpdateBottleAsync_WhenDistilleryIdNotFound_ReturnsNotFound()
    {
        var db = CreateDbContext();
        var user = SeedUser(db);
        var bottle = SeedBottle(db, user.Id, "Old Name");
        var service = CreateBottleService(db, user.Id);
        var request = new UpdateBottleRequest { Name = "New Name", DistilleryId = Guid.NewGuid() };

        var result = await service.UpdateBottleAsync(bottle.Id, request, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Distillery not found.", result.Error);
    }

    [Fact]
    public async Task UpdateBottleAsync_WhenValid_ReturnsUpdatedBottle()
    {
        var db = CreateDbContext();
        var user = SeedUser(db);
        var bottle = SeedBottle(db, user.Id, "Old Name");
        var service = CreateBottleService(db, user.Id);
        var request = new UpdateBottleRequest
        {
            Name = "New Name",
            Category = SpiritCategory.Rum,
            Condition = BottleCondition.Opened
        };

        var result = await service.UpdateBottleAsync(bottle.Id, request, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("New Name", result.Data!.Name);
        Assert.Equal(SpiritCategory.Rum, result.Data.Category);
        Assert.Equal(BottleCondition.Opened, result.Data.Condition);
    }

    [Fact]
    public async Task UpdateBottleAsync_WithCancelledToken_Throws()
    {
        var db = CreateDbContext();
        var service = CreateBottleService(db, Guid.NewGuid());
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => service.UpdateBottleAsync(Guid.NewGuid(), new UpdateBottleRequest { Name = "Valid" }, cts.Token));
    }

    #endregion

    #region RemoveBottleAsync

    [Fact]
    public async Task RemoveBottleAsync_WhenNotFound_ReturnsNotFound()
    {
        var db = CreateDbContext();
        var service = CreateBottleService(db, Guid.NewGuid());

        var result = await service.RemoveBottleAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Bottle not found.", result.Error);
    }

    [Fact]
    public async Task RemoveBottleAsync_WhenForbidden_ReturnsForbidden()
    {
        var db = CreateDbContext();
        var owner = SeedUser(db);
        var bottle = SeedBottle(db, owner.Id);
        var service = CreateBottleService(db, Guid.NewGuid());

        var result = await service.RemoveBottleAsync(bottle.Id, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Forbidden.", result.Error);
    }

    [Fact]
    public async Task RemoveBottleAsync_WhenValid_SoftDeletesBottle()
    {
        var db = CreateDbContext();
        var user = SeedUser(db);
        var bottle = SeedBottle(db, user.Id);
        var service = CreateBottleService(db, user.Id);

        var result = await service.RemoveBottleAsync(bottle.Id, CancellationToken.None);

        Assert.True(result.Success);
        var dbBottle = await db.Bottles.FindAsync(bottle.Id);
        Assert.True(dbBottle!.IsDeleted);
        Assert.NotNull(dbBottle.DeletedAt);
    }

    [Fact]
    public async Task RemoveBottleAsync_WithCancelledToken_Throws()
    {
        var db = CreateDbContext();
        var service = CreateBottleService(db, Guid.NewGuid());
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => service.RemoveBottleAsync(Guid.NewGuid(), cts.Token));
    }

    #endregion

    #region ListForSaleAsync

    [Fact]
    public async Task ListForSaleAsync_WhenNotFound_ReturnsNotFound()
    {
        var db = CreateDbContext();
        var service = CreateBottleService(db, Guid.NewGuid());
        var request = new ListForSaleRequest { AskingPrice = 100m, Currency = "USD" };

        var result = await service.ListForSaleAsync(Guid.NewGuid(), request, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Bottle not found.", result.Error);
    }

    [Fact]
    public async Task ListForSaleAsync_WhenForbidden_ReturnsForbidden()
    {
        var db = CreateDbContext();
        var owner = SeedUser(db);
        var bottle = SeedBottle(db, owner.Id);
        var service = CreateBottleService(db, Guid.NewGuid());
        var request = new ListForSaleRequest { AskingPrice = 100m, Currency = "USD" };

        var result = await service.ListForSaleAsync(bottle.Id, request, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Forbidden.", result.Error);
    }

    [Fact]
    public async Task ListForSaleAsync_WhenValid_SetsForSale()
    {
        var db = CreateDbContext();
        var user = SeedUser(db);
        var bottle = SeedBottle(db, user.Id);
        var service = CreateBottleService(db, user.Id);
        var request = new ListForSaleRequest { AskingPrice = 250m, Currency = "EUR" };

        var result = await service.ListForSaleAsync(bottle.Id, request, CancellationToken.None);

        Assert.True(result.Success);
        var dbBottle = await db.Bottles.FindAsync(bottle.Id);
        Assert.True(dbBottle!.IsForSale);
        Assert.Equal(250m, dbBottle.AskingPrice);
        Assert.Equal("EUR", dbBottle.Currency);
    }

    [Fact]
    public async Task ListForSaleAsync_WhenAlreadyForSale_ReturnsConflict()
    {
        var db = CreateDbContext();
        var user = SeedUser(db);
        var bottle = SeedBottle(db, user.Id, isForSale: true, askingPrice: 100m, currency: "USD");
        var notificationMock = new Mock<INotificationService>();
        var service = CreateBottleService(db, user.Id, notificationMock.Object);
        var request = new ListForSaleRequest { AskingPrice = 250m, Currency = "EUR" };

        var result = await service.ListForSaleAsync(bottle.Id, request, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ErrorCode.Conflict, result.ErrorCode);
        Assert.Equal("Bottle is already listed for sale.", result.Error);

        // No re-stamp and, crucially, no fan-out re-notification.
        var dbBottle = await db.Bottles.FindAsync(bottle.Id);
        Assert.Equal(100m, dbBottle!.AskingPrice);
        notificationMock.Verify(n => n.CreateBulkAsync(
            It.IsAny<IEnumerable<Guid>>(), It.IsAny<NotificationType>(), It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ListForSaleAsync_WithCancelledToken_Throws()
    {
        var db = CreateDbContext();
        var service = CreateBottleService(db, Guid.NewGuid());
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => service.ListForSaleAsync(Guid.NewGuid(), new ListForSaleRequest { AskingPrice = 1m, Currency = "USD" }, cts.Token));
    }

    #endregion

    #region UnlistFromSaleAsync

    [Fact]
    public async Task UnlistFromSaleAsync_WhenNotFound_ReturnsNotFound()
    {
        var db = CreateDbContext();
        var service = CreateBottleService(db, Guid.NewGuid());

        var result = await service.UnlistFromSaleAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Bottle not found.", result.Error);
    }

    [Fact]
    public async Task UnlistFromSaleAsync_WhenForbidden_ReturnsForbidden()
    {
        var db = CreateDbContext();
        var owner = SeedUser(db);
        var bottle = SeedBottle(db, owner.Id, isForSale: true);
        var service = CreateBottleService(db, Guid.NewGuid());

        var result = await service.UnlistFromSaleAsync(bottle.Id, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Forbidden.", result.Error);
    }

    [Fact]
    public async Task UnlistFromSaleAsync_WhenNotForSale_ReturnsFail()
    {
        var db = CreateDbContext();
        var user = SeedUser(db);
        var bottle = SeedBottle(db, user.Id, isForSale: false);
        var service = CreateBottleService(db, user.Id);

        var result = await service.UnlistFromSaleAsync(bottle.Id, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Bottle is not listed for sale.", result.Error);
    }

    [Fact]
    public async Task UnlistFromSaleAsync_WhenValid_RemovesListing()
    {
        var db = CreateDbContext();
        var user = SeedUser(db);
        var bottle = SeedBottle(db, user.Id, isForSale: true, askingPrice: 100m, currency: "USD");
        var service = CreateBottleService(db, user.Id);

        var result = await service.UnlistFromSaleAsync(bottle.Id, CancellationToken.None);

        Assert.True(result.Success);
        var dbBottle = await db.Bottles.FindAsync(bottle.Id);
        Assert.False(dbBottle!.IsForSale);
        Assert.Null(dbBottle.AskingPrice);
        Assert.Null(dbBottle.Currency);
    }

    [Fact]
    public async Task UnlistFromSaleAsync_WithCancelledToken_Throws()
    {
        var db = CreateDbContext();
        var service = CreateBottleService(db, Guid.NewGuid());
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => service.UnlistFromSaleAsync(Guid.NewGuid(), cts.Token));
    }

    #endregion

    #region Follower notifications

    [Fact]
    public async Task AddBottleAsync_WhenUserHasFollowers_NotifiesFollowers()
    {
        var db = CreateDbContext();
        var user = SeedUser(db);
        var follower1 = SeedUser(db, "Follower1");
        var follower2 = SeedUser(db, "Follower2");
        db.UserFollows.AddRange(
            new UserFollow { FollowerId = follower1.Id, FollowedId = user.Id, FollowedAt = DateTime.UtcNow },
            new UserFollow { FollowerId = follower2.Id, FollowedId = user.Id, FollowedAt = DateTime.UtcNow });
        db.SaveChanges();

        var notificationMock = new Mock<INotificationService>();
        var service = CreateBottleService(db, user.Id, notificationMock.Object);

        await service.AddBottleAsync(new AddBottleRequest { Name = "Lagavulin 16", Category = SpiritCategory.Whisky, Condition = BottleCondition.Sealed }, CancellationToken.None);

        notificationMock.Verify(n => n.CreateBulkAsync(
            It.Is<IEnumerable<Guid>>(ids => ids.Contains(follower1.Id) && ids.Contains(follower2.Id)),
            NotificationType.NewBottleFromFollowing,
            It.IsAny<Guid?>(),
            "Lagavulin 16",
            CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task ListForSaleAsync_WhenUserHasFollowers_NotifiesFollowers()
    {
        var db = CreateDbContext();
        var user = SeedUser(db);
        var follower = SeedUser(db, "Follower");
        db.UserFollows.Add(new UserFollow { FollowerId = follower.Id, FollowedId = user.Id, FollowedAt = DateTime.UtcNow });
        var bottle = SeedBottle(db, user.Id, "Glenfarclas 25");

        var notificationMock = new Mock<INotificationService>();
        var service = CreateBottleService(db, user.Id, notificationMock.Object);

        await service.ListForSaleAsync(bottle.Id, new ListForSaleRequest { AskingPrice = 500m, Currency = "USD" }, CancellationToken.None);

        notificationMock.Verify(n => n.CreateBulkAsync(
            It.Is<IEnumerable<Guid>>(ids => ids.Contains(follower.Id)),
            NotificationType.BottleListedForSale,
            bottle.Id,
            "Glenfarclas 25",
            CancellationToken.None), Times.Once);
    }

    #endregion

    #region WishList notifications

    [Fact]
    public async Task ListForSaleAsync_WhenMatchingWishListItem_NotifiesWishListOwner()
    {
        var db = CreateDbContext();
        var seller = SeedUser(db, "Seller");
        var wisher = SeedUser(db, "Wisher");
        var lagavulin = SeedDistillery(db, "Lagavulin");
        var bottle = SeedBottle(db, seller.Id, "Lagavulin 16", distilleryId: lagavulin.Id);
        db.WishListItems.Add(new WishListItem
        {
            UserId = wisher.Id,
            DistilleryId = lagavulin.Id,
            Category = SpiritCategory.Whisky
        });
        db.SaveChanges();

        var notificationMock = new Mock<INotificationService>();
        var service = CreateBottleService(db, seller.Id, notificationMock.Object);

        await service.ListForSaleAsync(bottle.Id, new ListForSaleRequest { AskingPrice = 500m, Currency = "USD" }, CancellationToken.None);

        notificationMock.Verify(n => n.CreateBulkAsync(
            It.Is<IEnumerable<Guid>>(ids => ids.Contains(wisher.Id)),
            NotificationType.WishListMatch,
            bottle.Id,
            "Lagavulin 16",
            CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task ListForSaleAsync_WhenNoMatchingWishList_DoesNotNotifyForWishList()
    {
        var db = CreateDbContext();
        var seller = SeedUser(db, "Seller");
        var wisher = SeedUser(db, "Wisher");
        var lagavulin = SeedDistillery(db, "Lagavulin");
        var macallan = SeedDistillery(db, "Macallan");
        var bottle = SeedBottle(db, seller.Id, "Lagavulin 16", distilleryId: lagavulin.Id);
        db.WishListItems.Add(new WishListItem
        {
            UserId = wisher.Id,
            DistilleryId = macallan.Id,
            Category = SpiritCategory.Whisky
        });
        db.SaveChanges();

        var notificationMock = new Mock<INotificationService>();
        var service = CreateBottleService(db, seller.Id, notificationMock.Object);

        await service.ListForSaleAsync(bottle.Id, new ListForSaleRequest { AskingPrice = 500m, Currency = "USD" }, CancellationToken.None);

        notificationMock.Verify(n => n.CreateBulkAsync(
            It.Is<IEnumerable<Guid>>(ids => !ids.Any()),
            NotificationType.WishListMatch,
            bottle.Id,
            "Lagavulin 16",
            CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task ListForSaleAsync_WhenWishListMatchesByCategoryOnly_Notifies()
    {
        var db = CreateDbContext();
        var seller = SeedUser(db, "Seller");
        var wisher = SeedUser(db, "Wisher");
        var bottle = SeedBottle(db, seller.Id, "Lagavulin 16", distilleryId: null);
        db.WishListItems.Add(new WishListItem
        {
            UserId = wisher.Id,
            DistilleryId = null,
            Category = SpiritCategory.Whisky
        });
        db.SaveChanges();

        var notificationMock = new Mock<INotificationService>();
        var service = CreateBottleService(db, seller.Id, notificationMock.Object);

        await service.ListForSaleAsync(bottle.Id, new ListForSaleRequest { AskingPrice = 500m, Currency = "USD" }, CancellationToken.None);

        notificationMock.Verify(n => n.CreateBulkAsync(
            It.Is<IEnumerable<Guid>>(ids => ids.Contains(wisher.Id)),
            NotificationType.WishListMatch,
            It.IsAny<Guid?>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ListForSaleAsync_WhenWishListMatchesByDistilleryOnly_Notifies()
    {
        var db = CreateDbContext();
        var seller = SeedUser(db, "Seller");
        var wisher = SeedUser(db, "Wisher");
        var lagavulin = SeedDistillery(db, "Lagavulin");
        var bottle = SeedBottle(db, seller.Id, "Lagavulin 16", distilleryId: lagavulin.Id);
        db.WishListItems.Add(new WishListItem
        {
            UserId = wisher.Id,
            DistilleryId = lagavulin.Id,
            Category = null
        });
        db.SaveChanges();

        var notificationMock = new Mock<INotificationService>();
        var service = CreateBottleService(db, seller.Id, notificationMock.Object);

        await service.ListForSaleAsync(bottle.Id, new ListForSaleRequest { AskingPrice = 500m, Currency = "USD" }, CancellationToken.None);

        notificationMock.Verify(n => n.CreateBulkAsync(
            It.Is<IEnumerable<Guid>>(ids => ids.Contains(wisher.Id)),
            NotificationType.WishListMatch,
            It.IsAny<Guid?>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ListForSaleAsync_WhenBottleDistilleryIsNullAndWishItemSpecifiesDistillery_DoesNotNotify()
    {
        var db = CreateDbContext();
        var seller = SeedUser(db, "Seller");
        var wisher = SeedUser(db, "Wisher");
        var lagavulin = SeedDistillery(db, "Lagavulin");
        var bottle = SeedBottle(db, seller.Id, "Lagavulin 16", distilleryId: null);
        db.WishListItems.Add(new WishListItem
        {
            UserId = wisher.Id,
            DistilleryId = lagavulin.Id,
            Category = SpiritCategory.Whisky
        });
        db.SaveChanges();

        var notificationMock = new Mock<INotificationService>();
        var service = CreateBottleService(db, seller.Id, notificationMock.Object);

        await service.ListForSaleAsync(bottle.Id, new ListForSaleRequest { AskingPrice = 500m, Currency = "USD" }, CancellationToken.None);

        notificationMock.Verify(n => n.CreateBulkAsync(
            It.Is<IEnumerable<Guid>>(ids => ids.Contains(wisher.Id)),
            NotificationType.WishListMatch,
            It.IsAny<Guid?>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ListForSaleAsync_WhenSellerHasMatchingWishListItem_DoesNotNotifySelf()
    {
        var db = CreateDbContext();
        var seller = SeedUser(db, "Seller");
        var lagavulin = SeedDistillery(db, "Lagavulin");
        var bottle = SeedBottle(db, seller.Id, "Lagavulin 16", distilleryId: lagavulin.Id);
        db.WishListItems.Add(new WishListItem
        {
            UserId = seller.Id,
            DistilleryId = lagavulin.Id,
            Category = SpiritCategory.Whisky
        });
        db.SaveChanges();

        var notificationMock = new Mock<INotificationService>();
        var service = CreateBottleService(db, seller.Id, notificationMock.Object);

        await service.ListForSaleAsync(bottle.Id, new ListForSaleRequest { AskingPrice = 500m, Currency = "USD" }, CancellationToken.None);

        notificationMock.Verify(n => n.CreateBulkAsync(
            It.Is<IEnumerable<Guid>>(ids => ids.Contains(seller.Id)),
            NotificationType.WishListMatch,
            It.IsAny<Guid?>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ListForSaleAsync_WhenSameUserHasTwoMatchingWishItems_NotifiesOnce()
    {
        var db = CreateDbContext();
        var seller = SeedUser(db, "Seller");
        var wisher = SeedUser(db, "Wisher");
        var lagavulin = SeedDistillery(db, "Lagavulin");
        var bottle = SeedBottle(db, seller.Id, "Lagavulin 16", distilleryId: lagavulin.Id);
        db.WishListItems.AddRange(
            new WishListItem
            {
                UserId = wisher.Id,
                DistilleryId = lagavulin.Id,
                Category = SpiritCategory.Whisky
            },
            new WishListItem
            {
                UserId = wisher.Id,
                DistilleryId = null,
                Category = SpiritCategory.Whisky
            });
        db.SaveChanges();

        var notificationMock = new Mock<INotificationService>();
        var service = CreateBottleService(db, seller.Id, notificationMock.Object);

        await service.ListForSaleAsync(bottle.Id, new ListForSaleRequest { AskingPrice = 500m, Currency = "USD" }, CancellationToken.None);

        notificationMock.Verify(n => n.CreateBulkAsync(
            It.Is<IEnumerable<Guid>>(ids => ids.Count(id => id == wisher.Id) == 1),
            NotificationType.WishListMatch,
            It.IsAny<Guid?>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region DistilleryName

    [Fact]
    public async Task GetBottlesByUserAsync_WhenBottleHasDistillery_ReturnsDistilleryName()
    {
        var db = CreateDbContext();
        var user = SeedUser(db);
        var distillery = SeedDistillery(db, "Macallan");
        SeedBottle(db, user.Id, "Macallan 18", distilleryId: distillery.Id);
        var service = CreateBottleService(db, user.Id);

        var result = await service.GetBottlesByUserAsync(user.Id, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Single(result.Data!);
        Assert.Equal(distillery.Name, result.Data![0].DistilleryName);
    }

    [Fact]
    public async Task GetBottlesByUserAsync_WhenBottleHasNoDistillery_ReturnsNullDistilleryName()
    {
        var db = CreateDbContext();
        var user = SeedUser(db);
        SeedBottle(db, user.Id, "No Distillery", distilleryId: null);
        var service = CreateBottleService(db, user.Id);

        var result = await service.GetBottlesByUserAsync(user.Id, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Single(result.Data!);
        Assert.Null(result.Data![0].DistilleryName);
    }

    #endregion

    #region Marketplace search

    [Fact]
    public async Task GetMarketplaceAsync_WhenSearchMatchesDistilleryName_ReturnsBottle()
    {
        var db = CreateDbContext();
        var user = SeedUser(db);
        var distillery = SeedDistillery(db, "Macallan");
        var bottle = SeedBottle(db, user.Id, "Sherry Oak", isForSale: true, distilleryId: distillery.Id);
        var service = CreateBottleService(db, user.Id);

        var result = await service.GetMarketplaceAsync(new MarketplaceQuery { Search = "Macallan" }, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Single(result.Data!);
        Assert.Equal(bottle.Id, result.Data![0].Id);
    }

    [Fact]
    public async Task GetMarketplaceAsync_WhenSearchDoesNotMatchDistilleryName_ReturnsEmpty()
    {
        var db = CreateDbContext();
        var user = SeedUser(db);
        var distillery = SeedDistillery(db, "Macallan");
        SeedBottle(db, user.Id, "Sherry Oak", isForSale: true, distilleryId: distillery.Id);
        var service = CreateBottleService(db, user.Id);

        var result = await service.GetMarketplaceAsync(new MarketplaceQuery { Search = "Glenfiddich" }, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Empty(result.Data!);
    }

    #endregion
}
