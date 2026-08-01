using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using VirtualBar.Application.DTOs.ProductRequests;
using VirtualBar.Application.Interfaces;
using VirtualBar.Domain.Entities;
using VirtualBar.Domain.Enums;
using VirtualBar.Infrastructure.Decorators;
using VirtualBar.Infrastructure.Persistence;
using VirtualBar.Infrastructure.Services;

namespace VirtualBar.Tests.Integration;

/// <summary>
/// End-to-end coverage for "collector proposes a bottle → admin approves it → collector is told, and
/// gets the contributor badge". Unlike the per-service test classes, this one wires the REAL
/// <see cref="NotificationService"/> and <see cref="BadgeService"/> behind their decorators — mocking
/// them here would hide the very chain under test. The graph is assembled exactly as
/// <c>DependencyInjection.cs</c> assembles it, one graph per acting user (a request scope).
/// </summary>
public sealed class ProductRequestApprovalFlowTests
{
    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static ICurrentUser CreateCurrentUser(Guid userId, bool isAdmin = false)
    {
        var mock = new Mock<ICurrentUser>();
        mock.Setup(u => u.UserId).Returns(userId);
        mock.Setup(u => u.IsAuthenticated).Returns(true);
        mock.Setup(u => u.IsAdmin).Returns(isAdmin);
        return mock.Object;
    }

    /// <summary>One request scope: the full service graph acting as a single user.</summary>
    private sealed record Scope(
        IProductRequestService Requests,
        INotificationService Notifications,
        IBadgeService Badges,
        Mock<ILogger<BadgeService>> BadgeLogger);

