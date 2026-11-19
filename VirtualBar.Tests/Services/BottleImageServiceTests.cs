using System.Text.Json;
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

    private static BottleImage SeedImage(
        AppDbContext db, Guid bottleId, string url = "/uploads/bottles/test.jpg", bool isDeleted = false)
    {
        var image = new BottleImage
        {
            BottleId = bottleId, Url = url,
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
        Assert.Equal("Only JPEG, PNG and WebP images are allowed.", result.Error);
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

        var result = await service.DeleteImageAsync(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Image not found.", result.Error);
        Assert.Equal(ErrorCode.NotFound, result.ErrorCode);
    }

    /// <summary>
    /// The image belongs to another bottle of the same owner. The route names a bottle that has no such image, so the
    /// image stays (#72).
    /// </summary>
    [Fact]
    public async Task DeleteImageAsync_WhenImageBelongsToAnotherBottle_ReturnsNotFoundAndKeepsTheImage()
    {
        using var db = CreateDbContext();
        var user = SeedUser(db);
        var namedBottle = SeedBottle(db, user.Id);
        var imagesBottle = SeedBottle(db, user.Id);
        var image = SeedImage(db, imagesBottle.Id);
        var service = CreateService(db, user.Id);

        var result = await service.DeleteImageAsync(namedBottle.Id, image.Id, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Image not found.", result.Error);
        Assert.Equal(ErrorCode.NotFound, result.ErrorCode);
        Assert.False((await db.BottleImages.IgnoreQueryFilters().SingleAsync(i => i.Id == image.Id)).IsDeleted);
    }

    [Fact]
    public async Task DeleteImageAsync_WhenBottleDeleted_ReturnsNotFound()
    {
        using var db = CreateDbContext();
        var user = SeedUser(db);
        var bottle = SeedBottle(db, user.Id, isDeleted: true);
        var image = SeedImage(db, bottle.Id);
        var service = CreateService(db, user.Id);

        var result = await service.DeleteImageAsync(bottle.Id, image.Id, CancellationToken.None);

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

        var result = await service.DeleteImageAsync(bottle.Id, image.Id, CancellationToken.None);

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

        var result = await service.DeleteImageAsync(bottle.Id, image.Id, CancellationToken.None);

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

        var result = await service.DeleteImageAsync(bottle.Id, image.Id, CancellationToken.None);

        Assert.True(result.Success);
        Assert.False(File.Exists(fullPath));
    }

    /// <summary>
    /// A bottle linked from the catalog can carry the very upload another bottle shows (a product approved with its
    /// source bottle's image). Deleting one of the two images used to delete the file under both (#134).
    /// </summary>
    [Fact]
    public async Task DeleteImageAsync_WhenAnotherBottleShowsTheSameUpload_KeepsTheFile()
    {
        using var webRoot = new TempWebRoot();
        var file = webRoot.CreateFile("uploads/bottles/shared.jpg");
        using var db = CreateDbContext();
        var owner = SeedUser(db, "Owner");
        var deleter = SeedUser(db, "Deleter");
        SeedImage(db, SeedBottle(db, owner.Id).Id, "/uploads/bottles/shared.jpg");
        var bottle = SeedBottle(db, deleter.Id);
        var image = SeedImage(db, bottle.Id, "/uploads/bottles/shared.jpg");
        var service = CreateService(db, deleter.Id, webRoot.Root);

        var result = await service.DeleteImageAsync(bottle.Id, image.Id, CancellationToken.None);

        Assert.True(result.Success);
        Assert.True((await db.BottleImages.SingleAsync(i => i.Id == image.Id)).IsDeleted);
        Assert.True(File.Exists(file));
    }

    /// <summary>
    /// A catalog product's image can be a bottle's upload; deleting that bottle's image used to delete the product's
    /// image too (#134). Kept for a soft-deleted product as well: no file is deleted while any product names it.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DeleteImageAsync_WhenAProductShowsTheUpload_KeepsTheFile(bool productDeleted)
    {
        using var webRoot = new TempWebRoot();
        var file = webRoot.CreateFile("uploads/bottles/product.jpg");
        using var db = CreateDbContext();
        var user = SeedUser(db);
        var bottle = SeedBottle(db, user.Id);
        var image = SeedImage(db, bottle.Id, "/uploads/bottles/product.jpg");
        SeedProduct(db, imageUrl: "/uploads/bottles/product.jpg", isDeleted: productDeleted);
        var service = CreateService(db, user.Id, webRoot.Root);

        var result = await service.DeleteImageAsync(bottle.Id, image.Id, CancellationToken.None);

        Assert.True(result.Success);
        Assert.True((await db.BottleImages.SingleAsync(i => i.Id == image.Id)).IsDeleted);
        Assert.True(File.Exists(file));
    }

    /// <summary>
    /// The path used to be built from the stored URL as it was, so a URL with <c>..</c>, or one naming another folder,
    /// made the API delete a file outside the bottle uploads (#134). The image is still deleted; the file is not.
    /// </summary>
    [Theory]
    [InlineData("/../outside.txt", "outside.txt")]
    [InlineData("/uploads/bottles/../../../outside.txt", "outside.txt")]
    [InlineData("/uploads/avatars/someone.png", "wwwroot/uploads/avatars/someone.png")]
    public async Task DeleteImageAsync_WhenTheUrlPointsOutsideTheBottleUploads_KeepsTheFile(string url, string target)
    {
        using var webRoot = new TempWebRoot();
        var file = webRoot.CreateFileFromBase(target);
        using var db = CreateDbContext();
        var user = SeedUser(db);
        var bottle = SeedBottle(db, user.Id);
        var image = SeedImage(db, bottle.Id, url);
        var service = CreateService(db, user.Id, webRoot.Root);

        var result = await service.DeleteImageAsync(bottle.Id, image.Id, CancellationToken.None);

        Assert.True(result.Success);
        Assert.True((await db.BottleImages.SingleAsync(i => i.Id == image.Id)).IsDeleted);
        Assert.True(File.Exists(file));
    }

    /// <summary>Only a live image keeps an upload: one deleted earlier does not, so the last live user deletes the file.</summary>
    [Fact]
    public async Task DeleteImageAsync_WhenTheOtherImageOfTheUploadIsDeleted_DeletesTheFile()
    {
        using var webRoot = new TempWebRoot();
        var file = webRoot.CreateFile("uploads/bottles/once-shared.jpg");
        using var db = CreateDbContext();
        var user = SeedUser(db);
        SeedImage(db, SeedBottle(db, user.Id).Id, "/uploads/bottles/once-shared.jpg", isDeleted: true);
        var bottle = SeedBottle(db, user.Id);
        var image = SeedImage(db, bottle.Id, "/uploads/bottles/once-shared.jpg");
        var service = CreateService(db, user.Id, webRoot.Root);

        var result = await service.DeleteImageAsync(bottle.Id, image.Id, CancellationToken.None);

        Assert.True(result.Success);
        Assert.False(File.Exists(file));
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
            () => service.DeleteImageAsync(bottle.Id, image.Id, cts.Token));
    }

    /// <summary>
    /// Linking used to store whatever address the body carried, so a collector could make every viewer of the bottle
    /// load a tracking pixel (#131). The body is read the way the API binds it, so the test sends exactly what such a
    /// client sends; it must be refused and leave the bottle without an image.
    /// </summary>
    [Fact]
    public async Task LinkImageAsync_WhenTheBodyCarriesAnAddress_ReturnsFailAndStoresNothing()
    {
        using var db = CreateDbContext();
        var user = SeedUser(db);
        var bottle = SeedBottle(db, user.Id);
        var service = CreateService(db, user.Id);
        var request = JsonSerializer.Deserialize<LinkImageRequest>(
            """{ "url": "https://tracker.example/pixel.png" }""", JsonSerializerOptions.Web)!;

        var result = await service.LinkImageAsync(bottle.Id, request, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ErrorCode.Validation, result.ErrorCode);
        Assert.Empty(db.BottleImages);
    }

    [Fact]
    public async Task LinkImageAsync_WhenProductIdMissing_ReturnsFail()
    {
        using var db = CreateDbContext();
        var user = SeedUser(db);
        var bottle = SeedBottle(db, user.Id);
        var service = CreateService(db, user.Id);

        var result = await service.LinkImageAsync(bottle.Id, new LinkImageRequest(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Product is required.", result.Error);
        Assert.Equal(ErrorCode.Validation, result.ErrorCode);
    }

    [Fact]
    public async Task LinkImageAsync_WhenBottleNotFound_ReturnsNotFound()
    {
        using var db = CreateDbContext();
        var user = SeedUser(db);
        var product = SeedProduct(db);
        var service = CreateService(db, user.Id);

        var result = await service.LinkImageAsync(Guid.NewGuid(), LinkTo(product), CancellationToken.None);

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
        var product = SeedProduct(db);
        var service = CreateService(db, other.Id);

        var result = await service.LinkImageAsync(bottle.Id, LinkTo(product), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Forbidden.", result.Error);
        Assert.Equal(ErrorCode.Forbidden, result.ErrorCode);
        Assert.Empty(db.BottleImages);
    }

    [Fact]
    public async Task LinkImageAsync_WhenProductNotFound_ReturnsNotFound()
    {
        using var db = CreateDbContext();
        var user = SeedUser(db);
        var bottle = SeedBottle(db, user.Id);
        var service = CreateService(db, user.Id);

        var result = await service.LinkImageAsync(
            bottle.Id, new LinkImageRequest { ProductId = Guid.NewGuid() }, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Product not found.", result.Error);
        Assert.Equal(ErrorCode.NotFound, result.ErrorCode);
        Assert.Empty(db.BottleImages);
    }

    [Fact]
    public async Task LinkImageAsync_WhenProductDeleted_ReturnsNotFound()
    {
        using var db = CreateDbContext();
        var user = SeedUser(db);
        var bottle = SeedBottle(db, user.Id);
        var product = SeedProduct(db, isDeleted: true);
        var service = CreateService(db, user.Id);

        var result = await service.LinkImageAsync(bottle.Id, LinkTo(product), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Product not found.", result.Error);
        Assert.Equal(ErrorCode.NotFound, result.ErrorCode);
        Assert.Empty(db.BottleImages);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public async Task LinkImageAsync_WhenProductHasNoImage_ReturnsFail(string? imageUrl)
    {
        using var db = CreateDbContext();
        var user = SeedUser(db);
        var bottle = SeedBottle(db, user.Id);
        var product = SeedProduct(db, imageUrl: imageUrl);
        var service = CreateService(db, user.Id);

        var result = await service.LinkImageAsync(bottle.Id, LinkTo(product), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("The product has no image.", result.Error);
        Assert.Equal(ErrorCode.Validation, result.ErrorCode);
        Assert.Empty(db.BottleImages);
    }

    /// <summary>The bottle takes the product's own image address, the only one linking can store (#131).</summary>
    [Fact]
    public async Task LinkImageAsync_WhenFirstImage_StoresTheProductsImageAsPrimary()
    {
        using var db = CreateDbContext();
        var user = SeedUser(db);
        var bottle = SeedBottle(db, user.Id);
        var product = SeedProduct(db, imageUrl: "https://images.example.test/lagavulin-16.png");
        var service = CreateService(db, user.Id);

        var result = await service.LinkImageAsync(bottle.Id, LinkTo(product), CancellationToken.None);

        Assert.True(result.Success);
        Assert.True(result.Data!.IsPrimary);
        Assert.Equal("https://images.example.test/lagavulin-16.png", result.Data.Url);
        Assert.Equal(0, result.Data.SortOrder);
        var stored = Assert.Single(db.BottleImages);
        Assert.Equal(bottle.Id, stored.BottleId);
        Assert.Equal("https://images.example.test/lagavulin-16.png", stored.Url);
    }

    [Fact]
    public async Task LinkImageAsync_WhenSecondImage_ReturnsNotPrimary()
    {
        using var db = CreateDbContext();
        var user = SeedUser(db);
        var bottle = SeedBottle(db, user.Id);
        SeedImage(db, bottle.Id);
        var product = SeedProduct(db);
        var service = CreateService(db, user.Id);

        var result = await service.LinkImageAsync(bottle.Id, LinkTo(product), CancellationToken.None);

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
        var product = SeedProduct(db);
        var service = CreateService(db, user.Id);
        var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => service.LinkImageAsync(bottle.Id, LinkTo(product), cts.Token));
    }

    private static Product SeedProduct(
        AppDbContext db, string? imageUrl = "https://images.example.test/product.png", bool isDeleted = false)
    {
        var product = new Product
        {
            Name = "Lagavulin 16",
            CanonicalKey = $"lagavulin|16|{Guid.NewGuid():N}",
            Category = VirtualBar.Domain.Enums.SpiritCategory.Whisky,
            ImageUrl = imageUrl,
            IsDeleted = isDeleted, DeletedAt = isDeleted ? DateTime.UtcNow : null
        };

        db.Products.Add(product);
        db.SaveChanges();

        return product;
    }

    private static LinkImageRequest LinkTo(Product product) => new() { ProductId = product.Id };

    /// <summary>
    /// A web root of its own (<c>wwwroot</c>) inside a new temporary folder, so a test can check which file on disk
    /// survives, including one outside the web root. The whole folder is removed when the test ends.
    /// </summary>
    private sealed class TempWebRoot : IDisposable
    {
        private readonly string baseDirectory =
            Path.Combine(Path.GetTempPath(), $"virtualbar-tests-{Guid.NewGuid():N}");

        public TempWebRoot() => Directory.CreateDirectory(Root);

        /// <summary>The web root the service is given.</summary>
        public string Root => Path.Combine(baseDirectory, "wwwroot");

        /// <summary>Creates a file at <paramref name="relativePath"/> under the web root; returns its full path.</summary>
        public string CreateFile(string relativePath) => CreateFileFromBase($"wwwroot/{relativePath}");

        /// <summary>Creates a file at <paramref name="relativePath"/> under the temporary folder, the web root's parent.</summary>
        public string CreateFileFromBase(string relativePath)
        {
            var fullPath = Path.Combine(baseDirectory, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            File.WriteAllText(fullPath, "x");
            return fullPath;
        }

        public void Dispose() => Directory.Delete(baseDirectory, recursive: true);
    }
}
