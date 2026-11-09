using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using VirtualBar.Application.Common;
using VirtualBar.Application.DTOs.Offers;
using VirtualBar.Application.Interfaces;
using VirtualBar.Domain.Entities;
using VirtualBar.Domain.Enums;
using VirtualBar.Infrastructure.Decorators;
using VirtualBar.Infrastructure.Persistence;
using VirtualBar.Infrastructure.Services;

namespace VirtualBar.Tests.Services;

public sealed class OfferServiceTests
{
    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    // SQLite in-memory is required for the ExecuteUpdateAsync paths (Accept/Decline/Withdraw) and for the
    // filtered unique index semantics — InMemory neither supports ExecuteUpdate nor honors index filters.
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

    private static ICurrentUser CreateCurrentUser(Guid userId)
    {
        var mock = new Mock<ICurrentUser>();
        mock.Setup(u => u.UserId).Returns(userId);
        mock.Setup(u => u.IsAuthenticated).Returns(true);
        return mock.Object;
    }

    private static OfferService CreateInnerOfferService(
        AppDbContext db,
        Guid currentUserId,
        INotificationService? notificationService = null,
        IBadgeService? badgeService = null,
        IWebHostEnvironment? env = null,
        ILogger<OfferService>? logger = null) =>
        new(
            db,
            CreateCurrentUser(currentUserId),
            notificationService ?? Mock.Of<INotificationService>(),
            badgeService ?? Mock.Of<IBadgeService>(),
            env ?? Mock.Of<IWebHostEnvironment>(),
            logger ?? NullLogger<OfferService>.Instance);

    private static IOfferService CreateOfferService(
        AppDbContext db,
        Guid currentUserId,
        INotificationService? notificationService = null,
        IBadgeService? badgeService = null,
        IWebHostEnvironment? env = null,
        ILogger<OfferService>? logger = null)
    {
        var inner = CreateInnerOfferService(db, currentUserId, notificationService, badgeService, env, logger);
        return new OfferValidationDecorator(inner, db, CreateCurrentUser(currentUserId));
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
        bool isDeleted = false,
        int quantity = 1,
        int reservedQuantity = 0,
        int forSaleQuantity = 0)
    {
        var bottle = new Bottle
        {
            UserId = userId,
            Name = name,
            Category = SpiritCategory.Whisky,
            Condition = BottleCondition.Sealed,
            IsDeleted = isDeleted,
            DeletedAt = isDeleted ? DateTime.UtcNow : null,
            Quantity = quantity,
            ReservedQuantity = reservedQuantity,
            IsForSale = forSaleQuantity > 0,
            ForSaleQuantity = forSaleQuantity,
            AskingPrice = forSaleQuantity > 0 ? 150m : null,
            Currency = forSaleQuantity > 0 ? "EUR" : null,
            ForSaleAt = forSaleQuantity > 0 ? DateTime.UtcNow : null
        };
        db.Bottles.Add(bottle);
        db.SaveChanges();
        return bottle;
    }

    private static Offer SeedOffer(
        AppDbContext db,
        Guid bottleId,
        Guid buyerId,
        Guid sellerId,
        OfferStatus status = OfferStatus.Pending,
        decimal offeredPrice = 100m,
        string currency = "USD",
        string? message = null,
        bool isDeleted = false,
        DateTime? createdAt = null)
    {
        var offer = new Offer
        {
            BottleId = bottleId,
            BuyerId = buyerId,
            SellerId = sellerId,
            OfferedPrice = offeredPrice,
            Currency = currency,
            Message = message,
            Status = status,
            IsDeleted = isDeleted,
            DeletedAt = isDeleted ? DateTime.UtcNow : null,
            CreatedAt = createdAt ?? DateTime.UtcNow
        };
        db.Offers.Add(offer);
        db.SaveChanges();
        return offer;
    }

    #region CreateAsync

