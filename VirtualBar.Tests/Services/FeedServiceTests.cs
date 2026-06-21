using Microsoft.EntityFrameworkCore;
using Moq;
using VirtualBar.Application.DTOs.Feed;
using VirtualBar.Application.Interfaces;
using VirtualBar.Domain.Entities;
using VirtualBar.Domain.Enums;
using VirtualBar.Infrastructure.Decorators;
using VirtualBar.Infrastructure.Persistence;
using VirtualBar.Infrastructure.Services;

namespace VirtualBar.Tests.Services;

public sealed class FeedServiceTests
{
    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static IFeedService CreateFeedService(AppDbContext db, Guid? currentUserId = null)
    {
        var mock = new Mock<ICurrentUser>();
        mock.Setup(u => u.UserId).Returns(currentUserId ?? Guid.Empty);
        mock.Setup(u => u.IsAuthenticated).Returns(currentUserId.HasValue);
        var inner = new FeedService(db, mock.Object);
        return new FeedValidationDecorator(inner);
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

    private static NewsPost SeedNewsPost(
        AppDbContext db,
        Guid authorId,
        IEnumerable<NewsPostTranslation>? translations = null,
        bool isDeleted = false)
    {
        var post = new NewsPost
        {
            Id = Guid.NewGuid(),
            AuthorId = authorId,
            IsDeleted = isDeleted,
            DeletedAt = isDeleted ? DateTime.UtcNow : null
        };

        if (translations is not null)
        {
            foreach (var translation in translations)
            {
                translation.PostId = post.Id;
                post.Translations.Add(translation);
            }
        }

        db.NewsPosts.Add(post);
        db.SaveChanges();
        return post;
    }

    private static NewsPostTranslation Translation(string languageCode, string title, string content)
        => new()
        {
            LanguageCode = languageCode,
            Title = title,
            Content = content
        };

    private static Bottle SeedBottle(
        AppDbContext db,
        Guid userId,
        string name = "Test Bottle",
        bool isForSale = false,
        DateTime? forSaleAt = null,
        bool isDeleted = false)
    {
        var bottle = new Bottle
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = name,
            Category = SpiritCategory.Whisky,
            Condition = BottleCondition.Sealed,
            IsForSale = isForSale,
            ForSaleAt = forSaleAt,
            IsDeleted = isDeleted,
            DeletedAt = isDeleted ? DateTime.UtcNow : null
        };
        db.Bottles.Add(bottle);
        db.SaveChanges();
        return bottle;
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

    #region GetFeedAsync — validation decorator

    [Fact]
    public async Task GetFeedAsync_WhenSkipNegative_NormalizesToZero()
    {
        var db = CreateDbContext();
        var author = SeedUser(db, "Author");
        SeedNewsPost(db, author.Id, new[] { Translation("bg", "BG Title", "BG Content") });
        var service = CreateFeedService(db);

        var result = await service.GetFeedAsync(-1, 20, "bg", CancellationToken.None);

        Assert.True(result.Success);
        Assert.Single(result.Data!);
    }

    [Fact]
    public async Task GetFeedAsync_WhenTakeZero_NormalizesToDefault()
    {
        var db = CreateDbContext();
        var author = SeedUser(db, "Author");
        SeedNewsPost(db, author.Id, new[] { Translation("bg", "BG Title", "BG Content") });
        var service = CreateFeedService(db);

        var result = await service.GetFeedAsync(0, 0, "bg", CancellationToken.None);

        Assert.True(result.Success);
        Assert.Single(result.Data!);
    }

    [Fact]
    public async Task GetFeedAsync_WhenTakeTooLarge_NormalizesToDefault()
    {
        var db = CreateDbContext();
        var author = SeedUser(db, "Author");
        SeedNewsPost(db, author.Id, new[] { Translation("bg", "BG Title", "BG Content") });
        var service = CreateFeedService(db);

        var result = await service.GetFeedAsync(0, 200, "bg", CancellationToken.None);

        Assert.True(result.Success);
        Assert.Single(result.Data!);
    }

    [Fact]
    public async Task GetFeedAsync_WhenLangWhitespace_DefaultsToBg()
    {
        var db = CreateDbContext();
        var author = SeedUser(db, "Author");
        SeedNewsPost(db, author.Id, new[] { Translation("bg", "BG Title", "BG Content") });
        var service = CreateFeedService(db);

        var result = await service.GetFeedAsync(0, 20, "   ", CancellationToken.None);

        Assert.True(result.Success);
        var item = Assert.Single(result.Data!);
        Assert.Equal("BG Title", item.PostTitle);
    }

    [Fact]
    public async Task GetFeedAsync_WithCancelledToken_Throws()
    {
        var db = CreateDbContext();
        var service = CreateFeedService(db);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => service.GetFeedAsync(0, 20, "bg", cts.Token));
    }

