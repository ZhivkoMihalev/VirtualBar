using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using VirtualBar.Application.Options;
using VirtualBar.Domain.Entities;
using VirtualBar.Domain.Enums;
using VirtualBar.Infrastructure.Persistence;
using VirtualBar.Infrastructure.Services.Pricing;

namespace VirtualBar.Tests.Services.Pricing;

public sealed class PriceEstimationServiceTests
{
    private const string BottleKey = "whisky|macallan 18|18yo|700";

    private const string HappyResponse = """
    {"stop_reason":"end_turn","content":[
      {"type":"web_search_tool_result","content":[{"url":"https://a.example/1","title":"Auction A"}]},
      {"type":"text","text":"{\"found\":true,\"min\":100,\"max\":200,\"currency\":\"EUR\",\"confidence\":\"high\",\"asOf\":\"2026-06-01\"}"}
    ]}
    """;

    private const string FoundFalseResponse = """
    {"stop_reason":"end_turn","content":[
      {"type":"web_search_tool_result","content":[{"url":"https://a/1","title":"A"}]},
      {"type":"text","text":"{\"found\":false}"}
    ]}
    """;

    private static AppDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static IOptions<PricingOptions> Pricing() =>
        Options.Create(new PricingOptions
        {
            BaseCurrency = "EUR",
            FxToBase = new Dictionary<string, decimal> { ["EUR"] = 1m, ["USD"] = 0.92m },
            SnapshotTtlDays = 5,
        });

    private static (PriceEstimationService Svc, FakeHttpHandler Claude) CreateOrchestrator(
        AppDbContext db,
        bool claudeEnabled = true,
        bool internalEnabled = true,
        int dailyBudget = 100,
        Func<HttpRequestMessage, int, HttpResponseMessage>? claudeResponder = null)
    {
        var pricing = Pricing();
        var handler = new FakeHttpHandler(claudeResponder ?? ((_, _) => FakeHttpHandler.JsonOk(HappyResponse)));
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.anthropic.com") };
        var anthropic = Options.Create(new AnthropicOptions
        {
            UseProviderStats = claudeEnabled,
            Model = "claude-sonnet-4-6",
            AnthropicVersion = "2023-06-01",
            MaxSearchesPerBottle = 5,
            DailyCallBudget = dailyBudget,
        });
        var budget = new AnthropicDailyCallBudget(anthropic, TimeProvider.System);
        var claude = new ClaudeMarketResearchProvider(http, anthropic, pricing, budget, NullLogger<ClaudeMarketResearchProvider>.Instance);
        var internalOpts = Options.Create(new InternalProviderOptions { UseProviderStats = internalEnabled, MinSamples = 3, MinApproxSamples = 2 });
        var internalProvider = new InternalMarketPriceProvider(db, internalOpts, pricing, NullLogger<InternalMarketPriceProvider>.Instance);
        return (new PriceEstimationService(db, internalProvider, claude, pricing), handler);
    }

    private static Bottle SeedBottle(AppDbContext db, BottleCondition condition = BottleCondition.Sealed)
    {
        var bottle = new Bottle
        {
            UserId = Guid.NewGuid(),
            Name = "Macallan 18",
            Category = SpiritCategory.Whisky,
            Age = 18,
            VolumeMl = 700,
            Condition = condition,
        };
        db.Bottles.Add(bottle);
        db.SaveChanges();
        return bottle;
    }

    private static void SeedListing(AppDbContext db, decimal price)
    {
        db.Bottles.Add(new Bottle
        {
            UserId = Guid.NewGuid(),
            Name = "Macallan 18",
            Category = SpiritCategory.Whisky,
            Age = 18,
            VolumeMl = 700,
            Condition = BottleCondition.Sealed,
            AskingPrice = price,
            Currency = "EUR",
        });
        db.SaveChanges();
    }