    [Fact]
    public async Task CreateAsync_WhenPriceNotPositive_ReturnsFail()
    {
        var db = CreateDbContext();
        var buyer = SeedUser(db, "Buyer");
        var service = CreateOfferService(db, buyer.Id);
        var request = new CreateOfferRequest { BottleId = Guid.NewGuid(), OfferedPrice = 0m, Currency = "USD" };

        var result = await service.CreateAsync(request, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Offered price must be greater than zero.", result.Error);
    }

    [Fact]
    public async Task CreateAsync_WhenCurrencyEmpty_ReturnsFail()
    {
        var db = CreateDbContext();
        var buyer = SeedUser(db, "Buyer");
        var service = CreateOfferService(db, buyer.Id);
        var request = new CreateOfferRequest { BottleId = Guid.NewGuid(), OfferedPrice = 50m, Currency = "  " };

        var result = await service.CreateAsync(request, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Currency is required.", result.Error);
    }

    [Theory]
    [InlineData("BGN")]
    [InlineData("CHF")]
    [InlineData("AUD")]
    public async Task CreateAsync_WhenCurrencyNotSupported_ReturnsFail(string currency)
    {
        var db = CreateDbContext();
        var seller = SeedUser(db, "Seller");
        var buyer = SeedUser(db, "Buyer");
        var bottle = SeedBottle(db, seller.Id);
        var service = CreateOfferService(db, buyer.Id);
        var request = new CreateOfferRequest { BottleId = bottle.Id, OfferedPrice = 50m, Currency = currency };

        var result = await service.CreateAsync(request, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Currency must be EUR, GBP or USD.", result.Error);
        Assert.Empty(db.Offers);
    }

    [Fact]
    public async Task CreateAsync_WhenCurrencyInLowerCase_StoresTheUpperCaseCode()
    {
        var db = CreateDbContext();
        var seller = SeedUser(db, "Seller");
        var buyer = SeedUser(db, "Buyer");
        var bottle = SeedBottle(db, seller.Id);
        var service = CreateOfferService(db, buyer.Id);
        var request = new CreateOfferRequest { BottleId = bottle.Id, OfferedPrice = 50m, Currency = " gbp " };

        var result = await service.CreateAsync(request, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("GBP", result.Data!.Currency);
        Assert.Equal("GBP", db.Offers.Single().Currency);
    }

    [Fact]
    public async Task CreateAsync_WhenBottleNotFound_ReturnsNotFound()
    {
        var db = CreateDbContext();
        var buyer = SeedUser(db, "Buyer");
        var service = CreateOfferService(db, buyer.Id);
        var request = new CreateOfferRequest { BottleId = Guid.NewGuid(), OfferedPrice = 50m, Currency = "USD" };

        var result = await service.CreateAsync(request, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Bottle not found.", result.Error);
    }

    [Fact]
    public async Task CreateAsync_WhenBottleSoftDeleted_ReturnsNotFound()
    {
        var db = CreateDbContext();
        var seller = SeedUser(db, "Seller");
        var buyer = SeedUser(db, "Buyer");
        var bottle = SeedBottle(db, seller.Id, isDeleted: true);
        var service = CreateOfferService(db, buyer.Id);
        var request = new CreateOfferRequest { BottleId = bottle.Id, OfferedPrice = 50m, Currency = "USD" };

        var result = await service.CreateAsync(request, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Bottle not found.", result.Error);
    }

    [Fact]
    public async Task CreateAsync_WhenBuyerIsSeller_ReturnsFail()
    {
        var db = CreateDbContext();
        var owner = SeedUser(db, "Owner");
        var bottle = SeedBottle(db, owner.Id);
        var service = CreateOfferService(db, owner.Id);
        var request = new CreateOfferRequest { BottleId = bottle.Id, OfferedPrice = 50m, Currency = "USD" };

        var result = await service.CreateAsync(request, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("You cannot make an offer on your own bottle.", result.Error);
    }

    [Fact]
    public async Task CreateAsync_WhenExistingPendingOffer_ReturnsConflict()
    {
        var db = CreateDbContext();
        var seller = SeedUser(db, "Seller");
        var buyer = SeedUser(db, "Buyer");
        var bottle = SeedBottle(db, seller.Id);
        SeedOffer(db, bottle.Id, buyer.Id, seller.Id, OfferStatus.Pending);
        var service = CreateOfferService(db, buyer.Id);
        var request = new CreateOfferRequest { BottleId = bottle.Id, OfferedPrice = 75m, Currency = "USD" };

        var result = await service.CreateAsync(request, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("You already have a pending offer on this bottle.", result.Error);
    }

    [Fact]
    public async Task CreateAsync_WhenPreviousOfferNotPending_AllowsNewOffer()
    {
        // SQLite: the filtered unique index must permit a fresh pending offer after a declined one.
        var db = CreateSqliteDbContext();
        var seller = SeedUser(db, "Seller");
        var buyer = SeedUser(db, "Buyer");
        var bottle = SeedBottle(db, seller.Id);
        SeedOffer(db, bottle.Id, buyer.Id, seller.Id, OfferStatus.Declined);
        var service = CreateOfferService(db, buyer.Id);
        var request = new CreateOfferRequest { BottleId = bottle.Id, OfferedPrice = 75m, Currency = "USD" };

        var result = await service.CreateAsync(request, CancellationToken.None);

        Assert.True(result.Success);
    }

    [Fact]
    public async Task CreateAsync_WhenDuplicatePendingSlipsPastValidation_ReturnsConflict()
    {
        // Simulates losing the create race: the inner service is called directly (as if a concurrent
        // request already passed the decorator's pre-check) and the unique index rejects the insert.
        var db = CreateSqliteDbContext();
        var seller = SeedUser(db, "Seller");
        var buyer = SeedUser(db, "Buyer");
        var bottle = SeedBottle(db, seller.Id);
        SeedOffer(db, bottle.Id, buyer.Id, seller.Id, OfferStatus.Pending);
        var notificationMock = new Mock<INotificationService>();
        var inner = CreateInnerOfferService(db, buyer.Id, notificationMock.Object);
        var request = new CreateOfferRequest { BottleId = bottle.Id, OfferedPrice = 75m, Currency = "USD" };

        var result = await inner.CreateAsync(request, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("You already have a pending offer on this bottle.", result.Error);

        db.ChangeTracker.Clear();
        Assert.Equal(1, await db.Offers.CountAsync());
        notificationMock.Verify(n => n.CreateAsync(
            It.IsAny<Guid>(), It.IsAny<NotificationType>(), It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_WhenValid_SavesOfferAndNotifiesSeller()
    {
        var db = CreateDbContext();
        var seller = SeedUser(db, "Seller");
        var buyer = SeedUser(db, "Buyer");
        var bottle = SeedBottle(db, seller.Id, name: "Macallan 18");
        var notificationMock = new Mock<INotificationService>();
        var service = CreateOfferService(db, buyer.Id, notificationMock.Object);
        var request = new CreateOfferRequest
        {
            BottleId = bottle.Id,
            OfferedPrice = 250m,
            Currency = "EUR",
            Message = "Interested!"
        };

        var result = await service.CreateAsync(request, CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(bottle.Id, result.Data.BottleId);
        Assert.Equal("Macallan 18", result.Data.BottleName);
        Assert.Equal(buyer.Id, result.Data.BuyerId);
        Assert.Equal("Buyer", result.Data.BuyerDisplayName);
        Assert.Equal(seller.Id, result.Data.SellerId);
        Assert.Equal("Seller", result.Data.SellerDisplayName);
        Assert.Equal(250m, result.Data.OfferedPrice);
        Assert.Equal("EUR", result.Data.Currency);
        Assert.Equal("Interested!", result.Data.Message);
        Assert.Equal(OfferStatus.Pending, result.Data.Status);

        var stored = await db.Offers.SingleAsync();
        Assert.Equal(buyer.Id, stored.BuyerId);
        Assert.Equal(seller.Id, stored.SellerId);

        notificationMock.Verify(n => n.CreateAsync(
            seller.Id, NotificationType.OfferReceived, stored.Id, "Macallan 18", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_WhenBuyerMissing_UsesEmptyBuyerDisplayName()
    {
        var db = CreateDbContext();
        var seller = SeedUser(db, "Seller");
        var bottle = SeedBottle(db, seller.Id);
        var orphanBuyerId = Guid.NewGuid();
        var service = CreateOfferService(db, orphanBuyerId);
        var request = new CreateOfferRequest { BottleId = bottle.Id, OfferedPrice = 60m, Currency = "USD" };

        var result = await service.CreateAsync(request, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("", result.Data!.BuyerDisplayName);
    }

    [Fact]
    public async Task CreateAsync_WithCancelledToken_Throws()
    {
        var db = CreateDbContext();
        var service = CreateOfferService(db, Guid.NewGuid());
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => service.CreateAsync(new CreateOfferRequest { OfferedPrice = 10m, Currency = "USD" }, cts.Token));
    }


    [Fact]
    public async Task CreateAsync_WhenEveryBottleIsReserved_ReturnsConflict()
    {
        var db = CreateDbContext();
        var seller = SeedUser(db, "Seller");
        var buyer = SeedUser(db, "Buyer");
        var bottle = SeedBottle(db, seller.Id, quantity: 1, reservedQuantity: 1);
        var service = CreateOfferService(db, buyer.Id);

        var result = await service.CreateAsync(
            new CreateOfferRequest { BottleId = bottle.Id, OfferedPrice = 100m, Currency = "EUR" }, CancellationToken.None);

        Assert.Equal(ErrorCode.Conflict, result.ErrorCode);
        Assert.Equal(OfferService.NoBottleLeftMessage, result.Error);
        Assert.Equal(OfferErrorReasons.NoBottleLeft, result.Reason);
    }

    #endregion

    #region GetReceivedAsync

    [Fact]
    public async Task GetReceivedAsync_WhenNoOffers_ReturnsEmptyList()
    {
        var db = CreateDbContext();
        var seller = SeedUser(db, "Seller");
        var service = CreateOfferService(db, seller.Id);

        var result = await service.GetReceivedAsync(CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Empty(result.Data);
    }

    [Fact]
    public async Task GetReceivedAsync_WhenHasOffers_ReturnsOnlyOffersWhereCurrentUserIsSeller()
    {
        var db = CreateDbContext();
        var seller = SeedUser(db, "Seller");
        var buyer = SeedUser(db, "Buyer");
        var bottleOwned = SeedBottle(db, seller.Id, "Owned");
        var bottleOther = SeedBottle(db, buyer.Id, "Other");
        var received = SeedOffer(db, bottleOwned.Id, buyer.Id, seller.Id);
        SeedOffer(db, bottleOther.Id, seller.Id, buyer.Id);
        var service = CreateOfferService(db, seller.Id);

        var result = await service.GetReceivedAsync(CancellationToken.None);

        Assert.True(result.Success);
        Assert.Single(result.Data!);
        Assert.Equal(received.Id, result.Data![0].Id);
        Assert.Equal("Owned", result.Data![0].BottleName);
        Assert.Equal("Buyer", result.Data![0].BuyerDisplayName);
        Assert.Equal("Seller", result.Data![0].SellerDisplayName);
    }

    [Fact]
    public async Task GetReceivedAsync_WhenOfferSoftDeleted_ExcludesDeleted()
    {
        var db = CreateDbContext();
        var seller = SeedUser(db, "Seller");
        var buyer = SeedUser(db, "Buyer");
        var bottle = SeedBottle(db, seller.Id);
        SeedOffer(db, bottle.Id, buyer.Id, seller.Id, isDeleted: true);
        var service = CreateOfferService(db, seller.Id);

        var result = await service.GetReceivedAsync(CancellationToken.None);

        Assert.True(result.Success);
        Assert.Empty(result.Data!);
    }

    [Fact]
    public async Task GetReceivedAsync_WhenMultipleOffers_OrderedByCreatedAtDescending()
    {
        var db = CreateDbContext();
        var seller = SeedUser(db, "Seller");
        var buyer = SeedUser(db, "Buyer");
        var secondBuyer = SeedUser(db, "Second Buyer");
        var bottle = SeedBottle(db, seller.Id);
        SeedOffer(db, bottle.Id, buyer.Id, seller.Id, offeredPrice: 10m,
            createdAt: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        SeedOffer(db, bottle.Id, secondBuyer.Id, seller.Id, status: OfferStatus.Declined, offeredPrice: 20m,
            createdAt: new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc));
        var service = CreateOfferService(db, seller.Id);

        var result = await service.GetReceivedAsync(CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(2, result.Data!.Count);
        Assert.Equal(20m, result.Data![0].OfferedPrice);
        Assert.Equal(10m, result.Data![1].OfferedPrice);
    }

    [Fact]
    public async Task GetReceivedAsync_WithCancelledToken_Throws()
    {
        var db = CreateDbContext();
        var service = CreateOfferService(db, Guid.NewGuid());
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => service.GetReceivedAsync(cts.Token));
    }

    #endregion

    #region GetSentAsync

    [Fact]
    public async Task GetSentAsync_WhenNoOffers_ReturnsEmptyList()
    {
        var db = CreateDbContext();
        var buyer = SeedUser(db, "Buyer");
        var service = CreateOfferService(db, buyer.Id);

        var result = await service.GetSentAsync(CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Empty(result.Data);
    }

    [Fact]
    public async Task GetSentAsync_WhenHasOffers_ReturnsOnlyOffersWhereCurrentUserIsBuyer()
    {
        var db = CreateDbContext();
        var buyer = SeedUser(db, "Buyer");
        var seller = SeedUser(db, "Seller");
        var bottleSeller = SeedBottle(db, seller.Id, "Seller bottle");
        var bottleBuyer = SeedBottle(db, buyer.Id, "Buyer bottle");
        var sent = SeedOffer(db, bottleSeller.Id, buyer.Id, seller.Id);
        SeedOffer(db, bottleBuyer.Id, seller.Id, buyer.Id);
        var service = CreateOfferService(db, buyer.Id);

        var result = await service.GetSentAsync(CancellationToken.None);

        Assert.True(result.Success);
        Assert.Single(result.Data!);
        Assert.Equal(sent.Id, result.Data![0].Id);
        Assert.Equal("Seller bottle", result.Data![0].BottleName);
    }

    [Fact]
    public async Task GetSentAsync_WhenOfferSoftDeleted_ExcludesDeleted()
    {
        var db = CreateDbContext();
        var buyer = SeedUser(db, "Buyer");
        var seller = SeedUser(db, "Seller");
        var bottle = SeedBottle(db, seller.Id);
        SeedOffer(db, bottle.Id, buyer.Id, seller.Id, isDeleted: true);
        var service = CreateOfferService(db, buyer.Id);

        var result = await service.GetSentAsync(CancellationToken.None);

        Assert.True(result.Success);
        Assert.Empty(result.Data!);
    }

    [Fact]
    public async Task GetSentAsync_WhenMultipleOffers_OrderedByCreatedAtDescending()
    {
        var db = CreateDbContext();
        var buyer = SeedUser(db, "Buyer");
        var seller = SeedUser(db, "Seller");
        var bottle = SeedBottle(db, seller.Id);
        var secondBottle = SeedBottle(db, seller.Id, "Second Bottle");
        SeedOffer(db, bottle.Id, buyer.Id, seller.Id, offeredPrice: 10m,
            createdAt: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        SeedOffer(db, secondBottle.Id, buyer.Id, seller.Id, status: OfferStatus.Declined, offeredPrice: 20m,
            createdAt: new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc));
        var service = CreateOfferService(db, buyer.Id);

        var result = await service.GetSentAsync(CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(20m, result.Data![0].OfferedPrice);
        Assert.Equal(10m, result.Data![1].OfferedPrice);
    }

    [Fact]
    public async Task GetSentAsync_WithCancelledToken_Throws()
    {
        var db = CreateDbContext();
        var service = CreateOfferService(db, Guid.NewGuid());
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => service.GetSentAsync(cts.Token));
    }

    #endregion

    #region AcceptAsync

    [Fact]
    public async Task AcceptAsync_WhenNotFound_ReturnsNotFound()
    {
        var db = CreateDbContext();
        var seller = SeedUser(db, "Seller");
        var service = CreateOfferService(db, seller.Id);

        var result = await service.AcceptAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Offer not found.", result.Error);
    }

    [Fact]
    public async Task AcceptAsync_WhenNotSeller_ReturnsForbidden()
    {
        var db = CreateDbContext();
        var seller = SeedUser(db, "Seller");
        var buyer = SeedUser(db, "Buyer");
        var bottle = SeedBottle(db, seller.Id);
        var offer = SeedOffer(db, bottle.Id, buyer.Id, seller.Id);
        var service = CreateOfferService(db, buyer.Id);

        var result = await service.AcceptAsync(offer.Id, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Only the seller can accept this offer.", result.Error);
    }

    [Fact]
    public async Task AcceptAsync_WhenNotPending_ReturnsConflict()
    {
        var db = CreateDbContext();
        var seller = SeedUser(db, "Seller");
        var buyer = SeedUser(db, "Buyer");
        var bottle = SeedBottle(db, seller.Id);
        var offer = SeedOffer(db, bottle.Id, buyer.Id, seller.Id, OfferStatus.Accepted);
        var service = CreateOfferService(db, seller.Id);

        var result = await service.AcceptAsync(offer.Id, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Only pending offers can be accepted.", result.Error);
    }

    /// <summary>Since #130 an accepted offer awaits the buyer's confirmation, with one bottle reserved for them.</summary>
    [Fact]
    public async Task AcceptAsync_WhenValid_AwaitsReceiptReservesABottleAndNotifiesBuyer()
    {
        var db = CreateSqliteDbContext();
        var seller = SeedUser(db, "Seller");
        var buyer = SeedUser(db, "Buyer");
        var bottle = SeedBottle(db, seller.Id, name: "Yamazaki 12");
        var offer = SeedOffer(db, bottle.Id, buyer.Id, seller.Id);
        var notificationMock = new Mock<INotificationService>();
        var service = CreateOfferService(db, seller.Id, notificationMock.Object);

        var result = await service.AcceptAsync(offer.Id, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(OfferStatus.AwaitingReceipt, result.Data!.Status);
        Assert.NotNull(result.Data.RespondedAt);
        Assert.Null(result.Data.CompletedAt);

        db.ChangeTracker.Clear(); // ExecuteUpdate bypasses the tracker — read the row fresh
        var stored = await db.Offers.FindAsync(offer.Id);
        Assert.Equal(OfferStatus.AwaitingReceipt, stored!.Status);
        Assert.NotNull(stored.RespondedAt);
        var storedBottle = await db.Bottles.FindAsync(bottle.Id);
        Assert.Equal(1, storedBottle!.Quantity);
        Assert.Equal(1, storedBottle.ReservedQuantity);
        Assert.False(storedBottle.IsDeleted);

        notificationMock.Verify(n => n.CreateAsync(
            buyer.Id, NotificationType.OfferAccepted, offer.Id, "Yamazaki 12", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AcceptAsync_WhenLosesRaceToResolvedOffer_ReturnsConflictWithoutNotification()
    {
        // Simulates losing the accept race: the inner service is called directly (as if the decorator's
        // pending check passed just before a concurrent request resolved the offer).
        var db = CreateSqliteDbContext();
        var seller = SeedUser(db, "Seller");
        var buyer = SeedUser(db, "Buyer");
        var bottle = SeedBottle(db, seller.Id);
        var offer = SeedOffer(db, bottle.Id, buyer.Id, seller.Id, OfferStatus.Accepted);
        var notificationMock = new Mock<INotificationService>();
        var inner = CreateInnerOfferService(db, seller.Id, notificationMock.Object);

        var result = await inner.AcceptAsync(offer.Id, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Only pending offers can be accepted.", result.Error);
        notificationMock.Verify(n => n.CreateAsync(
            It.IsAny<Guid>(), It.IsAny<NotificationType>(), It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AcceptAsync_WithCancelledToken_Throws()
    {
        var db = CreateDbContext();
        var service = CreateOfferService(db, Guid.NewGuid());
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => service.AcceptAsync(Guid.NewGuid(), cts.Token));
    }

    /// <summary>
    /// FirstSale / FirstPurchase count completed deals, so since #130 they are evaluated when the buyer confirms
    /// receipt (see the ConfirmReceiptAsync tests), never on accept.
    /// </summary>
    [Fact]
    public async Task AcceptAsync_WhenValid_EvaluatesNoBadges()
    {
        var db = CreateSqliteDbContext();
        var seller = SeedUser(db, "Seller");
        var buyer = SeedUser(db, "Buyer");
        var bottle = SeedBottle(db, seller.Id);
        var offer = SeedOffer(db, bottle.Id, buyer.Id, seller.Id);
        var badgeMock = new Mock<IBadgeService>();
        var service = CreateOfferService(db, seller.Id, badgeService: badgeMock.Object);

        var result = await service.AcceptAsync(offer.Id, CancellationToken.None);

        Assert.True(result.Success);
        badgeMock.Verify(b => b.EvaluateAsync(
            It.IsAny<Guid>(), It.IsAny<BadgeTrigger>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AcceptAsync_WhenLosesRaceToResolvedOffer_DoesNotEvaluateBadges()
    {
        // Loser slipped past the decorator's pending check — call the inner service directly against an
        // already-resolved offer. The conditional update claims zero rows, so no badge evaluation fires.
        var db = CreateSqliteDbContext();
        var seller = SeedUser(db, "Seller");
        var buyer = SeedUser(db, "Buyer");
        var bottle = SeedBottle(db, seller.Id);
        var offer = SeedOffer(db, bottle.Id, buyer.Id, seller.Id, OfferStatus.Accepted);
        var badgeMock = new Mock<IBadgeService>();
        var inner = CreateInnerOfferService(db, seller.Id, badgeService: badgeMock.Object);

        var result = await inner.AcceptAsync(offer.Id, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Only pending offers can be accepted.", result.Error);
        badgeMock.Verify(b => b.EvaluateAsync(
            It.IsAny<Guid>(), It.IsAny<BadgeTrigger>(), It.IsAny<CancellationToken>()), Times.Never);
    }


    [Fact]
    public async Task AcceptAsync_WhenEveryBottleIsReserved_ReturnsConflict()
    {
        var db = CreateDbContext();
        var seller = SeedUser(db, "Seller");
        var buyer = SeedUser(db, "Buyer");
        var bottle = SeedBottle(db, seller.Id, quantity: 1, reservedQuantity: 1);
        var offer = SeedOffer(db, bottle.Id, buyer.Id, seller.Id);
        var service = CreateOfferService(db, seller.Id);

        var result = await service.AcceptAsync(offer.Id, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ErrorCode.Conflict, result.ErrorCode);
        Assert.Equal(OfferService.NoBottleLeftMessage, result.Error);
        Assert.Equal(OfferErrorReasons.NoBottleLeft, result.Reason);
    }

    /// <summary>
    /// Two accepts racing for the last free bottle (#130): the loser passed the decorator's check before the winner
    /// reserved it, so it reaches the service, finds no free bottle and rolls its claim back.
    /// </summary>
    [Fact]
    public async Task AcceptAsync_WhenTheLastFreeBottleWasJustReserved_ReturnsConflictAndLeavesTheOfferPending()
    {
        var db = CreateSqliteDbContext();
        var seller = SeedUser(db, "Seller");
        var buyer = SeedUser(db, "Buyer");
        var bottle = SeedBottle(db, seller.Id, quantity: 1, reservedQuantity: 1);
        var offer = SeedOffer(db, bottle.Id, buyer.Id, seller.Id);
        var notificationMock = new Mock<INotificationService>();
        var inner = CreateInnerOfferService(db, seller.Id, notificationMock.Object);

        var result = await inner.AcceptAsync(offer.Id, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ErrorCode.Conflict, result.ErrorCode);
        Assert.Equal(OfferService.NoBottleLeftMessage, result.Error);
        Assert.Equal(OfferErrorReasons.NoBottleLeft, result.Reason);
        db.ChangeTracker.Clear();
        Assert.Equal(OfferStatus.Pending, (await db.Offers.FindAsync(offer.Id))!.Status);
        Assert.Equal(1, (await db.Bottles.FindAsync(bottle.Id))!.ReservedQuantity);
        notificationMock.Verify(n => n.CreateAsync(
            It.IsAny<Guid>(), It.IsAny<NotificationType>(), It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AcceptAsync_WhenTheBottleIsListed_TakesOneBottleOffSale()
    {
        var db = CreateSqliteDbContext();
        var seller = SeedUser(db, "Seller");
        var buyer = SeedUser(db, "Buyer");
        var bottle = SeedBottle(db, seller.Id, quantity: 3, forSaleQuantity: 2);
        var offer = SeedOffer(db, bottle.Id, buyer.Id, seller.Id);
        var service = CreateOfferService(db, seller.Id);

        var result = await service.AcceptAsync(offer.Id, CancellationToken.None);

        Assert.True(result.Success);
        db.ChangeTracker.Clear();
        var stored = (await db.Bottles.FindAsync(bottle.Id))!;
        Assert.True(stored.IsForSale);
        Assert.Equal(1, stored.ForSaleQuantity);
        Assert.Equal(150m, stored.AskingPrice);
        Assert.Equal("EUR", stored.Currency);
        Assert.NotNull(stored.ForSaleAt);
        Assert.Equal(3, stored.Quantity);
        Assert.Equal(1, stored.ReservedQuantity);
    }

    [Fact]
    public async Task AcceptAsync_WhenTheLastListedBottleIsAccepted_UnlistsTheEntry()
    {
        var db = CreateSqliteDbContext();
        var seller = SeedUser(db, "Seller");
        var buyer = SeedUser(db, "Buyer");
        var bottle = SeedBottle(db, seller.Id, quantity: 2, forSaleQuantity: 1);
        var offer = SeedOffer(db, bottle.Id, buyer.Id, seller.Id);
        var service = CreateOfferService(db, seller.Id);

        var result = await service.AcceptAsync(offer.Id, CancellationToken.None);

        Assert.True(result.Success);
        db.ChangeTracker.Clear();
        var stored = (await db.Bottles.FindAsync(bottle.Id))!;
        Assert.False(stored.IsForSale);
        Assert.Equal(0, stored.ForSaleQuantity);
        Assert.Null(stored.AskingPrice);
        Assert.Null(stored.Currency);
        Assert.Null(stored.ForSaleAt);
        Assert.Equal(1, stored.ReservedQuantity);
    }

    [Fact]
    public async Task AcceptAsync_WhenTheBottleIsNotListed_LeavesItOffSale()
    {
        var db = CreateSqliteDbContext();
        var seller = SeedUser(db, "Seller");
        var buyer = SeedUser(db, "Buyer");
        var bottle = SeedBottle(db, seller.Id, quantity: 2);
        var offer = SeedOffer(db, bottle.Id, buyer.Id, seller.Id);
        var service = CreateOfferService(db, seller.Id);

        var result = await service.AcceptAsync(offer.Id, CancellationToken.None);

        Assert.True(result.Success);
        db.ChangeTracker.Clear();
        var stored = (await db.Bottles.FindAsync(bottle.Id))!;
        Assert.False(stored.IsForSale);
        Assert.Equal(0, stored.ForSaleQuantity);
        Assert.Equal(1, stored.ReservedQuantity);
    }

    /// <summary>
    /// Once no bottle is free, nobody else can buy it (#130): the other pending offers are declined and their buyers
    /// told. An offer that was not pending, or a deleted one, is left as it is.
    /// </summary>
    [Fact]
    public async Task AcceptAsync_WhenNoFreeBottleIsLeft_DeclinesTheOtherPendingOffersAndNotifiesTheirBuyers()
    {
        var db = CreateSqliteDbContext();
        var seller = SeedUser(db, "Seller");
        var buyer = SeedUser(db, "Buyer");
        var other = SeedUser(db, "Other");
        var withdrawer = SeedUser(db, "Withdrawer");
        var deleter = SeedUser(db, "Deleter");
        var bottle = SeedBottle(db, seller.Id, name: "Port Ellen 1979");
        var offer = SeedOffer(db, bottle.Id, buyer.Id, seller.Id);
        var otherOffer = SeedOffer(db, bottle.Id, other.Id, seller.Id);
        var withdrawnOffer = SeedOffer(db, bottle.Id, withdrawer.Id, seller.Id, OfferStatus.Withdrawn);
        var deletedOffer = SeedOffer(db, bottle.Id, deleter.Id, seller.Id, isDeleted: true);
        var notificationMock = new Mock<INotificationService>();
        var service = CreateOfferService(db, seller.Id, notificationMock.Object);

        var result = await service.AcceptAsync(offer.Id, CancellationToken.None);

        Assert.True(result.Success);
        db.ChangeTracker.Clear();
        Assert.Equal(OfferStatus.Declined, (await db.Offers.FindAsync(otherOffer.Id))!.Status);
        Assert.NotNull((await db.Offers.FindAsync(otherOffer.Id))!.RespondedAt);
        Assert.Equal(OfferStatus.Withdrawn, (await db.Offers.FindAsync(withdrawnOffer.Id))!.Status);
        Assert.Equal(OfferStatus.Pending, (await db.Offers.FindAsync(deletedOffer.Id))!.Status);
        notificationMock.Verify(n => n.CreateAsync(
            other.Id, NotificationType.OfferDeclined, otherOffer.Id, "Port Ellen 1979", It.IsAny<CancellationToken>()), Times.Once);
        notificationMock.Verify(n => n.CreateAsync(
            withdrawer.Id, It.IsAny<NotificationType>(), It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
        notificationMock.Verify(n => n.CreateAsync(
            deleter.Id, It.IsAny<NotificationType>(), It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AcceptAsync_WhenFreeBottlesRemain_LeavesTheOtherOffersPending()
    {
        var db = CreateSqliteDbContext();
        var seller = SeedUser(db, "Seller");
        var buyer = SeedUser(db, "Buyer");
        var other = SeedUser(db, "Other");
        var bottle = SeedBottle(db, seller.Id, quantity: 2);
        var offer = SeedOffer(db, bottle.Id, buyer.Id, seller.Id);
        var otherOffer = SeedOffer(db, bottle.Id, other.Id, seller.Id);
        var notificationMock = new Mock<INotificationService>();
        var service = CreateOfferService(db, seller.Id, notificationMock.Object);

        var result = await service.AcceptAsync(offer.Id, CancellationToken.None);

        Assert.True(result.Success);
        db.ChangeTracker.Clear();
        Assert.Equal(OfferStatus.Pending, (await db.Offers.FindAsync(otherOffer.Id))!.Status);
        notificationMock.Verify(n => n.CreateAsync(
            It.IsAny<Guid>(), NotificationType.OfferDeclined, It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region DeclineAsync

    [Fact]
    public async Task DeclineAsync_WhenNotFound_ReturnsNotFound()
    {
        var db = CreateDbContext();
        var seller = SeedUser(db, "Seller");
        var service = CreateOfferService(db, seller.Id);

        var result = await service.DeclineAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Offer not found.", result.Error);
    }

    [Fact]
    public async Task DeclineAsync_WhenNotSeller_ReturnsForbidden()
    {
        var db = CreateDbContext();
        var seller = SeedUser(db, "Seller");
        var buyer = SeedUser(db, "Buyer");
        var bottle = SeedBottle(db, seller.Id);
        var offer = SeedOffer(db, bottle.Id, buyer.Id, seller.Id);
        var service = CreateOfferService(db, buyer.Id);

        var result = await service.DeclineAsync(offer.Id, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Only the seller can decline this offer.", result.Error);
    }

    [Fact]
    public async Task DeclineAsync_WhenNotPending_ReturnsConflict()
    {
        var db = CreateDbContext();
        var seller = SeedUser(db, "Seller");
        var buyer = SeedUser(db, "Buyer");
        var bottle = SeedBottle(db, seller.Id);
        var offer = SeedOffer(db, bottle.Id, buyer.Id, seller.Id, OfferStatus.Withdrawn);
        var service = CreateOfferService(db, seller.Id);

        var result = await service.DeclineAsync(offer.Id, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Only pending offers can be declined.", result.Error);
    }

    [Fact]
    public async Task DeclineAsync_WhenValid_SetsDeclinedAndNotifiesBuyer()
    {
        var db = CreateSqliteDbContext();
        var seller = SeedUser(db, "Seller");
        var buyer = SeedUser(db, "Buyer");
        var bottle = SeedBottle(db, seller.Id, name: "Hibiki");
        var offer = SeedOffer(db, bottle.Id, buyer.Id, seller.Id);
        var notificationMock = new Mock<INotificationService>();
        var service = CreateOfferService(db, seller.Id, notificationMock.Object);

        var result = await service.DeclineAsync(offer.Id, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(OfferStatus.Declined, result.Data!.Status);
        Assert.NotNull(result.Data.RespondedAt);

        db.ChangeTracker.Clear(); // ExecuteUpdate bypasses the tracker — read the row fresh
        var stored = await db.Offers.FindAsync(offer.Id);
        Assert.Equal(OfferStatus.Declined, stored!.Status);
        Assert.NotNull(stored.RespondedAt);

        notificationMock.Verify(n => n.CreateAsync(
            buyer.Id, NotificationType.OfferDeclined, offer.Id, "Hibiki", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeclineAsync_WhenLosesRaceToResolvedOffer_ReturnsConflictWithoutNotification()
    {
        var db = CreateSqliteDbContext();
        var seller = SeedUser(db, "Seller");
        var buyer = SeedUser(db, "Buyer");
        var bottle = SeedBottle(db, seller.Id);
        var offer = SeedOffer(db, bottle.Id, buyer.Id, seller.Id, OfferStatus.Withdrawn);
        var notificationMock = new Mock<INotificationService>();
        var inner = CreateInnerOfferService(db, seller.Id, notificationMock.Object);

        var result = await inner.DeclineAsync(offer.Id, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Only pending offers can be declined.", result.Error);
        notificationMock.Verify(n => n.CreateAsync(
            It.IsAny<Guid>(), It.IsAny<NotificationType>(), It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeclineAsync_WithCancelledToken_Throws()
    {
        var db = CreateDbContext();
        var service = CreateOfferService(db, Guid.NewGuid());
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => service.DeclineAsync(Guid.NewGuid(), cts.Token));
    }

    #endregion

    #region WithdrawAsync

    [Fact]
    public async Task WithdrawAsync_WhenNotFound_ReturnsNotFound()
    {
        var db = CreateDbContext();
        var buyer = SeedUser(db, "Buyer");
        var service = CreateOfferService(db, buyer.Id);

        var result = await service.WithdrawAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Offer not found.", result.Error);
    }

    [Fact]
    public async Task WithdrawAsync_WhenNotBuyer_ReturnsForbidden()
    {
        var db = CreateDbContext();
        var seller = SeedUser(db, "Seller");
        var buyer = SeedUser(db, "Buyer");
        var bottle = SeedBottle(db, seller.Id);
        var offer = SeedOffer(db, bottle.Id, buyer.Id, seller.Id);
        var service = CreateOfferService(db, seller.Id);

        var result = await service.WithdrawAsync(offer.Id, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Only the buyer can withdraw this offer.", result.Error);
    }

    [Fact]
    public async Task WithdrawAsync_WhenNotPending_ReturnsConflict()
    {
        var db = CreateDbContext();
        var seller = SeedUser(db, "Seller");
        var buyer = SeedUser(db, "Buyer");
        var bottle = SeedBottle(db, seller.Id);
        var offer = SeedOffer(db, bottle.Id, buyer.Id, seller.Id, OfferStatus.Accepted);
        var service = CreateOfferService(db, buyer.Id);

        var result = await service.WithdrawAsync(offer.Id, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Only pending offers can be withdrawn.", result.Error);
    }

    [Fact]
    public async Task WithdrawAsync_WhenValid_SetsWithdrawn()
    {
        var db = CreateSqliteDbContext();
        var seller = SeedUser(db, "Seller");
        var buyer = SeedUser(db, "Buyer");
        var bottle = SeedBottle(db, seller.Id);
        var offer = SeedOffer(db, bottle.Id, buyer.Id, seller.Id);
        var service = CreateOfferService(db, buyer.Id);

        var result = await service.WithdrawAsync(offer.Id, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(OfferStatus.Withdrawn, result.Data!.Status);

        db.ChangeTracker.Clear(); // ExecuteUpdate bypasses the tracker — read the row fresh
        var stored = await db.Offers.FindAsync(offer.Id);
        Assert.Equal(OfferStatus.Withdrawn, stored!.Status);
    }

    [Fact]
    public async Task WithdrawAsync_WhenLosesRaceToAccept_ReturnsConflict()
    {
        // The accept-vs-withdraw race: the seller accepted a heartbeat before the buyer's withdraw.
        var db = CreateSqliteDbContext();
        var seller = SeedUser(db, "Seller");
        var buyer = SeedUser(db, "Buyer");
        var bottle = SeedBottle(db, seller.Id);
        var offer = SeedOffer(db, bottle.Id, buyer.Id, seller.Id, OfferStatus.Accepted);
        var inner = CreateInnerOfferService(db, buyer.Id);

        var result = await inner.WithdrawAsync(offer.Id, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Only pending offers can be withdrawn.", result.Error);

        db.ChangeTracker.Clear();
        var stored = await db.Offers.FindAsync(offer.Id);
        Assert.Equal(OfferStatus.Accepted, stored!.Status); // the winner's state is preserved
    }

    [Fact]
    public async Task WithdrawAsync_WithCancelledToken_Throws()
    {
        var db = CreateDbContext();
        var service = CreateOfferService(db, Guid.NewGuid());
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => service.WithdrawAsync(Guid.NewGuid(), cts.Token));
    }

    #endregion

    #region ConfirmReceiptAsync

    private static Product SeedProduct(AppDbContext db, bool isDeleted = false)
    {
        var product = new Product
        {
            Name = "16 Year Old",
            Brand = "Lagavulin",
            Category = SpiritCategory.Whisky,
            CanonicalKey = $"lagavulin|16-year-old|{Guid.NewGuid()}",
            Origin = ProductOrigin.Seeded,
            IsDeleted = isDeleted
        };
        db.Products.Add(product);
        db.SaveChanges();
        return product;
    }

    private static Distillery SeedDistillery(AppDbContext db, bool isDeleted = false)
    {
        var distillery = new Distillery { Name = $"Lagavulin {Guid.NewGuid()}", IsDeleted = isDeleted };
        db.Distilleries.Add(distillery);
        db.SaveChanges();
        return distillery;
    }

    private static void SeedImage(AppDbContext db, Guid bottleId, string url, bool isPrimary = true)
    {
        db.BottleImages.Add(new BottleImage { BottleId = bottleId, Url = url, IsPrimary = isPrimary });
        db.SaveChanges();
    }

    /// <summary>A seller's bottle with every identity field set, reserved for a deal awaiting receipt.</summary>
    private static (AppUser Seller, AppUser Buyer, Bottle Bottle, Offer Offer) SeedDealAwaitingReceipt(
        AppDbContext db, int quantity = 1, Guid? productId = null, Guid? distilleryId = null, string? barcode = null)
    {
        var seller = SeedUser(db, "Seller");
        var buyer = SeedUser(db, "Buyer");
        var bottle = new Bottle
        {
            UserId = seller.Id,
            Name = "Lagavulin 16",
            Category = SpiritCategory.Whisky,
            Condition = BottleCondition.Sealed,
            Region = "Islay",
            Country = "Scotland",
            Age = 16,
            VintageYear = 2005,
            AbvPercent = 43,
            VolumeMl = 700,
            IsLimited = true,
            Description = "The seller's own notes",
            ProductId = productId,
            DistilleryId = distilleryId,
            Barcode = barcode,
            Quantity = quantity,
            ReservedQuantity = 1
        };
        db.Bottles.Add(bottle);
        db.SaveChanges();
        var offer = SeedOffer(db, bottle.Id, buyer.Id, seller.Id, OfferStatus.AwaitingReceipt);
        return (seller, buyer, bottle, offer);
    }

    private static Task<Bottle> BoughtBottleAsync(AppDbContext db, Guid buyerId)
    {
        db.ChangeTracker.Clear();
        return db.Bottles.Include(b => b.Images).SingleAsync(b => b.UserId == buyerId);
    }

    private static IWebHostEnvironment CreateEnvironment(string? webRootPath, string? contentRootPath = null)
    {
        var env = new Mock<IWebHostEnvironment>();
        env.Setup(e => e.WebRootPath).Returns(webRootPath!);
        env.Setup(e => e.ContentRootPath).Returns(contentRootPath!);
        return env.Object;
    }

    [Fact]
    public async Task ConfirmReceiptAsync_WhenNotFound_ReturnsNotFound()
    {
        var db = CreateDbContext();
        var service = CreateOfferService(db, Guid.NewGuid());

        var result = await service.ConfirmReceiptAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ErrorCode.NotFound, result.ErrorCode);
    }

    [Fact]
    public async Task ConfirmReceiptAsync_WhenOfferSoftDeleted_ReturnsNotFound()
    {
        var db = CreateDbContext();
        var seller = SeedUser(db, "Seller");
        var buyer = SeedUser(db, "Buyer");
        var bottle = SeedBottle(db, seller.Id, reservedQuantity: 1);
        var offer = SeedOffer(db, bottle.Id, buyer.Id, seller.Id, OfferStatus.AwaitingReceipt, isDeleted: true);
        var service = CreateOfferService(db, buyer.Id);

        var result = await service.ConfirmReceiptAsync(offer.Id, CancellationToken.None);

        Assert.Equal(ErrorCode.NotFound, result.ErrorCode);
    }

    [Fact]
    public async Task ConfirmReceiptAsync_WhenTheSellerConfirms_ReturnsForbidden()
    {
        var db = CreateDbContext();
        var seller = SeedUser(db, "Seller");
        var buyer = SeedUser(db, "Buyer");
        var bottle = SeedBottle(db, seller.Id, reservedQuantity: 1);
        var offer = SeedOffer(db, bottle.Id, buyer.Id, seller.Id, OfferStatus.AwaitingReceipt);
        var service = CreateOfferService(db, seller.Id);

        var result = await service.ConfirmReceiptAsync(offer.Id, CancellationToken.None);

        Assert.Equal(ErrorCode.Forbidden, result.ErrorCode);
        Assert.Equal("Only the buyer can confirm receiving the bottle.", result.Error);
    }

    [Theory]
    [InlineData(OfferStatus.Pending)]
    [InlineData(OfferStatus.Accepted)]
    [InlineData(OfferStatus.Cancelled)]
    public async Task ConfirmReceiptAsync_WhenNotAwaitingReceipt_ReturnsConflict(OfferStatus status)
    {
        var db = CreateDbContext();
        var seller = SeedUser(db, "Seller");
        var buyer = SeedUser(db, "Buyer");
        var bottle = SeedBottle(db, seller.Id);
        var offer = SeedOffer(db, bottle.Id, buyer.Id, seller.Id, status);
        var service = CreateOfferService(db, buyer.Id);

        var result = await service.ConfirmReceiptAsync(offer.Id, CancellationToken.None);

        Assert.Equal(ErrorCode.Conflict, result.ErrorCode);
        Assert.Equal("Only a deal awaiting receipt can be confirmed.", result.Error);
    }

    [Fact]
    public async Task ConfirmReceiptAsync_WithCancelledToken_Throws()
    {
        var db = CreateDbContext();
        var service = CreateOfferService(db, Guid.NewGuid());
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => service.ConfirmReceiptAsync(Guid.NewGuid(), cts.Token));
    }

    /// <summary>A double click: the second confirmation passed the decorator before the first completed the deal.</summary>
    [Fact]
    public async Task ConfirmReceiptAsync_WhenTheDealWasJustCompleted_ReturnsConflictAndMovesNothing()
    {
        var db = CreateSqliteDbContext();
        var (seller, buyer, bottle, offer) = SeedDealAwaitingReceipt(db);
        await db.Offers.Where(o => o.Id == offer.Id).ExecuteUpdateAsync(s => s.SetProperty(o => o.Status, OfferStatus.Accepted));
        var inner = CreateInnerOfferService(db, buyer.Id);

        var result = await inner.ConfirmReceiptAsync(offer.Id, CancellationToken.None);

        Assert.Equal(ErrorCode.Conflict, result.ErrorCode);
        db.ChangeTracker.Clear();
        Assert.Equal(1, (await db.Bottles.FindAsync(bottle.Id))!.Quantity);
        Assert.False(await db.Bottles.AnyAsync(b => b.UserId == buyer.Id));
    }

    [Fact]
    public async Task ConfirmReceiptAsync_WhenTheReservedBottleIsGone_ReturnsConflictAndRollsBack()
    {
        var db = CreateSqliteDbContext();
        var seller = SeedUser(db, "Seller");
        var buyer = SeedUser(db, "Buyer");
        var bottle = SeedBottle(db, seller.Id, reservedQuantity: 0);
        var offer = SeedOffer(db, bottle.Id, buyer.Id, seller.Id, OfferStatus.AwaitingReceipt);
        var service = CreateOfferService(db, buyer.Id);

        var result = await service.ConfirmReceiptAsync(offer.Id, CancellationToken.None);

        Assert.Equal(ErrorCode.Conflict, result.ErrorCode);
        Assert.Equal("The reserved bottle is no longer in the seller's bar.", result.Error);
        db.ChangeTracker.Clear();
        Assert.Equal(OfferStatus.AwaitingReceipt, (await db.Offers.FindAsync(offer.Id))!.Status);
        Assert.False(await db.Bottles.AnyAsync(b => b.UserId == buyer.Id));
    }

    /// <summary>
    /// The buyer confirms (#130): the deal completes, one bottle leaves the seller's entry, and a copy of it joins
    /// the buyer's bar with its catalog link, distillery and barcode, which is recorded for the product.
    /// </summary>
    [Fact]
    public async Task ConfirmReceiptAsync_WhenValid_CompletesTheDealAndMovesOneBottleToTheBuyer()
    {
        var db = CreateSqliteDbContext();
        var product = SeedProduct(db);
        var distillery = SeedDistillery(db);
        var (_, buyer, bottle, offer) = SeedDealAwaitingReceipt(
            db, quantity: 2, productId: product.Id, distilleryId: distillery.Id, barcode: "5000281005409");
        var service = CreateOfferService(db, buyer.Id);

        var result = await service.ConfirmReceiptAsync(offer.Id, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(OfferStatus.Accepted, result.Data!.Status);
        Assert.NotNull(result.Data.CompletedAt);

        db.ChangeTracker.Clear();
        var sellerBottle = (await db.Bottles.FindAsync(bottle.Id))!;
        Assert.Equal(1, sellerBottle.Quantity);
        Assert.Equal(0, sellerBottle.ReservedQuantity);
        Assert.False(sellerBottle.IsDeleted);

        var bought = await BoughtBottleAsync(db, buyer.Id);
        Assert.Equal("Lagavulin 16", bought.Name);
        Assert.Equal(product.Id, bought.ProductId);
        Assert.Equal(distillery.Id, bought.DistilleryId);
        Assert.Equal("5000281005409", bought.Barcode);
        Assert.Equal(SpiritCategory.Whisky, bought.Category);
        Assert.Equal("Islay", bought.Region);
        Assert.Equal("Scotland", bought.Country);
        Assert.Equal(16, bought.Age);
        Assert.Equal(2005, bought.VintageYear);
        Assert.Equal(43, bought.AbvPercent);
        Assert.Equal(700, bought.VolumeMl);
        Assert.Equal(BottleCondition.Sealed, bought.Condition);
        Assert.True(bought.IsLimited);
        Assert.Null(bought.Description);
        Assert.Equal(1, bought.Quantity);
        Assert.False(bought.IsForSale);
        Assert.Equal(offer.Id, bought.AcquiredFromOfferId);
        Assert.Empty(bought.Images);
        Assert.True(await db.ProductBarcodes.AnyAsync(b =>
            b.ProductId == product.Id && b.Barcode == "5000281005409" && b.AddedByUserId == buyer.Id));
    }

    [Fact]
    public async Task ConfirmReceiptAsync_WhenTheLastBottleLeaves_RemovesTheSellerEntry()
    {
        var db = CreateSqliteDbContext();
        var (_, buyer, bottle, offer) = SeedDealAwaitingReceipt(db, quantity: 1);
        var service = CreateOfferService(db, buyer.Id);

        var result = await service.ConfirmReceiptAsync(offer.Id, CancellationToken.None);

        Assert.True(result.Success);
        db.ChangeTracker.Clear();
        var sellerBottle = (await db.Bottles.FindAsync(bottle.Id))!;
        Assert.True(sellerBottle.IsDeleted);
        Assert.NotNull(sellerBottle.DeletedAt);
        Assert.Equal(0, sellerBottle.Quantity);
        Assert.Equal(0, sellerBottle.ReservedQuantity);
        Assert.Null((await BoughtBottleAsync(db, buyer.Id)).Barcode);
    }

    [Fact]
    public async Task ConfirmReceiptAsync_WhenProductAndDistilleryWereDeleted_LeavesThemOutAndRecordsNoBarcode()
    {
        var db = CreateSqliteDbContext();
        var product = SeedProduct(db, isDeleted: true);
        var distillery = SeedDistillery(db, isDeleted: true);
        var (_, buyer, _, offer) = SeedDealAwaitingReceipt(
            db, productId: product.Id, distilleryId: distillery.Id, barcode: "5000281005409");
        var service = CreateOfferService(db, buyer.Id);

        var result = await service.ConfirmReceiptAsync(offer.Id, CancellationToken.None);

        Assert.True(result.Success);
        var bought = await BoughtBottleAsync(db, buyer.Id);
        Assert.Null(bought.ProductId);
        Assert.Null(bought.DistilleryId);
        Assert.Equal("5000281005409", bought.Barcode);
        Assert.False(await db.ProductBarcodes.AnyAsync());
    }

    [Fact]
    public async Task ConfirmReceiptAsync_WhenValid_NotifiesTheSellerAndTheBuyersFollowersAndEvaluatesBadges()
    {
        var db = CreateSqliteDbContext();
        var (seller, buyer, _, offer) = SeedDealAwaitingReceipt(db);
        var follower = SeedUser(db, "Follower");
        db.UserFollows.Add(new UserFollow { FollowerId = follower.Id, FollowedId = buyer.Id });
        db.SaveChanges();
        var notificationMock = new Mock<INotificationService>();
        var badgeMock = new Mock<IBadgeService>();
        var service = CreateOfferService(db, buyer.Id, notificationMock.Object, badgeMock.Object);

        var result = await service.ConfirmReceiptAsync(offer.Id, CancellationToken.None);

        Assert.True(result.Success);
        var bought = await BoughtBottleAsync(db, buyer.Id);
        notificationMock.Verify(n => n.CreateAsync(
            seller.Id, NotificationType.SaleCompleted, offer.Id, "Lagavulin 16", It.IsAny<CancellationToken>()), Times.Once);
        notificationMock.Verify(n => n.CreateBulkAsync(
            It.Is<IEnumerable<Guid>>(ids => ids.SequenceEqual(new[] { follower.Id })),
            NotificationType.NewBottleFromFollowing, bought.Id, "Lagavulin 16", It.IsAny<CancellationToken>()), Times.Once);
        badgeMock.Verify(b => b.EvaluateAsync(seller.Id, BadgeTrigger.OfferAccepted, It.IsAny<CancellationToken>()), Times.Once);
        badgeMock.Verify(b => b.EvaluateAsync(buyer.Id, BadgeTrigger.OfferAccepted, It.IsAny<CancellationToken>()), Times.Once);
        badgeMock.Verify(b => b.EvaluateAsync(buyer.Id, BadgeTrigger.BottleAdded, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>An uploaded cover is copied, so the seller deleting their own image later leaves the buyer's (#130).</summary>
    [Fact]
    public async Task ConfirmReceiptAsync_WhenTheCoverIsAnUpload_CopiesTheFileForTheBuyer()
    {
        var webRoot = Directory.CreateTempSubdirectory().FullName;
        var uploads = Directory.CreateDirectory(Path.Combine(webRoot, "uploads", "bottles")).FullName;
        await File.WriteAllBytesAsync(Path.Combine(uploads, "cover.png"), [1, 2, 3]);
        var db = CreateSqliteDbContext();
        var (_, buyer, bottle, offer) = SeedDealAwaitingReceipt(db);
        SeedImage(db, bottle.Id, "/uploads/bottles/cover.png");
        var service = CreateOfferService(db, buyer.Id, env: CreateEnvironment(webRoot));

        var result = await service.ConfirmReceiptAsync(offer.Id, CancellationToken.None);

        Assert.True(result.Success);
        var image = Assert.Single((await BoughtBottleAsync(db, buyer.Id)).Images);
        Assert.True(image.IsPrimary);
        Assert.StartsWith("/uploads/bottles/", image.Url);
        Assert.NotEqual("/uploads/bottles/cover.png", image.Url);
        Assert.EndsWith(".png", image.Url);
        Assert.Equal([1, 2, 3], await File.ReadAllBytesAsync(Path.Combine(webRoot, image.Url.TrimStart('/'))));
        Assert.True(File.Exists(Path.Combine(uploads, "cover.png")));
    }

    [Fact]
    public async Task ConfirmReceiptAsync_WhenTheOnlyImageIsNotTheCover_UsesItWithTheContentRoot()
    {
        var contentRoot = Directory.CreateTempSubdirectory().FullName;
        var uploads = Directory.CreateDirectory(Path.Combine(contentRoot, "wwwroot", "uploads", "bottles")).FullName;
        await File.WriteAllBytesAsync(Path.Combine(uploads, "side.jpg"), [4]);
        var db = CreateSqliteDbContext();
        var (_, buyer, bottle, offer) = SeedDealAwaitingReceipt(db);
        SeedImage(db, bottle.Id, "/uploads/bottles/side.jpg", isPrimary: false);
        var service = CreateOfferService(db, buyer.Id, env: CreateEnvironment(null, contentRoot));

        var result = await service.ConfirmReceiptAsync(offer.Id, CancellationToken.None);

        Assert.True(result.Success);
        var image = Assert.Single((await BoughtBottleAsync(db, buyer.Id)).Images);
        Assert.EndsWith(".jpg", image.Url);
        Assert.True(File.Exists(Path.Combine(contentRoot, "wwwroot", image.Url.TrimStart('/'))));
    }

    [Fact]
    public async Task ConfirmReceiptAsync_WhenTheCoverIsAnExternalUrl_KeepsTheUrl()
    {
        var db = CreateSqliteDbContext();
        var (_, buyer, bottle, offer) = SeedDealAwaitingReceipt(db);
        SeedImage(db, bottle.Id, "https://images.example.com/lagavulin.jpg");
        var service = CreateOfferService(db, buyer.Id);

        var result = await service.ConfirmReceiptAsync(offer.Id, CancellationToken.None);

        Assert.True(result.Success);
        var image = Assert.Single((await BoughtBottleAsync(db, buyer.Id)).Images);
        Assert.Equal("https://images.example.com/lagavulin.jpg", image.Url);
    }

    [Fact]
    public async Task ConfirmReceiptAsync_WhenTheUploadedFileIsMissing_GivesNoImage()
    {
        var webRoot = Directory.CreateTempSubdirectory().FullName;
        var db = CreateSqliteDbContext();
        var (_, buyer, bottle, offer) = SeedDealAwaitingReceipt(db);
        SeedImage(db, bottle.Id, "/uploads/bottles/gone.png");
        var service = CreateOfferService(db, buyer.Id, env: CreateEnvironment(webRoot));

        var result = await service.ConfirmReceiptAsync(offer.Id, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Empty((await BoughtBottleAsync(db, buyer.Id)).Images);
    }

    /// <summary>A stored URL can be any text a collector linked; nothing outside the bottle uploads is ever copied.</summary>
    [Fact]
    public async Task ConfirmReceiptAsync_WhenTheUrlLeavesTheUploadsFolder_CopiesNothing()
    {
        var webRoot = Directory.CreateTempSubdirectory().FullName;
        Directory.CreateDirectory(Path.Combine(webRoot, "uploads", "bottles"));
        await File.WriteAllTextAsync(Path.Combine(webRoot, "secret.json"), "{}");
        var db = CreateSqliteDbContext();
        var (_, buyer, bottle, offer) = SeedDealAwaitingReceipt(db);
        SeedImage(db, bottle.Id, "/uploads/bottles/../../secret.json");
        var service = CreateOfferService(db, buyer.Id, env: CreateEnvironment(webRoot));

        var result = await service.ConfirmReceiptAsync(offer.Id, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Empty((await BoughtBottleAsync(db, buyer.Id)).Images);
        Assert.Single(Directory.GetFiles(webRoot, "*.json", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task ConfirmReceiptAsync_WhenCopyingTheImageFails_LogsTheErrorAndKeepsTheDeal()
    {
        var db = CreateSqliteDbContext();
        var (_, buyer, bottle, offer) = SeedDealAwaitingReceipt(db);
        SeedImage(db, bottle.Id, "/uploads/bottles/cover.png");
        var logger = new Mock<ILogger<OfferService>>();
        var env = new Mock<IWebHostEnvironment>();
        env.Setup(e => e.WebRootPath).Throws(new IOException("disk unavailable"));
        var service = CreateOfferService(db, buyer.Id, env: env.Object, logger: logger.Object);

        var result = await service.ConfirmReceiptAsync(offer.Id, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Empty((await BoughtBottleAsync(db, buyer.Id)).Images);
        logger.Verify(l => l.Log(
            LogLevel.Error, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), It.IsAny<IOException>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
    }

    [Fact]
    public async Task ConfirmReceiptAsync_WhenCancelledWhileCopyingTheImage_ThrowsAfterTheDealIsComplete()
    {
        var db = CreateSqliteDbContext();
        var (_, buyer, bottle, offer) = SeedDealAwaitingReceipt(db);
        SeedImage(db, bottle.Id, "/uploads/bottles/cover.png");
        var env = new Mock<IWebHostEnvironment>();
        env.Setup(e => e.WebRootPath).Throws(new OperationCanceledException());
        var service = CreateOfferService(db, buyer.Id, env: env.Object);

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => service.ConfirmReceiptAsync(offer.Id, CancellationToken.None));

        db.ChangeTracker.Clear();
        Assert.Equal(OfferStatus.Accepted, (await db.Offers.FindAsync(offer.Id))!.Status);
    }

    #endregion

    #region CancelAsync

    [Fact]
    public async Task CancelAsync_WhenNotFound_ReturnsNotFound()
    {
        var db = CreateDbContext();
        var service = CreateOfferService(db, Guid.NewGuid());

        var result = await service.CancelAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.Equal(ErrorCode.NotFound, result.ErrorCode);
    }

    [Fact]
    public async Task CancelAsync_WhenNeitherBuyerNorSeller_ReturnsForbidden()
    {
        var db = CreateDbContext();
        var seller = SeedUser(db, "Seller");
        var buyer = SeedUser(db, "Buyer");
        var bottle = SeedBottle(db, seller.Id, reservedQuantity: 1);
        var offer = SeedOffer(db, bottle.Id, buyer.Id, seller.Id, OfferStatus.AwaitingReceipt);
        var service = CreateOfferService(db, Guid.NewGuid());

        var result = await service.CancelAsync(offer.Id, CancellationToken.None);

        Assert.Equal(ErrorCode.Forbidden, result.ErrorCode);
        Assert.Equal("Only the buyer or the seller can cancel this deal.", result.Error);
    }

    [Theory]
    [InlineData(OfferStatus.Pending)]
    [InlineData(OfferStatus.Accepted)]
    [InlineData(OfferStatus.Cancelled)]
    public async Task CancelAsync_WhenNotAwaitingReceipt_ReturnsConflict(OfferStatus status)
    {
        var db = CreateDbContext();
        var seller = SeedUser(db, "Seller");
        var buyer = SeedUser(db, "Buyer");
        var bottle = SeedBottle(db, seller.Id);
        var offer = SeedOffer(db, bottle.Id, buyer.Id, seller.Id, status);
        var service = CreateOfferService(db, seller.Id);

        var result = await service.CancelAsync(offer.Id, CancellationToken.None);

        Assert.Equal(ErrorCode.Conflict, result.ErrorCode);
        Assert.Equal("Only a deal awaiting receipt can be cancelled.", result.Error);
    }

    [Fact]
    public async Task CancelAsync_WithCancelledToken_Throws()
    {
        var db = CreateDbContext();
        var service = CreateOfferService(db, Guid.NewGuid());
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => service.CancelAsync(Guid.NewGuid(), cts.Token));
    }

    [Fact]
    public async Task CancelAsync_WhenTheDealWasJustCompleted_ReturnsConflictAndKeepsTheReservation()
    {
        var db = CreateSqliteDbContext();
        var (seller, _, bottle, offer) = SeedDealAwaitingReceipt(db);
        await db.Offers.Where(o => o.Id == offer.Id).ExecuteUpdateAsync(s => s.SetProperty(o => o.Status, OfferStatus.Accepted));
        var inner = CreateInnerOfferService(db, seller.Id);

        var result = await inner.CancelAsync(offer.Id, CancellationToken.None);

        Assert.Equal(ErrorCode.Conflict, result.ErrorCode);
        db.ChangeTracker.Clear();
        Assert.Equal(1, (await db.Bottles.FindAsync(bottle.Id))!.ReservedQuantity);
    }

    /// <summary>The buyer cancels (#130): the bottle stays with the seller, off sale, and the seller is told.</summary>
    [Fact]
    public async Task CancelAsync_WhenTheBuyerCancels_FreesTheBottleAndNotifiesTheSeller()
    {
        var db = CreateSqliteDbContext();
        var (seller, buyer, bottle, offer) = SeedDealAwaitingReceipt(db);
        var notificationMock = new Mock<INotificationService>();
        var service = CreateOfferService(db, buyer.Id, notificationMock.Object);

        var result = await service.CancelAsync(offer.Id, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(OfferStatus.Cancelled, result.Data!.Status);
        Assert.NotNull(result.Data.CancelledAt);
        db.ChangeTracker.Clear();
        var stored = (await db.Bottles.FindAsync(bottle.Id))!;
        Assert.Equal(0, stored.ReservedQuantity);
        Assert.Equal(1, stored.Quantity);
        Assert.False(stored.IsForSale);
        Assert.False(stored.IsDeleted);
        notificationMock.Verify(n => n.CreateAsync(
            seller.Id, NotificationType.DealCancelled, offer.Id, "Lagavulin 16", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CancelAsync_WhenTheSellerCancels_NotifiesTheBuyer()
    {
        var db = CreateSqliteDbContext();
        var (seller, buyer, _, offer) = SeedDealAwaitingReceipt(db);
        var notificationMock = new Mock<INotificationService>();
        var service = CreateOfferService(db, seller.Id, notificationMock.Object);

        var result = await service.CancelAsync(offer.Id, CancellationToken.None);

        Assert.True(result.Success);
        notificationMock.Verify(n => n.CreateAsync(
            buyer.Id, NotificationType.DealCancelled, offer.Id, "Lagavulin 16", It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion
}