    #endregion

    #region GetFeedAsync — news

    [Fact]
    public async Task GetFeedAsync_WhenNotAuthenticated_ReturnsOnlyNews()
    {
        var db = CreateDbContext();
        var author = SeedUser(db, "Author");
        var followed = SeedUser(db, "Followed");
        SeedNewsPost(db, author.Id, new[] { Translation("bg", "News", "BG Content") });
        SeedBottle(db, followed.Id, "Bottle A");
        var service = CreateFeedService(db);

        var result = await service.GetFeedAsync(0, 20, "bg", CancellationToken.None);

        Assert.True(result.Success);
        var item = Assert.Single(result.Data!);
        Assert.Equal(FeedItemType.News, item.Type);
    }

    [Fact]
    public async Task GetFeedAsync_WhenLangMatchFound_UsesThatTranslation()
    {
        var db = CreateDbContext();
        var author = SeedUser(db, "Author");
        SeedNewsPost(db, author.Id, new[]
        {
            Translation("bg", "BG", "BG Content"),
            Translation("en", "English Title", "EN content")
        });
        var service = CreateFeedService(db);

        var result = await service.GetFeedAsync(0, 20, "en", CancellationToken.None);

        Assert.True(result.Success);
        var item = Assert.Single(result.Data!);
        Assert.Equal("English Title", item.PostTitle);
        Assert.Equal("EN content", item.PostContent);
    }

    [Fact]
    public async Task GetFeedAsync_WhenLangMatchMissing_FallsBackToBg()
    {
        var db = CreateDbContext();
        var author = SeedUser(db, "Author");
        SeedNewsPost(db, author.Id, new[]
        {
            Translation("bg", "BG Title", "BG Content")
        });
        var service = CreateFeedService(db);

        var result = await service.GetFeedAsync(0, 20, "en", CancellationToken.None);

        Assert.True(result.Success);
        var item = Assert.Single(result.Data!);
        Assert.Equal("BG Title", item.PostTitle);
        Assert.Equal("BG Content", item.PostContent);
    }

    [Fact]
    public async Task GetFeedAsync_WhenNoTranslations_UsesEmptyStrings()
    {
        var db = CreateDbContext();
        var author = SeedUser(db, "Author");
        SeedNewsPost(db, author.Id);
        var service = CreateFeedService(db);

        var result = await service.GetFeedAsync(0, 20, "bg", CancellationToken.None);

        Assert.True(result.Success);
        var item = Assert.Single(result.Data!);
        Assert.Equal(string.Empty, item.PostTitle);
        Assert.Equal(string.Empty, item.PostContent);
    }

    [Fact]
    public async Task GetFeedAsync_ExcludesDeletedNewsPosts()
    {
        var db = CreateDbContext();
        var author = SeedUser(db, "Author");
        SeedNewsPost(db, author.Id, new[] { Translation("bg", "Deleted News", "BG Content") }, isDeleted: true);
        var service = CreateFeedService(db);

        var result = await service.GetFeedAsync(0, 20, "bg", CancellationToken.None);

        Assert.True(result.Success);
        Assert.Empty(result.Data!);
    }

