using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using VirtualBar.Application.DTOs.Offers;
using VirtualBar.Application.Interfaces;
using VirtualBar.Domain.Entities;
using VirtualBar.Domain.Enums;
using VirtualBar.Infrastructure.Decorators;
using VirtualBar.Infrastructure.Persistence;
using VirtualBar.Infrastructure.Services;

namespace VirtualBar.Tests.Integration;

/// <summary>
/// End-to-end coverage of a deal in two steps (#130): a buyer makes an offer, the seller accepts it and the bottle is
/// reserved, then the buyer confirms receiving it, or one side cancels. Like
/// <see cref="ProductRequestApprovalFlowTests"/>, it wires the REAL <see cref="NotificationService"/> and
/// <see cref="BadgeService"/> behind their decorators, one graph per acting user. SQLite, because accepting,
/// confirming and cancelling are conditional updates in a transaction.
/// </summary>
public sealed class OfferDealFlowTests
{
    private static AppDbContext CreateDbContext()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
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

    /// <summary>One request scope: the full service graph acting as a single user.</summary>
    private sealed record Scope(IOfferService Offers, Mock<ILogger<BadgeService>> BadgeLogger);

    private static Scope CreateScope(AppDbContext db, Guid currentUserId)
    {
        var currentUser = CreateCurrentUser(currentUserId);

        var notifications = new NotificationValidationDecorator(
            new NotificationService(db, currentUser), db, currentUser);

        var badgeLogger = new Mock<ILogger<BadgeService>>();
        var badges = new BadgeValidationDecorator(
            new BadgeService(db, currentUser, notifications, badgeLogger.Object), db);

        var offers = new OfferValidationDecorator(
            new OfferService(
                db, currentUser, notifications, badges, Mock.Of<IWebHostEnvironment>(), NullLogger<OfferService>.Instance),
            db,
            currentUser);

        return new Scope(offers, badgeLogger);
    }

    private static AppUser SeedUser(AppDbContext db, string displayName)
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

    private static Bottle SeedListedBottle(AppDbContext db, Guid sellerId)
    {
        var bottle = new Bottle
        {
            UserId = sellerId,
            Name = "Springbank 21",
            Category = SpiritCategory.Whisky,
            Condition = BottleCondition.Sealed,
            Quantity = 1,
            IsForSale = true,
            ForSaleQuantity = 1,
            AskingPrice = 900m,
            Currency = "EUR",
            ForSaleAt = DateTime.UtcNow
        };
        db.Bottles.Add(bottle);
        db.SaveChanges();
        return bottle;
    }

