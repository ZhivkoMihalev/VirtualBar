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

    private static AppUser SeedUser(AppDbContext db, string displayName = "Test User", string? avatarUrl = null)
    {
        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            UserName = $"{displayName}@example.com",
            Email = $"{displayName}@example.com",
            DisplayName = displayName,
            AvatarUrl = avatarUrl
        };
        db.Users.Add(user);
        db.SaveChanges();
        return user;
    }

    private static NewsPost SeedNewsPost(
        AppDbContext db,
        Guid authorId,
        IEnumerable<NewsPostTranslation>? translations = null,
        bool isDeleted = false,
        DateTime? createdAt = null)
    {
        var post = new NewsPost
        {
            Id = Guid.NewGuid(),
            AuthorId = authorId,
            IsDeleted = isDeleted,
            DeletedAt = isDeleted ? DateTime.UtcNow : null,
            CreatedAt = createdAt ?? DateTime.UtcNow
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
        bool isDeleted = false,
        DateTime? createdAt = null,
        Guid? productId = null)
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
            DeletedAt = isDeleted ? DateTime.UtcNow : null,
            CreatedAt = createdAt ?? DateTime.UtcNow,
            ProductId = productId
        };
        db.Bottles.Add(bottle);
        db.SaveChanges();
        return bottle;
    }

    private static BottleImage SeedPrimaryImage(AppDbContext db, Guid bottleId, string url)
    {
        var image = new BottleImage
        {
            Id = Guid.NewGuid(),
            BottleId = bottleId,
            Url = url,
            IsPrimary = true
        };
        db.BottleImages.Add(image);
        db.SaveChanges();
        return image;
    }

    private static Product SeedProduct(
        AppDbContext db,
        string name = "Lagavulin 16",
        bool isDeleted = false,
        string? brand = null,
        Guid? distilleryId = null)
    {
        var product = new Product
        {
            Id = Guid.NewGuid(),
            Name = name,
            Brand = brand,
            DistilleryId = distilleryId,
            Category = SpiritCategory.Whisky,
            CanonicalKey = $"key-{Guid.NewGuid():N}",
            IsDeleted = isDeleted,
            DeletedAt = isDeleted ? DateTime.UtcNow : null
        };
        db.Products.Add(product);
        db.SaveChanges();
        return product;
    }

    private static Distillery SeedDistillery(AppDbContext db, string name, bool isDeleted = false)
    {
        var distillery = new Distillery
        {
            Id = Guid.NewGuid(),
            Name = name,
            IsDeleted = isDeleted,
            DeletedAt = isDeleted ? DateTime.UtcNow : null
        };
        db.Distilleries.Add(distillery);
        db.SaveChanges();
        return distillery;
    }

    private static BottleReview SeedReview(
        AppDbContext db,
        Guid bottleId,
        Guid userId,
        int score = 90,
        string? summary = null,
        IEnumerable<FlavorTag>? flavors = null,
        DateTime? createdAt = null,
        bool isDeleted = false)
    {
        var review = new BottleReview
        {
            Id = Guid.NewGuid(),
            BottleId = bottleId,
            UserId = userId,
            Score = score,
            Summary = summary,
            CreatedAt = createdAt ?? DateTime.UtcNow,
            UpdatedAt = createdAt ?? DateTime.UtcNow,
            IsDeleted = isDeleted,
            DeletedAt = isDeleted ? DateTime.UtcNow : null
        };

        foreach (var flavor in flavors ?? [])
            review.Flavors.Add(new BottleReviewFlavor { ReviewId = review.Id, Flavor = flavor });

        db.BottleReviews.Add(review);
        db.SaveChanges();
        return review;
    }

    /// <summary>The single <see cref="FeedItemType.Reviewed"/> item of a feed.</summary>
    private static FeedItemDto SingleReviewed(IEnumerable<FeedItemDto> feed) =>
        Assert.Single(feed, i => i.Type == FeedItemType.Reviewed);

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

    #region GetFeedAsync — reviews

    [Fact]
    public async Task GetFeedAsync_WhenFollowedUserReviewsABottle_IncludesReviewWithReviewerAndOwner()
    {
        var db = CreateDbContext();
        var me = SeedUser(db, "Me");
        var reviewer = SeedUser(db, "Reviewer", avatarUrl: "/uploads/avatars/reviewer.png");
        var owner = SeedUser(db, "Owner");
        SeedFollow(db, me.Id, reviewer.Id);
        var bottle = SeedBottle(db, owner.Id, "Owner Bottle");
        SeedPrimaryImage(db, bottle.Id, "/uploads/bottles/owner-bottle.png");
        var createdAt = DateTime.UtcNow.AddHours(-1);
        var review = SeedReview(db, bottle.Id, reviewer.Id, score: 92, summary: "Long and smoky.",
            flavors: [FlavorTag.Smoky], createdAt: createdAt);
        var service = CreateFeedService(db, me.Id);

        var result = await service.GetFeedAsync(0, 20, "bg", CancellationToken.None);

        Assert.True(result.Success);
        var item = Assert.Single(result.Data!);
        Assert.Equal(FeedItemType.Reviewed, item.Type);
        Assert.Equal(createdAt, item.Timestamp);
        Assert.Equal(review.Id, item.ReviewId);
        Assert.Equal(92, item.ReviewScore);
        Assert.Equal("Long and smoky.", item.ReviewSummary);
        Assert.Equal([FlavorTag.Smoky], item.ReviewFlavors);
        Assert.Equal(reviewer.Id, item.ActorUserId);
        Assert.Equal("Reviewer", item.ActorDisplayName);
        Assert.Equal("/uploads/avatars/reviewer.png", item.ActorAvatarUrl);
        Assert.Equal(bottle.Id, item.BottleId);
        Assert.Equal("Owner Bottle", item.BottleName);
        Assert.Equal("Whisky", item.BottleCategory);
        Assert.Equal("/uploads/bottles/owner-bottle.png", item.BottlePrimaryImageUrl);
        Assert.Equal(owner.Id, item.BottleUserId);
        Assert.Equal("Owner", item.BottleUserDisplayName);
        Assert.Null(item.ProductId);
        Assert.Null(item.ProductName);
    }

    [Fact]
    public async Task GetFeedAsync_WhenFollowedUserReviewsOwnBottle_IncludesReviewNextToTheNewBottle()
    {
        var db = CreateDbContext();
        var me = SeedUser(db, "Me");
        var followed = SeedUser(db, "Followed");
        SeedFollow(db, me.Id, followed.Id);
        var bottle = SeedBottle(db, followed.Id, "Own Bottle", createdAt: DateTime.UtcNow.AddHours(-2));
        SeedReview(db, bottle.Id, followed.Id, createdAt: DateTime.UtcNow.AddHours(-1));
        var service = CreateFeedService(db, me.Id);

        var result = await service.GetFeedAsync(0, 20, "bg", CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal([FeedItemType.Reviewed, FeedItemType.NewBottle], result.Data!.Select(i => i.Type));
        var item = SingleReviewed(result.Data!);
        Assert.Equal(followed.Id, item.ActorUserId);
        Assert.Equal(followed.Id, item.BottleUserId);
    }

    [Fact]
    public async Task GetFeedAsync_WhenReviewedBottleLinkedToProduct_IncludesTheProductAndItsBrand()
    {
        var db = CreateDbContext();
        var distillery = SeedDistillery(db, "Balmenach");
        var product = SeedProduct(db, "Caorunn Small Batch", brand: "Caorunn", distilleryId: distillery.Id);

        var item = await SingleReviewedOfProductAsync(db, product.Id);

        Assert.Equal(product.Id, item.ProductId);
        Assert.Equal("Caorunn Small Batch", item.ProductName);
        Assert.Equal("Caorunn", item.ProductBrand);
        Assert.Equal("Reviewed Bottle", item.BottleName);
    }

    [Fact]
    public async Task GetFeedAsync_WhenReviewedProductHasNoBrand_ShowsItsDistilleryAsTheBrand()
    {
        var db = CreateDbContext();
        var distillery = SeedDistillery(db, "Lagavulin");
        var product = SeedProduct(db, "16 Year Old", distilleryId: distillery.Id);

        var item = await SingleReviewedOfProductAsync(db, product.Id);

        Assert.Equal("Lagavulin", item.ProductBrand);
    }

    [Fact]
    public async Task GetFeedAsync_WhenReviewedProductHasNoBrandAndADeletedDistillery_ShowsNoBrand()
    {
        var db = CreateDbContext();
        var distillery = SeedDistillery(db, "Closed Distillery", isDeleted: true);
        var product = SeedProduct(db, "25 Year Old", distilleryId: distillery.Id);

        var item = await SingleReviewedOfProductAsync(db, product.Id);

        Assert.Equal(product.Id, item.ProductId);
        Assert.Null(item.ProductBrand);
    }

    [Fact]
    public async Task GetFeedAsync_WhenReviewedProductHasNeitherBrandNorDistillery_ShowsNoBrand()
    {
        var db = CreateDbContext();
        var product = SeedProduct(db, "Blended Malt");

        var item = await SingleReviewedOfProductAsync(db, product.Id);

        Assert.Equal(product.Id, item.ProductId);
        Assert.Null(item.ProductBrand);
    }

    /// <summary>The Reviewed item of a feed whose only activity is one review of a bottle linked to the product.</summary>
    private static async Task<FeedItemDto> SingleReviewedOfProductAsync(AppDbContext db, Guid productId)
    {
        var me = SeedUser(db, "Me");
        var reviewer = SeedUser(db, "Reviewer");
        var owner = SeedUser(db, "Owner");
        SeedFollow(db, me.Id, reviewer.Id);
        var bottle = SeedBottle(db, owner.Id, "Reviewed Bottle", productId: productId);
        SeedReview(db, bottle.Id, reviewer.Id);
        var service = CreateFeedService(db, me.Id);

        var result = await service.GetFeedAsync(0, 20, "bg", CancellationToken.None);

        Assert.True(result.Success);
        return SingleReviewed(result.Data!);
    }

    [Fact]
    public async Task GetFeedAsync_WhenReviewedBottleProductDeleted_LeavesTheProductOut()
    {
        var db = CreateDbContext();
        var me = SeedUser(db, "Me");
        var reviewer = SeedUser(db, "Reviewer");
        var owner = SeedUser(db, "Owner");
        SeedFollow(db, me.Id, reviewer.Id);
        var product = SeedProduct(db, "Deleted Product", isDeleted: true, brand: "Gone");
        var bottle = SeedBottle(db, owner.Id, "Orphaned Bottle", productId: product.Id);
        SeedReview(db, bottle.Id, reviewer.Id);
        var service = CreateFeedService(db, me.Id);

        var result = await service.GetFeedAsync(0, 20, "bg", CancellationToken.None);

        Assert.True(result.Success);
        var item = SingleReviewed(result.Data!);
        Assert.Null(item.ProductId);
        Assert.Null(item.ProductName);
        Assert.Null(item.ProductBrand);
        Assert.Equal("Orphaned Bottle", item.BottleName);
    }

    [Fact]
    public async Task GetFeedAsync_WhenReviewDeleted_ExcludesIt()
    {
        var db = CreateDbContext();
        var me = SeedUser(db, "Me");
        var reviewer = SeedUser(db, "Reviewer");
        var owner = SeedUser(db, "Owner");
        SeedFollow(db, me.Id, reviewer.Id);
        var bottle = SeedBottle(db, owner.Id);
        SeedReview(db, bottle.Id, reviewer.Id, isDeleted: true);
        var service = CreateFeedService(db, me.Id);

        var result = await service.GetFeedAsync(0, 20, "bg", CancellationToken.None);

        Assert.True(result.Success);
        Assert.Empty(result.Data!);
    }

    [Fact]
    public async Task GetFeedAsync_WhenReviewedBottleDeleted_ExcludesTheReview()
    {
        var db = CreateDbContext();
        var me = SeedUser(db, "Me");
        var reviewer = SeedUser(db, "Reviewer");
        var owner = SeedUser(db, "Owner");
        SeedFollow(db, me.Id, reviewer.Id);
        var bottle = SeedBottle(db, owner.Id, isDeleted: true);
        SeedReview(db, bottle.Id, reviewer.Id);
        var service = CreateFeedService(db, me.Id);

        var result = await service.GetFeedAsync(0, 20, "bg", CancellationToken.None);

        Assert.True(result.Success);
        Assert.Empty(result.Data!);
    }

    [Fact]
    public async Task GetFeedAsync_WhenReviewerNotFollowed_ExcludesTheReview()
    {
        var db = CreateDbContext();
        var me = SeedUser(db, "Me");
        var followed = SeedUser(db, "Followed");
        var stranger = SeedUser(db, "Stranger");
        SeedFollow(db, me.Id, followed.Id);
        var bottle = SeedBottle(db, followed.Id, "Followed Bottle");
        SeedReview(db, bottle.Id, stranger.Id);
        var service = CreateFeedService(db, me.Id);

        var result = await service.GetFeedAsync(0, 20, "bg", CancellationToken.None);

        Assert.True(result.Success);
        var item = Assert.Single(result.Data!);
        Assert.Equal(FeedItemType.NewBottle, item.Type);
    }

    [Fact]
    public async Task GetFeedAsync_WhenNotAuthenticated_LeavesReviewsOut()
    {
        var db = CreateDbContext();
        var author = SeedUser(db, "Author");
        var reviewer = SeedUser(db, "Reviewer");
        SeedNewsPost(db, author.Id, [Translation("bg", "News", "BG Content")]);
        var bottle = SeedBottle(db, reviewer.Id);
        SeedReview(db, bottle.Id, reviewer.Id);
        var service = CreateFeedService(db);

        var result = await service.GetFeedAsync(0, 20, "bg", CancellationToken.None);

        Assert.True(result.Success);
        var item = Assert.Single(result.Data!);
        Assert.Equal(FeedItemType.News, item.Type);
    }

    [Fact]
    public async Task GetFeedAsync_WhenAuthenticatedWithoutFollows_LeavesReviewsOut()
    {
        var db = CreateDbContext();
        var me = SeedUser(db, "Me");
        var reviewer = SeedUser(db, "Reviewer");
        var bottle = SeedBottle(db, reviewer.Id);
        SeedReview(db, bottle.Id, reviewer.Id);
        var service = CreateFeedService(db, me.Id);

        var result = await service.GetFeedAsync(0, 20, "bg", CancellationToken.None);

        Assert.True(result.Success);
        Assert.Empty(result.Data!);
    }

    [Fact]
    public async Task GetFeedAsync_WhenReviewEdited_KeepsItsCreatedAtAsTimestamp()
    {
        var db = CreateDbContext();
        var me = SeedUser(db, "Me");
        var reviewer = SeedUser(db, "Reviewer");
        var owner = SeedUser(db, "Owner");
        SeedFollow(db, me.Id, reviewer.Id);
        var bottle = SeedBottle(db, owner.Id);
        var createdAt = DateTime.UtcNow.AddDays(-3);
        var review = SeedReview(db, bottle.Id, reviewer.Id, createdAt: createdAt);
        review.UpdatedAt = DateTime.UtcNow;
        review.Score = 95;
        db.SaveChanges();
        var service = CreateFeedService(db, me.Id);

        var result = await service.GetFeedAsync(0, 20, "bg", CancellationToken.None);

        Assert.True(result.Success);
        var item = SingleReviewed(result.Data!);
        Assert.Equal(createdAt, item.Timestamp);
        Assert.Equal(95, item.ReviewScore);
    }

    [Fact]
    public async Task GetFeedAsync_WhenReviewHasMoreThanThreeFlavors_ShowsTheFirstThreeInEnumOrder()
    {
        var db = CreateDbContext();
        var me = SeedUser(db, "Me");
        var reviewer = SeedUser(db, "Reviewer");
        var owner = SeedUser(db, "Owner");
        SeedFollow(db, me.Id, reviewer.Id);
        var bottle = SeedBottle(db, owner.Id);
        SeedReview(db, bottle.Id, reviewer.Id,
            flavors: [FlavorTag.Oak, FlavorTag.Vanilla, FlavorTag.Smoky, FlavorTag.Honey, FlavorTag.Peaty]);
        var service = CreateFeedService(db, me.Id);

        var result = await service.GetFeedAsync(0, 20, "bg", CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(
            [FlavorTag.Smoky, FlavorTag.Peaty, FlavorTag.Vanilla],
            SingleReviewed(result.Data!).ReviewFlavors);
    }

    [Fact]
    public async Task GetFeedAsync_WhenReviewHasNoFlavors_ShowsNoFlavors()
    {
        var db = CreateDbContext();
        var me = SeedUser(db, "Me");
        var reviewer = SeedUser(db, "Reviewer");
        var owner = SeedUser(db, "Owner");
        SeedFollow(db, me.Id, reviewer.Id);
        var bottle = SeedBottle(db, owner.Id);
        SeedReview(db, bottle.Id, reviewer.Id);
        var service = CreateFeedService(db, me.Id);

        var result = await service.GetFeedAsync(0, 20, "bg", CancellationToken.None);

        Assert.True(result.Success);
        Assert.Empty(SingleReviewed(result.Data!).ReviewFlavors!);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetFeedAsync_WhenReviewSummaryBlank_LeavesTheSummaryOut(string? summary)
    {
        var item = await SingleReviewedWithSummaryAsync(summary);

        Assert.Null(item.ReviewSummary);
    }

    [Fact]
    public async Task GetFeedAsync_WhenReviewSummaryFitsTheLimit_ShowsItWhole()
    {
        var summary = new string('x', FeedService.ReviewSummaryMaxLength);

        var item = await SingleReviewedWithSummaryAsync(summary);

        Assert.Equal(summary, item.ReviewSummary);
    }

    [Fact]
    public async Task GetFeedAsync_WhenReviewSummaryLonger_CutsAtTheLastSpaceThatFitsWithAnEllipsis()
    {
        // 100 × "peat" with spaces: word n starts at 5n, so the 57th word (n = 56) starts at index 280 and runs past
        // the limit. The cut falls on the space before it, after 56 whole words.
        var summary = string.Join(' ', Enumerable.Repeat("peat", 100));

        var item = await SingleReviewedWithSummaryAsync(summary);

        Assert.Equal(string.Join(' ', Enumerable.Repeat("peat", 56)) + "…", item.ReviewSummary);
    }

    [Fact]
    public async Task GetFeedAsync_WhenReviewSummaryLongerWithoutSpaces_CutsAtTheLimitWithAnEllipsis()
    {
        var item = await SingleReviewedWithSummaryAsync(new string('x', FeedService.ReviewSummaryMaxLength + 20));

        Assert.Equal(new string('x', FeedService.ReviewSummaryMaxLength) + "…", item.ReviewSummary);
    }

    /// <summary>The Reviewed item of a feed whose only activity is one review with <paramref name="summary"/>.</summary>
    private static async Task<FeedItemDto> SingleReviewedWithSummaryAsync(string? summary)
    {
        var db = CreateDbContext();
        var me = SeedUser(db, "Me");
        var reviewer = SeedUser(db, "Reviewer");
        var owner = SeedUser(db, "Owner");
        SeedFollow(db, me.Id, reviewer.Id);
        var bottle = SeedBottle(db, owner.Id);
        SeedReview(db, bottle.Id, reviewer.Id, summary: summary);
        var service = CreateFeedService(db, me.Id);

        var result = await service.GetFeedAsync(0, 20, "bg", CancellationToken.None);

        Assert.True(result.Success);
        return SingleReviewed(result.Data!);
    }

    #endregion

    #region GetFeedAsync — order and paging

    [Fact]
    public async Task GetFeedAsync_MergesEverySourceNewestFirst()
    {
        var db = CreateDbContext();
        var now = DateTime.UtcNow;
        var me = SeedUser(db, "Me");
        var followed = SeedUser(db, "Followed");
        var author = SeedUser(db, "Author");
        SeedFollow(db, me.Id, followed.Id);
        SeedNewsPost(db, author.Id, [Translation("bg", "Older News", "")], createdAt: now.AddHours(-5));
        SeedNewsPost(db, author.Id, [Translation("bg", "Newer News", "")], createdAt: now.AddHours(-1));
        var kept = SeedBottle(db, followed.Id, "Kept", createdAt: now.AddHours(-4));
        SeedBottle(db, followed.Id, "Sold", isForSale: true, forSaleAt: now.AddHours(-3), createdAt: now.AddHours(-6));
        SeedReview(db, kept.Id, followed.Id, createdAt: now.AddHours(-2));
        var service = CreateFeedService(db, me.Id);

        var result = await service.GetFeedAsync(0, 20, "bg", CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(
            [
                (FeedItemType.News, "Newer News"),
                (FeedItemType.Reviewed, "Kept"),
                (FeedItemType.ForSale, "Sold"),
                (FeedItemType.NewBottle, "Kept"),
                (FeedItemType.News, "Older News"),
                (FeedItemType.NewBottle, "Sold"),
            ],
            result.Data!.Select(i => (i.Type, i.PostTitle ?? i.BottleName)));
    }

    // Each source loads only its newest skip + take items. These pin that a page deep inside one source still gets
    // every item it needs from it: loading only `take` per source would leave the page empty.

    [Fact]
    public async Task GetFeedAsync_WhenPageStartsDeepInTheNewBottles_ReturnsTheOlderBottles()
    {
        var db = CreateDbContext();
        var now = DateTime.UtcNow;
        var me = SeedUser(db, "Me");
        var followed = SeedUser(db, "Followed");
        SeedFollow(db, me.Id, followed.Id);
        for (var hour = 1; hour <= 4; hour++)
            SeedBottle(db, followed.Id, $"Bottle {hour}h", createdAt: now.AddHours(-hour));
        var service = CreateFeedService(db, me.Id);

        var result = await service.GetFeedAsync(2, 2, "bg", CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(["Bottle 3h", "Bottle 4h"], result.Data!.Select(i => i.BottleName));
    }

    [Fact]
    public async Task GetFeedAsync_WhenPageStartsDeepInTheListings_ReturnsTheOlderListings()
    {
        var db = CreateDbContext();
        var now = DateTime.UtcNow;
        var me = SeedUser(db, "Me");
        var followed = SeedUser(db, "Followed");
        SeedFollow(db, me.Id, followed.Id);
        for (var hour = 1; hour <= 4; hour++)
            SeedBottle(db, followed.Id, $"Listing {hour}h", isForSale: true, forSaleAt: now.AddHours(-hour),
                createdAt: now.AddDays(-1));
        var service = CreateFeedService(db, me.Id);

        var result = await service.GetFeedAsync(2, 2, "bg", CancellationToken.None);

        Assert.True(result.Success);
        Assert.All(result.Data!, i => Assert.Equal(FeedItemType.ForSale, i.Type));
        Assert.Equal(["Listing 3h", "Listing 4h"], result.Data!.Select(i => i.BottleName));
    }

    [Fact]
    public async Task GetFeedAsync_WhenPageStartsDeepInTheReviews_ReturnsTheOlderReviews()
    {
        var db = CreateDbContext();
        var now = DateTime.UtcNow;
        var me = SeedUser(db, "Me");
        var reviewer = SeedUser(db, "Reviewer");
        var owner = SeedUser(db, "Owner");
        SeedFollow(db, me.Id, reviewer.Id);
        for (var hour = 1; hour <= 4; hour++)
        {
            var bottle = SeedBottle(db, owner.Id, $"Reviewed {hour}h");
            SeedReview(db, bottle.Id, reviewer.Id, createdAt: now.AddHours(-hour));
        }
        var service = CreateFeedService(db, me.Id);

        var result = await service.GetFeedAsync(2, 2, "bg", CancellationToken.None);

        Assert.True(result.Success);
        Assert.All(result.Data!, i => Assert.Equal(FeedItemType.Reviewed, i.Type));
        Assert.Equal(["Reviewed 3h", "Reviewed 4h"], result.Data!.Select(i => i.BottleName));
    }

    #endregion
}