    private static void SeedSnapshot(
        AppDbContext db,
        DateTime fetchedAt,
        decimal price = 300m,
        string sourcesJson = "[]",
        string key = BottleKey)
    {
        db.PriceSnapshots.Add(new PriceSnapshot
        {
            ProductKey = key,
            Category = SpiritCategory.Whisky,
            EstimatedPrice = price,
            LowEstimate = price,
            HighEstimate = price,
            Currency = "EUR",
            SampleSize = 1,
            Source = PriceSource.Internal,
            Confidence = PriceConfidence.Medium,
            SourcesJson = sourcesJson,
            AsOf = fetchedAt,
            FetchedAt = fetchedAt,
        });
        db.SaveChanges();
    }

    [Fact]
    public async Task GetBottleEstimateAsync_WhenBottleMissing_ReturnsNotFound()
    {
        await using var db = CreateDbContext();
        var (svc, _) = CreateOrchestrator(db);

        var result = await svc.GetBottleEstimateAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(Application.Common.ErrorCode.NotFound, result.ErrorCode);
    }

    [Fact]
    public async Task GetBottleEstimateAsync_WhenFreshSnapshot_ReturnsCachedAndDoesNotCallClaude()
    {
        await using var db = CreateDbContext();
        var bottle = SeedBottle(db);
        SeedSnapshot(db, DateTime.UtcNow, price: 321m);
        var (svc, claude) = CreateOrchestrator(db);

        var result = await svc.GetBottleEstimateAsync(bottle.Id, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(321m, result.Data!.EstimatedPrice);
        Assert.Equal(0, claude.CallCount);
    }

    [Fact]
    public async Task GetBottleEstimateAsync_WhenStaleSnapshot_RefreshesViaClaude()
    {
        await using var db = CreateDbContext();
        var bottle = SeedBottle(db);
        SeedSnapshot(db, DateTime.UtcNow.AddDays(-10), price: 321m);
        var (svc, claude) = CreateOrchestrator(db, internalEnabled: false);

        var result = await svc.GetBottleEstimateAsync(bottle.Id, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(PriceSource.ClaudeResearch, result.Data!.Source);
        Assert.Equal(150m, result.Data.EstimatedPrice);
        Assert.Equal(1, claude.CallCount);

        var snapshot = await db.PriceSnapshots.SingleAsync(s => s.ProductKey == BottleKey);
        Assert.Equal(150m, snapshot.EstimatedPrice);
    }

    [Fact]
    public async Task GetBottleEstimateAsync_WhenClaudeReturns_IsUsedOverInternal()
    {
        await using var db = CreateDbContext();
        var bottle = SeedBottle(db);
        SeedListing(db, 100m);
        SeedListing(db, 110m);
        SeedListing(db, 120m); // internal would have data too
        var (svc, claude) = CreateOrchestrator(db);

        var result = await svc.GetBottleEstimateAsync(bottle.Id, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(PriceSource.ClaudeResearch, result.Data!.Source);
        Assert.Equal(1, claude.CallCount);
    }

    [Fact]
    public async Task GetBottleEstimateAsync_WhenClaudeReturnsNull_FallsBackToInternal()
    {
        await using var db = CreateDbContext();
        var bottle = SeedBottle(db);
        SeedListing(db, 100m);
        SeedListing(db, 200m);
        var (svc, _) = CreateOrchestrator(db, claudeResponder: (_, _) => FakeHttpHandler.JsonOk(FoundFalseResponse));

        var result = await svc.GetBottleEstimateAsync(bottle.Id, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(PriceSource.Internal, result.Data!.Source);
    }

    [Fact]
    public async Task GetBottleEstimateAsync_WhenBothProvidersReturnNull_ReturnsNotFound()
    {
        await using var db = CreateDbContext();
        var bottle = SeedBottle(db);
        var (svc, _) = CreateOrchestrator(db, claudeResponder: (_, _) => FakeHttpHandler.JsonOk(FoundFalseResponse));

        var result = await svc.GetBottleEstimateAsync(bottle.Id, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(Application.Common.ErrorCode.NotFound, result.ErrorCode);
    }

    [Fact]
    public async Task GetBottleEstimateAsync_WhenLosesUpsertRace_UpdatesWinnerRowInsteadOfThrowing()
    {
        // SQLite (not InMemory) so the unique ProductKey index is actually enforced. A second context on
        // the SAME connection inserts the "winner" row from inside the Claude responder — i.e. exactly in
        // the race window, after the orchestrator's cache-miss read and before its own insert — so the
        // orchestrator's SaveChanges hits a unique violation and must recover onto the winner's row.
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        await using var db = new AppDbContext(options);
        db.Database.EnsureCreated();

        // SQLite enforces the Bottle → AppUser FK (InMemory doesn't), so seed a real owner.
        var owner = new AppUser { Id = Guid.NewGuid(), UserName = "owner@example.com", Email = "owner@example.com", DisplayName = "Owner" };
        db.Users.Add(owner);
        var bottle = new Bottle
        {
            UserId = owner.Id,
            Name = "Macallan 18",
            Category = SpiritCategory.Whisky,
            Age = 18,
            VolumeMl = 700,
            Condition = BottleCondition.Sealed,
        };
        db.Bottles.Add(bottle);
        db.SaveChanges();

        var (svc, claude) = CreateOrchestrator(db, internalEnabled: false, claudeResponder: (_, _) =>
        {
            using var concurrentRun = new AppDbContext(options);
            SeedSnapshot(concurrentRun, DateTime.UtcNow.AddDays(-1), price: 999m);
            return FakeHttpHandler.JsonOk(HappyResponse);
        });

        var result = await svc.GetBottleEstimateAsync(bottle.Id, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(150m, result.Data!.EstimatedPrice); // the paid-for research is returned, not dropped
        Assert.Equal(1, claude.CallCount);

        db.ChangeTracker.Clear();
        var snapshot = await db.PriceSnapshots.SingleAsync(s => s.ProductKey == BottleKey); // exactly one row
        Assert.Equal(150m, snapshot.EstimatedPrice); // the winner's row now carries the fresh estimate
        Assert.Equal(PriceSource.ClaudeResearch, snapshot.Source);
    }

    [Fact]
    public async Task GetBottleEstimateAsync_PersistsSourcesJson()
    {
        await using var db = CreateDbContext();
        var bottle = SeedBottle(db);
        var (svc, _) = CreateOrchestrator(db, internalEnabled: false);

        await svc.GetBottleEstimateAsync(bottle.Id, CancellationToken.None);

        var snapshot = await db.PriceSnapshots.SingleAsync(s => s.ProductKey == BottleKey);
        Assert.Contains("https://a.example/1", snapshot.SourcesJson);
    }

    [Fact]
    public async Task GetBottleEstimateAsync_WhenClaudeThrows_SwallowsAndFallsBackToInternal()
    {
        await using var db = CreateDbContext();
        var bottle = SeedBottle(db);
        SeedListing(db, 100m);
        var (svc, _) = CreateOrchestrator(db, claudeResponder: (_, _) => throw new HttpRequestException("network down"));

        var result = await svc.GetBottleEstimateAsync(bottle.Id, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(PriceSource.Internal, result.Data!.Source);
    }

    [Fact]
    public async Task GetCachedBottleEstimateAsync_WhenBottleMissing_ReturnsNotFound()
    {
        await using var db = CreateDbContext();
        var (svc, _) = CreateOrchestrator(db);

        var result = await svc.GetCachedBottleEstimateAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(Application.Common.ErrorCode.NotFound, result.ErrorCode);
    }

    [Fact]
    public async Task GetCachedBottleEstimateAsync_WhenNoSnapshot_ReturnsSuccessWithNullData()
    {
        await using var db = CreateDbContext();
        var bottle = SeedBottle(db);
        var (svc, claude) = CreateOrchestrator(db);

        var result = await svc.GetCachedBottleEstimateAsync(bottle.Id, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Null(result.Data);
        Assert.Equal(0, claude.CallCount);
    }

    [Fact]
    public async Task GetCachedBottleEstimateAsync_WhenSnapshotPresent_ReturnsDtoWithSources()
    {
        await using var db = CreateDbContext();
        var bottle = SeedBottle(db);
        // SourcesJson round-trips with default System.Text.Json (PascalCase property names).
        SeedSnapshot(db, DateTime.UtcNow, price: 250m,
            sourcesJson: """[{"Url":"https://x/1","Title":"X"}]""");
        var (svc, _) = CreateOrchestrator(db);

        var result = await svc.GetCachedBottleEstimateAsync(bottle.Id, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(250m, result.Data!.EstimatedPrice);
        Assert.Single(result.Data.Sources);
        Assert.Equal("https://x/1", result.Data.Sources[0].Url);
    }

    [Fact]
    public async Task GetCachedBottleEstimateAsync_WhenSourcesJsonInvalid_ReturnsEmptySources()
    {
        await using var db = CreateDbContext();
        var bottle = SeedBottle(db);
        SeedSnapshot(db, DateTime.UtcNow, sourcesJson: "this is not json");
        var (svc, _) = CreateOrchestrator(db);

        var result = await svc.GetCachedBottleEstimateAsync(bottle.Id, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Empty(result.Data!.Sources);
    }

    [Fact]
    public async Task GetCachedBottleEstimateAsync_WhenSourcesJsonLiteralNull_ReturnsEmptySources()
    {
        await using var db = CreateDbContext();
        var bottle = SeedBottle(db);
        SeedSnapshot(db, DateTime.UtcNow, sourcesJson: "null"); // deserializes to null → coalesced to []
        var (svc, _) = CreateOrchestrator(db);

        var result = await svc.GetCachedBottleEstimateAsync(bottle.Id, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Empty(result.Data!.Sources);
    }

    [Fact]
    public async Task GetCachedBottleEstimateAsync_WhenSourcesJsonEmpty_ReturnsEmptySources()
    {
        await using var db = CreateDbContext();
        var bottle = SeedBottle(db);
        SeedSnapshot(db, DateTime.UtcNow, sourcesJson: "");
        var (svc, _) = CreateOrchestrator(db);

        var result = await svc.GetCachedBottleEstimateAsync(bottle.Id, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Empty(result.Data!.Sources);
    }

    [Fact]
    public async Task GetCollectionValueAsync_WhenNoBottles_ReturnsEmpty()
    {
        await using var db = CreateDbContext();
        var (svc, _) = CreateOrchestrator(db);

        var result = await svc.GetCollectionValueAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(0m, result.Data!.TotalValue);
        Assert.Equal("EUR", result.Data.Currency);
        Assert.Empty(result.Data.Items);
    }

    [Fact]
    public async Task GetCollectionValueAsync_TotalsSealedOnlyAndFlagsLines()
    {
        await using var db = CreateDbContext();
        var userId = Guid.NewGuid();
        AddOwnedBottle(db, userId, BottleCondition.Sealed);
        AddOwnedBottle(db, userId, BottleCondition.Sealed);
        AddOwnedBottle(db, userId, BottleCondition.Opened);
        SeedSnapshot(db, DateTime.UtcNow, price: 100m);
        var (svc, _) = CreateOrchestrator(db);

        var result = await svc.GetCollectionValueAsync(userId, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(200m, result.Data!.TotalValue); // two sealed only
        Assert.Equal(3, result.Data.PricedCount);     // all three share the priced key
        Assert.Equal(3, result.Data.TotalCount);
        Assert.Equal(2, result.Data.Items.Count(i => i.CountedInTotal));
    }

    [Fact]
    public async Task GetCollectionValueAsync_WhenBottleUnpriced_ExcludesFromTotalAndPricedCount()
    {
        await using var db = CreateDbContext();
        var userId = Guid.NewGuid();
        AddOwnedBottle(db, userId, BottleCondition.Sealed); // no matching snapshot
        var (svc, _) = CreateOrchestrator(db);

        var result = await svc.GetCollectionValueAsync(userId, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(0m, result.Data!.TotalValue);
        Assert.Equal(0, result.Data.PricedCount);
        Assert.Equal(1, result.Data.TotalCount);
        var line = Assert.Single(result.Data.Items);
        Assert.Null(line.EstimatedPrice);
        Assert.False(line.CountedInTotal);
    }

    [Fact]
    public async Task GetCollectionValueAsync_WhenEntryHoldsSeveralBottles_ScalesTotalAndCounts()
    {
        await using var db = CreateDbContext();
        var userId = Guid.NewGuid();
        AddOwnedBottle(db, userId, BottleCondition.Sealed, quantity: 3);
        SeedSnapshot(db, DateTime.UtcNow, price: 100m);
        var (svc, _) = CreateOrchestrator(db);

        var result = await svc.GetCollectionValueAsync(userId, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(300m, result.Data!.TotalValue);
        Assert.Equal(3, result.Data.PricedCount);
        Assert.Equal(3, result.Data.TotalCount);
        var line = Assert.Single(result.Data.Items);
        Assert.Equal(100m, line.EstimatedPrice);
        Assert.Equal(3, line.Quantity);
        Assert.True(line.CountedInTotal);
    }

    [Fact]
    public async Task GetCollectionValueAsync_WhenOpenedEntryHoldsSeveralBottles_CountsThemButNotTheirValue()
    {
        await using var db = CreateDbContext();
        var userId = Guid.NewGuid();
        AddOwnedBottle(db, userId, BottleCondition.Opened, quantity: 4);
        SeedSnapshot(db, DateTime.UtcNow, price: 100m);
        var (svc, _) = CreateOrchestrator(db);

        var result = await svc.GetCollectionValueAsync(userId, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(0m, result.Data!.TotalValue);
        Assert.Equal(4, result.Data.PricedCount);
        Assert.Equal(4, result.Data.TotalCount);
        Assert.False(Assert.Single(result.Data.Items).CountedInTotal);
    }

    [Fact]
    public async Task GetCollectionValueAsync_WhenUnpricedEntryHoldsSeveralBottles_CountsThemInTotalCountOnly()
    {
        await using var db = CreateDbContext();
        var userId = Guid.NewGuid();
        AddOwnedBottle(db, userId, BottleCondition.Sealed, quantity: 5);
        var (svc, _) = CreateOrchestrator(db);

        var result = await svc.GetCollectionValueAsync(userId, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(0m, result.Data!.TotalValue);
        Assert.Equal(0, result.Data.PricedCount);
        Assert.Equal(5, result.Data.TotalCount);
        Assert.Equal(5, Assert.Single(result.Data.Items).Quantity);
    }

    private static void AddOwnedBottle(AppDbContext db, Guid userId, BottleCondition condition, int quantity = 1)
    {
        db.Bottles.Add(new Bottle
        {
            UserId = userId,
            Name = "Macallan 18",
            Category = SpiritCategory.Whisky,
            Age = 18,
            VolumeMl = 700,
            Condition = condition,
            Quantity = quantity,
        });
        db.SaveChanges();
    }

    private const string ProductCanonicalKey = "whisky|macallan|sherry oak|18yo|700";

    private static Product SeedProduct(
        AppDbContext db,
        string? brand = "Macallan",
        string? barcode = "5010314101011",
        Distillery? distillery = null,
        bool isDeleted = false)
    {
        var product = new Product
        {
            Name = "Sherry Oak 18",
            Brand = brand,
            DistilleryId = distillery?.Id,
            Category = SpiritCategory.Whisky,
            Age = 18,
            VolumeMl = 700,
            Barcode = barcode,
            CanonicalKey = ProductCanonicalKey,
            Origin = ProductOrigin.Seeded,
            IsDeleted = isDeleted,
        };
        db.Products.Add(product);
        db.SaveChanges();
        return product;
    }

    private static Distillery SeedDistillery(AppDbContext db, string name, bool isDeleted = false)
    {
        var distillery = new Distillery { Name = name, IsDeleted = isDeleted };
        db.Distilleries.Add(distillery);
        db.SaveChanges();
        return distillery;
    }

    private static Bottle SeedLinkedBottle(
        AppDbContext db,
        Guid productId,
        string name = "Macallan 18 Sherry",
        int? vintageYear = null,
        string? barcode = null,
        SpiritCategory category = SpiritCategory.Whisky,
        Guid? userId = null)
    {
        var bottle = new Bottle
        {
            UserId = userId ?? Guid.NewGuid(),
            Name = name,
            Category = category,
            Age = 18,
            VintageYear = vintageYear,
            VolumeMl = 700,
            Barcode = barcode,
            Condition = BottleCondition.Sealed,
            ProductId = productId,
        };
        db.Bottles.Add(bottle);
        db.SaveChanges();
        return bottle;
    }

    /// <summary>Captures the request body Claude receives, so a test can assert what was researched.</summary>
    private static Func<HttpRequestMessage, int, HttpResponseMessage> Capturing(List<string> bodies) =>
        (request, _) =>
        {
            bodies.Add(request.Content!.ReadAsStringAsync().GetAwaiter().GetResult());
            return FakeHttpHandler.JsonOk(HappyResponse);
        };

    [Fact]
    public async Task GetBottleEstimateAsync_WhenTwoBottlesLinkedToSameProduct_ShareOneSnapshot()
    {
        await using var db = CreateDbContext();
        var product = SeedProduct(db);
        var first = SeedLinkedBottle(db, product.Id, name: "Macallan 18 Sherry", vintageYear: 1998);
        var second = SeedLinkedBottle(db, product.Id, name: "The Macallan 18yo Sherry Oak");
        var (svc, claude) = CreateOrchestrator(db, internalEnabled: false);

        var firstResult = await svc.GetBottleEstimateAsync(first.Id, CancellationToken.None);
        var secondResult = await svc.GetBottleEstimateAsync(second.Id, CancellationToken.None);

        Assert.True(firstResult.Success);
        Assert.True(secondResult.Success);
        Assert.Equal(1, claude.CallCount);
        var snapshot = await db.PriceSnapshots.SingleAsync();
        Assert.Equal(ProductCanonicalKey, snapshot.ProductKey);
    }

    [Fact]
    public async Task GetBottleEstimateAsync_WhenLinked_ResearchesTheProductWithoutVintage()
    {
        await using var db = CreateDbContext();
        var distillery = SeedDistillery(db, "The Macallan");
        var product = SeedProduct(db, distillery: distillery);
        // The bottle's own category disagrees with the product: the product wins for the snapshot too.
        var bottle = SeedLinkedBottle(db, product.Id, name: "My Mac", vintageYear: 1998, barcode: "1111111111111", category: SpiritCategory.Other);
        var bodies = new List<string>();
        var (svc, _) = CreateOrchestrator(db, internalEnabled: false, claudeResponder: Capturing(bodies));

        var result = await svc.GetBottleEstimateAsync(bottle.Id, CancellationToken.None);

        Assert.True(result.Success);
        var body = Assert.Single(bodies);
        Assert.Contains("Name: Sherry Oak 18", body);
        Assert.Contains("Distillery/brand: The Macallan", body);
        Assert.Contains("Barcode/UPC: 5010314101011", body);
        Assert.DoesNotContain("Vintage:", body);
        Assert.DoesNotContain("My Mac", body);
        var snapshot = await db.PriceSnapshots.SingleAsync();
        Assert.Equal("5010314101011", snapshot.Barcode);
        Assert.Equal(SpiritCategory.Whisky, snapshot.Category);
    }

    [Fact]
    public async Task GetBottleEstimateAsync_WhenLinkedProductHasNoDistilleryOrBarcode_UsesBrandAndBottleBarcode()
    {
        await using var db = CreateDbContext();
        var product = SeedProduct(db, brand: "Macallan", barcode: null);
        var bottle = SeedLinkedBottle(db, product.Id, barcode: "2222222222222");
        var bodies = new List<string>();
        var (svc, _) = CreateOrchestrator(db, internalEnabled: false, claudeResponder: Capturing(bodies));

        await svc.GetBottleEstimateAsync(bottle.Id, CancellationToken.None);

        var body = Assert.Single(bodies);
        Assert.Contains("Distillery/brand: Macallan", body);
        Assert.Contains("Barcode/UPC: 2222222222222", body);
        Assert.Equal("2222222222222", (await db.PriceSnapshots.SingleAsync()).Barcode);
    }

    [Fact]
    public async Task GetBottleEstimateAsync_WhenLinkedProductDistilleryDeleted_UsesBrand()
    {
        await using var db = CreateDbContext();
        var distillery = SeedDistillery(db, "Gone Distillery", isDeleted: true);
        var product = SeedProduct(db, brand: "Macallan", distillery: distillery);
        var bottle = SeedLinkedBottle(db, product.Id);
        var bodies = new List<string>();
        var (svc, _) = CreateOrchestrator(db, internalEnabled: false, claudeResponder: Capturing(bodies));

        await svc.GetBottleEstimateAsync(bottle.Id, CancellationToken.None);

        var body = Assert.Single(bodies);
        Assert.Contains("Distillery/brand: Macallan", body);
        Assert.DoesNotContain("Gone Distillery", body);
    }

    [Fact]
    public async Task GetBottleEstimateAsync_WhenLinkedProductDeleted_FallsBackToTheBottleKey()
    {
        await using var db = CreateDbContext();
        var product = SeedProduct(db, isDeleted: true);
        var bottle = SeedLinkedBottle(db, product.Id, name: "Macallan 18");
        var (svc, _) = CreateOrchestrator(db, internalEnabled: false);

        var result = await svc.GetBottleEstimateAsync(bottle.Id, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(BottleKey, (await db.PriceSnapshots.SingleAsync()).ProductKey);
    }

    [Fact]
    public async Task GetCachedBottleEstimateAsync_WhenLinked_ReadsTheProductSnapshot()
    {
        await using var db = CreateDbContext();
        var product = SeedProduct(db);
        var bottle = SeedLinkedBottle(db, product.Id, vintageYear: 2001);
        SeedSnapshot(db, DateTime.UtcNow, price: 456m, key: ProductCanonicalKey);
        var (svc, claude) = CreateOrchestrator(db);

        var result = await svc.GetCachedBottleEstimateAsync(bottle.Id, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(456m, result.Data!.EstimatedPrice);
        Assert.Equal(0, claude.CallCount);
    }

    [Fact]
    public async Task GetCollectionValueAsync_WhenLinked_PricesFromTheProductSnapshot()
    {
        await using var db = CreateDbContext();
        var userId = Guid.NewGuid();
        var product = SeedProduct(db);
        SeedLinkedBottle(db, product.Id, vintageYear: 1998, userId: userId);
        SeedLinkedBottle(db, product.Id, name: "Other spelling", userId: userId);
        SeedSnapshot(db, DateTime.UtcNow, price: 200m, key: ProductCanonicalKey);
        var (svc, _) = CreateOrchestrator(db);

        var result = await svc.GetCollectionValueAsync(userId, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(400m, result.Data!.TotalValue);
        Assert.Equal(2, result.Data.PricedCount);
    }

    [Fact]
    public async Task GetCachedProductEstimateAsync_WhenProductMissing_ReturnsNotFound()
    {
        await using var db = CreateDbContext();
        var (svc, _) = CreateOrchestrator(db);

        var result = await svc.GetCachedProductEstimateAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(Application.Common.ErrorCode.NotFound, result.ErrorCode);
    }

    [Fact]
    public async Task GetCachedProductEstimateAsync_WhenProductDeleted_ReturnsNotFound()
    {
        await using var db = CreateDbContext();
        var product = SeedProduct(db, isDeleted: true);
        SeedSnapshot(db, DateTime.UtcNow, key: ProductCanonicalKey);
        var (svc, _) = CreateOrchestrator(db);

        var result = await svc.GetCachedProductEstimateAsync(product.Id, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(Application.Common.ErrorCode.NotFound, result.ErrorCode);
    }

    [Fact]
    public async Task GetCachedProductEstimateAsync_WhenNoSnapshot_ReturnsSuccessWithNullData()
    {
        await using var db = CreateDbContext();
        var product = SeedProduct(db);
        var (svc, claude) = CreateOrchestrator(db);

        var result = await svc.GetCachedProductEstimateAsync(product.Id, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Null(result.Data);
        Assert.Equal(0, claude.CallCount);
    }

    [Fact]
    public async Task GetCachedProductEstimateAsync_WhenSnapshotPresent_ReturnsDto()
    {
        await using var db = CreateDbContext();
        var product = SeedProduct(db);
        SeedSnapshot(db, DateTime.UtcNow, price: 789m, key: ProductCanonicalKey);
        var (svc, claude) = CreateOrchestrator(db);

        var result = await svc.GetCachedProductEstimateAsync(product.Id, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(789m, result.Data!.EstimatedPrice);
        Assert.Equal(0, claude.CallCount);
    }
}
