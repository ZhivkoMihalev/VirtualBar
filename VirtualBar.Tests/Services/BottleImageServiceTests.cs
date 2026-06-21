using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;
using VirtualBar.Application.Common;
using VirtualBar.Application.DTOs.Bottles;
using VirtualBar.Application.Interfaces;
using VirtualBar.Domain.Entities;
using VirtualBar.Infrastructure.Decorators;
using VirtualBar.Infrastructure.Persistence;
using VirtualBar.Infrastructure.Services;

namespace VirtualBar.Tests.Services;

public sealed class BottleImageServiceTests
{
    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    private static IBottleImageService CreateService(AppDbContext db, Guid currentUserId, string? webRootPath = null)
    {
        var mockUser = new Mock<ICurrentUser>();
        mockUser.Setup(u => u.UserId).Returns(currentUserId);
        mockUser.Setup(u => u.IsAuthenticated).Returns(true);

        var mockEnv = new Mock<IWebHostEnvironment>();
        mockEnv.Setup(e => e.WebRootPath).Returns(webRootPath ?? Path.GetTempPath());
        mockEnv.Setup(e => e.ContentRootPath).Returns(Path.GetTempPath());

        var inner = new BottleImageService(db, mockEnv.Object);
        return new BottleImageValidationDecorator(inner, db, mockUser.Object);
    }

