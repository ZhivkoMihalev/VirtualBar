using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using VirtualBar.Application.DTOs.Reviews;
using VirtualBar.Application.Interfaces;
using VirtualBar.Domain.Entities;
using VirtualBar.Domain.Enums;
using VirtualBar.Infrastructure.Decorators;
using VirtualBar.Infrastructure.Persistence;
using VirtualBar.Infrastructure.Services;

namespace VirtualBar.Tests.Integration;

/// <summary>
/// End-to-end coverage for "a collector writes a review → earns the review badge → is told" (#125). Like the other
/// flow tests, it wires the REAL <see cref="NotificationService"/> and <see cref="BadgeService"/> behind their
/// decorators, exactly as <c>DependencyInjection.cs</c> does, one graph per acting user (a request scope).
/// </summary>
public sealed class ReviewBadgeFlowTests
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

    /// <summary>One request scope: the full service graph acting as a single user.</summary>
    private sealed record Scope(
        IBottleReviewService Reviews,
        INotificationService Notifications,
        IBadgeService Badges,
        Mock<ILogger<BadgeService>> BadgeLogger);

    private static Scope CreateScope(AppDbContext db, Guid currentUserId)
    {
        var currentUser = CreateCurrentUser(currentUserId);

        var notifications = new NotificationValidationDecorator(
            new NotificationService(db, currentUser), db, currentUser);

        // The engine swallows and logs its own exceptions, so the logger is asserted on: a silent failure would
        // otherwise read as "no badge awarded" with no explanation.
        var badgeLogger = new Mock<ILogger<BadgeService>>();
        var badges = new BadgeValidationDecorator(
            new BadgeService(db, currentUser, notifications, badgeLogger.Object), db);

        var reviews = new BottleReviewValidationDecorator(
            new BottleReviewService(db, currentUser, notifications, badges), db, currentUser);

        return new Scope(reviews, notifications, badges, badgeLogger);
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

    private static Bottle SeedBottle(AppDbContext db, Guid ownerId, string name = "Lagavulin 16")
    {
        var bottle = new Bottle
        {
            UserId = ownerId,
            Name = name,
            Category = SpiritCategory.Whisky,
            Condition = BottleCondition.Sealed
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

    [Fact]
    public async Task FirstReview_NotifiesTheOwnerAndAwardsTheReviewerFirstReview()
    {
        var db = CreateDbContext();
        var owner = SeedUser(db, "Owner");
        var reviewer = SeedUser(db, "Reviewer");
        var bottle = SeedBottle(db, owner.Id);
        var reviewerScope = CreateScope(db, reviewer.Id);

        var result = await reviewerScope.Reviews.AddReviewAsync(
            bottle.Id, new AddReviewRequest { Score = 91, Summary = "Smoke and brine." }, CancellationToken.None);

        Assert.True(result.Success);
        AssertNoBadgeEngineErrors(reviewerScope.BadgeLogger);

        // The reviewer earned FirstReview; the owner earned nothing.
        var badge = Assert.Single(await db.UserBadges.AsNoTracking().ToListAsync());
        Assert.Equal(reviewer.Id, badge.UserId);
        Assert.Equal(BadgeType.FirstReview, badge.Badge);

        // The owner is told about the review, the reviewer about the badge (a system notification: no resource id,
        // the enum name the frontend translates).
        var notifications = await db.Notifications.AsNoTracking().ToListAsync();
        Assert.Equal(2, notifications.Count);
        var reviewed = Assert.Single(notifications, n => n.Type == NotificationType.BottleReviewed);
        Assert.Equal(owner.Id, reviewed.UserId);
        var earned = Assert.Single(notifications, n => n.Type == NotificationType.BadgeEarned);
        Assert.Equal(reviewer.Id, earned.UserId);
        Assert.Null(earned.ResourceId);
        Assert.Equal(nameof(BadgeType.FirstReview), earned.ResourceName);

        // What GET /api/badges/progress returns to the reviewer: FirstReview earned, Reviewer10 at 1 of 10.
        var progress = await reviewerScope.Badges.GetMyProgressAsync(CancellationToken.None);
        Assert.True(Assert.Single(progress.Data!, p => p.Badge == BadgeType.FirstReview).Earned);
        var reviewer10 = Assert.Single(progress.Data!, p => p.Badge == BadgeType.Reviewer10);
        Assert.False(reviewer10.Earned);
        Assert.Equal(1, reviewer10.Current);
        Assert.Equal(10, reviewer10.Threshold);
    }

    /// <summary>A review of one's own bottle counts (#125): no "reviewed" notification to oneself, but the badge.</summary>
    [Fact]
    public async Task ReviewOfOwnBottle_AwardsFirstReviewWithoutNotifyingAboutTheReview()
    {
        var db = CreateDbContext();
        var collector = SeedUser(db, "Collector");
        var bottle = SeedBottle(db, collector.Id);
        var scope = CreateScope(db, collector.Id);

        var result = await scope.Reviews.AddReviewAsync(bottle.Id, new AddReviewRequest { Score = 88 }, CancellationToken.None);

        Assert.True(result.Success);
        AssertNoBadgeEngineErrors(scope.BadgeLogger);
        Assert.Equal(BadgeType.FirstReview, Assert.Single(await db.UserBadges.AsNoTracking().ToListAsync()).Badge);
        var notification = Assert.Single(await db.Notifications.AsNoTracking().ToListAsync());
        Assert.Equal(NotificationType.BadgeEarned, notification.Type);
        Assert.Equal(nameof(BadgeType.FirstReview), notification.ResourceName);
    }

    [Fact]
    public async Task SecondReview_DoesNotReawardFirstReviewOrRenotify()
    {
        var db = CreateDbContext();
        var owner = SeedUser(db, "Owner");
        var reviewer = SeedUser(db, "Reviewer");
        var first = SeedBottle(db, owner.Id, "Lagavulin 16");
        var second = SeedBottle(db, owner.Id, "Ardbeg 10");
        var reviewerScope = CreateScope(db, reviewer.Id);

        await reviewerScope.Reviews.AddReviewAsync(first.Id, new AddReviewRequest { Score = 90 }, CancellationToken.None);
        var result = await reviewerScope.Reviews.AddReviewAsync(second.Id, new AddReviewRequest { Score = 85 }, CancellationToken.None);

        Assert.True(result.Success);
        AssertNoBadgeEngineErrors(reviewerScope.BadgeLogger);
        Assert.Single(await db.UserBadges.AsNoTracking().ToListAsync());
        Assert.Single(await db.Notifications.AsNoTracking().Where(n => n.Type == NotificationType.BadgeEarned).ToListAsync());
    }
}