    #endregion

    #region GetFeedAsync — followed users

    [Fact]
    public async Task GetFeedAsync_WhenAuthenticatedWithoutFollows_ReturnsOnlyNews()
    {
        var db = CreateDbContext();
        var me = SeedUser(db, "Me");
        var other = SeedUser(db, "Other");
        var author = SeedUser(db, "Author");
        SeedNewsPost(db, author.Id, new[] { Translation("bg", "News", "BG Content") });
        SeedBottle(db, other.Id, "Bottle A");
        var service = CreateFeedService(db, me.Id);

        var result = await service.GetFeedAsync(0, 20, "bg", CancellationToken.None);

        Assert.True(result.Success);
        var item = Assert.Single(result.Data!);
        Assert.Equal(FeedItemType.News, item.Type);
    }

    [Fact]
    public async Task GetFeedAsync_WhenFollowsUser_IncludesNewBottle()
    {
        var db = CreateDbContext();
        var me = SeedUser(db, "Me");
        var followed = SeedUser(db, "Followed");
        SeedFollow(db, me.Id, followed.Id);
        SeedBottle(db, followed.Id, "Followed Bottle");
        var service = CreateFeedService(db, me.Id);

        var result = await service.GetFeedAsync(0, 20, "bg", CancellationToken.None);

        Assert.True(result.Success);
        var item = Assert.Single(result.Data!);
        Assert.Equal(FeedItemType.NewBottle, item.Type);
        Assert.Equal("Followed Bottle", item.BottleName);
        Assert.Equal("Followed", item.BottleUserDisplayName);
    }

    [Fact]
    public async Task GetFeedAsync_WhenForSaleWithForSaleAt_IncludesForSaleItem()
    {
        var db = CreateDbContext();
        var me = SeedUser(db, "Me");
        var followed = SeedUser(db, "Followed");
        SeedFollow(db, me.Id, followed.Id);
        var bottle = SeedBottle(db, followed.Id, "Sale Bottle", isForSale: true, forSaleAt: DateTime.UtcNow);
        bottle.AskingPrice = 199.99m;
        bottle.Currency = "EUR";
        db.SaveChanges();
        var service = CreateFeedService(db, me.Id);

        var result = await service.GetFeedAsync(0, 20, "bg", CancellationToken.None);

        Assert.True(result.Success);
        Assert.Contains(result.Data!, i => i.Type == FeedItemType.ForSale && i.AskingPrice == 199.99m && i.Currency == "EUR");
    }

    [Fact]
    public async Task GetFeedAsync_WhenForSaleAtNull_ExcludesForSaleItem()
    {
        var db = CreateDbContext();
        var me = SeedUser(db, "Me");
        var followed = SeedUser(db, "Followed");
        SeedFollow(db, me.Id, followed.Id);
        SeedBottle(db, followed.Id, "Sale Bottle", isForSale: true, forSaleAt: null);
        var service = CreateFeedService(db, me.Id);

        var result = await service.GetFeedAsync(0, 20, "bg", CancellationToken.None);

        Assert.True(result.Success);
        Assert.DoesNotContain(result.Data!, i => i.Type == FeedItemType.ForSale);
    }

    [Fact]
    public async Task GetFeedAsync_WhenFollowsUser_ExcludesDeletedBottle()
    {
        var db = CreateDbContext();
        var me = SeedUser(db, "Me");
        var followed = SeedUser(db, "Followed");
        SeedFollow(db, me.Id, followed.Id);
        SeedBottle(db, followed.Id, "Deleted Bottle", isDeleted: true);
        var service = CreateFeedService(db, me.Id);

        var result = await service.GetFeedAsync(0, 20, "bg", CancellationToken.None);

        Assert.True(result.Success);
        Assert.Empty(result.Data!);
    }

    #endregion
}
