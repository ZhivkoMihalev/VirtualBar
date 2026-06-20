using Microsoft.EntityFrameworkCore;
using Moq;
using VirtualBar.Application.Interfaces;
using VirtualBar.Domain.Entities;
using VirtualBar.Infrastructure.Decorators;
using VirtualBar.Infrastructure.Persistence;
using VirtualBar.Infrastructure.Services;

namespace VirtualBar.Tests.Services;

public sealed class UserFollowServiceTests
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

    private static IUserFollowService CreateUserFollowService(AppDbContext db, Guid currentUserId)
    {
        var currentUser = CreateCurrentUser(currentUserId);
        var inner = new UserFollowService(db, currentUser);
        return new UserFollowValidationDecorator(inner, db, currentUser);
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

    #region FollowAsync

    [Fact]
    public async Task FollowAsync_WhenSelf_ReturnsFail()
    {
        var db = CreateDbContext();
        var userId = Guid.NewGuid();
        var service = CreateUserFollowService(db, userId);

        var result = await service.FollowAsync(userId, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Cannot follow yourself.", result.Error);
    }

    [Fact]
    public async Task FollowAsync_WhenUserNotFound_ReturnsNotFound()
    {
        var db = CreateDbContext();
        var service = CreateUserFollowService(db, Guid.NewGuid());

        var result = await service.FollowAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("User not found.", result.Error);
    }

    [Fact]
    public async Task FollowAsync_WhenAlreadyFollowing_ReturnsConflict()
    {
        var db = CreateDbContext();
        var follower = SeedUser(db, "Follower");
        var target = SeedUser(db, "Target");
        SeedFollow(db, follower.Id, target.Id);
        var service = CreateUserFollowService(db, follower.Id);

        var result = await service.FollowAsync(target.Id, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Already following this user.", result.Error);
    }

    [Fact]
    public async Task FollowAsync_WhenValid_ReturnsOk()
    {
        var db = CreateDbContext();
        var follower = SeedUser(db, "Follower");
        var target = SeedUser(db, "Target");
        var service = CreateUserFollowService(db, follower.Id);

        var result = await service.FollowAsync(target.Id, CancellationToken.None);

        Assert.True(result.Success);
        var follow = await db.UserFollows
            .FirstOrDefaultAsync(f => f.FollowerId == follower.Id && f.FollowedId == target.Id);
        Assert.NotNull(follow);
    }

    [Fact]
    public async Task FollowAsync_WithCancelledToken_Throws()
    {
        var db = CreateDbContext();
        var service = CreateUserFollowService(db, Guid.NewGuid());
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => service.FollowAsync(Guid.NewGuid(), cts.Token));
    }

    #endregion

    #region UnfollowAsync

    [Fact]
    public async Task UnfollowAsync_WhenSelf_ReturnsFail()
    {
        var db = CreateDbContext();
        var userId = Guid.NewGuid();
        var service = CreateUserFollowService(db, userId);

        var result = await service.UnfollowAsync(userId, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Cannot unfollow yourself.", result.Error);
    }

    [Fact]
    public async Task UnfollowAsync_WhenUserNotFound_ReturnsNotFound()
    {
        var db = CreateDbContext();
        var service = CreateUserFollowService(db, Guid.NewGuid());

        var result = await service.UnfollowAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("User not found.", result.Error);
    }

    [Fact]
    public async Task UnfollowAsync_WhenNotFollowing_ReturnsFail()
    {
        var db = CreateDbContext();
        var follower = SeedUser(db, "Follower");
        var target = SeedUser(db, "Target");
        var service = CreateUserFollowService(db, follower.Id);

        var result = await service.UnfollowAsync(target.Id, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Not following this user.", result.Error);
    }

    [Fact]
    public async Task UnfollowAsync_WhenValid_ReturnsOk()
    {
        var db = CreateDbContext();
        var follower = SeedUser(db, "Follower");
        var target = SeedUser(db, "Target");
        SeedFollow(db, follower.Id, target.Id);
        var service = CreateUserFollowService(db, follower.Id);

        var result = await service.UnfollowAsync(target.Id, CancellationToken.None);

        Assert.True(result.Success);
        var follow = await db.UserFollows
            .FirstOrDefaultAsync(f => f.FollowerId == follower.Id && f.FollowedId == target.Id);
        Assert.Null(follow);
    }

    [Fact]
    public async Task UnfollowAsync_WithCancelledToken_Throws()
    {
        var db = CreateDbContext();
        var service = CreateUserFollowService(db, Guid.NewGuid());
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => service.UnfollowAsync(Guid.NewGuid(), cts.Token));
    }

    #endregion

    #region GetFollowersAsync

    [Fact]
    public async Task GetFollowersAsync_ReturnsFollowers()
    {
        var db = CreateDbContext();
        var follower = SeedUser(db, "Follower");
        var followed = SeedUser(db, "Followed");
        SeedFollow(db, follower.Id, followed.Id);
        var service = CreateUserFollowService(db, follower.Id);

        var result = await service.GetFollowersAsync(followed.Id, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Single(result.Data!);
        Assert.Equal(follower.Id, result.Data![0].Id);
        Assert.Equal("Follower", result.Data[0].DisplayName);
    }

    [Fact]
    public async Task GetFollowersAsync_WithCancelledToken_Throws()
    {
        var db = CreateDbContext();
        var service = CreateUserFollowService(db, Guid.NewGuid());
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => service.GetFollowersAsync(Guid.NewGuid(), cts.Token));
    }

    #endregion

    #region GetFollowingAsync

    [Fact]
    public async Task GetFollowingAsync_ReturnsFollowing()
    {
        var db = CreateDbContext();
        var follower = SeedUser(db, "Follower");
        var followed = SeedUser(db, "Followed");
        SeedFollow(db, follower.Id, followed.Id);
        var service = CreateUserFollowService(db, follower.Id);

        var result = await service.GetFollowingAsync(follower.Id, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Single(result.Data!);
        Assert.Equal(followed.Id, result.Data![0].Id);
        Assert.Equal("Followed", result.Data[0].DisplayName);
    }

    [Fact]
    public async Task GetFollowingAsync_WithCancelledToken_Throws()
    {
        var db = CreateDbContext();
        var service = CreateUserFollowService(db, Guid.NewGuid());
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => service.GetFollowingAsync(Guid.NewGuid(), cts.Token));
    }

    #endregion
}
