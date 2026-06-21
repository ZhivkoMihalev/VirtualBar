using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;
using VirtualBar.Application.DTOs.News;
using VirtualBar.Application.Interfaces;
using VirtualBar.Domain.Entities;
using VirtualBar.Infrastructure.Decorators;
using VirtualBar.Infrastructure.Persistence;
using VirtualBar.Infrastructure.Services;

namespace VirtualBar.Tests.Services;

public sealed class NewsServiceTests
{
    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static INewsService CreateNewsService(AppDbContext db, bool isAdmin = false)
    {
        var mock = new Mock<ICurrentUser>();
        mock.Setup(u => u.IsAdmin).Returns(isAdmin);
        mock.Setup(u => u.UserId).Returns(Guid.NewGuid());
        mock.Setup(u => u.IsAuthenticated).Returns(true);
        var inner = new NewsService(db, mock.Object);
        return new NewsValidationDecorator(inner, db, mock.Object);
    }

    private static INewsService CreateNewsService(AppDbContext db, Guid userId, bool isAdmin = true)
    {
        var mock = new Mock<ICurrentUser>();
        mock.Setup(u => u.IsAdmin).Returns(isAdmin);
        mock.Setup(u => u.UserId).Returns(userId);
        mock.Setup(u => u.IsAuthenticated).Returns(true);
        var inner = new NewsService(db, mock.Object);
        return new NewsValidationDecorator(inner, db, mock.Object);
    }

    private static AppUser SeedUser(AppDbContext db, string displayName = "Author")
    {
        var user = new AppUser { Id = Guid.NewGuid(), UserName = $"{displayName}@x.com", Email = $"{displayName}@x.com", DisplayName = displayName };
        db.Users.Add(user); db.SaveChanges(); return user;
    }

    private static NewsPost SeedPost(AppDbContext db, Guid authorId, string lang = "bg", string title = "Title", string content = "Content")
    {
        var post = new NewsPost { AuthorId = authorId };
        post.Translations.Add(new NewsPostTranslation { LanguageCode = lang, Title = title, Content = content });
        db.NewsPosts.Add(post); db.SaveChanges(); return post;
    }

