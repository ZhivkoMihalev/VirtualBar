using Microsoft.EntityFrameworkCore;
using Moq;
using VirtualBar.Application.DTOs.Comments;
using VirtualBar.Application.Interfaces;
using VirtualBar.Domain.Entities;
using VirtualBar.Domain.Enums;
using VirtualBar.Infrastructure.Decorators;
using VirtualBar.Infrastructure.Persistence;
using VirtualBar.Infrastructure.Services;

namespace VirtualBar.Tests.Services;

public sealed class BottleCommentServiceTests
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

    private static IBottleCommentService CreateBottleCommentService(AppDbContext db, Guid currentUserId)
    {
        var currentUser = CreateCurrentUser(currentUserId);
        var inner = new BottleCommentService(db, currentUser);
        return new BottleCommentValidationDecorator(inner, db, currentUser);
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

    private static BottleComment SeedComment(AppDbContext db, Guid bottleId, Guid userId, bool isDeleted = false)
    {
        var comment = new BottleComment
        {
            BottleId = bottleId,
            UserId = userId,
            Content = "Lovely dram.",
            IsDeleted = isDeleted,
            DeletedAt = isDeleted ? DateTime.UtcNow : null
        };
        db.BottleComments.Add(comment);
        db.SaveChanges();
        return comment;
    }

    #region GetCommentsAsync

    [Fact]
    public async Task GetCommentsAsync_WhenBottleNotFound_ReturnsNotFound()
    {
        var db = CreateDbContext();
        var service = CreateBottleCommentService(db, Guid.NewGuid());

        var result = await service.GetCommentsAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Bottle not found.", result.Error);
    }

    [Fact]
    public async Task GetCommentsAsync_WhenNoComments_ReturnsEmptyList()
    {
        var db = CreateDbContext();
        var user = SeedUser(db);
        var bottle = SeedBottle(db, user.Id);
        var service = CreateBottleCommentService(db, user.Id);

        var result = await service.GetCommentsAsync(bottle.Id, CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Empty(result.Data);
    }

    [Fact]
    public async Task GetCommentsAsync_DoesNotReturnDeletedComments()
    {
        var db = CreateDbContext();
        var user = SeedUser(db, "Author");
        var bottle = SeedBottle(db, user.Id);
        SeedComment(db, bottle.Id, user.Id);
        SeedComment(db, bottle.Id, user.Id, isDeleted: true);
        var service = CreateBottleCommentService(db, user.Id);

        var result = await service.GetCommentsAsync(bottle.Id, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Single(result.Data!);
        Assert.Equal("Author", result.Data![0].UserDisplayName);
    }

    [Fact]
    public async Task GetCommentsAsync_WithCancelledToken_Throws()
    {
        var db = CreateDbContext();
        var service = CreateBottleCommentService(db, Guid.NewGuid());
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => service.GetCommentsAsync(Guid.NewGuid(), cts.Token));
    }

    #endregion

    #region AddCommentAsync

    [Fact]
    public async Task AddCommentAsync_WhenContentEmpty_ReturnsFail()
    {
        var db = CreateDbContext();
        var service = CreateBottleCommentService(db, Guid.NewGuid());

        var result = await service.AddCommentAsync(Guid.NewGuid(), new AddCommentRequest { Content = "" }, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Content is required.", result.Error);
    }

    [Fact]
    public async Task AddCommentAsync_WhenContentWhitespace_ReturnsFail()
    {
        var db = CreateDbContext();
        var service = CreateBottleCommentService(db, Guid.NewGuid());

        var result = await service.AddCommentAsync(Guid.NewGuid(), new AddCommentRequest { Content = "   " }, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Content is required.", result.Error);
    }

    [Fact]
    public async Task AddCommentAsync_WhenBottleNotFound_ReturnsNotFound()
    {
        var db = CreateDbContext();
        var service = CreateBottleCommentService(db, Guid.NewGuid());

        var result = await service.AddCommentAsync(Guid.NewGuid(), new AddCommentRequest { Content = "Nice." }, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Bottle not found.", result.Error);
    }

    [Fact]
    public async Task AddCommentAsync_WhenValid_ReturnsCommentDto()
    {
        var db = CreateDbContext();
        var user = SeedUser(db, "Commenter");
        var bottle = SeedBottle(db, user.Id);
        var service = CreateBottleCommentService(db, user.Id);

        var result = await service.AddCommentAsync(bottle.Id, new AddCommentRequest { Content = "Great bottle!" }, CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal("Great bottle!", result.Data.Content);
        Assert.Equal(bottle.Id, result.Data.BottleId);
        Assert.Equal(user.Id, result.Data.UserId);
        Assert.Equal("Commenter", result.Data.UserDisplayName);
    }

    #endregion

    #region DeleteCommentAsync

    [Fact]
    public async Task DeleteCommentAsync_WhenNotFound_ReturnsNotFound()
    {
        var db = CreateDbContext();
        var service = CreateBottleCommentService(db, Guid.NewGuid());

        var result = await service.DeleteCommentAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Comment not found.", result.Error);
    }

    [Fact]
    public async Task DeleteCommentAsync_WhenForbidden_ReturnsForbidden()
    {
        var db = CreateDbContext();
        var author = SeedUser(db, "Author");
        var bottle = SeedBottle(db, author.Id);
        var comment = SeedComment(db, bottle.Id, author.Id);
        var service = CreateBottleCommentService(db, Guid.NewGuid());

        var result = await service.DeleteCommentAsync(comment.Id, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Forbidden.", result.Error);
    }

    [Fact]
    public async Task DeleteCommentAsync_WhenValid_SoftDeletesComment()
    {
        var db = CreateDbContext();
        var user = SeedUser(db);
        var bottle = SeedBottle(db, user.Id);
        var comment = SeedComment(db, bottle.Id, user.Id);
        var service = CreateBottleCommentService(db, user.Id);

        var result = await service.DeleteCommentAsync(comment.Id, CancellationToken.None);

        Assert.True(result.Success);
        var dbComment = await db.BottleComments.FindAsync(comment.Id);
        Assert.True(dbComment!.IsDeleted);
        Assert.NotNull(dbComment.DeletedAt);
    }

    [Fact]
    public async Task DeleteCommentAsync_WithCancelledToken_Throws()
    {
        var db = CreateDbContext();
        var service = CreateBottleCommentService(db, Guid.NewGuid());
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => service.DeleteCommentAsync(Guid.NewGuid(), cts.Token));
    }

    #endregion
}