    private static IFormFile CreateFormFile(string fileName = "img.jpg", string contentType = "image/jpeg", long length = 512)
    {
        var mock = new Mock<IFormFile>();
        mock.Setup(f => f.FileName).Returns(fileName);
        mock.Setup(f => f.ContentType).Returns(contentType);
        mock.Setup(f => f.Length).Returns(length);
        mock.Setup(f => f.CopyToAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        return mock.Object;
    }

    private static AppUser SeedUser(AppDbContext db, string name = "User")
    {
        var user = new AppUser 
        { 
            Id = Guid.NewGuid(), 
            UserName = $"{name}@x.com", 
            Email = $"{name}@x.com", 
            DisplayName = name 
        };

        db.Users.Add(user); 
        db.SaveChanges(); 

        return user;
    }

    private static Bottle SeedBottle(AppDbContext db, Guid userId, bool isDeleted = false)
    {
        var bottle = new Bottle
        {
            UserId = userId, Name = "Bottle",
            Category = VirtualBar.Domain.Enums.SpiritCategory.Whisky,
            Condition = VirtualBar.Domain.Enums.BottleCondition.Sealed,
            IsDeleted = isDeleted, DeletedAt = isDeleted ? DateTime.UtcNow : null
        };

        db.Bottles.Add(bottle); 
        db.SaveChanges(); 
        
        return bottle;
    }

    private static BottleImage SeedImage(AppDbContext db, Guid bottleId, bool isDeleted = false)
    {
        var image = new BottleImage
        {
            BottleId = bottleId, Url = "/uploads/bottles/test.jpg",
            IsPrimary = true, SortOrder = 0,
            IsDeleted = isDeleted, DeletedAt = isDeleted ? DateTime.UtcNow : null
        };

        db.BottleImages.Add(image); 
        db.SaveChanges();

        return image;
    }

    [Fact]
    public async Task AddImageAsync_WhenFileIsNull_ReturnsFail()
    {
        using var db = CreateDbContext();
        var user = SeedUser(db);
        var bottle = SeedBottle(db, user.Id);
        var service = CreateService(db, user.Id);

        var result = await service.AddImageAsync(bottle.Id, (IFormFile)null!, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("No file provided.", result.Error);
        Assert.Equal(ErrorCode.Validation, result.ErrorCode);
    }

    [Fact]
    public async Task AddImageAsync_WhenFileLengthIsZero_ReturnsFail()
    {
        using var db = CreateDbContext();
        var user = SeedUser(db);
        var bottle = SeedBottle(db, user.Id);
        var service = CreateService(db, user.Id);

        var result = await service.AddImageAsync(bottle.Id, CreateFormFile(length: 0), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("No file provided.", result.Error);
    }

    [Fact]
    public async Task AddImageAsync_WhenContentTypeNotAllowed_ReturnsFail()
    {
        using var db = CreateDbContext();
        var user = SeedUser(db);
        var bottle = SeedBottle(db, user.Id);
        var service = CreateService(db, user.Id);

        var result = await service.AddImageAsync(bottle.Id, CreateFormFile(contentType: "application/pdf"), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Only JPEG, PNG, WebP, and GIF images are allowed.", result.Error);
    }

    [Fact]
    public async Task AddImageAsync_WhenFileTooLarge_ReturnsFail()
    {
        using var db = CreateDbContext();
        var user = SeedUser(db);
        var bottle = SeedBottle(db, user.Id);
        var service = CreateService(db, user.Id);

        var result = await service.AddImageAsync(bottle.Id, CreateFormFile(length: 10 * 1024 * 1024 + 1), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Image must be under 10 MB.", result.Error);
    }

    [Fact]
    public async Task AddImageAsync_WhenBottleNotFound_ReturnsNotFound()
    {
        using var db = CreateDbContext();
        var user = SeedUser(db);
        var service = CreateService(db, user.Id);

        var result = await service.AddImageAsync(Guid.NewGuid(), CreateFormFile(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Bottle not found.", result.Error);
        Assert.Equal(ErrorCode.NotFound, result.ErrorCode);
    }

    [Fact]
    public async Task AddImageAsync_WhenWrongOwner_ReturnsForbidden()
    {
        using var db = CreateDbContext();
        var owner = SeedUser(db, "Owner");
        var other = SeedUser(db, "Other");
        var bottle = SeedBottle(db, owner.Id);
        var service = CreateService(db, other.Id);

        var result = await service.AddImageAsync(bottle.Id, CreateFormFile(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Forbidden.", result.Error);
        Assert.Equal(ErrorCode.Forbidden, result.ErrorCode);
    }

    [Fact]
    public async Task AddImageAsync_WhenFirstImage_ReturnsPrimary()
    {
        using var db = CreateDbContext();
        var user = SeedUser(db);
        var bottle = SeedBottle(db, user.Id);
        var service = CreateService(db, user.Id);

        var result = await service.AddImageAsync(bottle.Id, CreateFormFile(), CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.True(result.Data!.IsPrimary);
        Assert.Equal(0, result.Data.SortOrder);
    }

    [Fact]
    public async Task AddImageAsync_WhenSecondImage_ReturnsNotPrimary()
    {
        using var db = CreateDbContext();
        var user = SeedUser(db);
        var bottle = SeedBottle(db, user.Id);
        SeedImage(db, bottle.Id);
        var service = CreateService(db, user.Id);

        var result = await service.AddImageAsync(bottle.Id, CreateFormFile(), CancellationToken.None);

        Assert.True(result.Success);
        Assert.False(result.Data!.IsPrimary);
        Assert.Equal(1, result.Data.SortOrder);
    }

    [Fact]
    public async Task AddImageAsync_WhenWebRootPathNull_UsesContentRootFallback()
    {
        using var db = CreateDbContext();
        var user = SeedUser(db);
        var bottle = SeedBottle(db, user.Id);
        var service = CreateService(db, user.Id, webRootPath: null!);

        var result = await service.AddImageAsync(bottle.Id, CreateFormFile(), CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
    }

    [Fact]
    public async Task AddImageAsync_WhenCancelled_Throws()
    {
        using var db = CreateDbContext();
        var user = SeedUser(db);
        var bottle = SeedBottle(db, user.Id);
        var service = CreateService(db, user.Id);
        var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => service.AddImageAsync(bottle.Id, CreateFormFile(), cts.Token));
    }

    [Fact]
    public async Task DeleteImageAsync_WhenImageNotFound_ReturnsNotFound()
    {
        using var db = CreateDbContext();
        var user = SeedUser(db);
        var service = CreateService(db, user.Id);

        var result = await service.DeleteImageAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Image not found.", result.Error);
        Assert.Equal(ErrorCode.NotFound, result.ErrorCode);
    }

    [Fact]
    public async Task DeleteImageAsync_WhenBottleDeleted_ReturnsNotFound()
    {
        using var db = CreateDbContext();
        var user = SeedUser(db);
        var bottle = SeedBottle(db, user.Id, isDeleted: true);
        var image = SeedImage(db, bottle.Id);
        var service = CreateService(db, user.Id);

        var result = await service.DeleteImageAsync(image.Id, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Bottle not found.", result.Error);
        Assert.Equal(ErrorCode.NotFound, result.ErrorCode);
    }

    [Fact]
    public async Task DeleteImageAsync_WhenWrongOwner_ReturnsForbidden()
    {
        using var db = CreateDbContext();
        var owner = SeedUser(db, "Owner");
        var other = SeedUser(db, "Other");
        var bottle = SeedBottle(db, owner.Id);
        var image = SeedImage(db, bottle.Id);
        var service = CreateService(db, other.Id);

        var result = await service.DeleteImageAsync(image.Id, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Forbidden.", result.Error);
        Assert.Equal(ErrorCode.Forbidden, result.ErrorCode);
    }

    [Fact]
    public async Task DeleteImageAsync_WhenValidAndFileMissing_SoftDeletes()
    {
        using var db = CreateDbContext();
        var user = SeedUser(db);
        var bottle = SeedBottle(db, user.Id);
        var image = SeedImage(db, bottle.Id);
        var service = CreateService(db, user.Id);

        var result = await service.DeleteImageAsync(image.Id, CancellationToken.None);

        Assert.True(result.Success);
        Assert.True(result.Data);
        var stored = await db.BottleImages.IgnoreQueryFilters().FirstAsync(i => i.Id == image.Id);
        Assert.True(stored.IsDeleted);
        Assert.NotNull(stored.DeletedAt);
    }

    [Fact]
    public async Task DeleteImageAsync_WhenFileExists_DeletesPhysicalFile()
    {
        using var db = CreateDbContext();
        var user = SeedUser(db);
        var bottle = SeedBottle(db, user.Id);

        var webRoot = Path.GetTempPath();
        var bottlesDir = Path.Combine(webRoot, "uploads", "bottles");
        Directory.CreateDirectory(bottlesDir);
        var fileName = $"{Guid.NewGuid()}.jpg";
        var fullPath = Path.Combine(bottlesDir, fileName);
        await File.WriteAllTextAsync(fullPath, "x");

        var image = new BottleImage
        {
            BottleId = bottle.Id,
            Url = $"/uploads/bottles/{fileName}",
            IsPrimary = true,
            SortOrder = 0
        };

        db.BottleImages.Add(image);
        db.SaveChanges();

        var service = CreateService(db, user.Id, webRootPath: webRoot);

        var result = await service.DeleteImageAsync(image.Id, CancellationToken.None);

        Assert.True(result.Success);
        Assert.False(File.Exists(fullPath));
    }

    [Fact]
    public async Task DeleteImageAsync_WhenCancelled_Throws()
    {
        using var db = CreateDbContext();
        var user = SeedUser(db);
        var bottle = SeedBottle(db, user.Id);
        var image = SeedImage(db, bottle.Id);
        var service = CreateService(db, user.Id);
        var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => service.DeleteImageAsync(image.Id, cts.Token));
    }

    [Fact]
    public async Task LinkImageAsync_WhenUrlWhitespace_ReturnsFail()
    {
        using var db = CreateDbContext();
        var user = SeedUser(db);
        var bottle = SeedBottle(db, user.Id);
        var service = CreateService(db, user.Id);

        var result = await service.LinkImageAsync(bottle.Id, new LinkImageRequest { Url = "   " }, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Image URL is required.", result.Error);
    }

    [Fact]
    public async Task LinkImageAsync_WhenBottleNotFound_ReturnsNotFound()
    {
        using var db = CreateDbContext();
        var user = SeedUser(db);
        var service = CreateService(db, user.Id);

        var result = await service.LinkImageAsync(Guid.NewGuid(), new LinkImageRequest { Url = "https://x.com/a.jpg" }, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Bottle not found.", result.Error);
        Assert.Equal(ErrorCode.NotFound, result.ErrorCode);
    }

    [Fact]
    public async Task LinkImageAsync_WhenWrongOwner_ReturnsForbidden()
    {
        using var db = CreateDbContext();
        var owner = SeedUser(db, "Owner");
        var other = SeedUser(db, "Other");
        var bottle = SeedBottle(db, owner.Id);
        var service = CreateService(db, other.Id);

        var result = await service.LinkImageAsync(bottle.Id, new LinkImageRequest { Url = "https://x.com/a.jpg" }, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Forbidden.", result.Error);
        Assert.Equal(ErrorCode.Forbidden, result.ErrorCode);
    }

    [Fact]
    public async Task LinkImageAsync_WhenFirstImage_ReturnsPrimary()
    {
        using var db = CreateDbContext();
        var user = SeedUser(db);
        var bottle = SeedBottle(db, user.Id);
        var service = CreateService(db, user.Id);

        var result = await service.LinkImageAsync(bottle.Id, new LinkImageRequest { Url = "https://x.com/a.jpg" }, CancellationToken.None);

        Assert.True(result.Success);
        Assert.True(result.Data!.IsPrimary);
        Assert.Equal("https://x.com/a.jpg", result.Data.Url);
        Assert.Equal(0, result.Data.SortOrder);
    }

    [Fact]
    public async Task LinkImageAsync_WhenSecondImage_ReturnsNotPrimary()
    {
        using var db = CreateDbContext();
        var user = SeedUser(db);
        var bottle = SeedBottle(db, user.Id);
        SeedImage(db, bottle.Id);
        var service = CreateService(db, user.Id);

        var result = await service.LinkImageAsync(bottle.Id, new LinkImageRequest { Url = "https://x.com/b.jpg" }, CancellationToken.None);

        Assert.True(result.Success);
        Assert.False(result.Data!.IsPrimary);
        Assert.Equal(1, result.Data.SortOrder);
    }

    [Fact]
    public async Task LinkImageAsync_WhenCancelled_Throws()
    {
        using var db = CreateDbContext();
        var user = SeedUser(db);
        var bottle = SeedBottle(db, user.Id);
        var service = CreateService(db, user.Id);
        var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => service.LinkImageAsync(bottle.Id, new LinkImageRequest { Url = "https://x.com/a.jpg" }, cts.Token));
    }
}
