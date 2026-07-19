using Microsoft.Data.Sqlite;
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

public sealed class BottleLikeServiceTests
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

    private static IBottleLikeService CreateBottleLikeService(AppDbContext db, Guid currentUserId, IBadgeService? badgeService = null)
    {
        var currentUser = CreateCurrentUser(currentUserId);
        var inner = new BottleLikeService(db, currentUser, Mock.Of<INotificationService>(), badgeService ?? Mock.Of<IBadgeService>());
        return new BottleLikeValidationDecorator(inner, db, currentUser);
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

    private static Bottle SeedBottle(AppDbContext db, Guid userId, bool isDeleted = false)
    {
        var bottle = new Bottle
        {
            UserId = userId,
            Name = "Lagavulin 16",
            Category = SpiritCategory.Whisky,
            Condition = BottleCondition.Sealed,
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

    #region LikeAsync

    [Fact]
    public async Task LikeAsync_WhenBottleNotFound_ReturnsNotFound()
    {
        var db = CreateDbContext();
        var service = CreateBottleLikeService(db, Guid.NewGuid());

        var result = await service.LikeAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Bottle not found.", result.Error);
    }

    [Fact]
    public async Task LikeAsync_WhenAlreadyLiked_ReturnsConflict()
    {
        var db = CreateDbContext();
        var user = SeedUser(db);
        var bottle = SeedBottle(db, user.Id);
        SeedLike(db, bottle.Id, user.Id);
        var service = CreateBottleLikeService(db, user.Id);

        var result = await service.LikeAsync(bottle.Id, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Bottle already liked.", result.Error);
    }

    [Fact]
    public async Task LikeAsync_WhenValid_ReturnsOk()
    {
        var db = CreateDbContext();
        var user = SeedUser(db);
        var bottle = SeedBottle(db, user.Id);
        var service = CreateBottleLikeService(db, user.Id);

        var result = await service.LikeAsync(bottle.Id, CancellationToken.None);

        Assert.True(result.Success);
        var like = await db.BottleLikes.FirstOrDefaultAsync(l => l.BottleId == bottle.Id && l.UserId == user.Id);
        Assert.NotNull(like);
    }

    [Fact]
    public async Task LikeAsync_WhenLosesLikeRace_ReturnsConflictWithoutNotifying()
    {
        // SQLite so the composite (BottleId, UserId) PK is actually enforced. The "winner" like is
        // persisted via a SEPARATE context on the same connection, so the service's context doesn't track
        // it (which would fail at Add). The loser's insert then hits the PK constraint at SaveChanges.
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        await using var db = new AppDbContext(options);
        db.Database.EnsureCreated();

        var owner = SeedUser(db, "Owner");
        var liker = SeedUser(db, "Liker");
        var bottle = SeedBottle(db, owner.Id);
        await using (var winnerContext = new AppDbContext(options))
        {
            winnerContext.BottleLikes.Add(new BottleLike { BottleId = bottle.Id, UserId = liker.Id, LikedAt = DateTime.UtcNow });
            await winnerContext.SaveChangesAsync();
        }

        // Loser slipped past the decorator's pre-check — call the inner service directly.
        var notificationMock = new Mock<INotificationService>();
        var inner = new BottleLikeService(db, CreateCurrentUser(liker.Id), notificationMock.Object, Mock.Of<IBadgeService>());

        var result = await inner.LikeAsync(bottle.Id, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ErrorCode.Conflict, result.ErrorCode);
        Assert.Equal("Bottle already liked.", result.Error);
        notificationMock.Verify(n => n.CreateAsync(
            It.IsAny<Guid>(), It.IsAny<NotificationType>(), It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task LikeAsync_WithCancelledToken_Throws()
    {
        var db = CreateDbContext();
        var service = CreateBottleLikeService(db, Guid.NewGuid());
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => service.LikeAsync(Guid.NewGuid(), cts.Token));
    }

    [Fact]
    public async Task LikeAsync_WhenValid_EvaluatesLikeReceivedBadgeForOwnerNotLiker()
    {
        var db = CreateDbContext();
        var owner = SeedUser(db, "Owner");
        var liker = SeedUser(db, "Liker");
        var bottle = SeedBottle(db, owner.Id);
        var badgeMock = new Mock<IBadgeService>();
        var service = CreateBottleLikeService(db, liker.Id, badgeMock.Object);

        var result = await service.LikeAsync(bottle.Id, CancellationToken.None);

        Assert.True(result.Success);
        badgeMock.Verify(b => b.EvaluateAsync(owner.Id, BadgeTrigger.LikeReceived, It.IsAny<CancellationToken>()), Times.Once);
        badgeMock.Verify(b => b.EvaluateAsync(liker.Id, BadgeTrigger.LikeReceived, It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region UnlikeAsync

    [Fact]
    public async Task UnlikeAsync_WhenBottleNotFound_ReturnsNotFound()
    {
        var db = CreateDbContext();
        var service = CreateBottleLikeService(db, Guid.NewGuid());

        var result = await service.UnlikeAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Bottle not found.", result.Error);
    }

    [Fact]
    public async Task UnlikeAsync_WhenNotLiked_ReturnsFail()
    {
        var db = CreateDbContext();
        var user = SeedUser(db);
        var bottle = SeedBottle(db, user.Id);
        var service = CreateBottleLikeService(db, user.Id);

        var result = await service.UnlikeAsync(bottle.Id, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Bottle is not liked.", result.Error);
    }

    [Fact]
    public async Task UnlikeAsync_WhenValid_ReturnsOk()
    {
        var db = CreateDbContext();
        var user = SeedUser(db);
        var bottle = SeedBottle(db, user.Id);
        SeedLike(db, bottle.Id, user.Id);
        var service = CreateBottleLikeService(db, user.Id);

        var result = await service.UnlikeAsync(bottle.Id, CancellationToken.None);

        Assert.True(result.Success);
        var like = await db.BottleLikes.FirstOrDefaultAsync(l => l.BottleId == bottle.Id && l.UserId == user.Id);
        Assert.Null(like);
    }

    [Fact]
    public async Task UnlikeAsync_WithCancelledToken_Throws()
    {
        var db = CreateDbContext();
        var service = CreateBottleLikeService(db, Guid.NewGuid());
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => service.UnlikeAsync(Guid.NewGuid(), cts.Token));
    }

    #endregion
}
