using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using VirtualBar.Application.Common;
using VirtualBar.Application.DTOs.Pricing;
using VirtualBar.Application.Options;
using VirtualBar.Domain.Enums;
using VirtualBar.Infrastructure.Services.Pricing;

namespace VirtualBar.Tests.Services.Pricing;

public sealed class ClaudeMarketResearchProviderTests
{
    private static PriceProviderInput Input() =>
        new("Macallan 18", "Macallan", SpiritCategory.Whisky, 18, null, 700, null,
            ProductKey.For("Macallan", "Macallan 18", SpiritCategory.Whisky, 18, null, 700));

    private static ClaudeMarketResearchProvider Create(
        FakeHttpHandler handler,
        bool enabled = true,
        int dailyBudget = 100,
        string[]? allowedDomains = null,
        string[]? blockedDomains = null,
        int maxSearches = 5)
    {
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.anthropic.com") };
        var anthropic = Options.Create(new AnthropicOptions
        {
            UseProviderStats = enabled,
            BaseUrl = "https://api.anthropic.com",
            Model = "claude-sonnet-4-6",
            AnthropicVersion = "2023-06-01",
            MaxSearchesPerBottle = maxSearches,
            DailyCallBudget = dailyBudget,
            AllowedDomains = allowedDomains ?? [],
            BlockedDomains = blockedDomains ?? [],
        });
        var pricing = Options.Create(new PricingOptions
        {
            BaseCurrency = "EUR",
            FxToBase = new Dictionary<string, decimal> { ["EUR"] = 1m, ["USD"] = 0.92m, ["GBP"] = 1.17m },
            SnapshotTtlDays = 5,
        });
        var budget = new AnthropicDailyCallBudget(anthropic, TimeProvider.System);
        return new ClaudeMarketResearchProvider(http, anthropic, pricing, budget, NullLogger<ClaudeMarketResearchProvider>.Instance);
    }

    // A response whose text block carries the strict JSON, plus a web_search_tool_result block with a citation.
    private const string HappyResponse = """
    {"stop_reason":"end_turn","content":[
      {"type":"web_search_tool_result","content":[{"url":"https://a.example/1","title":"Auction A"}]},
      {"type":"text","text":"{\"found\":true,\"min\":100,\"max\":200,\"currency\":\"EUR\",\"confidence\":\"high\",\"asOf\":\"2026-06-01\"}"}
    ]}
    """;

