using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using VirtualBar.Application.Interfaces;
using VirtualBar.Domain.Entities;
using VirtualBar.Infrastructure.Decorators;
using VirtualBar.Infrastructure.Persistence;
using VirtualBar.Infrastructure.Services;

namespace VirtualBar.Tests.Services;

public sealed class NotificationServiceTests
{
    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

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

    private static INotificationService CreateService(AppDbContext db, Guid currentUserId)
    {
        var currentUser = CreateCurrentUser(currentUserId);
        var inner = new NotificationService(db, currentUser);
        return new NotificationValidationDecorator(inner, db, currentUser);
    }

    private static AppUser SeedUser(AppDbContext db, string name = "Test User")
    {
        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            UserName = $"{name}@example.com",
            Email = $"{name}@example.com",
            DisplayName = name
        };
        db.Users.Add(user);
        db.SaveChanges();
        return user;
    }

    private static Notification SeedNotification(AppDbContext db, Guid userId, Guid? actorId = null, bool isRead = false)
    {
        var notification = new Notification
        {
            UserId = userId,
            Type = NotificationType.BottleLiked,
            ActorId = actorId ?? userId,
            ActorDisplayName = "Actor",
            IsRead = isRead
        };
        db.Notifications.Add(notification);
        db.SaveChanges();
        return notification;
    }

    #region GetNotificationsAsync

    [Fact]
    public async Task GetNotificationsAsync_WhenNoNotifications_ReturnsEmptyWithZeroUnread()
    {
        var db = CreateDbContext();
        var user = SeedUser(db);
        var service = CreateService(db, user.Id);

        var result = await service.GetNotificationsAsync(CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Empty(result.Data.Notifications);
        Assert.Equal(0, result.Data.UnreadCount);
    }

    [Fact]
    public async Task GetNotificationsAsync_WhenHasMixedNotifications_ReturnsUnreadCount()
    {
        var db = CreateDbContext();
        var user = SeedUser(db);
        SeedNotification(db, user.Id, isRead: false);
        SeedNotification(db, user.Id, isRead: false);
        SeedNotification(db, user.Id, isRead: true);
        var service = CreateService(db, user.Id);

        var result = await service.GetNotificationsAsync(CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(3, result.Data!.Notifications.Count);
        Assert.Equal(2, result.Data.UnreadCount);
    }

    [Fact]
    public async Task GetNotificationsAsync_WhenMoreThan30Notifications_ReturnsOnly30()
    {
        var db = CreateDbContext();
        var user = SeedUser(db);
        for (var i = 0; i < 35; i++)
            SeedNotification(db, user.Id, isRead: false);
        var service = CreateService(db, user.Id);

        var result = await service.GetNotificationsAsync(CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(30, result.Data!.Notifications.Count);
        Assert.Equal(35, result.Data.UnreadCount);
    }

    [Fact]
    public async Task GetNotificationsAsync_WhenSoftDeleted_ExcludesFromListAndUnreadCount()
    {
        var db = CreateDbContext();
        var user = SeedUser(db);
        var visible = SeedNotification(db, user.Id, isRead: false);
        var deleted = SeedNotification(db, user.Id, isRead: false);
        deleted.IsDeleted = true;
        deleted.DeletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        var service = CreateService(db, user.Id);

        var result = await service.GetNotificationsAsync(CancellationToken.None);

        Assert.True(result.Success);
        Assert.Single(result.Data!.Notifications);
        Assert.Equal(visible.Id, result.Data.Notifications[0].Id);
        Assert.Equal(1, result.Data.UnreadCount);
    }

    [Fact]
    public async Task GetNotificationsAsync_WhenOtherUserHasNotifications_ReturnsOnlyOwnNotifications()
    {
        var db = CreateDbContext();
        var user = SeedUser(db, "User");
        var other = SeedUser(db, "Other");
        SeedNotification(db, user.Id, isRead: false);
        SeedNotification(db, other.Id, isRead: false);
        var service = CreateService(db, user.Id);

        var result = await service.GetNotificationsAsync(CancellationToken.None);

        Assert.True(result.Success);
        Assert.Single(result.Data!.Notifications);
        Assert.Equal(user.Id, result.Data.Notifications[0].ActorId == user.Id ? user.Id : result.Data.Notifications[0].ActorId);
        Assert.All(result.Data.Notifications, n => Assert.Equal(user.Id, db.Notifications.Find(n.Id)!.UserId));
    }

    #endregion

    #region MarkReadAsync

    [Fact]
    public async Task MarkReadAsync_WhenNotFound_ReturnsNotFound()
    {
        var db = CreateDbContext();
        var service = CreateService(db, Guid.NewGuid());

        var result = await service.MarkReadAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Notification not found.", result.Error);
    }

    [Fact]
    public async Task MarkReadAsync_WhenBelongsToAnotherUser_ReturnsForbidden()
    {
        var db = CreateDbContext();
        var owner = SeedUser(db, "Owner");
        var notification = SeedNotification(db, owner.Id);
        var service = CreateService(db, Guid.NewGuid());

        var result = await service.MarkReadAsync(notification.Id, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Access denied.", result.Error);
    }

    [Fact]
    public async Task MarkReadAsync_WhenValid_SetsIsRead()
    {
        var db = CreateDbContext();
        var user = SeedUser(db);
        var notification = SeedNotification(db, user.Id, isRead: false);
        var service = CreateService(db, user.Id);

        var result = await service.MarkReadAsync(notification.Id, CancellationToken.None);

        Assert.True(result.Success);
        var stored = await db.Notifications.FindAsync(notification.Id);
        Assert.True(stored!.IsRead);
    }

    [Fact]
    public async Task MarkReadAsync_WithCancelledToken_Throws()
    {
        var db = CreateDbContext();
        var service = CreateService(db, Guid.NewGuid());
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => service.MarkReadAsync(Guid.NewGuid(), cts.Token));
    }

    #endregion

    #region MarkAllReadAsync

    [Fact]
    public async Task MarkAllReadAsync_WhenNoUnread_ReturnsSuccess()
    {
        var db = CreateSqliteDbContext();
        var user = SeedUser(db);
        var service = CreateService(db, user.Id);

        var result = await service.MarkAllReadAsync(CancellationToken.None);

        Assert.True(result.Success);
    }

    [Fact]
    public async Task MarkAllReadAsync_WhenHasUnread_MarksAllRead()
    {
        var db = CreateSqliteDbContext();
        var user = SeedUser(db);
        SeedNotification(db, user.Id, isRead: false);
        SeedNotification(db, user.Id, isRead: false);
        var service = CreateService(db, user.Id);

        var result = await service.MarkAllReadAsync(CancellationToken.None);

        Assert.True(result.Success);
        var unread = await db.Notifications.CountAsync(n => n.UserId == user.Id && !n.IsRead);
        Assert.Equal(0, unread);
    }

    [Fact]
    public async Task MarkAllReadAsync_DoesNotMarkOtherUsersNotifications()
    {
        var db = CreateSqliteDbContext();
        var user = SeedUser(db, "User");
        var other = SeedUser(db, "Other");
        SeedNotification(db, user.Id, isRead: false);
        SeedNotification(db, other.Id, isRead: false);
        var service = CreateService(db, user.Id);

        await service.MarkAllReadAsync(CancellationToken.None);

        var otherUnread = await db.Notifications.CountAsync(n => n.UserId == other.Id && !n.IsRead);
        Assert.Equal(1, otherUnread);
    }

    #endregion

    #region CreateBulkAsync

    [Fact]
    public async Task CreateBulkAsync_WhenEmptyList_CreatesNoNotifications()
    {
        var db = CreateDbContext();
        var actor = SeedUser(db, "Actor");
        var service = CreateService(db, actor.Id);

        await service.CreateBulkAsync([], NotificationType.NewBottleFromFollowing, null, null, CancellationToken.None);

        Assert.Equal(0, await db.Notifications.CountAsync());
    }

    [Fact]
    public async Task CreateBulkAsync_WhenValid_CreatesOneNotificationPerRecipient()
    {
        var db = CreateDbContext();
        var actor = SeedUser(db, "Actor");
        var r1 = SeedUser(db, "Recipient1");
        var r2 = SeedUser(db, "Recipient2");
        var service = CreateService(db, actor.Id);
        var resourceId = Guid.NewGuid();

        await service.CreateBulkAsync([r1.Id, r2.Id], NotificationType.NewBottleFromFollowing, resourceId, "Glenfarclas 25", CancellationToken.None);

        var notifications = await db.Notifications.ToListAsync();
        Assert.Equal(2, notifications.Count);
        Assert.All(notifications, n =>
        {
            Assert.Equal(NotificationType.NewBottleFromFollowing, n.Type);
            Assert.Equal(actor.Id, n.ActorId);
            Assert.Equal("Actor", n.ActorDisplayName);
            Assert.Equal(resourceId, n.ResourceId);
            Assert.Equal("Glenfarclas 25", n.ResourceName);
            Assert.False(n.IsRead);
        });
        Assert.Contains(notifications, n => n.UserId == r1.Id);
        Assert.Contains(notifications, n => n.UserId == r2.Id);
    }

    [Fact]
    public async Task CreateBulkAsync_WhenListIncludesSelf_SkipsSelf()
    {
        var db = CreateDbContext();
        var actor = SeedUser(db, "Actor");
        var other = SeedUser(db, "Other");
        var service = CreateService(db, actor.Id);

        await service.CreateBulkAsync([actor.Id, other.Id], NotificationType.NewBottleFromFollowing, null, null, CancellationToken.None);

        var notifications = await db.Notifications.ToListAsync();
        Assert.Single(notifications);
        Assert.Equal(other.Id, notifications[0].UserId);
    }

    #endregion

    #region CreateAsync

    [Fact]
    public async Task CreateAsync_WhenRecipientIsSelf_SkipsCreation()
    {
        var db = CreateDbContext();
        var user = SeedUser(db);
        var service = CreateService(db, user.Id);

        await service.CreateAsync(user.Id, NotificationType.BottleLiked, Guid.NewGuid(), "Bottle", CancellationToken.None);

        Assert.Equal(0, await db.Notifications.CountAsync());
    }

    [Fact]
    public async Task CreateAsync_WhenValid_CreatesNotification()
    {
        var db = CreateDbContext();
        var actor = SeedUser(db, "Actor");
        var recipient = SeedUser(db, "Recipient");
        var service = CreateService(db, actor.Id);
        var resourceId = Guid.NewGuid();

        await service.CreateAsync(recipient.Id, NotificationType.BottleCommented, resourceId, "Lagavulin 16", CancellationToken.None);

        var stored = await db.Notifications.SingleAsync();
        Assert.Equal(recipient.Id, stored.UserId);
        Assert.Equal(NotificationType.BottleCommented, stored.Type);
        Assert.Equal(actor.Id, stored.ActorId);
        Assert.Equal("Actor", stored.ActorDisplayName);
        Assert.Equal(resourceId, stored.ResourceId);
        Assert.Equal("Lagavulin 16", stored.ResourceName);
        Assert.False(stored.IsRead);
    }

    #endregion
}
