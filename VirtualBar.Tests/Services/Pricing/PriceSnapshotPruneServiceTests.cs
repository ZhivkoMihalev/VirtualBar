using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;
using VirtualBar.Application.Common;
using VirtualBar.Application.Interfaces;
using VirtualBar.Application.Options;
using VirtualBar.Domain.Entities;
using VirtualBar.Domain.Enums;
using VirtualBar.Infrastructure.Decorators;
using VirtualBar.Infrastructure.Persistence;
using VirtualBar.Infrastructure.Services.Pricing;

namespace VirtualBar.Tests.Services.Pricing;

/// <summary>
/// Pruning the price snapshots no path reads any more (#58). SQLite in-memory, because the real run deletes with
/// <c>ExecuteDeleteAsync</c>, which EF InMemory does not support.
/// </summary>
public sealed class PriceSnapshotPruneServiceTests : IDisposable
{
    private const int TtlDays = 5;

    private readonly SqliteConnection connection;
    private readonly AppDbContext db;

    private static readonly DateTime Stale = DateTime.UtcNow.AddDays(-(TtlDays + 1));
    private static readonly DateTime Fresh = DateTime.UtcNow.AddDays(-1);

    public PriceSnapshotPruneServiceTests()
    {
        connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        db.Dispose();
        connection.Dispose();
    }

    private IPriceSnapshotPruneService CreateService(bool isAdmin = true)
    {
        var currentUser = new Mock<ICurrentUser>();
        currentUser.Setup(u => u.IsAuthenticated).Returns(true);
        currentUser.Setup(u => u.IsAdmin).Returns(isAdmin);
        var pricing = Options.Create(new PricingOptions { BaseCurrency = "EUR", SnapshotTtlDays = TtlDays });
        return new PriceSnapshotPruneValidationDecorator(new PriceSnapshotPruneService(db, pricing), currentUser.Object);
    }

    private AppUser SeedUser()
    {
        var name = $"collector-{Guid.NewGuid():N}";
        var user = new AppUser { Id = Guid.NewGuid(), UserName = name, Email = $"{name}@example.com", DisplayName = name };
        db.Users.Add(user);
        db.SaveChanges();
        return user;
    }

    private Distillery SeedDistillery(string name)
    {
        var distillery = new Distillery { Name = name };
        db.Distilleries.Add(distillery);
        db.SaveChanges();
        return distillery;
    }

    private Product SeedProduct(string canonicalKey, bool isDeleted = false)
    {
        var product = new Product
        {
            Name = canonicalKey,
            Category = SpiritCategory.Whisky,
            CanonicalKey = canonicalKey,
            Origin = ProductOrigin.Seeded,
            IsDeleted = isDeleted,
            DeletedAt = isDeleted ? DateTime.UtcNow : null,
        };
        db.Products.Add(product);
        db.SaveChanges();
        return product;
    }

    private Bottle SeedBottle(
        Guid userId,
        string name,
        Guid? distilleryId = null,
        Guid? productId = null,
        bool isDeleted = false)
    {
        var bottle = new Bottle
        {
            UserId = userId,
            Name = name,
            Category = SpiritCategory.Whisky,
            Age = 12,
            VolumeMl = 700,
            Condition = BottleCondition.Sealed,
            DistilleryId = distilleryId,
            ProductId = productId,
            IsDeleted = isDeleted,
            DeletedAt = isDeleted ? DateTime.UtcNow : null,
        };
        db.Bottles.Add(bottle);
        db.SaveChanges();
        return bottle;
    }

    /// <summary>The bottle key every pricing path computes for a bottle seeded by <see cref="SeedBottle"/>.</summary>
    private static string BottleKey(string name, string? distilleryName = null) =>
        ProductKey.For(distilleryName, name, SpiritCategory.Whisky, 12, null, 700);

    private void SeedSnapshot(string key, DateTime fetchedAt)
    {
        db.PriceSnapshots.Add(new PriceSnapshot
        {
            ProductKey = key,
            Category = SpiritCategory.Whisky,
            EstimatedPrice = 100m,
            Currency = "EUR",
            SampleSize = 1,
            Source = PriceSource.Internal,
            Confidence = PriceConfidence.Medium,
            SourcesJson = "[]",
            AsOf = fetchedAt,
            FetchedAt = fetchedAt,
        });
        db.SaveChanges();
    }

    private List<string> RemainingKeys() =>
        db.PriceSnapshots.AsNoTracking().Select(s => s.ProductKey).OrderBy(k => k).ToList();