    [Fact]
    public async Task TryEstimateAsync_WhenHappyPath_ReturnsRangeWithSources()
    {
        var provider = Create(new FakeHttpHandler(_ => FakeHttpHandler.JsonOk(HappyResponse)));

        var dto = await provider.TryEstimateAsync(Input(), CancellationToken.None);

        Assert.NotNull(dto);
        Assert.Equal(PriceSource.ClaudeResearch, dto!.Source);
        Assert.Equal(100m, dto.LowEstimate);
        Assert.Equal(200m, dto.HighEstimate);
        Assert.Equal(150m, dto.EstimatedPrice);
        Assert.Equal("EUR", dto.Currency);
        Assert.Equal(PriceConfidence.High, dto.Confidence);
        Assert.Single(dto.Sources);
        Assert.Equal("https://a.example/1", dto.Sources[0].Url);
        Assert.Equal(new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc), dto.AsOf);
    }

    [Fact]
    public async Task TryEstimateAsync_WhenDisabled_DoesNotCallApiAndReturnsNull()
    {
        var handler = new FakeHttpHandler(_ => FakeHttpHandler.JsonOk(HappyResponse));
        var provider = Create(handler, enabled: false);

        Assert.Null(await provider.TryEstimateAsync(Input(), CancellationToken.None));
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task TryEstimateAsync_WhenBudgetExhausted_DoesNotCallApiAndReturnsNull()
    {
        var handler = new FakeHttpHandler(_ => FakeHttpHandler.JsonOk(HappyResponse));
        var provider = Create(handler, dailyBudget: 0);

        Assert.Null(await provider.TryEstimateAsync(Input(), CancellationToken.None));
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task TryEstimateAsync_WhenFoundFalse_ReturnsNull()
    {
        const string json = """
        {"stop_reason":"end_turn","content":[
          {"type":"web_search_tool_result","content":[{"url":"https://a/1","title":"A"}]},
          {"type":"text","text":"{\"found\":false}"}
        ]}
        """;
        var provider = Create(new FakeHttpHandler(_ => FakeHttpHandler.JsonOk(json)));

        Assert.Null(await provider.TryEstimateAsync(Input(), CancellationToken.None));
    }

    [Fact]
    public async Task TryEstimateAsync_WhenNoCitations_ReturnsNull()
    {
        const string json = """
        {"stop_reason":"end_turn","content":[
          {"type":"text","text":"{\"found\":true,\"min\":100,\"max\":200,\"currency\":\"EUR\",\"confidence\":\"high\"}"}
        ]}
        """;
        var provider = Create(new FakeHttpHandler(_ => FakeHttpHandler.JsonOk(json)));

        Assert.Null(await provider.TryEstimateAsync(Input(), CancellationToken.None));
    }

    [Fact]
    public async Task TryEstimateAsync_WhenNoTextBlock_ReturnsNull()
    {
        const string json = """
        {"stop_reason":"end_turn","content":[
          {"type":"web_search_tool_result","content":[{"url":"https://a/1","title":"A"}]}
        ]}
        """;
        var provider = Create(new FakeHttpHandler(_ => FakeHttpHandler.JsonOk(json)));

        Assert.Null(await provider.TryEstimateAsync(Input(), CancellationToken.None));
    }

    [Fact]
    public async Task TryEstimateAsync_WhenContentMissing_ReturnsNull()
    {
        const string json = """{"stop_reason":"end_turn"}""";
        var provider = Create(new FakeHttpHandler(_ => FakeHttpHandler.JsonOk(json)));

        Assert.Null(await provider.TryEstimateAsync(Input(), CancellationToken.None));
    }

    [Fact]
    public async Task TryEstimateAsync_WhenPauseTurnThenEndTurn_LoopsAndReturnsResult()
    {
        const string pause = """
        {"stop_reason":"pause_turn","content":[{"type":"text","text":"searching"}]}
        """;
        var handler = new FakeHttpHandler((_, call) =>
            FakeHttpHandler.JsonOk(call == 0 ? pause : HappyResponse));
        var provider = Create(handler);

        var dto = await provider.TryEstimateAsync(Input(), CancellationToken.None);

        Assert.NotNull(dto);
        Assert.Equal(2, handler.CallCount);
        Assert.Equal(150m, dto!.EstimatedPrice);
    }

    [Theory]
    [InlineData(200, 100)] // min > max
    [InlineData(-5, 50)]   // negative min
    public async Task TryEstimateAsync_WhenRangeFailsSanityBounds_ReturnsNull(int min, int max)
    {
        var json = $$"""
        {"stop_reason":"end_turn","content":[
          {"type":"web_search_tool_result","content":[{"url":"https://a/1","title":"A"}]},
          {"type":"text","text":"{\"found\":true,\"min\":{{min}},\"max\":{{max}},\"currency\":\"EUR\"}"}
        ]}
        """;
        var provider = Create(new FakeHttpHandler(_ => FakeHttpHandler.JsonOk(json)));

        Assert.Null(await provider.TryEstimateAsync(Input(), CancellationToken.None));
    }

    [Fact]
    public async Task TryEstimateAsync_WhenMinOrMaxMissing_ReturnsNull()
    {
        const string json = """
        {"stop_reason":"end_turn","content":[
          {"type":"web_search_tool_result","content":[{"url":"https://a/1","title":"A"}]},
          {"type":"text","text":"{\"found\":true,\"currency\":\"EUR\"}"}
        ]}
        """;
        var provider = Create(new FakeHttpHandler(_ => FakeHttpHandler.JsonOk(json)));

        Assert.Null(await provider.TryEstimateAsync(Input(), CancellationToken.None));
    }

    [Fact]
    public async Task TryEstimateAsync_WhenMalformedText_ReturnsNull()
    {
        const string json = """
        {"stop_reason":"end_turn","content":[
          {"type":"web_search_tool_result","content":[{"url":"https://a/1","title":"A"}]},
          {"type":"text","text":"Sorry, I could not find reliable pricing."}
        ]}
        """;
        var provider = Create(new FakeHttpHandler(_ => FakeHttpHandler.JsonOk(json)));

        Assert.Null(await provider.TryEstimateAsync(Input(), CancellationToken.None));
    }

    [Fact]
    public async Task TryEstimateAsync_WhenJsonWrappedInProse_ExtractsAndParses()
    {
        const string json = """
        {"stop_reason":"end_turn","content":[
          {"type":"web_search_tool_result","content":[{"url":"https://a/1","title":"A"}]},
          {"type":"text","text":"Here is the result: {\"found\":true,\"min\":10,\"max\":20,\"currency\":\"EUR\"} — hope it helps"}
        ]}
        """;
        var provider = Create(new FakeHttpHandler(_ => FakeHttpHandler.JsonOk(json)));

        var dto = await provider.TryEstimateAsync(Input(), CancellationToken.None);

        Assert.NotNull(dto);
        Assert.Equal(10m, dto!.LowEstimate);
        Assert.Equal(20m, dto.HighEstimate);
    }

    [Fact]
    public async Task TryEstimateAsync_WhenNonSuccessStatus_ReturnsNull()
    {
        var provider = Create(new FakeHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError)));

        Assert.Null(await provider.TryEstimateAsync(Input(), CancellationToken.None));
    }

    [Fact]
    public async Task TryEstimateAsync_WhenCurrencyAndAsOfMissing_DefaultsBaseCurrencyAndConfidence()
    {
        const string json = """
        {"stop_reason":"end_turn","content":[
          {"type":"web_search_tool_result","content":[{"url":"https://a/1","title":"A"}]},
          {"type":"text","text":"{\"found\":true,\"min\":50,\"max\":60,\"confidence\":\"medium\"}"}
        ]}
        """;
        var provider = Create(new FakeHttpHandler(_ => FakeHttpHandler.JsonOk(json)));

        var dto = await provider.TryEstimateAsync(Input(), CancellationToken.None);

        Assert.NotNull(dto);
        Assert.Equal("EUR", dto!.Currency);
        Assert.Equal(PriceConfidence.Medium, dto.Confidence);
    }

    [Fact]
    public async Task TryEstimateAsync_WhenConfidenceUnknown_DefaultsToLow()
    {
        const string json = """
        {"stop_reason":"end_turn","content":[
          {"type":"web_search_tool_result","content":[{"url":"https://a/1","title":"A"}]},
          {"type":"text","text":"{\"found\":true,\"min\":50,\"max\":60,\"currency\":\"EUR\",\"confidence\":\"frobnicate\"}"}
        ]}
        """;
        var provider = Create(new FakeHttpHandler(_ => FakeHttpHandler.JsonOk(json)));

        var dto = await provider.TryEstimateAsync(Input(), CancellationToken.None);

        Assert.NotNull(dto);
        Assert.Equal(PriceConfidence.Low, dto!.Confidence);
    }

    [Fact]
    public async Task TryEstimateAsync_WhenCitationsOnTextBlock_CollectsThemAndDedupes()
    {
        // Citations attached to the text block, with a duplicate url and an empty-url entry.
        const string json = """
        {"stop_reason":"end_turn","content":[
          {"type":"text","text":"{\"found\":true,\"min\":100,\"max\":200,\"currency\":\"EUR\"}",
           "citations":[{"url":"https://dup/1","title":"One"},{"url":"https://dup/1","title":"One again"},{"url":"","title":"empty"}]}
        ]}
        """;
        var provider = Create(new FakeHttpHandler(_ => FakeHttpHandler.JsonOk(json)));

        var dto = await provider.TryEstimateAsync(Input(), CancellationToken.None);

        Assert.NotNull(dto);
        Assert.Single(dto!.Sources);
        Assert.Equal("https://dup/1", dto.Sources[0].Url);
    }

    [Fact]
    public async Task TryEstimateAsync_WhenForeignCurrency_ConvertsToBase()
    {
        const string json = """
        {"stop_reason":"end_turn","content":[
          {"type":"web_search_tool_result","content":[{"url":"https://a/1","title":"A"}]},
          {"type":"text","text":"{\"found\":true,\"min\":100,\"max\":100,\"currency\":\"USD\",\"confidence\":\"high\"}"}
        ]}
        """;
        var provider = Create(new FakeHttpHandler(_ => FakeHttpHandler.JsonOk(json)));

        var dto = await provider.TryEstimateAsync(Input(), CancellationToken.None);

        Assert.NotNull(dto);
        Assert.Equal(92m, dto!.EstimatedPrice); // 100 USD * 0.92
    }

    [Fact]
    public async Task TryEstimateAsync_WithAllowedDomains_StillReturnsResult()
    {
        var provider = Create(new FakeHttpHandler(_ => FakeHttpHandler.JsonOk(HappyResponse)),
            allowedDomains: ["whisky.example"]);

        Assert.NotNull(await provider.TryEstimateAsync(Input(), CancellationToken.None));
    }

    [Fact]
    public async Task TryEstimateAsync_WithBlockedDomainsAndNoMaxUses_StillReturnsResult()
    {
        var provider = Create(new FakeHttpHandler(_ => FakeHttpHandler.JsonOk(HappyResponse)),
            blockedDomains: ["spam.example"], maxSearches: 0);

        Assert.NotNull(await provider.TryEstimateAsync(Input(), CancellationToken.None));
    }

    [Fact]
    public async Task TryEstimateAsync_WhenInputHasVintageAndBarcode_StillReturnsResult()
    {
        var input = new PriceProviderInput("Port Ellen", "Port Ellen", SpiritCategory.Whisky, null, 1983, 700, "5012345678900",
            ProductKey.For("Port Ellen", "Port Ellen", SpiritCategory.Whisky, null, 1983, 700));
        var provider = Create(new FakeHttpHandler(_ => FakeHttpHandler.JsonOk(HappyResponse)));

        Assert.NotNull(await provider.TryEstimateAsync(input, CancellationToken.None));
    }

    [Fact]
    public async Task TryEstimateAsync_WhenContentHasNonObjectAndDefensiveBlocks_ParsesRobustly()
    {
        // Exercises the defensive guards: a bare (non-object) element, an object with no "type", a text
        // block with no "text", a tool-result with a non-object entry, an entry with no url, and an entry
        // with a url but no title (which falls back to the url as the title).
        const string json = """
        {"stop_reason":"end_turn","content":[
          "a bare string element",
          {"note":"object without a type"},
          {"type":"text"},
          {"type":"web_search_tool_result","content":[42,{"title":"no url key"},{"url":"https://b/2"},{"url":"https://a/1","title":"A"}]},
          {"type":"text","text":"{\"found\":true,\"min\":100,\"max\":200,\"currency\":\"EUR\",\"confidence\":\"high\"}"}
        ]}
        """;
        var provider = Create(new FakeHttpHandler(_ => FakeHttpHandler.JsonOk(json)));

        var dto = await provider.TryEstimateAsync(Input(), CancellationToken.None);

        Assert.NotNull(dto);
        Assert.Equal(2, dto!.Sources.Count); // https://b/2 (title = url) and https://a/1
        Assert.Equal("https://b/2", dto.Sources[0].Title);
    }

    [Fact]
    public async Task TryEstimateAsync_WhenProseContainsInvalidBraces_ReturnsNull()
    {
        const string json = """
        {"stop_reason":"end_turn","content":[
          {"type":"web_search_tool_result","content":[{"url":"https://a/1","title":"A"}]},
          {"type":"text","text":"Here you go: { not valid json at all } cheers"}
        ]}
        """;
        var provider = Create(new FakeHttpHandler(_ => FakeHttpHandler.JsonOk(json)));

        Assert.Null(await provider.TryEstimateAsync(Input(), CancellationToken.None));
    }
}
