using Microsoft.EntityFrameworkCore;
using VirtualBar.Domain.Entities;
using VirtualBar.Domain.Enums;
using VirtualBar.Infrastructure.Persistence;

namespace VirtualBar.Tests.Services;

// DistillerySeeder is strictly INSERT-only (top-up semantics): it adds distilleries missing by name
// (case-insensitive) plus missing (DistilleryId, Category) pairs, and never updates an existing row.
public sealed class DistillerySeederTests
{
    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static Distillery SeedDistillery(
        AppDbContext db,
        string name,
        string? country = null,
        string? region = null)
    {
        var distillery = new Distillery { Name = name, Country = country, Region = region };
        db.Distilleries.Add(distillery);
        db.SaveChanges();
        return distillery;
    }

    private static DistilleryCategory SeedCategory(AppDbContext db, Guid distilleryId, SpiritCategory category)
    {
        var pair = new DistilleryCategory { DistilleryId = distilleryId, Category = category };
        db.DistilleryCategories.Add(pair);
        db.SaveChanges();
        return pair;
    }

    [Fact]
    public async Task SeedDistilleriesAsync_WhenDatabaseEmpty_SeedsDistilleriesAndCategories()
    {
        var db = CreateDbContext();

        await DistillerySeeder.SeedDistilleriesAsync(db, CancellationToken.None);

        var distilleries = await db.Distilleries.AsNoTracking().ToListAsync();
        var categories = await db.DistilleryCategories.AsNoTracking().ToListAsync();

        Assert.True(distilleries.Count > 0);
        Assert.True(categories.Count >= distilleries.Count);
        Assert.All(distilleries, d => Assert.Contains(categories, c => c.DistilleryId == d.Id));
        Assert.All(
            distilleries.GroupBy(d => d.Name, StringComparer.OrdinalIgnoreCase),
            g => Assert.Single(g));

        var macallan = distilleries.Single(d => d.Name == "Macallan");
        Assert.Equal("Scotland", macallan.Country);
        Assert.Contains(categories, c => c.DistilleryId == macallan.Id && c.Category == SpiritCategory.Whisky);
    }

    [Fact]
    public async Task SeedDistilleriesAsync_WhenRunTwice_InsertsNothingOnTheSecondRun()
    {
        var db = CreateDbContext();

        await DistillerySeeder.SeedDistilleriesAsync(db, CancellationToken.None);
        var distilleriesAfterFirstRun = await db.Distilleries.CountAsync();
        var categoriesAfterFirstRun = await db.DistilleryCategories.CountAsync();

        await DistillerySeeder.SeedDistilleriesAsync(db, CancellationToken.None);

        Assert.Equal(distilleriesAfterFirstRun, await db.Distilleries.CountAsync());
        Assert.Equal(categoriesAfterFirstRun, await db.DistilleryCategories.CountAsync());
    }

    [Fact]
    public async Task SeedDistilleriesAsync_WhenDistilleryPresentWithoutCategories_BackfillsCategories()
    {
        var db = CreateDbContext();
        var macallan = SeedDistillery(db, "Macallan");

        await DistillerySeeder.SeedDistilleriesAsync(db, CancellationToken.None);

        Assert.Single(await db.Distilleries.AsNoTracking().Where(d => d.Name == "Macallan").ToListAsync());
        Assert.True(await db.DistilleryCategories
            .AnyAsync(c => c.DistilleryId == macallan.Id && c.Category == SpiritCategory.Whisky));
    }

    [Fact]
    public async Task SeedDistilleriesAsync_WhenDistilleryAlreadyExists_KeepsItsCountryAndRegion()
    {
        var db = CreateDbContext();
        var macallan = SeedDistillery(db, "Macallan", country: "Bulgaria", region: "Sofia");

        await DistillerySeeder.SeedDistilleriesAsync(db, CancellationToken.None);

        var stored = await db.Distilleries.AsNoTracking().SingleAsync(d => d.Name == "Macallan");
        Assert.Equal(macallan.Id, stored.Id);
        Assert.Equal("Bulgaria", stored.Country);
        Assert.Equal("Sofia", stored.Region);
    }

    [Fact]
    public async Task SeedDistilleriesAsync_WhenExistingNameDiffersInCasing_DoesNotDuplicateIt()
    {
        var db = CreateDbContext();
        var macallan = SeedDistillery(db, "macallan");

        await DistillerySeeder.SeedDistilleriesAsync(db, CancellationToken.None);

        var matches = await db.Distilleries.AsNoTracking()
            .Where(d => d.Name == "macallan" || d.Name == "Macallan")
            .ToListAsync();
        Assert.Single(matches);
        Assert.Equal(macallan.Id, matches[0].Id);
        Assert.True(await db.DistilleryCategories
            .AnyAsync(c => c.DistilleryId == macallan.Id && c.Category == SpiritCategory.Whisky));
    }

    [Fact]
    public async Task SeedDistilleriesAsync_WhenCategoryPairAlreadyExists_DoesNotDuplicateIt()
    {
        var db = CreateDbContext();
        var macallan = SeedDistillery(db, "Macallan");
        SeedCategory(db, macallan.Id, SpiritCategory.Whisky);

        await DistillerySeeder.SeedDistilleriesAsync(db, CancellationToken.None);

        Assert.Equal(1, await db.DistilleryCategories
            .CountAsync(c => c.DistilleryId == macallan.Id && c.Category == SpiritCategory.Whisky));
    }
}