    /// <summary>
    /// One snapshot of each kind: three are read by a pricing path or still fresh, three are stale and read by nobody.
    /// Returns the stale orphans' keys, in key order.
    /// </summary>
    private List<string> SeedEveryKind()
    {
        var user = SeedUser();
        var distillery = SeedDistillery("Talisker");

        // Read: an unlinked bottle's own key (with a distillery, so its name is part of the key).
        SeedBottle(user.Id, "Talisker 10", distilleryId: distillery.Id);
        SeedSnapshot(BottleKey("Talisker 10", "Talisker"), Stale);

        // Read: a linked bottle is priced under its product's key, and the product page reads that key too.
        var linked = SeedProduct("linked-product-key");
        SeedBottle(user.Id, "Lagavulin 16 Old Spelling", productId: linked.Id);
        SeedSnapshot("linked-product-key", Stale);

        // Read: the product page reads a non-deleted product's key even when no bottle is linked to it.
        SeedProduct("unlinked-product-key");
        SeedSnapshot("unlinked-product-key", Stale);

        // Fresh: nobody reads it, but it is within the TTL.
        SeedSnapshot("fresh-orphan-key", Fresh);

        // Orphan: written under the linked bottle's own key before #50; the bottle is now priced as its product.
        SeedSnapshot(BottleKey("Lagavulin 16 Old Spelling"), Stale);

        // Orphan: the key of a deleted bottle.
        SeedBottle(user.Id, "Deleted Bottle", isDeleted: true);
        SeedSnapshot(BottleKey("Deleted Bottle"), Stale);

        // Orphan: the key of a deleted product, whose bottle is priced under its own key again.
        var deletedProduct = SeedProduct("deleted-product-key", isDeleted: true);
        SeedBottle(user.Id, "Bottle Of Deleted Product", productId: deletedProduct.Id);
        SeedSnapshot("deleted-product-key", Stale);
        SeedSnapshot(BottleKey("Bottle Of Deleted Product"), Stale);

        return new[] { BottleKey("Lagavulin 16 Old Spelling"), BottleKey("Deleted Bottle"), "deleted-product-key" }
            .OrderBy(k => k, StringComparer.Ordinal)
            .ToList();
    }

    [Fact]
    public async Task PruneAsync_WithCancelledToken_Throws()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => CreateService().PruneAsync(dryRun: true, cts.Token));
    }

    [Fact]
    public async Task PruneAsync_WhenNotAdmin_ReturnsForbiddenAndDeletesNothing()
    {
        SeedEveryKind();
        var before = RemainingKeys();

        var result = await CreateService(isAdmin: false).PruneAsync(dryRun: false, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ErrorCode.Forbidden, result.ErrorCode);
        Assert.Equal(before, RemainingKeys());
    }

    [Fact]
    public async Task PruneAsync_WhenDryRun_ReportsTheStaleOrphansAndDeletesNothing()
    {
        var orphans = SeedEveryKind();
        var before = RemainingKeys();

        var result = await CreateService().PruneAsync(dryRun: true, CancellationToken.None);

        Assert.True(result.Success);
        var report = result.Data!;
        Assert.True(report.DryRun);
        Assert.Equal(before.Count, report.TotalCount);
        Assert.Equal(orphans.Count, report.OrphanCount);
        Assert.Equal(0, report.DeletedCount);
        Assert.Equal(orphans, report.SampleKeys);
        Assert.Equal(before, RemainingKeys());
    }

    [Fact]
    public async Task PruneAsync_WhenNotDryRun_DeletesOnlyStaleOrphans()
    {
        var orphans = SeedEveryKind();
        var before = RemainingKeys();

        var result = await CreateService().PruneAsync(dryRun: false, CancellationToken.None);

        Assert.True(result.Success);
        var report = result.Data!;
        Assert.False(report.DryRun);
        Assert.Equal(before.Count, report.TotalCount);
        Assert.Equal(orphans.Count, report.OrphanCount);
        Assert.Equal(orphans.Count, report.DeletedCount);
        Assert.Equal(before.Except(orphans).OrderBy(k => k).ToList(), RemainingKeys());
        Assert.Contains(BottleKey("Talisker 10", "Talisker"), RemainingKeys());
        Assert.Contains("linked-product-key", RemainingKeys());
        Assert.Contains("unlinked-product-key", RemainingKeys());
        Assert.Contains("fresh-orphan-key", RemainingKeys());
        Assert.Contains(BottleKey("Bottle Of Deleted Product"), RemainingKeys());
    }

    [Fact]
    public async Task PruneAsync_WhenManyOrphans_DeletesThemAllInBatchesAndSamplesTheFirstTwentyInKeyOrder()
    {
        var keys = Enumerable.Range(0, 501).Select(i => $"orphan-{i:D4}").ToList();
        foreach (var key in keys)
            SeedSnapshot(key, Stale);

        var result = await CreateService().PruneAsync(dryRun: false, CancellationToken.None);

        Assert.Equal(501, result.Data!.OrphanCount);
        Assert.Equal(501, result.Data.DeletedCount);
        Assert.Equal(keys.Take(20).ToList(), result.Data.SampleKeys);
        Assert.Empty(RemainingKeys());
    }

    [Fact]
    public async Task PruneAsync_WhenTableEmpty_ReportsZeros()
    {
        var result = await CreateService().PruneAsync(dryRun: false, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(0, result.Data!.TotalCount);
        Assert.Equal(0, result.Data.OrphanCount);
        Assert.Equal(0, result.Data.DeletedCount);
        Assert.Empty(result.Data.SampleKeys);
    }
}
