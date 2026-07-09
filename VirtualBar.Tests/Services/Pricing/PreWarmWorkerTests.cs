using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using VirtualBar.Application.Common;
using VirtualBar.Application.Options;
using VirtualBar.Domain.Entities;
using VirtualBar.Domain.Enums;
using VirtualBar.Infrastructure.Persistence;
using VirtualBar.Infrastructure.Services.Pricing;

namespace VirtualBar.Tests.Services.Pricing;

public sealed class PreWarmWorkerTests
{
    private const string HappyResponse = """
    {"stop_reason":"end_turn","content":[
      {"type":"web_search_tool_result","content":[{"url":"https://a/1","title":"A"}]},
      {"type":"text","text":"{\"found\":true,\"min\":100,\"max\":200,\"currency\":\"EUR\",\"confidence\":\"high\"}"}
    ]}
    """;

    private static AppDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static (PreWarmWorker Worker, FakeHttpHandler Claude) CreateWorker(AppDbContext db, int topN, int dailyBudget)
    {
        var pricing = Options.Create(new PricingOptions
        {
            BaseCurrency = "EUR",
            FxToBase = new Dictionary<string, decimal> { ["EUR"] = 1m },
            SnapshotTtlDays = 5,
            PreWarmTopNBottles = topN,
            RefreshIntervalHours = 24,
            RefreshEnabled = true,
        });
        var anthropic = Options.Create(new AnthropicOptions
        {
            UseProviderStats = true,
            Model = "claude-sonnet-4-6",
            AnthropicVersion = "2023-06-01",
            MaxSearchesPerBottle = 5,
            DailyCallBudget = dailyBudget,
        });
        var budget = new AnthropicDailyCallBudget(anthropic, TimeProvider.System);
        var handler = new FakeHttpHandler(_ => FakeHttpHandler.JsonOk(HappyResponse));
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.anthropic.com") };
        var claude = new ClaudeMarketResearchProvider(http, anthropic, pricing, budget, NullLogger<ClaudeMarketResearchProvider>.Instance);
        // Internal disabled so every research goes to Claude and consumes the shared budget.
        var internalOpts = Options.Create(new InternalProviderOptions { UseProviderStats = false, MinSamples = 3, MinApproxSamples = 2 });
        var internalProvider = new InternalMarketPriceProvider(db, internalOpts, pricing, NullLogger<InternalMarketPriceProvider>.Instance);
        var orchestrator = new PriceEstimationService(db, internalProvider, claude, pricing);
        var worker = new PreWarmWorker(db, orchestrator, budget, pricing, NullLogger<PreWarmWorker>.Instance);
        return (worker, handler);
    }

    private static Bottle SeedBottle(AppDbContext db, string name)
    {
        var bottle = new Bottle
        {
            UserId = Guid.NewGuid(),
            Name = name,
            Category = SpiritCategory.Whisky,
            Condition = BottleCondition.Sealed,
        };
        db.Bottles.Add(bottle);
        db.SaveChanges();
        return bottle;
    }

    private static void SeedSnapshot(AppDbContext db, string key, DateTime fetchedAt)
    {
        db.PriceSnapshots.Add(new PriceSnapshot
        {
            ProductKey = key,
            Category = SpiritCategory.Whisky,
            EstimatedPrice = 100m,
            Currency = "EUR",
            Source = PriceSource.ClaudeResearch,
            Confidence = PriceConfidence.High,
            SourcesJson = "[]",
            AsOf = fetchedAt,
            FetchedAt = fetchedAt,
        });
        db.SaveChanges();
    }

    [Fact]
    public async Task PreWarmAsync_WhenTopNZero_ResearchesNothing()
    {
        await using var db = CreateDbContext();
        SeedBottle(db, "Macallan");
        var (worker, claude) = CreateWorker(db, topN: 0, dailyBudget: 10);

        var researched = await worker.PreWarmAsync(CancellationToken.None);

        Assert.Equal(0, researched);
        Assert.Equal(0, claude.CallCount);
    }