    private static IFormFile CreateFormFile(string fileName = "test.jpg", string contentType = "image/jpeg", long length = 1024)
    {
        var mock = new Mock<IFormFile>();
        mock.Setup(f => f.FileName).Returns(fileName);
        mock.Setup(f => f.ContentType).Returns(contentType);
        mock.Setup(f => f.Length).Returns(length);
        mock.Setup(f => f.CopyToAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return mock.Object;
    }

    private static List<NewsPostTranslationRequest> ValidTranslations() =>
    [
        new NewsPostTranslationRequest { LanguageCode = "bg", Title = "BG Title", Content = "BG Content" }
    ];

    #region GetAllAsync

    [Fact]
    public async Task GetAllAsync_WhenSkipNegative_ClampsAndReturnsOk()
    {
        var db = CreateDbContext();
        var author = SeedUser(db);
        SeedPost(db, author.Id);
        var service = CreateNewsService(db);

        var result = await service.GetAllAsync(-5, 10, "bg", CancellationToken.None);

        Assert.True(result.Success);
        Assert.Single(result.Data!);
    }

    [Fact]
    public async Task GetAllAsync_WhenTakeBelowOne_ClampsToTwentyAndReturnsOk()
    {
        var db = CreateDbContext();
        var author = SeedUser(db);
        SeedPost(db, author.Id);
        var service = CreateNewsService(db);

        var result = await service.GetAllAsync(0, 0, "bg", CancellationToken.None);

        Assert.True(result.Success);
        Assert.Single(result.Data!);
    }

    [Fact]
    public async Task GetAllAsync_WhenTakeAboveHundred_ClampsToTwentyAndReturnsOk()
    {
        var db = CreateDbContext();
        var author = SeedUser(db);
        SeedPost(db, author.Id);
        var service = CreateNewsService(db);

        var result = await service.GetAllAsync(0, 500, "bg", CancellationToken.None);

        Assert.True(result.Success);
        Assert.Single(result.Data!);
    }

    [Fact]
    public async Task GetAllAsync_WhenLangWhitespace_DefaultsToBgAndReturnsOk()
    {
        var db = CreateDbContext();
        var author = SeedUser(db);
        SeedPost(db, author.Id, "bg", "BG Title");
        var service = CreateNewsService(db);

        var result = await service.GetAllAsync(0, 10, "  ", CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("BG Title", result.Data!.Single().Title);
    }

    [Fact]
    public async Task GetAllAsync_WithCancelledToken_Throws()
    {
        var db = CreateDbContext();
        var service = CreateNewsService(db);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => service.GetAllAsync(0, 10, "bg", cts.Token));
    }

    #endregion

    #region GetByIdAsync

    [Fact]
    public async Task GetByIdAsync_WhenIdEmpty_ReturnsNotFound()
    {
        var db = CreateDbContext();
        var service = CreateNewsService(db);

        var result = await service.GetByIdAsync(Guid.Empty, "bg", CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("News post not found.", result.Error);
    }

    [Fact]
    public async Task GetByIdAsync_WhenIdNotInDb_ReturnsNotFound()
    {
        var db = CreateDbContext();
        var service = CreateNewsService(db);

        var result = await service.GetByIdAsync(Guid.NewGuid(), "bg", CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("News post not found.", result.Error);
    }

    [Fact]
    public async Task GetByIdAsync_WhenLangWhitespace_DefaultsToBgAndReturnsPost()
    {
        var db = CreateDbContext();
        var author = SeedUser(db);
        var post = SeedPost(db, author.Id, "bg", "BG Title");
        var service = CreateNewsService(db);

        var result = await service.GetByIdAsync(post.Id, "   ", CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("BG Title", result.Data!.Title);
    }

    [Fact]
    public async Task GetByIdAsync_WhenValid_ReturnsPostWithCorrectTitle()
    {
        var db = CreateDbContext();
        var author = SeedUser(db);
        var post = SeedPost(db, author.Id, "bg", "My Title");
        var service = CreateNewsService(db);

        var result = await service.GetByIdAsync(post.Id, "bg", CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("My Title", result.Data!.Title);
    }

    [Fact]
    public async Task GetByIdAsync_WithCancelledToken_Throws()
    {
        var db = CreateDbContext();
        var service = CreateNewsService(db);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => service.GetByIdAsync(Guid.NewGuid(), "bg", cts.Token));
    }

    #endregion

    #region CreateAsync

    [Fact]
    public async Task CreateAsync_WhenNotAdmin_ReturnsForbidden()
    {
        var db = CreateDbContext();
        var service = CreateNewsService(db, isAdmin: false);
        var request = new CreateNewsPostRequest { Translations = ValidTranslations() };

        var result = await service.CreateAsync(request, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Only administrators can create news posts.", result.Error);
    }

    [Fact]
    public async Task CreateAsync_WhenNoTranslations_ReturnsFail()
    {
        var db = CreateDbContext();
        var service = CreateNewsService(db, isAdmin: true);
        var request = new CreateNewsPostRequest { Translations = [] };

        var result = await service.CreateAsync(request, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("At least one translation is required.", result.Error);
    }

    [Fact]
    public async Task CreateAsync_WhenNoBulgarianTranslation_ReturnsFail()
    {
        var db = CreateDbContext();
        var service = CreateNewsService(db, isAdmin: true);
        var request = new CreateNewsPostRequest
        {
            Translations = [new NewsPostTranslationRequest { LanguageCode = "en", Title = "T", Content = "C" }]
        };

        var result = await service.CreateAsync(request, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Bulgarian translation is required.", result.Error);
    }

    [Fact]
    public async Task CreateAsync_WhenLanguageCodeEmpty_ReturnsFail()
    {
        var db = CreateDbContext();
        var service = CreateNewsService(db, isAdmin: true);
        var request = new CreateNewsPostRequest
        {
            Translations =
            [
                new NewsPostTranslationRequest { LanguageCode = "bg", Title = "T", Content = "C" },
                new NewsPostTranslationRequest { LanguageCode = "   ", Title = "T", Content = "C" }
            ]
        };

        var result = await service.CreateAsync(request, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Language code is required.", result.Error);
    }

    [Fact]
    public async Task CreateAsync_WhenTitleEmpty_ReturnsFail()
    {
        var db = CreateDbContext();
        var service = CreateNewsService(db, isAdmin: true);
        var request = new CreateNewsPostRequest
        {
            Translations = [new NewsPostTranslationRequest { LanguageCode = "bg", Title = "  ", Content = "C" }]
        };

        var result = await service.CreateAsync(request, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Title is required.", result.Error);
    }

    [Fact]
    public async Task CreateAsync_WhenContentEmpty_ReturnsFail()
    {
        var db = CreateDbContext();
        var service = CreateNewsService(db, isAdmin: true);
        var request = new CreateNewsPostRequest
        {
            Translations = [new NewsPostTranslationRequest { LanguageCode = "bg", Title = "T", Content = "  " }]
        };

        var result = await service.CreateAsync(request, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Content is required.", result.Error);
    }

    [Fact]
    public async Task CreateAsync_WhenValid_ReturnsCreatedPost()
    {
        var db = CreateDbContext();
        var author = SeedUser(db, "Admin");
        var service = CreateNewsService(db, author.Id, isAdmin: true);
        var request = new CreateNewsPostRequest
        {
            Translations = [new NewsPostTranslationRequest { LanguageCode = "bg", Title = "BG Title", Content = "BG Content" }]
        };

        var result = await service.CreateAsync(request, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("BG Title", result.Data!.Title);
        Assert.Equal("Admin", result.Data.AuthorDisplayName);
    }

    [Fact]
    public async Task CreateAsync_WithCancelledToken_Throws()
    {
        var db = CreateDbContext();
        var service = CreateNewsService(db, isAdmin: true);
        var request = new CreateNewsPostRequest { Translations = ValidTranslations() };
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => service.CreateAsync(request, cts.Token));
    }

    #endregion

    #region UpdateAsync

    [Fact]
    public async Task UpdateAsync_WhenNotAdmin_ReturnsForbidden()
    {
        var db = CreateDbContext();
        var service = CreateNewsService(db, isAdmin: false);
        var request = new UpdateNewsPostRequest { Translations = ValidTranslations() };

        var result = await service.UpdateAsync(Guid.NewGuid(), request, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Only administrators can update news posts.", result.Error);
    }

    [Fact]
    public async Task UpdateAsync_WhenIdEmpty_ReturnsNotFound()
    {
        var db = CreateDbContext();
        var service = CreateNewsService(db, isAdmin: true);
        var request = new UpdateNewsPostRequest { Translations = ValidTranslations() };

        var result = await service.UpdateAsync(Guid.Empty, request, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("News post not found.", result.Error);
    }

    [Fact]
    public async Task UpdateAsync_WhenPostNotFound_ReturnsNotFound()
    {
        var db = CreateDbContext();
        var service = CreateNewsService(db, isAdmin: true);
        var request = new UpdateNewsPostRequest { Translations = ValidTranslations() };

        var result = await service.UpdateAsync(Guid.NewGuid(), request, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("News post not found.", result.Error);
    }

    [Fact]
    public async Task UpdateAsync_WhenNoTranslations_ReturnsFail()
    {
        var db = CreateDbContext();
        var author = SeedUser(db);
        var post = SeedPost(db, author.Id);
        var service = CreateNewsService(db, isAdmin: true);
        var request = new UpdateNewsPostRequest { Translations = [] };

        var result = await service.UpdateAsync(post.Id, request, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("At least one translation is required.", result.Error);
    }

    [Fact]
    public async Task UpdateAsync_WhenLanguageCodeEmpty_ReturnsFail()
    {
        var db = CreateDbContext();
        var author = SeedUser(db);
        var post = SeedPost(db, author.Id);
        var service = CreateNewsService(db, isAdmin: true);
        var request = new UpdateNewsPostRequest
        {
            Translations = [new NewsPostTranslationRequest { LanguageCode = "  ", Title = "T", Content = "C" }]
        };

        var result = await service.UpdateAsync(post.Id, request, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Language code is required.", result.Error);
    }

    [Fact]
    public async Task UpdateAsync_WhenTitleEmpty_ReturnsFail()
    {
        var db = CreateDbContext();
        var author = SeedUser(db);
        var post = SeedPost(db, author.Id);
        var service = CreateNewsService(db, isAdmin: true);
        var request = new UpdateNewsPostRequest
        {
            Translations = [new NewsPostTranslationRequest { LanguageCode = "bg", Title = "  ", Content = "C" }]
        };

        var result = await service.UpdateAsync(post.Id, request, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Title is required.", result.Error);
    }

    [Fact]
    public async Task UpdateAsync_WhenContentEmpty_ReturnsFail()
    {
        var db = CreateDbContext();
        var author = SeedUser(db);
        var post = SeedPost(db, author.Id);
        var service = CreateNewsService(db, isAdmin: true);
        var request = new UpdateNewsPostRequest
        {
            Translations = [new NewsPostTranslationRequest { LanguageCode = "bg", Title = "T", Content = "  " }]
        };

        var result = await service.UpdateAsync(post.Id, request, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Content is required.", result.Error);
    }

    [Fact]
    public async Task UpdateAsync_WhenExistingTranslation_UpdatesInPlace()
    {
        var db = CreateDbContext();
        var author = SeedUser(db);
        var post = SeedPost(db, author.Id, "bg", "Old", "OldContent");
        var service = CreateNewsService(db, isAdmin: true);
        var request = new UpdateNewsPostRequest
        {
            Translations = [new NewsPostTranslationRequest { LanguageCode = "bg", Title = "New", Content = "NewContent" }]
        };

        var result = await service.UpdateAsync(post.Id, request, CancellationToken.None);

        Assert.True(result.Success);
        var reloaded = await db.NewsPosts.Include(p => p.Translations).FirstAsync(p => p.Id == post.Id);
        var tr = Assert.Single(reloaded.Translations);
        Assert.Equal("New", tr.Title);
        Assert.Equal("NewContent", tr.Content);
    }

    [Fact]
    public async Task UpdateAsync_WhenNewTranslation_AddsToPost()
    {
        var db = CreateDbContext();
        var author = SeedUser(db);
        var post = SeedPost(db, author.Id, "bg", "BG Title");
        var service = CreateNewsService(db, isAdmin: true);
        var request = new UpdateNewsPostRequest
        {
            Translations =
            [
                new NewsPostTranslationRequest { LanguageCode = "bg", Title = "BG Title", Content = "BG Content" },
                new NewsPostTranslationRequest { LanguageCode = "en", Title = "Title", Content = "Content" }
            ]
        };

        var result = await service.UpdateAsync(post.Id, request, CancellationToken.None);

        Assert.True(result.Success);
        var reloaded = await db.NewsPosts.Include(p => p.Translations).FirstAsync(p => p.Id == post.Id);
        Assert.Equal(2, reloaded.Translations.Count);
        Assert.Contains(reloaded.Translations, t => t.LanguageCode == "en" && t.Title == "Title");
    }

    [Fact]
    public async Task UpdateAsync_WithCancelledToken_Throws()
    {
        var db = CreateDbContext();
        var service = CreateNewsService(db, isAdmin: true);
        var request = new UpdateNewsPostRequest { Translations = ValidTranslations() };
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => service.UpdateAsync(Guid.NewGuid(), request, cts.Token));
    }

    #endregion

    #region DeleteAsync

    [Fact]
    public async Task DeleteAsync_WhenNotAdmin_ReturnsForbidden()
    {
        var db = CreateDbContext();
        var service = CreateNewsService(db, isAdmin: false);

        var result = await service.DeleteAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Only administrators can delete news posts.", result.Error);
    }

    [Fact]
    public async Task DeleteAsync_WhenIdEmpty_ReturnsFail()
    {
        var db = CreateDbContext();
        var service = CreateNewsService(db, isAdmin: true);

        var result = await service.DeleteAsync(Guid.Empty, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Post ID is required.", result.Error);
    }

    [Fact]
    public async Task DeleteAsync_WhenPostNotFound_ReturnsNotFound()
    {
        var db = CreateDbContext();
        var service = CreateNewsService(db, isAdmin: true);

        var result = await service.DeleteAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("News post not found.", result.Error);
    }

    [Fact]
    public async Task DeleteAsync_WhenValid_ReturnsOkAndSoftDeletes()
    {
        var db = CreateDbContext();
        var author = SeedUser(db);
        var post = SeedPost(db, author.Id);
        var service = CreateNewsService(db, isAdmin: true);

        var result = await service.DeleteAsync(post.Id, CancellationToken.None);

        Assert.True(result.Success);
        Assert.True(result.Data);
        var reloaded = await db.NewsPosts.FindAsync(post.Id);
        Assert.True(reloaded!.IsDeleted);
    }

    [Fact]
    public async Task DeleteAsync_WithCancelledToken_Throws()
    {
        var db = CreateDbContext();
        var service = CreateNewsService(db, isAdmin: true);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => service.DeleteAsync(Guid.NewGuid(), cts.Token));
    }

    #endregion

    #region UploadCoverAsync

    [Fact]
    public async Task UploadCoverAsync_WhenNotAdmin_ReturnsForbidden()
    {
        var db = CreateDbContext();
        var service = CreateNewsService(db, isAdmin: false);

        var result = await service.UploadCoverAsync(CreateFormFile(), Path.GetTempPath(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Only administrators can upload news covers.", result.Error);
    }

    [Fact]
    public async Task UploadCoverAsync_WhenFileNull_ReturnsFail()
    {
        var db = CreateDbContext();
        var service = CreateNewsService(db, isAdmin: true);

        var result = await service.UploadCoverAsync(null!, Path.GetTempPath(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("No file provided.", result.Error);
    }

    [Fact]
    public async Task UploadCoverAsync_WhenFileEmpty_ReturnsFail()
    {
        var db = CreateDbContext();
        var service = CreateNewsService(db, isAdmin: true);

        var result = await service.UploadCoverAsync(CreateFormFile(length: 0), Path.GetTempPath(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("No file provided.", result.Error);
    }

    [Fact]
    public async Task UploadCoverAsync_WhenFileTooLarge_ReturnsFail()
    {
        var db = CreateDbContext();
        var service = CreateNewsService(db, isAdmin: true);

        var result = await service.UploadCoverAsync(CreateFormFile(length: 11 * 1024 * 1024), Path.GetTempPath(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("File must be under 10 MB.", result.Error);
    }

    [Fact]
    public async Task UploadCoverAsync_WhenWrongContentType_ReturnsFail()
    {
        var db = CreateDbContext();
        var service = CreateNewsService(db, isAdmin: true);

        var result = await service.UploadCoverAsync(CreateFormFile(contentType: "application/pdf"), Path.GetTempPath(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Only JPEG, PNG, WebP and GIF images are allowed.", result.Error);
    }

    [Fact]
    public async Task UploadCoverAsync_WhenValid_ReturnsUrl()
    {
        var db = CreateDbContext();
        var service = CreateNewsService(db, isAdmin: true);
        var file = CreateFormFile(length: 100);

        var result = await service.UploadCoverAsync(file, Path.GetTempPath(), CancellationToken.None);

        Assert.True(result.Success);
        Assert.StartsWith("/uploads/news/", result.Data!);
    }

    [Fact]
    public async Task UploadCoverAsync_WithCancelledToken_Throws()
    {
        var db = CreateDbContext();
        var service = CreateNewsService(db, isAdmin: true);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => service.UploadCoverAsync(CreateFormFile(), Path.GetTempPath(), cts.Token));
    }

    #endregion
}