    private static void AssertNoBadgeEngineErrors(Mock<ILogger<BadgeService>> logger) =>
        logger.Verify(
            l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                (Func<It.IsAnyType, Exception?, string>)It.IsAny<object>()),
            Times.Never);

    private static Task<List<NotificationType>> NotificationsOfAsync(AppDbContext db, Guid userId) =>
        db.Notifications.AsNoTracking().Where(n => n.UserId == userId).Select(n => n.Type).ToListAsync();

    [Fact]
    public async Task ConfirmedDeal_MovesTheBottleToTheBuyerNotifiesBothAndAwardsTheDealBadges()
    {
        var db = CreateDbContext();
        var seller = SeedUser(db, "Seller");
        var buyer = SeedUser(db, "Buyer");
        var bottle = SeedListedBottle(db, seller.Id);
        var sellerScope = CreateScope(db, seller.Id);
        var buyerScope = CreateScope(db, buyer.Id);

        // 1. The buyer makes an offer, and the seller accepts it: the bottle is reserved and taken off sale.
        var offer = await buyerScope.Offers.CreateAsync(
            new CreateOfferRequest { BottleId = bottle.Id, OfferedPrice = 850m, Currency = "EUR" }, CancellationToken.None);
        var accepted = await sellerScope.Offers.AcceptAsync(offer.Data!.Id, CancellationToken.None);

        Assert.Equal(OfferStatus.AwaitingReceipt, accepted.Data!.Status);
        db.ChangeTracker.Clear();
        var reserved = await db.Bottles.AsNoTracking().SingleAsync(b => b.Id == bottle.Id);
        Assert.Equal(1, reserved.ReservedQuantity);
        Assert.False(reserved.IsForSale);
        Assert.Empty(await db.UserBadges.AsNoTracking().ToListAsync());

        // 2. The buyer confirms they received it.
        var completed = await buyerScope.Offers.ConfirmReceiptAsync(offer.Data.Id, CancellationToken.None);

        Assert.True(completed.Success);
        Assert.Equal(OfferStatus.Accepted, completed.Data!.Status);
        AssertNoBadgeEngineErrors(sellerScope.BadgeLogger);
        AssertNoBadgeEngineErrors(buyerScope.BadgeLogger);

        // 3. The bottle left the seller's bar and joined the buyer's.
        db.ChangeTracker.Clear();
        Assert.True((await db.Bottles.AsNoTracking().SingleAsync(b => b.Id == bottle.Id)).IsDeleted);
        var bought = await db.Bottles.AsNoTracking().SingleAsync(b => b.UserId == buyer.Id);
        Assert.Equal("Springbank 21", bought.Name);
        Assert.Equal(offer.Data.Id, bought.AcquiredFromOfferId);

        // 4. The completed deal awarded the deal badges, and the buyer's first bottle its own.
        var badges = await db.UserBadges.AsNoTracking().ToListAsync();
        Assert.Contains(badges, b => b.UserId == seller.Id && b.Badge == BadgeType.FirstSale);
        Assert.Contains(badges, b => b.UserId == buyer.Id && b.Badge == BadgeType.FirstPurchase);
        Assert.Contains(badges, b => b.UserId == buyer.Id && b.Badge == BadgeType.FirstBottle);

        // 5. Each side heard about every step that concerned them.
        var sellerNotifications = await NotificationsOfAsync(db, seller.Id);
        Assert.Contains(NotificationType.OfferReceived, sellerNotifications);
        Assert.Contains(NotificationType.SaleCompleted, sellerNotifications);
        Assert.Contains(NotificationType.BadgeEarned, sellerNotifications);
        var buyerNotifications = await NotificationsOfAsync(db, buyer.Id);
        Assert.Contains(NotificationType.OfferAccepted, buyerNotifications);
        Assert.Equal(2, buyerNotifications.Count(t => t == NotificationType.BadgeEarned));
    }

    [Fact]
    public async Task CancelledDeal_LeavesTheBottleWithTheSellerAndTellsTheOtherSide()
    {
        var db = CreateDbContext();
        var seller = SeedUser(db, "Seller");
        var buyer = SeedUser(db, "Buyer");
        var bottle = SeedListedBottle(db, seller.Id);
        var sellerScope = CreateScope(db, seller.Id);
        var buyerScope = CreateScope(db, buyer.Id);

        var offer = await buyerScope.Offers.CreateAsync(
            new CreateOfferRequest { BottleId = bottle.Id, OfferedPrice = 850m, Currency = "EUR" }, CancellationToken.None);
        await sellerScope.Offers.AcceptAsync(offer.Data!.Id, CancellationToken.None);
        db.ChangeTracker.Clear(); // the next request reads the offer fresh, as a new request scope would

        var cancelled = await buyerScope.Offers.CancelAsync(offer.Data.Id, CancellationToken.None);

        Assert.True(cancelled.Success);
        Assert.Equal(OfferStatus.Cancelled, cancelled.Data!.Status);
        db.ChangeTracker.Clear();
        var stored = await db.Bottles.AsNoTracking().SingleAsync(b => b.Id == bottle.Id);
        Assert.False(stored.IsDeleted);
        Assert.Equal(0, stored.ReservedQuantity);
        Assert.False(await db.Bottles.AnyAsync(b => b.UserId == buyer.Id));
        Assert.Empty(await db.UserBadges.AsNoTracking().ToListAsync());
        Assert.Contains(NotificationType.DealCancelled, await NotificationsOfAsync(db, seller.Id));
    }
}