    [Fact]
    public async Task PreWarmAsync_WhenBudgetZero_ResearchesNothing()
    {
        await using var db = CreateDbContext();
        SeedBottle(db, "Macallan");
        var (worker, claude) = CreateWorker(db, topN: 10, dailyBudget: 0);

        var researched = await worker.PreWarmAsync(CancellationToken.None);

        Assert.Equal(0, researched);
        Assert.Equal(0, claude.CallCount);
    }

    [Fact]
    public async Task PreWarmAsync_ResearchesStaleAndMissingBottles()
    {
        await using var db = CreateDbContext();
        SeedBottle(db, "Macallan");
        SeedBottle(db, "Lagavulin");
        var (worker, claude) = CreateWorker(db, topN: 10, dailyBudget: 10);

        var researched = await worker.PreWarmAsync(CancellationToken.None);

        Assert.Equal(2, researched);
        Assert.Equal(2, claude.CallCount);
        Assert.Equal(2, await db.PriceSnapshots.CountAsync());
    }

    [Fact]
    public async Task PreWarmAsync_SkipsFreshSnapshots()
    {
        await using var db = CreateDbContext();
        SeedBottle(db, "Macallan");
        SeedBottle(db, "Lagavulin");
        SeedSnapshot(db, "whisky|macallan", DateTime.UtcNow); // fresh → skip
        var (worker, claude) = CreateWorker(db, topN: 10, dailyBudget: 10);

        var researched = await worker.PreWarmAsync(CancellationToken.None);

        Assert.Equal(1, researched); // only Lagavulin
        Assert.Equal(1, claude.CallCount);
    }

    [Fact]
    public async Task PreWarmAsync_ResearchesStaleSnapshots()
    {
        await using var db = CreateDbContext();
        SeedBottle(db, "Macallan");
        SeedSnapshot(db, "whisky|macallan", DateTime.UtcNow.AddDays(-10)); // stale → research
        var (worker, claude) = CreateWorker(db, topN: 10, dailyBudget: 10);

        var researched = await worker.PreWarmAsync(CancellationToken.None);

        Assert.Equal(1, researched);
        Assert.Equal(1, claude.CallCount);
    }

    [Fact]
    public async Task PreWarmAsync_StopsWhenDailyBudgetSpent()
    {
        await using var db = CreateDbContext();
        SeedBottle(db, "Macallan");
        SeedBottle(db, "Lagavulin");
        SeedBottle(db, "Ardbeg");
        var (worker, claude) = CreateWorker(db, topN: 10, dailyBudget: 1);

        var researched = await worker.PreWarmAsync(CancellationToken.None);

        Assert.Equal(1, researched);
        Assert.Equal(1, claude.CallCount);
    }

    [Fact]
    public async Task PreWarmAsync_RespectsTopNCap()
    {
        await using var db = CreateDbContext();
        SeedBottle(db, "Macallan");
        SeedBottle(db, "Lagavulin");
        SeedBottle(db, "Ardbeg");
        var (worker, claude) = CreateWorker(db, topN: 2, dailyBudget: 10);

        var researched = await worker.PreWarmAsync(CancellationToken.None);

        Assert.Equal(2, researched);
        Assert.Equal(2, claude.CallCount);
    }

    [Fact]
    public async Task PreWarmAsync_WhenNoBottles_ResearchesNothing()
    {
        await using var db = CreateDbContext();
        var (worker, claude) = CreateWorker(db, topN: 10, dailyBudget: 10);

        var researched = await worker.PreWarmAsync(CancellationToken.None);

        Assert.Equal(0, researched);
        Assert.Equal(0, claude.CallCount);
    }

    [Fact]
    public async Task PreWarmAsync_WhenCancelled_DoesNoResearch()
    {
        await using var db = CreateDbContext();
        SeedBottle(db, "Macallan");
        var (worker, claude) = CreateWorker(db, topN: 10, dailyBudget: 10);
        var cts = new CancellationTokenSource();
        cts.Cancel();

        try
        {
            var researched = await worker.PreWarmAsync(cts.Token);
            Assert.Equal(0, researched);
        }
        catch (OperationCanceledException)
        {
            // EF honored the token during selection — also an acceptable cancellation outcome.
        }

        Assert.Equal(0, claude.CallCount);
    }
}
