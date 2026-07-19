using System.Reflection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using VirtualBar.Application.Common;
using VirtualBar.Application.Interfaces;
using VirtualBar.Domain.Common;
using VirtualBar.Domain.Entities;
using VirtualBar.Domain.Enums;
using VirtualBar.Infrastructure.Decorators;
using VirtualBar.Infrastructure.Persistence;
using VirtualBar.Infrastructure.Services;

namespace VirtualBar.Tests.Services;

public sealed class BadgeServiceTests
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

    private static IBadgeService CreateBadgeService(
        AppDbContext db,
        Guid currentUserId,
        INotificationService? notificationService = null,
        ILogger<BadgeService>? logger = null)
    {
        var inner = new BadgeService(
            db,
            CreateCurrentUser(currentUserId),
            notificationService ?? Mock.Of<INotificationService>(),
            logger ?? Mock.Of<ILogger<BadgeService>>());
        return new BadgeValidationDecorator(inner, db);
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

    private static Bottle SeedBottle(
        AppDbContext db,
        Guid userId,
        SpiritCategory category = SpiritCategory.Whisky,
        bool isLimited = false,
        bool isForSale = false,
        bool isDeleted = false)
    {
        var bottle = new Bottle
        {
            UserId = userId,
            Name = "Lagavulin 16",
            Category = category,
            Condition = BottleCondition.Sealed,
            IsLimited = isLimited,
            IsForSale = isForSale,
            IsDeleted = isDeleted,
            DeletedAt = isDeleted ? DateTime.UtcNow : null
        };
        db.Bottles.Add(bottle);
        db.SaveChanges();
        return bottle;
    }

    private static BottleLike SeedLike(AppDbContext db, Guid bottleId, Guid userId)
    {
        var like = new BottleLike
        {
            BottleId = bottleId,
            UserId = userId,
            LikedAt = DateTime.UtcNow
        };
        db.BottleLikes.Add(like);
        db.SaveChanges();
        return like;
    }

    private static UserFollow SeedFollow(AppDbContext db, Guid followerId, Guid followedId)
    {
        var follow = new UserFollow
        {
            FollowerId = followerId,
            FollowedId = followedId,
            FollowedAt = DateTime.UtcNow
        };
        db.UserFollows.Add(follow);
        db.SaveChanges();
        return follow;
    }

    private static Offer SeedOffer(
        AppDbContext db,
        Guid bottleId,
        Guid buyerId,
        Guid sellerId,
        OfferStatus status = OfferStatus.Accepted,
        bool isDeleted = false)
    {
        var offer = new Offer
        {
            BottleId = bottleId,
            BuyerId = buyerId,
            SellerId = sellerId,
            OfferedPrice = 100m,
            Currency = "USD",
            Status = status,
            IsDeleted = isDeleted,
            DeletedAt = isDeleted ? DateTime.UtcNow : null
        };
        db.Offers.Add(offer);
        db.SaveChanges();
        return offer;
    }

    private static UserBadge SeedBadge(AppDbContext db, Guid userId, BadgeType badge, DateTime? awardedAt = null)
    {
        var userBadge = new UserBadge
        {
            UserId = userId,
            Badge = badge,
            AwardedAt = awardedAt ?? DateTime.UtcNow
        };
        db.UserBadges.Add(userBadge);
        db.SaveChanges();
        return userBadge;
    }

    private static void VerifyAwarded(Mock<INotificationService> notificationMock, Guid userId, BadgeType badge, Times times) =>
        notificationMock.Verify(n => n.CreateSystemAsync(
            userId, NotificationType.BadgeEarned, null, badge.ToString(), It.IsAny<CancellationToken>()), times);

    #region EvaluateAsync — thresholds & catch-up

    [Fact]
    public async Task EvaluateAsync_WhenFifthBottleAdded_AwardsFirstBottleAndCollector5()
    {
        var db = CreateDbContext();
        var user = SeedUser(db);
        for (var i = 0; i < 5; i++)
            SeedBottle(db, user.Id);
        var notificationMock = new Mock<INotificationService>();
        var service = CreateBadgeService(db, user.Id, notificationMock.Object);

        await service.EvaluateAsync(user.Id, BadgeTrigger.BottleAdded, CancellationToken.None);

        var badges = await db.UserBadges.Where(b => b.UserId == user.Id).Select(b => b.Badge).ToListAsync();
        Assert.Contains(BadgeType.FirstBottle, badges);
        Assert.Contains(BadgeType.Collector5, badges);
        Assert.DoesNotContain(BadgeType.Collector10, badges);
        VerifyAwarded(notificationMock, user.Id, BadgeType.FirstBottle, Times.Once());
        VerifyAwarded(notificationMock, user.Id, BadgeType.Collector5, Times.Once());
        VerifyAwarded(notificationMock, user.Id, BadgeType.Collector10, Times.Never());
    }

    [Fact]
    public async Task EvaluateAsync_WhenFourthBottleAdded_AwardsFirstBottleButNotCollector5()
    {
        var db = CreateDbContext();
        var user = SeedUser(db);
        for (var i = 0; i < 4; i++)
            SeedBottle(db, user.Id);
        var notificationMock = new Mock<INotificationService>();
        var service = CreateBadgeService(db, user.Id, notificationMock.Object);

        await service.EvaluateAsync(user.Id, BadgeTrigger.BottleAdded, CancellationToken.None);

        var badges = await db.UserBadges.Where(b => b.UserId == user.Id).Select(b => b.Badge).ToListAsync();
        Assert.Contains(BadgeType.FirstBottle, badges);
        Assert.DoesNotContain(BadgeType.Collector5, badges);
        VerifyAwarded(notificationMock, user.Id, BadgeType.FirstBottle, Times.Once());
        VerifyAwarded(notificationMock, user.Id, BadgeType.Collector5, Times.Never());
    }

    [Fact]
    public async Task EvaluateAsync_WhenFiftyBottlesAndNoBadges_AwardsAllCollectionBadgesUpToFiftyOnly()
    {
        var db = CreateDbContext();
        var user = SeedUser(db);
        for (var i = 0; i < 50; i++)
            SeedBottle(db, user.Id);
        var notificationMock = new Mock<INotificationService>();
        var service = CreateBadgeService(db, user.Id, notificationMock.Object);

        await service.EvaluateAsync(user.Id, BadgeTrigger.BottleAdded, CancellationToken.None);

        var badges = await db.UserBadges.Where(b => b.UserId == user.Id).Select(b => b.Badge).OrderBy(b => b).ToListAsync();
        Assert.Equal(
            new[] { BadgeType.FirstBottle, BadgeType.Collector5, BadgeType.Collector10, BadgeType.Collector25, BadgeType.Collector50 }.OrderBy(b => b),
            badges);
        VerifyAwarded(notificationMock, user.Id, BadgeType.FirstBottle, Times.Once());
        VerifyAwarded(notificationMock, user.Id, BadgeType.Collector5, Times.Once());
        VerifyAwarded(notificationMock, user.Id, BadgeType.Collector10, Times.Once());
        VerifyAwarded(notificationMock, user.Id, BadgeType.Collector25, Times.Once());
        VerifyAwarded(notificationMock, user.Id, BadgeType.Collector50, Times.Once());
        VerifyAwarded(notificationMock, user.Id, BadgeType.Collector100, Times.Never());
        VerifyAwarded(notificationMock, user.Id, BadgeType.Explorer3, Times.Never());
        VerifyAwarded(notificationMock, user.Id, BadgeType.LimitedHunter, Times.Never());
        notificationMock.Verify(n => n.CreateSystemAsync(
            user.Id, NotificationType.BadgeEarned, null, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(5));
    }

    [Fact]
    public async Task EvaluateAsync_WhenAllTriggerBadgesAlreadyEarned_ReturnsWithoutAwarding()
    {
        // BottleListed maps to a single definition (FirstListing); pre-seeding it empties `missing`,
        // exercising the hot-path early exit.
        var db = CreateDbContext();
        var user = SeedUser(db);
        SeedBottle(db, user.Id, isForSale: true);
        SeedBadge(db, user.Id, BadgeType.FirstListing);
        var notificationMock = new Mock<INotificationService>();
        var service = CreateBadgeService(db, user.Id, notificationMock.Object);

        await service.EvaluateAsync(user.Id, BadgeTrigger.BottleListed, CancellationToken.None);

        Assert.Equal(1, await db.UserBadges.CountAsync(b => b.UserId == user.Id));
        notificationMock.Verify(n => n.CreateSystemAsync(
            It.IsAny<Guid>(), It.IsAny<NotificationType>(), It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EvaluateAsync_WhenBadgeAlreadyEarned_DoesNotReawardOrRenotify()
    {
        var db = CreateDbContext();
        var user = SeedUser(db);
        SeedBottle(db, user.Id);
        SeedBadge(db, user.Id, BadgeType.FirstBottle);
        var notificationMock = new Mock<INotificationService>();
        var service = CreateBadgeService(db, user.Id, notificationMock.Object);

        await service.EvaluateAsync(user.Id, BadgeTrigger.BottleAdded, CancellationToken.None);

        Assert.Equal(1, await db.UserBadges.CountAsync(b => b.UserId == user.Id));
        VerifyAwarded(notificationMock, user.Id, BadgeType.FirstBottle, Times.Never());
    }

    [Fact]
    public async Task EvaluateAsync_WhenPkRaceLost_SwallowsDetachesAndAwardsRemaining()
    {
        // SQLite so the composite (UserId, Badge) PK is really enforced: EF Core InMemory throws
        // ArgumentException (not DbUpdateException) on a duplicate PK, which the service's
        // catch(DbUpdateException) would NOT swallow — so the race path needs a real relational store,
        // exactly like the BottleLike/Offer race tests.
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        await using var db = new AppDbContext(options);
        db.Database.EnsureCreated();

        var user = SeedUser(db);
        for (var i = 0; i < 10; i++)
            SeedBottle(db, user.Id);

        // A concurrent trigger wins the race for Collector5 between the earned-set load and this run's save:
        // it inserts the row via a SEPARATE context on the same connection while FirstBottle is notifying.
        var notificationMock = new Mock<INotificationService>();
        notificationMock
            .Setup(n => n.CreateSystemAsync(user.Id, NotificationType.BadgeEarned, null, BadgeType.FirstBottle.ToString(), It.IsAny<CancellationToken>()))
            .Callback(() =>
            {
                using var concurrent = new AppDbContext(options);
                concurrent.UserBadges.Add(new UserBadge { UserId = user.Id, Badge = BadgeType.Collector5, AwardedAt = DateTime.UtcNow });
                concurrent.SaveChanges();
            })
            .Returns(Task.CompletedTask);
        var loggerMock = new Mock<ILogger<BadgeService>>();
        var inner = new BadgeService(db, CreateCurrentUser(user.Id), notificationMock.Object, loggerMock.Object);

        await inner.EvaluateAsync(user.Id, BadgeTrigger.BottleAdded, CancellationToken.None);

        db.ChangeTracker.Clear();
        var badges = await db.UserBadges.Where(b => b.UserId == user.Id).Select(b => b.Badge).ToListAsync();
        Assert.Contains(BadgeType.FirstBottle, badges);
        Assert.Contains(BadgeType.Collector5, badges);
        Assert.Contains(BadgeType.Collector10, badges);
        Assert.Equal(3, badges.Count);

        VerifyAwarded(notificationMock, user.Id, BadgeType.FirstBottle, Times.Once());
        VerifyAwarded(notificationMock, user.Id, BadgeType.Collector5, Times.Never());
        VerifyAwarded(notificationMock, user.Id, BadgeType.Collector10, Times.Once());
    }

    [Fact]
    public async Task EvaluateAsync_WhenNotificationThrows_SwallowsExceptionAndLogsError()
    {
        var db = CreateDbContext();
        var user = SeedUser(db);
        SeedBottle(db, user.Id);
        var notificationMock = new Mock<INotificationService>();
        notificationMock
            .Setup(n => n.CreateSystemAsync(It.IsAny<Guid>(), It.IsAny<NotificationType>(), It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));
        var loggerMock = new Mock<ILogger<BadgeService>>();
        var service = CreateBadgeService(db, user.Id, notificationMock.Object, loggerMock.Object);

        var exception = await Record.ExceptionAsync(
            () => service.EvaluateAsync(user.Id, BadgeTrigger.BottleAdded, CancellationToken.None));

        Assert.Null(exception);
        loggerMock.Verify(
            l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                (Func<It.IsAnyType, Exception?, string>)It.IsAny<object>()),
            Times.Once);
    }

    #endregion

    #region EvaluateAsync — count kinds

    [Fact]
    public async Task EvaluateAsync_WhenBottlesSoftDeleted_ExcludesThemFromBottleCount()
    {
        var db = CreateDbContext();
        var user = SeedUser(db);
        for (var i = 0; i < 4; i++)
            SeedBottle(db, user.Id);
        SeedBottle(db, user.Id, isDeleted: true);
        var notificationMock = new Mock<INotificationService>();
        var service = CreateBadgeService(db, user.Id, notificationMock.Object);

        await service.EvaluateAsync(user.Id, BadgeTrigger.BottleAdded, CancellationToken.None);

        var badges = await db.UserBadges.Where(b => b.UserId == user.Id).Select(b => b.Badge).ToListAsync();
        Assert.Contains(BadgeType.FirstBottle, badges);
        Assert.DoesNotContain(BadgeType.Collector5, badges);
    }

    [Fact]
    public async Task EvaluateAsync_WhenCategoriesRepeat_CountsDistinctCategories()
    {
        var db = CreateDbContext();
        var user = SeedUser(db);
        SeedBottle(db, user.Id, SpiritCategory.Whisky);
        SeedBottle(db, user.Id, SpiritCategory.Whisky);
        SeedBottle(db, user.Id, SpiritCategory.Rum);
        var notificationMock = new Mock<INotificationService>();
        var service = CreateBadgeService(db, user.Id, notificationMock.Object);

        await service.EvaluateAsync(user.Id, BadgeTrigger.BottleAdded, CancellationToken.None);

        var badges = await db.UserBadges.Where(b => b.UserId == user.Id).Select(b => b.Badge).ToListAsync();
        Assert.DoesNotContain(BadgeType.Explorer3, badges);
    }

    [Fact]
    public async Task EvaluateAsync_WhenThreeDistinctCategories_AwardsExplorer3()
    {
        var db = CreateDbContext();
        var user = SeedUser(db);
        SeedBottle(db, user.Id, SpiritCategory.Whisky);
        SeedBottle(db, user.Id, SpiritCategory.Rum);
        SeedBottle(db, user.Id, SpiritCategory.Gin);
        var notificationMock = new Mock<INotificationService>();
        var service = CreateBadgeService(db, user.Id, notificationMock.Object);

        await service.EvaluateAsync(user.Id, BadgeTrigger.BottleAdded, CancellationToken.None);

        var badges = await db.UserBadges.Where(b => b.UserId == user.Id).Select(b => b.Badge).ToListAsync();
        Assert.Contains(BadgeType.Explorer3, badges);
        Assert.DoesNotContain(BadgeType.Explorer5, badges);
        VerifyAwarded(notificationMock, user.Id, BadgeType.Explorer3, Times.Once());
    }

    [Fact]
    public async Task EvaluateAsync_WhenFiveLimitedBottles_AwardsLimitedHunter()
    {
        var db = CreateDbContext();
        var user = SeedUser(db);
        for (var i = 0; i < 5; i++)
            SeedBottle(db, user.Id, isLimited: true);
        var notificationMock = new Mock<INotificationService>();
        var service = CreateBadgeService(db, user.Id, notificationMock.Object);

        await service.EvaluateAsync(user.Id, BadgeTrigger.BottleAdded, CancellationToken.None);

        var badges = await db.UserBadges.Where(b => b.UserId == user.Id).Select(b => b.Badge).ToListAsync();
        Assert.Contains(BadgeType.LimitedHunter, badges);
        VerifyAwarded(notificationMock, user.Id, BadgeType.LimitedHunter, Times.Once());
    }

    [Fact]
    public async Task EvaluateAsync_WhenNonLimitedBottlesPresent_ExcludedFromLimitedCount()
    {
        var db = CreateDbContext();
        var user = SeedUser(db);
        for (var i = 0; i < 4; i++)
            SeedBottle(db, user.Id, isLimited: true);
        for (var i = 0; i < 5; i++)
            SeedBottle(db, user.Id, isLimited: false);
        var notificationMock = new Mock<INotificationService>();
        var service = CreateBadgeService(db, user.Id, notificationMock.Object);

        await service.EvaluateAsync(user.Id, BadgeTrigger.BottleAdded, CancellationToken.None);

        var badges = await db.UserBadges.Where(b => b.UserId == user.Id).Select(b => b.Badge).ToListAsync();
        Assert.DoesNotContain(BadgeType.LimitedHunter, badges);
    }

    [Fact]
    public async Task EvaluateAsync_WhenTenLikesReceived_AwardsLiked10()
    {
        var db = CreateDbContext();
        var owner = SeedUser(db, "Owner");
        var bottle = SeedBottle(db, owner.Id);
        for (var i = 0; i < 10; i++)
            SeedLike(db, bottle.Id, SeedUser(db, $"Liker{i}").Id);
        var notificationMock = new Mock<INotificationService>();
        var service = CreateBadgeService(db, owner.Id, notificationMock.Object);

        await service.EvaluateAsync(owner.Id, BadgeTrigger.LikeReceived, CancellationToken.None);

        var badges = await db.UserBadges.Where(b => b.UserId == owner.Id).Select(b => b.Badge).ToListAsync();
        Assert.Contains(BadgeType.Liked10, badges);
        Assert.DoesNotContain(BadgeType.Liked50, badges);
        VerifyAwarded(notificationMock, owner.Id, BadgeType.Liked10, Times.Once());
    }

    [Fact]
    public async Task EvaluateAsync_WhenLikesOnSoftDeletedBottle_ExcludedFromLikeCount()
    {
        var db = CreateDbContext();
        var owner = SeedUser(db, "Owner");
        var liveBottle = SeedBottle(db, owner.Id);
        var deletedBottle = SeedBottle(db, owner.Id, isDeleted: true);
        for (var i = 0; i < 9; i++)
            SeedLike(db, liveBottle.Id, SeedUser(db, $"Live{i}").Id);
        for (var i = 0; i < 5; i++)
            SeedLike(db, deletedBottle.Id, SeedUser(db, $"Dead{i}").Id);
        var notificationMock = new Mock<INotificationService>();
        var service = CreateBadgeService(db, owner.Id, notificationMock.Object);

        await service.EvaluateAsync(owner.Id, BadgeTrigger.LikeReceived, CancellationToken.None);

        var badges = await db.UserBadges.Where(b => b.UserId == owner.Id).Select(b => b.Badge).ToListAsync();
        Assert.DoesNotContain(BadgeType.Liked10, badges);
    }

    [Fact]
    public async Task EvaluateAsync_WhenFirstFollowerGained_AwardsFirstFollower()
    {
        var db = CreateDbContext();
        var target = SeedUser(db, "Target");
        var follower = SeedUser(db, "Follower");
        SeedFollow(db, follower.Id, target.Id);
        var notificationMock = new Mock<INotificationService>();
        var service = CreateBadgeService(db, target.Id, notificationMock.Object);

        await service.EvaluateAsync(target.Id, BadgeTrigger.FollowerGained, CancellationToken.None);

        var badges = await db.UserBadges.Where(b => b.UserId == target.Id).Select(b => b.Badge).ToListAsync();
        Assert.Contains(BadgeType.FirstFollower, badges);
        Assert.DoesNotContain(BadgeType.Popular10, badges);
        VerifyAwarded(notificationMock, target.Id, BadgeType.FirstFollower, Times.Once());
    }

    [Fact]
    public async Task EvaluateAsync_WhenBottleForSale_AwardsFirstListing()
    {
        var db = CreateDbContext();
        var user = SeedUser(db);
        SeedBottle(db, user.Id, isForSale: true);
        var notificationMock = new Mock<INotificationService>();
        var service = CreateBadgeService(db, user.Id, notificationMock.Object);

        await service.EvaluateAsync(user.Id, BadgeTrigger.BottleListed, CancellationToken.None);

        var badges = await db.UserBadges.Where(b => b.UserId == user.Id).Select(b => b.Badge).ToListAsync();
        Assert.Contains(BadgeType.FirstListing, badges);
        VerifyAwarded(notificationMock, user.Id, BadgeType.FirstListing, Times.Once());
    }

    [Fact]
    public async Task EvaluateAsync_WhenBottleNotForSale_DoesNotAwardFirstListing()
    {
        var db = CreateDbContext();
        var user = SeedUser(db);
        SeedBottle(db, user.Id, isForSale: false);
        var notificationMock = new Mock<INotificationService>();
        var service = CreateBadgeService(db, user.Id, notificationMock.Object);

        await service.EvaluateAsync(user.Id, BadgeTrigger.BottleListed, CancellationToken.None);

        Assert.Equal(0, await db.UserBadges.CountAsync(b => b.UserId == user.Id));
        VerifyAwarded(notificationMock, user.Id, BadgeType.FirstListing, Times.Never());
    }

    [Fact]
    public async Task EvaluateAsync_WhenAcceptedSaleAsSeller_AwardsFirstSaleNotFirstPurchase()
    {
        var db = CreateDbContext();
        var seller = SeedUser(db, "Seller");
        var buyer = SeedUser(db, "Buyer");
        var bottle = SeedBottle(db, seller.Id);
        SeedOffer(db, bottle.Id, buyer.Id, seller.Id, OfferStatus.Accepted);
        var notificationMock = new Mock<INotificationService>();
        var service = CreateBadgeService(db, seller.Id, notificationMock.Object);

        await service.EvaluateAsync(seller.Id, BadgeTrigger.OfferAccepted, CancellationToken.None);

        var badges = await db.UserBadges.Where(b => b.UserId == seller.Id).Select(b => b.Badge).ToListAsync();
        Assert.Contains(BadgeType.FirstSale, badges);
        Assert.DoesNotContain(BadgeType.FirstPurchase, badges);
        VerifyAwarded(notificationMock, seller.Id, BadgeType.FirstSale, Times.Once());
    }

    [Fact]
    public async Task EvaluateAsync_WhenSalesNotAcceptedOrDeleted_DoesNotAwardFirstSale()
    {
        var db = CreateDbContext();
        var seller = SeedUser(db, "Seller");
        var buyer = SeedUser(db, "Buyer");
        var bottle = SeedBottle(db, seller.Id);
        SeedOffer(db, bottle.Id, buyer.Id, seller.Id, OfferStatus.Accepted, isDeleted: true);
        SeedOffer(db, bottle.Id, buyer.Id, seller.Id, OfferStatus.Pending);
        SeedOffer(db, bottle.Id, buyer.Id, seller.Id, OfferStatus.Declined);
        var notificationMock = new Mock<INotificationService>();
        var service = CreateBadgeService(db, seller.Id, notificationMock.Object);

        await service.EvaluateAsync(seller.Id, BadgeTrigger.OfferAccepted, CancellationToken.None);

        Assert.Equal(0, await db.UserBadges.CountAsync(b => b.UserId == seller.Id));
        VerifyAwarded(notificationMock, seller.Id, BadgeType.FirstSale, Times.Never());
    }

    [Fact]
    public async Task EvaluateAsync_WhenAcceptedPurchaseAsBuyer_AwardsFirstPurchaseNotFirstSale()
    {
        var db = CreateDbContext();
        var seller = SeedUser(db, "Seller");
        var buyer = SeedUser(db, "Buyer");
        var bottle = SeedBottle(db, seller.Id);
        SeedOffer(db, bottle.Id, buyer.Id, seller.Id, OfferStatus.Accepted);
        var notificationMock = new Mock<INotificationService>();
        var service = CreateBadgeService(db, buyer.Id, notificationMock.Object);

        await service.EvaluateAsync(buyer.Id, BadgeTrigger.OfferAccepted, CancellationToken.None);

        var badges = await db.UserBadges.Where(b => b.UserId == buyer.Id).Select(b => b.Badge).ToListAsync();
        Assert.Contains(BadgeType.FirstPurchase, badges);
        Assert.DoesNotContain(BadgeType.FirstSale, badges);
        VerifyAwarded(notificationMock, buyer.Id, BadgeType.FirstPurchase, Times.Once());
    }

    [Fact]
    public async Task CountAsync_WhenCountKindOutOfRange_ReturnsZero()
    {
        // The switch's defensive `_` default arm is unreachable through the catalog (all eight kinds are
        // mapped), so invoke the private counter directly with an undefined value to cover it.
        var db = CreateDbContext();
        var user = SeedUser(db);
        var service = new BadgeService(db, CreateCurrentUser(user.Id), Mock.Of<INotificationService>(), Mock.Of<ILogger<BadgeService>>());
        var method = typeof(BadgeService).GetMethod("CountAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;

        var task = (Task<int>)method.Invoke(service, [(BadgeCountKind)999, user.Id, CancellationToken.None])!;
        var count = await task;

        Assert.Equal(0, count);
    }

    #endregion

    #region EvaluateAsync — decorator guards

    [Fact]
    public async Task EvaluateAsync_WhenUserIdEmpty_ReturnsSilentlyWithoutEvaluating()
    {
        var db = CreateDbContext();
        for (var i = 0; i < 5; i++)
            SeedBottle(db, Guid.Empty);
        var notificationMock = new Mock<INotificationService>();
        var service = CreateBadgeService(db, Guid.NewGuid(), notificationMock.Object);

        await service.EvaluateAsync(Guid.Empty, BadgeTrigger.BottleAdded, CancellationToken.None);

        Assert.Equal(0, await db.UserBadges.CountAsync());
        notificationMock.Verify(n => n.CreateSystemAsync(
            It.IsAny<Guid>(), It.IsAny<NotificationType>(), It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EvaluateAsync_WhenTriggerUndefined_ReturnsSilentlyWithoutEvaluating()
    {
        var db = CreateDbContext();
        var user = SeedUser(db);
        for (var i = 0; i < 5; i++)
            SeedBottle(db, user.Id);
        var notificationMock = new Mock<INotificationService>();
        var service = CreateBadgeService(db, user.Id, notificationMock.Object);

        await service.EvaluateAsync(user.Id, (BadgeTrigger)999, CancellationToken.None);

        Assert.Equal(0, await db.UserBadges.CountAsync());
        notificationMock.Verify(n => n.CreateSystemAsync(
            It.IsAny<Guid>(), It.IsAny<NotificationType>(), It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EvaluateAsync_WithCancelledToken_Throws()
    {
        var db = CreateDbContext();
        var service = CreateBadgeService(db, Guid.NewGuid());
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => service.EvaluateAsync(Guid.NewGuid(), BadgeTrigger.BottleAdded, cts.Token));
    }

    #endregion

    #region GetUserBadgesAsync

    [Fact]
    public async Task GetUserBadgesAsync_WhenUserNotFound_ReturnsNotFound()
    {
        var db = CreateDbContext();
        var service = CreateBadgeService(db, Guid.NewGuid());

        var result = await service.GetUserBadgesAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ErrorCode.NotFound, result.ErrorCode);
        Assert.Equal("User not found.", result.Error);
    }

    [Fact]
    public async Task GetUserBadgesAsync_WhenNoBadges_ReturnsEmptyList()
    {
        var db = CreateDbContext();
        var user = SeedUser(db);
        var service = CreateBadgeService(db, user.Id);

        var result = await service.GetUserBadgesAsync(user.Id, CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Empty(result.Data);
    }

    [Fact]
    public async Task GetUserBadgesAsync_WhenMultipleBadges_ReturnsNewestFirst()
    {
        var db = CreateDbContext();
        var user = SeedUser(db);
        SeedBadge(db, user.Id, BadgeType.FirstBottle, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        SeedBadge(db, user.Id, BadgeType.Collector10, new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc));
        SeedBadge(db, user.Id, BadgeType.Collector5, new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc));
        var service = CreateBadgeService(db, user.Id);

        var result = await service.GetUserBadgesAsync(user.Id, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(
            new[] { BadgeType.Collector10, BadgeType.Collector5, BadgeType.FirstBottle },
            result.Data!.Select(b => b.Badge));
    }

    [Fact]
    public async Task GetUserBadgesAsync_ReturnsOnlyOwnBadges()
    {
        var db = CreateDbContext();
        var user = SeedUser(db, "User");
        var other = SeedUser(db, "Other");
        SeedBadge(db, user.Id, BadgeType.FirstBottle);
        SeedBadge(db, other.Id, BadgeType.Collector5);
        var service = CreateBadgeService(db, user.Id);

        var result = await service.GetUserBadgesAsync(user.Id, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Single(result.Data!);
        Assert.Equal(BadgeType.FirstBottle, result.Data![0].Badge);
    }

    [Fact]
    public async Task GetUserBadgesAsync_WithCancelledToken_Throws()
    {
        var db = CreateDbContext();
        var service = CreateBadgeService(db, Guid.NewGuid());
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => service.GetUserBadgesAsync(Guid.NewGuid(), cts.Token));
    }

    #endregion

    #region GetMyProgressAsync

    [Fact]
    public async Task GetMyProgressAsync_ReturnsAllEighteenRowsInCatalogOrder()
    {
        var db = CreateDbContext();
        var user = SeedUser(db);
        var service = CreateBadgeService(db, user.Id);

        var result = await service.GetMyProgressAsync(CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(18, result.Data!.Count);
        Assert.Equal(BadgeCatalog.All.Select(d => d.Type), result.Data.Select(p => p.Badge));
        Assert.Equal(BadgeCatalog.All.Select(d => d.Threshold), result.Data.Select(p => p.Threshold));
    }

    [Fact]
    public async Task GetMyProgressAsync_ComputesCurrentPerCountKind()
    {
        var db = CreateDbContext();
        var user = SeedUser(db);
        SeedBottle(db, user.Id, SpiritCategory.Whisky, isLimited: true);
        SeedBottle(db, user.Id, SpiritCategory.Whisky);
        SeedBottle(db, user.Id, SpiritCategory.Rum);
        SeedFollow(db, SeedUser(db, "F1").Id, user.Id);
        SeedFollow(db, SeedUser(db, "F2").Id, user.Id);
        var service = CreateBadgeService(db, user.Id);

        var result = await service.GetMyProgressAsync(CancellationToken.None);

        Assert.True(result.Success);
        var rows = result.Data!;
        Assert.Equal(3, rows.Single(p => p.Badge == BadgeType.FirstBottle).Current);
        Assert.Equal(3, rows.Single(p => p.Badge == BadgeType.Collector5).Current);
        Assert.Equal(2, rows.Single(p => p.Badge == BadgeType.Explorer3).Current);
        Assert.Equal(1, rows.Single(p => p.Badge == BadgeType.LimitedHunter).Current);
        Assert.Equal(2, rows.Single(p => p.Badge == BadgeType.FirstFollower).Current);
        Assert.Equal(2, rows.Single(p => p.Badge == BadgeType.Popular10).Current);
        Assert.Equal(0, rows.Single(p => p.Badge == BadgeType.Liked10).Current);
        Assert.Equal(0, rows.Single(p => p.Badge == BadgeType.FirstListing).Current);
        Assert.Equal(0, rows.Single(p => p.Badge == BadgeType.FirstSale).Current);
        Assert.Equal(0, rows.Single(p => p.Badge == BadgeType.FirstPurchase).Current);
    }

    [Fact]
    public async Task GetMyProgressAsync_WhenBadgeEarned_SetsEarnedAndAwardedAt()
    {
        var db = CreateDbContext();
        var user = SeedUser(db);
        SeedBottle(db, user.Id);
        var awardedAt = new DateTime(2026, 2, 2, 12, 0, 0, DateTimeKind.Utc);
        SeedBadge(db, user.Id, BadgeType.FirstBottle, awardedAt);
        var service = CreateBadgeService(db, user.Id);

        var result = await service.GetMyProgressAsync(CancellationToken.None);

        Assert.True(result.Success);
        var earnedRow = result.Data!.Single(p => p.Badge == BadgeType.FirstBottle);
        Assert.True(earnedRow.Earned);
        Assert.Equal(awardedAt, earnedRow.AwardedAt);
        Assert.Equal(1, earnedRow.Current);

        var unearnedRow = result.Data!.Single(p => p.Badge == BadgeType.Collector5);
        Assert.False(unearnedRow.Earned);
        Assert.Null(unearnedRow.AwardedAt);
    }

    [Fact]
    public async Task GetMyProgressAsync_WhenZeroState_AllUnearnedWithZeroCurrent()
    {
        var db = CreateDbContext();
        var user = SeedUser(db);
        var service = CreateBadgeService(db, user.Id);

        var result = await service.GetMyProgressAsync(CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(18, result.Data!.Count);
        Assert.All(result.Data, p =>
        {
            Assert.False(p.Earned);
            Assert.Equal(0, p.Current);
            Assert.Null(p.AwardedAt);
        });
    }

    [Fact]
    public async Task GetMyProgressAsync_WithCancelledToken_Throws()
    {
        var db = CreateDbContext();
        var service = CreateBadgeService(db, Guid.NewGuid());
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => service.GetMyProgressAsync(cts.Token));
    }

    #endregion
}
