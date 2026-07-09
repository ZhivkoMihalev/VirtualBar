using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using VirtualBar.Application.Options;
using VirtualBar.Domain.Entities;
using VirtualBar.Domain.Enums;
using VirtualBar.Infrastructure.Persistence;
using VirtualBar.Infrastructure.Services.Pricing;

namespace VirtualBar.Tests.Services.Pricing;

public sealed class PreWarmRefreshJobTests
{
    private const string HappyResponse = """
    {"stop_reason":"end_turn","content":[
      {"type":"web_search_tool_result","content":[{"url":"https://a/1","title":"A"}]},
      {"type":"text","text":"{\"found\":true,\"min\":100,\"max\":200,\"currency\":\"EUR\",\"confidence\":\"high\"}"}
    ]}
    """;

    private static AppDbContext CreateDbContext(string databaseName) =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options);

    private static IOptions<PricingOptions> Pricing(bool refreshEnabled, int intervalHours) =>
        Options.Create(new PricingOptions
        {
            BaseCurrency = "EUR",
            FxToBase = new Dictionary<string, decimal> { ["EUR"] = 1m },
            SnapshotTtlDays = 5,
            PreWarmTopNBottles = 10,
            RefreshIntervalHours = intervalHours,
            RefreshEnabled = refreshEnabled,
        });

    private static PreWarmWorker BuildWorker(AppDbContext db, IOptions<PricingOptions> pricing)
    {
        var anthropic = Options.Create(new AnthropicOptions
        {
            UseProviderStats = true,
            Model = "claude-sonnet-4-6",
            AnthropicVersion = "2023-06-01",
            MaxSearchesPerBottle = 5,
            DailyCallBudget = 100,
        });
        var budget = new AnthropicDailyCallBudget(anthropic, TimeProvider.System);
        var handler = new FakeHttpHandler(_ => FakeHttpHandler.JsonOk(HappyResponse));
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.anthropic.com") };
        var claude = new ClaudeMarketResearchProvider(http, anthropic, pricing, budget, NullLogger<ClaudeMarketResearchProvider>.Instance);
        var internalOpts = Options.Create(new InternalProviderOptions { UseProviderStats = false, MinSamples = 3, MinApproxSamples = 2 });
        var internalProvider = new InternalMarketPriceProvider(db, internalOpts, pricing, NullLogger<InternalMarketPriceProvider>.Instance);
        var orchestrator = new PriceEstimationService(db, internalProvider, claude, pricing);
        return new PreWarmWorker(db, orchestrator, budget, pricing, NullLogger<PreWarmWorker>.Instance);
    }

    private static void SeedBottle(AppDbContext db, string name)
    {
        db.Bottles.Add(new Bottle
        {
            UserId = Guid.NewGuid(),
            Name = name,
            Category = SpiritCategory.Whisky,
            Condition = BottleCondition.Sealed,
        });
        db.SaveChanges();
    }

    [Fact]
    public async Task ExecuteAsync_WhenRefreshDisabled_DoesNotCreateScope()
    {
        var factory = new ThrowingScopeFactory();
        var job = new PreWarmRefreshJob(factory, Pricing(refreshEnabled: false, intervalHours: 24), NullLogger<PreWarmRefreshJob>.Instance);

        await job.StartAsync(CancellationToken.None);
        await job.StopAsync(CancellationToken.None);

        Assert.False(factory.WasUsed);
    }

    [Theory]
    [InlineData(24)]
    [InlineData(0)]
    public async Task ExecuteAsync_WhenEnabled_RunsPreWarmAtLeastOnce(int intervalHours)
    {
        // The job runs the worker on a background thread; the worker owns its own DbContext. The test
        // thread reads the SAME in-memory store through separate short-lived contexts to avoid sharing a
        // single (non-thread-safe) DbContext across threads.
        var databaseName = Guid.NewGuid().ToString();
        await using var workerDb = CreateDbContext(databaseName);
        SeedBottle(workerDb, "Macallan");
        var pricing = Pricing(refreshEnabled: true, intervalHours: intervalHours);
        var worker = BuildWorker(workerDb, pricing);
        var factory = new FakeScopeFactory(worker);
        var job = new PreWarmRefreshJob(factory, pricing, NullLogger<PreWarmRefreshJob>.Instance);

        await job.StartAsync(CancellationToken.None);
        await WaitUntilSnapshotExistsAsync(databaseName);
        await job.StopAsync(CancellationToken.None);

        Assert.True(factory.ScopesCreated >= 1);
        await using var probe = CreateDbContext(databaseName);
        Assert.True(await probe.PriceSnapshots.AnyAsync());
    }

    private static async Task WaitUntilSnapshotExistsAsync(string databaseName)
    {
        for (var i = 0; i < 200; i++)
        {
            await using var probe = CreateDbContext(databaseName);
            if (await probe.PriceSnapshots.AnyAsync())
                return;
            await Task.Delay(10);
        }
    }

    private sealed class FakeScopeFactory(PreWarmWorker worker) : IServiceScopeFactory, IServiceScope, IServiceProvider
    {
        public int ScopesCreated { get; private set; }

        public IServiceScope CreateScope()
        {
            ScopesCreated++;
            return this;
        }

        public IServiceProvider ServiceProvider => this;

        public object? GetService(Type serviceType) => serviceType == typeof(PreWarmWorker) ? worker : null;

        public void Dispose()
        {
        }
    }

    private sealed class ThrowingScopeFactory : IServiceScopeFactory
    {
        public bool WasUsed { get; private set; }

        public IServiceScope CreateScope()
        {
            WasUsed = true;
            throw new InvalidOperationException("The scope factory must not be used when refresh is disabled.");
        }
    }
}
