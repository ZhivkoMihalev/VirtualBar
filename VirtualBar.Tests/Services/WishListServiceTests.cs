using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;
using VirtualBar.Application.DTOs.WishList;
using VirtualBar.Application.Interfaces;
using VirtualBar.Domain.Entities;
using VirtualBar.Domain.Enums;
using VirtualBar.Infrastructure.Decorators;
using VirtualBar.Infrastructure.Persistence;
using VirtualBar.Infrastructure.Services;

namespace VirtualBar.Tests.Services;

public sealed class WishListServiceTests
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

    private static IWishListService CreateService(AppDbContext db, Guid currentUserId)
    {
        var currentUser = CreateCurrentUser(currentUserId);
        var env = Mock.Of<IWebHostEnvironment>(e => e.WebRootPath == Path.GetTempPath());
        var inner = new WishListService(db, currentUser, env);
        return new WishListValidationDecorator(inner, db, currentUser);
    }

    private static (IWishListService service, string tempRoot) CreateServiceWithTempRoot(AppDbContext db, Guid userId)
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempRoot);
        var env = Mock.Of<IWebHostEnvironment>(e => e.WebRootPath == tempRoot);
        var currentUser = CreateCurrentUser(userId);
        var inner = new WishListService(db, currentUser, env);
        var service = new WishListValidationDecorator(inner, db, currentUser);
        return (service, tempRoot);
    }

    private static IFormFile CreateFakeFile(string contentType, long sizeBytes, string fileName = "test.jpg")
    {
        var stream = new MemoryStream(new byte[sizeBytes]);
        var mock = new Mock<IFormFile>();
        mock.Setup(f => f.ContentType).Returns(contentType);
        mock.Setup(f => f.Length).Returns(sizeBytes);
        mock.Setup(f => f.FileName).Returns(fileName);
        mock.Setup(f => f.CopyToAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .Returns((Stream target, CancellationToken ct) => stream.CopyToAsync(target, ct));
        return mock.Object;
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

    private static WishListItem SeedItem(
        AppDbContext db,
        Guid userId,
        string? bottleName = "Looking for Lagavulin",
        string? distillery = "Lagavulin",
        SpiritCategory? category = SpiritCategory.Whisky,
        string? imageUrl = null,
        bool isDeleted = false)
    {
        var item = new WishListItem
        {
            UserId = userId,
            BottleName = bottleName,
            Distillery = distillery,
            Category = category,
            ImageUrl = imageUrl,
            IsDeleted = isDeleted,
            DeletedAt = isDeleted ? DateTime.UtcNow : null
        };
        db.WishListItems.Add(item);
        db.SaveChanges();
        return item;
    }

    #region GetWishListAsync

    [Fact]
    public async Task GetWishListAsync_WhenNoItems_ReturnsEmptyList()
    {
        var db = CreateDbContext();
        var user = SeedUser(db);
        var service = CreateService(db, user.Id);

        var result = await service.GetWishListAsync(CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Empty(result.Data);
    }

    [Fact]
    public async Task GetWishListAsync_WhenHasItems_ReturnsOnlyOwnItems()
    {
        var db = CreateDbContext();
        var user = SeedUser(db, "User");
        var other = SeedUser(db, "Other");
        SeedItem(db, user.Id, distillery: "Ardbeg");
        SeedItem(db, other.Id, distillery: "Macallan");
        var service = CreateService(db, user.Id);

        var result = await service.GetWishListAsync(CancellationToken.None);

        Assert.True(result.Success);
        Assert.Single(result.Data!);
        Assert.Equal("Ardbeg", result.Data![0].Distillery);
    }

    [Fact]
    public async Task GetWishListAsync_WhenHasDeletedItem_ExcludesDeleted()
    {
        var db = CreateDbContext();
        var user = SeedUser(db);
        SeedItem(db, user.Id, distillery: "Visible");
        SeedItem(db, user.Id, distillery: "Deleted", isDeleted: true);
        var service = CreateService(db, user.Id);

        var result = await service.GetWishListAsync(CancellationToken.None);

        Assert.True(result.Success);
        Assert.Single(result.Data!);
        Assert.Equal("Visible", result.Data![0].Distillery);
    }

    [Fact]
    public async Task GetWishListAsync_WithCancelledToken_Throws()
    {
        var db = CreateDbContext();
        var service = CreateService(db, Guid.NewGuid());
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => service.GetWishListAsync(cts.Token));
    }

    #endregion

    #region GetAllAsync

    [Fact]
    public async Task GetAllAsync_WhenNoItems_ReturnsEmptyList()
    {
        var db = CreateDbContext();
        var user = SeedUser(db);
        var service = CreateService(db, user.Id);

        var result = await service.GetAllAsync(CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Empty(result.Data);
    }

    [Fact]
    public async Task GetAllAsync_WhenHasItems_ReturnsAllUsersItems()
    {
        var db = CreateDbContext();
        var userA = SeedUser(db, "Alice");
        var userB = SeedUser(db, "Bob");
        SeedItem(db, userA.Id, distillery: "Ardbeg", imageUrl: "/uploads/wishlist/ardbeg.jpg");
        SeedItem(db, userB.Id, distillery: "Macallan");
        var service = CreateService(db, userA.Id);

        var result = await service.GetAllAsync(CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(2, result.Data!.Count);

        var alice = Assert.Single(result.Data, i => i.Distillery == "Ardbeg");
        Assert.Equal(userA.Id, alice.UserId);
        Assert.Equal("Alice", alice.UserDisplayName);
        Assert.Equal("/uploads/wishlist/ardbeg.jpg", alice.ImageUrl);

        var bob = Assert.Single(result.Data, i => i.Distillery == "Macallan");
        Assert.Equal(userB.Id, bob.UserId);
        Assert.Equal("Bob", bob.UserDisplayName);
    }

    [Fact]
    public async Task GetAllAsync_WhenHasDeletedItem_ExcludesDeleted()
    {
        var db = CreateDbContext();
        var user = SeedUser(db);
        SeedItem(db, user.Id, distillery: "Visible");
        SeedItem(db, user.Id, distillery: "Deleted", isDeleted: true);
        var service = CreateService(db, user.Id);

        var result = await service.GetAllAsync(CancellationToken.None);

        Assert.True(result.Success);
        Assert.Single(result.Data!);
        Assert.Equal("Visible", result.Data![0].Distillery);
    }

    [Fact]
    public async Task GetAllAsync_WithCancelledToken_Throws()
    {
        var db = CreateDbContext();
        var service = CreateService(db, Guid.NewGuid());
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => service.GetAllAsync(cts.Token));
    }

    [Fact]
    public async Task GetAllAsync_WhenHasItems_OrderedByCreatedAtDescending()
    {
        var db = CreateDbContext();
        var user = SeedUser(db);

        var older = new WishListItem
        {
            UserId = user.Id,
            Distillery = "Older",
            Category = SpiritCategory.Whisky,
            CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };
        var newer = new WishListItem
        {
            UserId = user.Id,
            Distillery = "Newer",
            Category = SpiritCategory.Whisky,
            CreatedAt = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc)
        };
        db.WishListItems.AddRange(older, newer);
        db.SaveChanges();

        var service = CreateService(db, user.Id);

        var result = await service.GetAllAsync(CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(2, result.Data!.Count);
        Assert.Equal("Newer", result.Data![0].Distillery);
        Assert.Equal("Older", result.Data![1].Distillery);
    }

    #endregion

    #region AddItemAsync

    [Fact]
    public async Task AddItemAsync_WhenNoCriteria_ReturnsFail()
    {
        var db = CreateDbContext();
        var user = SeedUser(db);
        var service = CreateService(db, user.Id);
        var request = new AddWishListItemRequest { BottleName = "Something", Distillery = null, Category = null };

        var result = await service.AddItemAsync(request, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("At least one matching criterion (distillery or category) is required.", result.Error);
    }

    [Fact]
    public async Task AddItemAsync_WhenOnlyDistillery_Succeeds()
    {
        var db = CreateDbContext();
        var user = SeedUser(db);
        var service = CreateService(db, user.Id);
        var request = new AddWishListItemRequest { Distillery = "Springbank", Category = null, ImageUrl = "/uploads/wishlist/abc.jpg" };

        var result = await service.AddItemAsync(request, CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal("Springbank", result.Data.Distillery);
        Assert.Null(result.Data.Category);
        Assert.Equal("/uploads/wishlist/abc.jpg", result.Data.ImageUrl);
        Assert.NotEqual(Guid.Empty, result.Data.Id);
    }

    [Fact]
    public async Task AddItemAsync_WhenImageUrlProvided_StoresImageUrl()
    {
        var db = CreateDbContext();
        var user = SeedUser(db);
        var service = CreateService(db, user.Id);
        var request = new AddWishListItemRequest { Distillery = "Bowmore", ImageUrl = "https://example.com/img.jpg" };

        var result = await service.AddItemAsync(request, CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal("https://example.com/img.jpg", result.Data.ImageUrl);

        var stored = await db.WishListItems.SingleAsync();
        Assert.Equal("https://example.com/img.jpg", stored.ImageUrl);
    }

    [Fact]
    public async Task AddItemAsync_WhenOnlyCategory_Succeeds()
    {
        var db = CreateDbContext();
        var user = SeedUser(db);
        var service = CreateService(db, user.Id);
        var request = new AddWishListItemRequest { Distillery = null, Category = SpiritCategory.Rum };

        var result = await service.AddItemAsync(request, CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Null(result.Data.Distillery);
        Assert.Equal(SpiritCategory.Rum, result.Data.Category);
    }

    [Fact]
    public async Task AddItemAsync_WhenBothCriteria_Succeeds()
    {
        var db = CreateDbContext();
        var user = SeedUser(db);
        var service = CreateService(db, user.Id);
        var request = new AddWishListItemRequest
        {
            BottleName = "Dream bottle",
            Distillery = "Glenfarclas",
            Category = SpiritCategory.Whisky
        };

        var result = await service.AddItemAsync(request, CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal("Dream bottle", result.Data.BottleName);
        Assert.Equal("Glenfarclas", result.Data.Distillery);
        Assert.Equal(SpiritCategory.Whisky, result.Data.Category);

        var stored = await db.WishListItems.SingleAsync();
        Assert.Equal(user.Id, stored.UserId);
    }

    [Fact]
    public async Task AddItemAsync_WithCancelledToken_Throws()
    {
        var db = CreateDbContext();
        var service = CreateService(db, Guid.NewGuid());
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => service.AddItemAsync(new AddWishListItemRequest { Distillery = "Valid" }, cts.Token));
    }

    #endregion

    #region RemoveItemAsync

    [Fact]
    public async Task RemoveItemAsync_WhenNotFound_ReturnsNotFound()
    {
        var db = CreateDbContext();
        var service = CreateService(db, Guid.NewGuid());

        var result = await service.RemoveItemAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Wish list item not found.", result.Error);
    }

    [Fact]
    public async Task RemoveItemAsync_WhenBelongsToAnotherUser_ReturnsForbidden()
    {
        var db = CreateDbContext();
        var owner = SeedUser(db, "Owner");
        var item = SeedItem(db, owner.Id);
        var service = CreateService(db, Guid.NewGuid());

        var result = await service.RemoveItemAsync(item.Id, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Forbidden.", result.Error);
    }

    [Fact]
    public async Task RemoveItemAsync_WhenValid_SoftDeletes()
    {
        var db = CreateDbContext();
        var user = SeedUser(db);
        var item = SeedItem(db, user.Id);
        var service = CreateService(db, user.Id);

        var result = await service.RemoveItemAsync(item.Id, CancellationToken.None);

        Assert.True(result.Success);
        var stored = await db.WishListItems.FindAsync(item.Id);
        Assert.True(stored!.IsDeleted);
        Assert.NotNull(stored.DeletedAt);
    }

    [Fact]
    public async Task RemoveItemAsync_WithCancelledToken_Throws()
    {
        var db = CreateDbContext();
        var service = CreateService(db, Guid.NewGuid());
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => service.RemoveItemAsync(Guid.NewGuid(), cts.Token));
    }

    #endregion

    #region UploadImageAsync

    [Fact]
    public async Task UploadImageAsync_WhenFileIsNull_ReturnsFail()
    {
        var db = CreateDbContext();
        var service = CreateService(db, Guid.NewGuid());

        var result = await service.UploadImageAsync(null!, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("File is required.", result.Error);
    }

    [Fact]
    public async Task UploadImageAsync_WhenFileIsEmpty_ReturnsFail()
    {
        var db = CreateDbContext();
        var service = CreateService(db, Guid.NewGuid());
        var file = CreateFakeFile("image/jpeg", 0);

        var result = await service.UploadImageAsync(file, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("File is required.", result.Error);
    }

    [Fact]
    public async Task UploadImageAsync_WhenInvalidContentType_ReturnsFail()
    {
        var db = CreateDbContext();
        var service = CreateService(db, Guid.NewGuid());
        var file = CreateFakeFile("application/pdf", 1024);

        var result = await service.UploadImageAsync(file, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Only JPEG, PNG and WebP images are allowed.", result.Error);
    }

    [Fact]
    public async Task UploadImageAsync_WhenFileTooLarge_ReturnsFail()
    {
        var db = CreateDbContext();
        var service = CreateService(db, Guid.NewGuid());
        var file = CreateFakeFile("image/jpeg", 6 * 1024 * 1024);

        var result = await service.UploadImageAsync(file, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("File size must not exceed 5 MB.", result.Error);
    }

    [Fact]
    public async Task UploadImageAsync_WhenValid_ReturnsUrlAndSavesFile()
    {
        var db = CreateDbContext();
        var (service, tempRoot) = CreateServiceWithTempRoot(db, Guid.NewGuid());

        try
        {
            var file = CreateFakeFile("image/jpeg", 1024, "photo.jpg");

            var result = await service.UploadImageAsync(file, CancellationToken.None);

            Assert.True(result.Success);
            Assert.NotNull(result.Data);
            Assert.StartsWith("/uploads/wishlist/", result.Data);

            var relativePath = result.Data!.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
            var fullPath = Path.Combine(tempRoot, relativePath);
            Assert.True(File.Exists(fullPath));
        }
        finally
        {
            Directory.Delete(tempRoot, true);
        }
    }

    [Fact]
    public async Task UploadImageAsync_WithCancelledToken_Throws()
    {
        var db = CreateDbContext();
        var service = CreateService(db, Guid.NewGuid());
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => service.UploadImageAsync(CreateFakeFile("image/jpeg", 1024), cts.Token));
    }

    #endregion
}
