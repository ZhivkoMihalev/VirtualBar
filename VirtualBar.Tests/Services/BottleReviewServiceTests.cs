using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using VirtualBar.Application.Common;
using VirtualBar.Application.DTOs.Reviews;
using VirtualBar.Application.Interfaces;
using VirtualBar.Domain.Entities;
using VirtualBar.Domain.Enums;
using VirtualBar.Infrastructure.Decorators;
using VirtualBar.Infrastructure.Persistence;
using VirtualBar.Infrastructure.Services;

namespace VirtualBar.Tests.Services;

public sealed class BottleReviewServiceTests
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

    private static IBottleReviewService CreateService(
        AppDbContext db,
        Guid currentUserId,
        INotificationService? notificationService = null)
    {
        var currentUser = CreateCurrentUser(currentUserId);
        var inner = new BottleReviewService(db, currentUser, notificationService ?? Mock.Of<INotificationService>());
        return new BottleReviewValidationDecorator(inner, db, currentUser);
    }

    private static BottleReviewService CreateInnerService(
        AppDbContext db,
        Guid currentUserId,
        INotificationService? notificationService = null) =>
        new(db, CreateCurrentUser(currentUserId), notificationService ?? Mock.Of<INotificationService>());

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

    private static Bottle SeedBottle(AppDbContext db, Guid userId, string name = "Lagavulin 16", bool isDeleted = false)
    {
        var bottle = new Bottle
        {
            UserId = userId,
            Name = name,
            Category = SpiritCategory.Whisky,
            Condition = BottleCondition.Sealed,
            IsDeleted = isDeleted,
            DeletedAt = isDeleted ? DateTime.UtcNow : null
        };
        db.Bottles.Add(bottle);
        db.SaveChanges();
        return bottle;
    }

    private static BottleReview SeedReview(
        AppDbContext db,
        Guid bottleId,
        Guid userId,
        int score = 90,
        bool isDeleted = false,
        DateTime? createdAt = null,
        IEnumerable<FlavorTag>? flavors = null)
    {
        var review = new BottleReview
        {
            BottleId = bottleId,
            UserId = userId,
            Score = score,
            IsDeleted = isDeleted,
            DeletedAt = isDeleted ? DateTime.UtcNow : null,
            Flavors = (flavors ?? []).Select(f => new BottleReviewFlavor { Flavor = f }).ToList()
        };
        if (createdAt is not null)
            review.CreatedAt = createdAt.Value;
        db.BottleReviews.Add(review);
        db.SaveChanges();
        return review;
    }

    #region GetReviewsAsync

    [Fact]
    public async Task GetReviewsAsync_WhenBottleNotFound_ReturnsNotFound()
    {
        var db = CreateDbContext();
        var service = CreateService(db, Guid.NewGuid());

        var result = await service.GetReviewsAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ErrorCode.NotFound, result.ErrorCode);
        Assert.Equal("Bottle not found.", result.Error);
    }

    [Fact]
    public async Task GetReviewsAsync_WhenBottleSoftDeleted_ReturnsNotFound()
    {
        var db = CreateDbContext();
        var user = SeedUser(db);
        var bottle = SeedBottle(db, user.Id, isDeleted: true);
        var service = CreateService(db, user.Id);

        var result = await service.GetReviewsAsync(bottle.Id, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Bottle not found.", result.Error);
    }

    [Fact]
    public async Task GetReviewsAsync_WhenNoReviews_ReturnsEmptySummary()
    {
        var db = CreateDbContext();
        var user = SeedUser(db);
        var bottle = SeedBottle(db, user.Id);
        var service = CreateService(db, user.Id);

        var result = await service.GetReviewsAsync(bottle.Id, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Null(result.Data!.AverageScore);
        Assert.Equal(0, result.Data.ReviewsCount);
        Assert.Empty(result.Data.TopFlavors);
        Assert.Empty(result.Data.Reviews);
        Assert.Null(result.Data.MyReview);
    }

    [Fact]
    public async Task GetReviewsAsync_WhenMultipleReviews_ReturnsRoundedAverageNewestFirst()
    {
        var db = CreateDbContext();
        var owner = SeedUser(db, "Owner");
        var u1 = SeedUser(db, "First");
        var u2 = SeedUser(db, "Second");
        var u3 = SeedUser(db, "Third");
        var bottle = SeedBottle(db, owner.Id);
        var baseTime = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        SeedReview(db, bottle.Id, u1.Id, score: 85, createdAt: baseTime);
        SeedReview(db, bottle.Id, u2.Id, score: 90, createdAt: baseTime.AddDays(1));
        SeedReview(db, bottle.Id, u3.Id, score: 91, createdAt: baseTime.AddDays(2));
        var service = CreateService(db, owner.Id);

        var result = await service.GetReviewsAsync(bottle.Id, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(88.7, result.Data!.AverageScore);
        Assert.Equal(3, result.Data.ReviewsCount);
        Assert.Equal(new[] { 91, 90, 85 }, result.Data.Reviews.Select(r => r.Score).ToArray());
    }

    [Fact]
    public async Task GetReviewsAsync_WhenReviewSoftDeleted_ExcludedFromListAndAggregates()
    {
        var db = CreateDbContext();
        var owner = SeedUser(db, "Owner");
        var u1 = SeedUser(db, "Active");
        var u2 = SeedUser(db, "Deleted");
        var bottle = SeedBottle(db, owner.Id);
        SeedReview(db, bottle.Id, u1.Id, score: 80, flavors: new[] { FlavorTag.Peaty });
        SeedReview(db, bottle.Id, u2.Id, score: 100, isDeleted: true, flavors: new[] { FlavorTag.Smoky });
        var service = CreateService(db, owner.Id);

        var result = await service.GetReviewsAsync(bottle.Id, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(1, result.Data!.ReviewsCount);
        Assert.Equal(80.0, result.Data.AverageScore);
        Assert.Single(result.Data.Reviews);
        Assert.Equal(80, result.Data.Reviews[0].Score);
        Assert.Equal(new[] { FlavorTag.Peaty }, result.Data.TopFlavors);
    }

    [Fact]
    public async Task GetReviewsAsync_WhenFlavorsHaveTie_ReturnsTopThreeInEnumOrder()
    {
        var db = CreateDbContext();
        var owner = SeedUser(db, "Owner");
        var u1 = SeedUser(db, "First");
        var u2 = SeedUser(db, "Second");
        var bottle = SeedBottle(db, owner.Id);
        // Counts: Smoky 2, Peaty 2, Vanilla 1, Oak 1.
        SeedReview(db, bottle.Id, u1.Id, flavors: new[] { FlavorTag.Smoky, FlavorTag.Peaty, FlavorTag.Vanilla });
        SeedReview(db, bottle.Id, u2.Id, flavors: new[] { FlavorTag.Smoky, FlavorTag.Peaty, FlavorTag.Oak });
        var service = CreateService(db, owner.Id);

        var result = await service.GetReviewsAsync(bottle.Id, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(new[] { FlavorTag.Smoky, FlavorTag.Peaty, FlavorTag.Vanilla }, result.Data!.TopFlavors);
        Assert.DoesNotContain(FlavorTag.Oak, result.Data.TopFlavors);
    }

    [Fact]
    public async Task GetReviewsAsync_WhenCurrentUserHasReview_SetsMyReview()
    {
        var db = CreateDbContext();
        var owner = SeedUser(db, "Owner");
        var me = SeedUser(db, "Me");
        var other = SeedUser(db, "Other");
        var bottle = SeedBottle(db, owner.Id);
        SeedReview(db, bottle.Id, other.Id, score: 70);
        SeedReview(db, bottle.Id, me.Id, score: 88);
        var service = CreateService(db, me.Id);

        var result = await service.GetReviewsAsync(bottle.Id, CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotNull(result.Data!.MyReview);
        Assert.Equal(me.Id, result.Data.MyReview!.UserId);
        Assert.Equal(88, result.Data.MyReview.Score);
    }

    [Fact]
    public async Task GetReviewsAsync_WhenAnonymous_MyReviewNull()
    {
        var db = CreateDbContext();
        var owner = SeedUser(db, "Owner");
        var u1 = SeedUser(db, "First");
        var u2 = SeedUser(db, "Second");
        var bottle = SeedBottle(db, owner.Id);
        SeedReview(db, bottle.Id, u1.Id, score: 70);
        SeedReview(db, bottle.Id, u2.Id, score: 90);
        var service = CreateService(db, Guid.Empty);

        var result = await service.GetReviewsAsync(bottle.Id, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(2, result.Data!.Reviews.Count);
        Assert.Null(result.Data.MyReview);
    }

    [Fact]
    public async Task GetReviewsAsync_MapsUserDisplayNameAndAvatar()
    {
        var db = CreateDbContext();
        var owner = SeedUser(db, "Owner");
        var author = SeedUser(db, "Author", avatarUrl: "/uploads/avatars/author.png");
        var bottle = SeedBottle(db, owner.Id);
        SeedReview(db, bottle.Id, author.Id, score: 77);
        var service = CreateService(db, owner.Id);

        var result = await service.GetReviewsAsync(bottle.Id, CancellationToken.None);

        Assert.True(result.Success);
        var dto = Assert.Single(result.Data!.Reviews);
        Assert.Equal("Author", dto.UserDisplayName);
        Assert.Equal("/uploads/avatars/author.png", dto.UserAvatarUrl);
    }

    [Fact]
    public async Task GetReviewsAsync_WithCancelledToken_Throws()
    {
        var db = CreateDbContext();
        var service = CreateService(db, Guid.NewGuid());
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => service.GetReviewsAsync(Guid.NewGuid(), cts.Token));
    }

    #endregion

    #region AddReviewAsync

    [Fact]
    public async Task AddReviewAsync_WhenValid_ReturnsOkAndNotifiesOwner()
    {
        var db = CreateDbContext();
        var owner = SeedUser(db, "Owner");
        var reviewer = SeedUser(db, "Reviewer");
        var bottle = SeedBottle(db, owner.Id, "Lagavulin 16");
        var notificationMock = new Mock<INotificationService>();
        var service = CreateService(db, reviewer.Id, notificationMock.Object);
        var request = new AddReviewRequest
        {
            Score = 92,
            Nose = "Peat smoke",
            Palate = "Rich oily",
            Finish = "Long warming",
            Summary = "A classic Islay",
            Flavors = [FlavorTag.Peaty, FlavorTag.Smoky]
        };

        var result = await service.AddReviewAsync(bottle.Id, request, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(92, result.Data!.Score);
        Assert.Equal(bottle.Id, result.Data.BottleId);
        Assert.Equal(reviewer.Id, result.Data.UserId);
        Assert.Equal("Reviewer", result.Data.UserDisplayName);
        Assert.Equal(new[] { FlavorTag.Smoky, FlavorTag.Peaty }, result.Data.Flavors);

        db.ChangeTracker.Clear();
        var stored = await db.BottleReviews.Include(r => r.Flavors).FirstAsync(r => r.BottleId == bottle.Id && !r.IsDeleted);
        Assert.Equal(2, stored.Flavors.Count);
        Assert.Contains(FlavorTag.Smoky, stored.Flavors.Select(f => f.Flavor));
        Assert.Contains(FlavorTag.Peaty, stored.Flavors.Select(f => f.Flavor));

        notificationMock.Verify(n => n.CreateAsync(
            owner.Id, NotificationType.BottleReviewed, bottle.Id, "Lagavulin 16", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AddReviewAsync_WhenScoreNegative_ReturnsFail()
    {
        var db = CreateDbContext();
        var service = CreateService(db, Guid.NewGuid());

        var result = await service.AddReviewAsync(Guid.NewGuid(), new AddReviewRequest { Score = -1 }, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Score must be between 0 and 100.", result.Error);
    }

    [Fact]
    public async Task AddReviewAsync_WhenScoreAbove100_ReturnsFail()
    {
        var db = CreateDbContext();
        var service = CreateService(db, Guid.NewGuid());

        var result = await service.AddReviewAsync(Guid.NewGuid(), new AddReviewRequest { Score = 101 }, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Score must be between 0 and 100.", result.Error);
    }

    [Fact]
    public async Task AddReviewAsync_WhenScoreZero_ReturnsOk()
    {
        var db = CreateDbContext();
        var reviewer = SeedUser(db, "Reviewer");
        var bottle = SeedBottle(db, reviewer.Id);
        var service = CreateService(db, reviewer.Id);

        var result = await service.AddReviewAsync(bottle.Id, new AddReviewRequest { Score = 0 }, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(0, result.Data!.Score);
    }

    [Fact]
    public async Task AddReviewAsync_WhenScore100_ReturnsOk()
    {
        var db = CreateDbContext();
        var reviewer = SeedUser(db, "Reviewer");
        var bottle = SeedBottle(db, reviewer.Id);
        var service = CreateService(db, reviewer.Id);

        var result = await service.AddReviewAsync(bottle.Id, new AddReviewRequest { Score = 100 }, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(100, result.Data!.Score);
    }

    [Fact]
    public async Task AddReviewAsync_WhenNoseTooLong_ReturnsFail()
    {
        var db = CreateDbContext();
        var service = CreateService(db, Guid.NewGuid());
        var request = new AddReviewRequest { Score = 80, Nose = new string('a', 2001) };

        var result = await service.AddReviewAsync(Guid.NewGuid(), request, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Nose must be 2000 characters or fewer.", result.Error);
    }

    [Fact]
    public async Task AddReviewAsync_WhenPalateTooLong_ReturnsFail()
    {
        var db = CreateDbContext();
        var service = CreateService(db, Guid.NewGuid());
        var request = new AddReviewRequest { Score = 80, Palate = new string('a', 2001) };

        var result = await service.AddReviewAsync(Guid.NewGuid(), request, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Palate must be 2000 characters or fewer.", result.Error);
    }

    [Fact]
    public async Task AddReviewAsync_WhenFinishTooLong_ReturnsFail()
    {
        var db = CreateDbContext();
        var service = CreateService(db, Guid.NewGuid());
        var request = new AddReviewRequest { Score = 80, Finish = new string('a', 2001) };

        var result = await service.AddReviewAsync(Guid.NewGuid(), request, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Finish must be 2000 characters or fewer.", result.Error);
    }

    [Fact]
    public async Task AddReviewAsync_WhenSummaryTooLong_ReturnsFail()
    {
        var db = CreateDbContext();
        var service = CreateService(db, Guid.NewGuid());
        var request = new AddReviewRequest { Score = 80, Summary = new string('a', 2001) };

        var result = await service.AddReviewAsync(Guid.NewGuid(), request, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Summary must be 2000 characters or fewer.", result.Error);
    }

    [Fact]
    public async Task AddReviewAsync_WhenNotesAtMaxLength_ReturnsOk()
    {
        var db = CreateDbContext();
        var reviewer = SeedUser(db, "Reviewer");
        var bottle = SeedBottle(db, reviewer.Id);
        var service = CreateService(db, reviewer.Id);
        var max = new string('a', 2000);
        var request = new AddReviewRequest { Score = 80, Nose = max, Palate = max, Finish = max, Summary = max };

        var result = await service.AddReviewAsync(bottle.Id, request, CancellationToken.None);

        Assert.True(result.Success);
        db.ChangeTracker.Clear();
        var stored = await db.BottleReviews.FirstAsync(r => r.BottleId == bottle.Id);
        Assert.Equal(2000, stored.Nose!.Length);
        Assert.Equal(2000, stored.Palate!.Length);
        Assert.Equal(2000, stored.Finish!.Length);
        Assert.Equal(2000, stored.Summary!.Length);
    }

    [Fact]
    public async Task AddReviewAsync_WhenTooManyFlavors_ReturnsFail()
    {
        var db = CreateDbContext();
        var service = CreateService(db, Guid.NewGuid());
        var request = new AddReviewRequest
        {
            Score = 80,
            Flavors =
            [
                FlavorTag.Smoky, FlavorTag.Peaty, FlavorTag.Vanilla,
                FlavorTag.Caramel, FlavorTag.Honey, FlavorTag.Oak
            ]
        };

        var result = await service.AddReviewAsync(Guid.NewGuid(), request, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("A review can have at most 5 flavor tags.", result.Error);
    }

    [Fact]
    public async Task AddReviewAsync_WhenDuplicateFlavors_ReturnsFail()
    {
        var db = CreateDbContext();
        var service = CreateService(db, Guid.NewGuid());
        var request = new AddReviewRequest { Score = 80, Flavors = [FlavorTag.Smoky, FlavorTag.Smoky] };

        var result = await service.AddReviewAsync(Guid.NewGuid(), request, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Flavor tags must be distinct.", result.Error);
    }

    [Fact]
    public async Task AddReviewAsync_WhenUndefinedFlavor_ReturnsFail()
    {
        var db = CreateDbContext();
        var service = CreateService(db, Guid.NewGuid());
        var request = new AddReviewRequest { Score = 80, Flavors = [(FlavorTag)999] };

        var result = await service.AddReviewAsync(Guid.NewGuid(), request, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("One or more flavor tags are invalid.", result.Error);
    }

    [Fact]
    public async Task AddReviewAsync_WhenFlavorsAndNotesNull_PersistsNullsAndNoFlavors()
    {
        var db = CreateDbContext();
        var reviewer = SeedUser(db, "Reviewer");
        var bottle = SeedBottle(db, reviewer.Id);
        var service = CreateService(db, reviewer.Id);
        var request = new AddReviewRequest { Score = 75, Flavors = null };

        var result = await service.AddReviewAsync(bottle.Id, request, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Empty(result.Data!.Flavors);
        db.ChangeTracker.Clear();
        var stored = await db.BottleReviews.Include(r => r.Flavors).FirstAsync(r => r.BottleId == bottle.Id);
        Assert.Null(stored.Nose);
        Assert.Null(stored.Palate);
        Assert.Null(stored.Finish);
        Assert.Null(stored.Summary);
        Assert.Empty(stored.Flavors);
    }

    [Fact]
    public async Task AddReviewAsync_WhenFlavorsEmpty_ReturnsOk()
    {
        var db = CreateDbContext();
        var reviewer = SeedUser(db, "Reviewer");
        var bottle = SeedBottle(db, reviewer.Id);
        var service = CreateService(db, reviewer.Id);
        var request = new AddReviewRequest { Score = 75, Flavors = [] };

        var result = await service.AddReviewAsync(bottle.Id, request, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Empty(result.Data!.Flavors);
        db.ChangeTracker.Clear();
        var stored = await db.BottleReviews.Include(r => r.Flavors).FirstAsync(r => r.BottleId == bottle.Id);
        Assert.Empty(stored.Flavors);
    }

    [Fact]
    public async Task AddReviewAsync_WhenNotesHaveWhitespace_TrimsBeforePersist()
    {
        var db = CreateDbContext();
        var reviewer = SeedUser(db, "Reviewer");
        var bottle = SeedBottle(db, reviewer.Id);
        var service = CreateService(db, reviewer.Id);
        var request = new AddReviewRequest
        {
            Score = 80,
            Nose = "  smoky nose  ",
            Palate = "  rich palate  ",
            Finish = "  long finish  ",
            Summary = "  fine summary  ",
            Flavors = [FlavorTag.Smoky]
        };

        var result = await service.AddReviewAsync(bottle.Id, request, CancellationToken.None);

        Assert.True(result.Success);
        db.ChangeTracker.Clear();
        var stored = await db.BottleReviews.FirstAsync(r => r.BottleId == bottle.Id);
        Assert.Equal("smoky nose", stored.Nose);
        Assert.Equal("rich palate", stored.Palate);
        Assert.Equal("long finish", stored.Finish);
        Assert.Equal("fine summary", stored.Summary);
    }

    [Fact]
    public async Task AddReviewAsync_WhenBottleNotFound_ReturnsNotFound()
    {
        var db = CreateDbContext();
        var service = CreateService(db, Guid.NewGuid());

        var result = await service.AddReviewAsync(Guid.NewGuid(), new AddReviewRequest { Score = 80 }, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ErrorCode.NotFound, result.ErrorCode);
        Assert.Equal("Bottle not found.", result.Error);
    }

    [Fact]
    public async Task AddReviewAsync_WhenBottleSoftDeleted_ReturnsNotFound()
    {
        var db = CreateDbContext();
        var owner = SeedUser(db, "Owner");
        var reviewer = SeedUser(db, "Reviewer");
        var bottle = SeedBottle(db, owner.Id, isDeleted: true);
        var service = CreateService(db, reviewer.Id);

        var result = await service.AddReviewAsync(bottle.Id, new AddReviewRequest { Score = 80 }, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Bottle not found.", result.Error);
    }

    [Fact]
    public async Task AddReviewAsync_WhenAlreadyReviewed_ReturnsConflict()
    {
        var db = CreateDbContext();
        var owner = SeedUser(db, "Owner");
        var reviewer = SeedUser(db, "Reviewer");
        var bottle = SeedBottle(db, owner.Id);
        SeedReview(db, bottle.Id, reviewer.Id, score: 70);
        var service = CreateService(db, reviewer.Id);

        var result = await service.AddReviewAsync(bottle.Id, new AddReviewRequest { Score = 90 }, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ErrorCode.Conflict, result.ErrorCode);
        Assert.Equal("You have already reviewed this bottle.", result.Error);
    }

    [Fact]
    public async Task AddReviewAsync_WhenDuplicateSlipsPastValidation_ReturnsConflict()
    {
        var db = CreateSqliteDbContext();
        var owner = SeedUser(db, "Owner");
        var reviewer = SeedUser(db, "Reviewer");
        var bottle = SeedBottle(db, owner.Id);
        SeedReview(db, bottle.Id, reviewer.Id, score: 70);
        var notificationMock = new Mock<INotificationService>();
        var inner = CreateInnerService(db, reviewer.Id, notificationMock.Object);

        var result = await inner.AddReviewAsync(bottle.Id, new AddReviewRequest { Score = 90 }, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ErrorCode.Conflict, result.ErrorCode);
        Assert.Equal("You have already reviewed this bottle.", result.Error);

        db.ChangeTracker.Clear();
        Assert.Equal(1, await db.BottleReviews.CountAsync());
        notificationMock.Verify(n => n.CreateAsync(
            It.IsAny<Guid>(), It.IsAny<NotificationType>(), It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AddReviewAsync_WithCancelledToken_Throws()
    {
        var db = CreateDbContext();
        var service = CreateService(db, Guid.NewGuid());
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => service.AddReviewAsync(Guid.NewGuid(), new AddReviewRequest { Score = 50 }, cts.Token));
    }

    #endregion

    #region UpdateReviewAsync

    [Fact]
    public async Task UpdateReviewAsync_WhenValid_OverwritesFieldsReplacesFlavorsAndTrims()
    {
        var db = CreateDbContext();
        var reviewer = SeedUser(db, "Reviewer");
        var bottle = SeedBottle(db, reviewer.Id);
        var review = SeedReview(db, bottle.Id, reviewer.Id, score: 70, flavors: new[] { FlavorTag.Smoky, FlavorTag.Peaty });
        var notificationMock = new Mock<INotificationService>();
        var service = CreateService(db, reviewer.Id, notificationMock.Object);
        var request = new UpdateReviewRequest
        {
            Score = 95,
            Nose = "  new nose  ",
            Palate = "  new palate  ",
            Finish = "  new finish  ",
            Summary = "  new summary  ",
            Flavors = [FlavorTag.Vanilla, FlavorTag.Oak]
        };

        var result = await service.UpdateReviewAsync(review.Id, request, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(95, result.Data!.Score);
        Assert.Equal("new nose", result.Data.Nose);
        Assert.Equal("new palate", result.Data.Palate);
        Assert.Equal("new finish", result.Data.Finish);
        Assert.Equal("new summary", result.Data.Summary);

        db.ChangeTracker.Clear();
        var stored = await db.BottleReviews.Include(r => r.Flavors).FirstAsync(r => r.Id == review.Id);
        Assert.Equal(95, stored.Score);
        Assert.Equal("new nose", stored.Nose);
        var storedFlavors = stored.Flavors.Select(f => f.Flavor).OrderBy(f => f).ToList();
        Assert.Equal(new List<FlavorTag> { FlavorTag.Vanilla, FlavorTag.Oak }, storedFlavors);
        Assert.DoesNotContain(FlavorTag.Smoky, storedFlavors);
        Assert.DoesNotContain(FlavorTag.Peaty, storedFlavors);

        var junctionRows = await db.BottleReviewFlavors.Where(f => f.ReviewId == review.Id).ToListAsync();
        Assert.Equal(2, junctionRows.Count);

        notificationMock.Verify(n => n.CreateAsync(
            It.IsAny<Guid>(), It.IsAny<NotificationType>(), It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateReviewAsync_WhenScoreNegative_ReturnsFail()
    {
        var db = CreateDbContext();
        var service = CreateService(db, Guid.NewGuid());

        var result = await service.UpdateReviewAsync(Guid.NewGuid(), new UpdateReviewRequest { Score = -1 }, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Score must be between 0 and 100.", result.Error);
    }

    [Fact]
    public async Task UpdateReviewAsync_WhenScoreAbove100_ReturnsFail()
    {
        var db = CreateDbContext();
        var service = CreateService(db, Guid.NewGuid());

        var result = await service.UpdateReviewAsync(Guid.NewGuid(), new UpdateReviewRequest { Score = 101 }, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Score must be between 0 and 100.", result.Error);
    }

    [Fact]
    public async Task UpdateReviewAsync_WhenNoseTooLong_ReturnsFail()
    {
        var db = CreateDbContext();
        var service = CreateService(db, Guid.NewGuid());
        var request = new UpdateReviewRequest { Score = 80, Nose = new string('a', 2001) };

        var result = await service.UpdateReviewAsync(Guid.NewGuid(), request, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Nose must be 2000 characters or fewer.", result.Error);
    }

    [Fact]
    public async Task UpdateReviewAsync_WhenPalateTooLong_ReturnsFail()
    {
        var db = CreateDbContext();
        var service = CreateService(db, Guid.NewGuid());
        var request = new UpdateReviewRequest { Score = 80, Palate = new string('a', 2001) };

        var result = await service.UpdateReviewAsync(Guid.NewGuid(), request, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Palate must be 2000 characters or fewer.", result.Error);
    }

    [Fact]
    public async Task UpdateReviewAsync_WhenFinishTooLong_ReturnsFail()
    {
        var db = CreateDbContext();
        var service = CreateService(db, Guid.NewGuid());
        var request = new UpdateReviewRequest { Score = 80, Finish = new string('a', 2001) };

        var result = await service.UpdateReviewAsync(Guid.NewGuid(), request, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Finish must be 2000 characters or fewer.", result.Error);
    }

    [Fact]
    public async Task UpdateReviewAsync_WhenSummaryTooLong_ReturnsFail()
    {
        var db = CreateDbContext();
        var service = CreateService(db, Guid.NewGuid());
        var request = new UpdateReviewRequest { Score = 80, Summary = new string('a', 2001) };

        var result = await service.UpdateReviewAsync(Guid.NewGuid(), request, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Summary must be 2000 characters or fewer.", result.Error);
    }

    [Fact]
    public async Task UpdateReviewAsync_WhenTooManyFlavors_ReturnsFail()
    {
        var db = CreateDbContext();
        var service = CreateService(db, Guid.NewGuid());
        var request = new UpdateReviewRequest
        {
            Score = 80,
            Flavors =
            [
                FlavorTag.Smoky, FlavorTag.Peaty, FlavorTag.Vanilla,
                FlavorTag.Caramel, FlavorTag.Honey, FlavorTag.Oak
            ]
        };

        var result = await service.UpdateReviewAsync(Guid.NewGuid(), request, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("A review can have at most 5 flavor tags.", result.Error);
    }

    [Fact]
    public async Task UpdateReviewAsync_WhenDuplicateFlavors_ReturnsFail()
    {
        var db = CreateDbContext();
        var service = CreateService(db, Guid.NewGuid());
        var request = new UpdateReviewRequest { Score = 80, Flavors = [FlavorTag.Oak, FlavorTag.Oak] };

        var result = await service.UpdateReviewAsync(Guid.NewGuid(), request, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Flavor tags must be distinct.", result.Error);
    }

    [Fact]
    public async Task UpdateReviewAsync_WhenUndefinedFlavor_ReturnsFail()
    {
        var db = CreateDbContext();
        var service = CreateService(db, Guid.NewGuid());
        var request = new UpdateReviewRequest { Score = 80, Flavors = [(FlavorTag)999] };

        var result = await service.UpdateReviewAsync(Guid.NewGuid(), request, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("One or more flavor tags are invalid.", result.Error);
    }

    [Fact]
    public async Task UpdateReviewAsync_WhenFlavorsNull_ClearsFlavorsAndNullsNotes()
    {
        var db = CreateDbContext();
        var reviewer = SeedUser(db, "Reviewer");
        var bottle = SeedBottle(db, reviewer.Id);
        var review = SeedReview(db, bottle.Id, reviewer.Id, score: 70, flavors: new[] { FlavorTag.Smoky });
        var service = CreateService(db, reviewer.Id);
        var request = new UpdateReviewRequest { Score = 60, Flavors = null };

        var result = await service.UpdateReviewAsync(review.Id, request, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Empty(result.Data!.Flavors);
        db.ChangeTracker.Clear();
        var stored = await db.BottleReviews.Include(r => r.Flavors).FirstAsync(r => r.Id == review.Id);
        Assert.Equal(60, stored.Score);
        Assert.Null(stored.Nose);
        Assert.Null(stored.Palate);
        Assert.Null(stored.Finish);
        Assert.Null(stored.Summary);
        Assert.Empty(stored.Flavors);
    }

    [Fact]
    public async Task UpdateReviewAsync_WhenNotFound_ReturnsNotFound()
    {
        var db = CreateDbContext();
        var service = CreateService(db, Guid.NewGuid());

        var result = await service.UpdateReviewAsync(Guid.NewGuid(), new UpdateReviewRequest { Score = 80 }, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ErrorCode.NotFound, result.ErrorCode);
        Assert.Equal("Review not found.", result.Error);
    }

    [Fact]
    public async Task UpdateReviewAsync_WhenSoftDeleted_ReturnsNotFound()
    {
        var db = CreateDbContext();
        var reviewer = SeedUser(db, "Reviewer");
        var bottle = SeedBottle(db, reviewer.Id);
        var review = SeedReview(db, bottle.Id, reviewer.Id, isDeleted: true);
        var service = CreateService(db, reviewer.Id);

        var result = await service.UpdateReviewAsync(review.Id, new UpdateReviewRequest { Score = 80 }, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Review not found.", result.Error);
    }

    [Fact]
    public async Task UpdateReviewAsync_WhenForbidden_ReturnsForbidden()
    {
        var db = CreateDbContext();
        var owner = SeedUser(db, "Owner");
        var author = SeedUser(db, "Author");
        var bottle = SeedBottle(db, owner.Id);
        var review = SeedReview(db, bottle.Id, author.Id, score: 70);
        var service = CreateService(db, Guid.NewGuid());

        var result = await service.UpdateReviewAsync(review.Id, new UpdateReviewRequest { Score = 80 }, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ErrorCode.Forbidden, result.ErrorCode);
        Assert.Equal("Forbidden.", result.Error);
    }

    [Fact]
    public async Task UpdateReviewAsync_WithCancelledToken_Throws()
    {
        var db = CreateDbContext();
        var service = CreateService(db, Guid.NewGuid());
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => service.UpdateReviewAsync(Guid.NewGuid(), new UpdateReviewRequest { Score = 50 }, cts.Token));
    }

    #endregion

    #region DeleteReviewAsync

    [Fact]
    public async Task DeleteReviewAsync_WhenValid_SoftDeletesReviewRowRemains()
    {
        var db = CreateDbContext();
        var reviewer = SeedUser(db, "Reviewer");
        var bottle = SeedBottle(db, reviewer.Id);
        var review = SeedReview(db, bottle.Id, reviewer.Id, score: 70);
        var service = CreateService(db, reviewer.Id);

        var result = await service.DeleteReviewAsync(review.Id, CancellationToken.None);

        Assert.True(result.Success);
        Assert.True(result.Data);
        var stored = await db.BottleReviews.FindAsync(review.Id);
        Assert.NotNull(stored);
        Assert.True(stored!.IsDeleted);
        Assert.NotNull(stored.DeletedAt);
    }

    [Fact]
    public async Task DeleteReviewAsync_WhenNotFound_ReturnsNotFound()
    {
        var db = CreateDbContext();
        var service = CreateService(db, Guid.NewGuid());

        var result = await service.DeleteReviewAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ErrorCode.NotFound, result.ErrorCode);
        Assert.Equal("Review not found.", result.Error);
    }

    [Fact]
    public async Task DeleteReviewAsync_WhenForbidden_ReturnsForbidden()
    {
        var db = CreateDbContext();
        var owner = SeedUser(db, "Owner");
        var author = SeedUser(db, "Author");
        var bottle = SeedBottle(db, owner.Id);
        var review = SeedReview(db, bottle.Id, author.Id, score: 70);
        var service = CreateService(db, Guid.NewGuid());

        var result = await service.DeleteReviewAsync(review.Id, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ErrorCode.Forbidden, result.ErrorCode);
        Assert.Equal("Forbidden.", result.Error);
    }

    [Fact]
    public async Task DeleteReviewAsync_WithCancelledToken_Throws()
    {
        var db = CreateDbContext();
        var service = CreateService(db, Guid.NewGuid());
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => service.DeleteReviewAsync(Guid.NewGuid(), cts.Token));
    }

    #endregion
}