    private static Scope CreateScope(AppDbContext db, Guid currentUserId, bool isAdmin = false)
    {
        var currentUser = CreateCurrentUser(currentUserId, isAdmin);

        var notifications = new NotificationValidationDecorator(
            new NotificationService(db, currentUser), db, currentUser);

        // The engine swallows its own exceptions and logs them, so the logger is asserted on: a silent
        // failure would otherwise read as "no badge awarded" with no explanation.
        var badgeLogger = new Mock<ILogger<BadgeService>>();
        var badges = new BadgeValidationDecorator(
            new BadgeService(db, currentUser, notifications, badgeLogger.Object), db);

        var requests = new ProductRequestValidationDecorator(
            new ProductRequestService(db, currentUser, notifications, badges), db, currentUser);

        return new Scope(requests, notifications, badges, badgeLogger);
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

    private static CreateProductRequestRequest ProposeBottle(string name = "Corryvreckan") => new()
    {
        Name = name,
        Brand = "Ardbeg",
        Category = SpiritCategory.Whisky,
        AbvPercent = 57.1,
        VolumeMl = 700,
        UserNote = "Липсва в каталога"
    };

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
    public async Task ApprovedRequest_NotifiesTheRequesterAndAwardsTheContributorBadge()
    {
        var db = CreateDbContext();
        var collector = SeedUser(db, "Collector");
        var admin = SeedUser(db, "Admin");

        var collectorScope = CreateScope(db, collector.Id);
        var adminScope = CreateScope(db, admin.Id, isAdmin: true);

        // 1. The collector proposes a bottle that is not in the catalog.
        var created = await collectorScope.Requests.CreateAsync(ProposeBottle(), CancellationToken.None);
        Assert.True(created.Success);
        Assert.Equal(ProductRequestStatus.Pending, created.Data!.Status);

        // 2. The admin approves it.
        var approved = await adminScope.Requests.ApproveAsync(
            created.Data.Id, new ResolveProductRequestRequest(), CancellationToken.None);

        Assert.True(approved.Success);
        Assert.Equal(ProductRequestStatus.Approved, approved.Data!.Status);
        AssertNoBadgeEngineErrors(adminScope.BadgeLogger);

        // 3. The product really landed in the catalog.
        var product = await db.Products.AsNoTracking().SingleAsync();
        Assert.Equal("Corryvreckan", product.Name);
        Assert.Equal(ProductOrigin.Approved, product.Origin);
        Assert.Equal(product.Id, approved.Data.ResolvedProductId);

        // 4. The collector earned the badge; the admin earned nothing.
        var badges = await db.UserBadges.AsNoTracking().ToListAsync();
        var badge = Assert.Single(badges);
        Assert.Equal(collector.Id, badge.UserId);
        Assert.Equal(BadgeType.FirstCatalogProduct, badge.Badge);
        Assert.NotEqual(default, badge.AwardedAt);

        // 5. Both notifications went to the collector and nobody else.
        var notifications = await db.Notifications.AsNoTracking().ToListAsync();
        Assert.Equal(2, notifications.Count);
        Assert.All(notifications, n => Assert.Equal(collector.Id, n.UserId));
        Assert.All(notifications, n => Assert.False(n.IsRead));

        // "Your product was added to the catalog" — actor is the admin, resource is the new product.
        var approvedNotification = Assert.Single(
            notifications, n => n.Type == NotificationType.ProductRequestApproved);
        Assert.Equal(admin.Id, approvedNotification.ActorId);
        Assert.Equal("Admin", approvedNotification.ActorDisplayName);
        Assert.Equal(product.Id, approvedNotification.ResourceId);
        Assert.Equal("Corryvreckan", approvedNotification.ResourceName);

        // "You earned a badge" — a system notification, so actor == recipient and ResourceId is null;
        // ResourceName carries the enum name the frontend translates via badges.<Name>.name.
        var badgeNotification = Assert.Single(notifications, n => n.Type == NotificationType.BadgeEarned);
        Assert.Equal(collector.Id, badgeNotification.ActorId);
        Assert.Equal("Collector", badgeNotification.ActorDisplayName);
        Assert.Null(badgeNotification.ResourceId);
        Assert.Equal(nameof(BadgeType.FirstCatalogProduct), badgeNotification.ResourceName);
    }

    [Fact]
    public async Task ApprovedRequest_ShowsUpInTheRequestersOwnBellAndBadgeProgress()
    {
        var db = CreateDbContext();
        var collector = SeedUser(db, "Collector");
        var admin = SeedUser(db, "Admin");

        var collectorScope = CreateScope(db, collector.Id);
        var adminScope = CreateScope(db, admin.Id, isAdmin: true);

        var created = await collectorScope.Requests.CreateAsync(ProposeBottle(), CancellationToken.None);
        await adminScope.Requests.ApproveAsync(
            created.Data!.Id, new ResolveProductRequestRequest(), CancellationToken.None);

        // What GET /api/notifications actually returns to the collector — this is what the bell renders.
        var bell = await collectorScope.Notifications.GetNotificationsAsync(CancellationToken.None);

        Assert.True(bell.Success);
        Assert.Equal(2, bell.Data!.UnreadCount);
        Assert.Equal(2, bell.Data.Notifications.Count);
        Assert.Contains(bell.Data.Notifications, n =>
            n.Type == NotificationType.ProductRequestApproved && n.ResourceName == "Corryvreckan");
        Assert.Contains(bell.Data.Notifications, n =>
            n.Type == NotificationType.BadgeEarned
            && n.ResourceName == nameof(BadgeType.FirstCatalogProduct));

        // What GET /api/badges/progress returns to the collector — this is what ProfilePage renders.
        var progress = await collectorScope.Badges.GetMyProgressAsync(CancellationToken.None);

        Assert.True(progress.Success);
        var row = Assert.Single(progress.Data!, p => p.Badge == BadgeType.FirstCatalogProduct);
        Assert.True(row.Earned);
        Assert.Equal(1, row.Current);
        Assert.Equal(1, row.Threshold);
        Assert.NotNull(row.AwardedAt);

        // The admin who approved it gets nothing — neither notification nor badge.
        var adminBell = await adminScope.Notifications.GetNotificationsAsync(CancellationToken.None);
        Assert.Empty(adminBell.Data!.Notifications);
        Assert.Equal(0, adminBell.Data.UnreadCount);
        Assert.DoesNotContain(
            await adminScope.Badges.GetUserBadgesAsync(admin.Id, CancellationToken.None) is { Data: { } d } ? d : [],
            b => b.Badge == BadgeType.FirstCatalogProduct);
    }

    [Fact]
    public async Task SecondApprovedRequest_DoesNotReawardTheBadgeOrRenotify()
    {
        var db = CreateDbContext();
        var collector = SeedUser(db, "Collector");
        var admin = SeedUser(db, "Admin");

        var collectorScope = CreateScope(db, collector.Id);
        var adminScope = CreateScope(db, admin.Id, isAdmin: true);

        var first = await collectorScope.Requests.CreateAsync(ProposeBottle("Corryvreckan"), CancellationToken.None);
        await adminScope.Requests.ApproveAsync(
            first.Data!.Id, new ResolveProductRequestRequest(), CancellationToken.None);

        var second = await collectorScope.Requests.CreateAsync(ProposeBottle("Uigeadail"), CancellationToken.None);
        var secondApproval = await adminScope.Requests.ApproveAsync(
            second.Data!.Id, new ResolveProductRequestRequest(), CancellationToken.None);

        Assert.True(secondApproval.Success);
        AssertNoBadgeEngineErrors(adminScope.BadgeLogger);

        // The award is permanent and once-only: still one badge row, still one BadgeEarned notification…
        var badge = Assert.Single(await db.UserBadges.AsNoTracking().ToListAsync());
        Assert.Equal(BadgeType.FirstCatalogProduct, badge.Badge);
        Assert.Single(
            await db.Notifications.AsNoTracking().ToListAsync(),
            n => n.Type == NotificationType.BadgeEarned);

        // …while the "added to the catalog" notification fires for every approval.
        Assert.Equal(
            2,
            await db.Notifications.CountAsync(n => n.Type == NotificationType.ProductRequestApproved));
    }

    [Fact]
    public async Task RejectedRequest_NotifiesTheRequesterButAwardsNoBadge()
    {
        var db = CreateDbContext();
        var collector = SeedUser(db, "Collector");
        var admin = SeedUser(db, "Admin");

        var collectorScope = CreateScope(db, collector.Id);
        var adminScope = CreateScope(db, admin.Id, isAdmin: true);

        var created = await collectorScope.Requests.CreateAsync(ProposeBottle(), CancellationToken.None);
        var rejected = await adminScope.Requests.RejectAsync(
            created.Data!.Id,
            new RejectProductRequestRequest { AdminNote = "Вече съществува под друго име" },
            CancellationToken.None);

        Assert.True(rejected.Success);
        Assert.Equal(ProductRequestStatus.Rejected, rejected.Data!.Status);

        Assert.Empty(await db.UserBadges.AsNoTracking().ToListAsync());
        Assert.Empty(await db.Products.AsNoTracking().ToListAsync());

        var notification = Assert.Single(await db.Notifications.AsNoTracking().ToListAsync());
        Assert.Equal(NotificationType.ProductRequestRejected, notification.Type);
        Assert.Equal(collector.Id, notification.UserId);
    }
}
