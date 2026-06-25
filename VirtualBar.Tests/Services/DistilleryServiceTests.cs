using Microsoft.EntityFrameworkCore;
using VirtualBar.Domain.Entities;
using VirtualBar.Infrastructure.Decorators;
using VirtualBar.Infrastructure.Persistence;
using VirtualBar.Infrastructure.Services;

namespace VirtualBar.Tests.Services;

public sealed class DistilleryServiceTests
{
    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static DistilleryService CreateService(AppDbContext db) => new(db);

    private static DistilleryValidationDecorator CreateDecorator(AppDbContext db)
    {
        var inner = new DistilleryService(db);
        return new DistilleryValidationDecorator(inner);
    }

    private static async Task<Distillery> SeedDistillery(AppDbContext db, string name = "Macallan", bool isDeleted = false)
    {
        var distillery = new Distillery
        {
            Name = name,
            Country = "Scotland",
            Region = "Speyside",
            IsDeleted = isDeleted,
            DeletedAt = isDeleted ? DateTime.UtcNow : null
        };
        db.Distilleries.Add(distillery);
        await db.SaveChangesAsync();
        return distillery;
    }

    #region Decorator GetAllAsync

    [Fact]
    public async Task GetAllAsync_WhenCalled_DelegatesToInnerAndReturnsList()
    {
        var db = CreateDb();
        await SeedDistillery(db, "Ardbeg");
        var decorator = CreateDecorator(db);

        var result = await decorator.GetAllAsync(null, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Single(result.Data!);
        Assert.Equal("Ardbeg", result.Data![0].Name);
    }

    [Fact]
    public async Task GetAllAsync_WithCancelledToken_Throws()
    {
        var db = CreateDb();
        var decorator = CreateDecorator(db);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => decorator.GetAllAsync(null, cts.Token));
    }

    #endregion

    #region Inner GetAllAsync

    [Fact]
    public async Task GetAllAsync_WhenNoDistilleries_ReturnsEmptyList()
    {
        var db = CreateDb();
        var service = CreateService(db);

        var result = await service.GetAllAsync(null, CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Empty(result.Data);
    }

    [Fact]
    public async Task GetAllAsync_WhenHasDeleted_ExcludesDeleted()
    {
        var db = CreateDb();
        await SeedDistillery(db, "Visible");
        await SeedDistillery(db, "Deleted", isDeleted: true);
        var service = CreateService(db);

        var result = await service.GetAllAsync(null, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Single(result.Data!);
        Assert.Equal("Visible", result.Data![0].Name);
    }

    [Fact]
    public async Task GetAllAsync_WhenHasDistilleries_ReturnsOrderedByName()
    {
        var db = CreateDb();
        await SeedDistillery(db, "Zilliken");
        await SeedDistillery(db, "Aberlour");
        await SeedDistillery(db, "Macallan");
        var service = CreateService(db);

        var result = await service.GetAllAsync(null, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(3, result.Data!.Count);
        Assert.Equal("Aberlour", result.Data![0].Name);
        Assert.Equal("Macallan", result.Data![1].Name);
        Assert.Equal("Zilliken", result.Data![2].Name);
    }

    #endregion
}
