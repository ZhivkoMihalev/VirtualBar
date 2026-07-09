using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using VirtualBar.Application.DTOs.Pricing;
using VirtualBar.Application.Options;
using VirtualBar.Domain.Enums;
using VirtualBar.Infrastructure.Services.Pricing;

namespace VirtualBar.Tests.Services.Pricing;

public sealed class PriceProviderBaseTests
{
    private static IOptions<PricingOptions> Pricing(string baseCurrency = "EUR") =>
        Options.Create(new PricingOptions
        {
            BaseCurrency = baseCurrency,
            FxToBase = new Dictionary<string, decimal> { ["EUR"] = 1m, ["USD"] = 0.92m, ["GBP"] = 1.17m },
            SnapshotTtlDays = 5,
        });

    private static PriceProviderInput Input(SpiritCategory category = SpiritCategory.Whisky) =>
        new("Name", "Distillery", category, null, null, null, null, "key");

    private static ProviderRawResult Raw(params PricePoint[] points) =>
        new(points, PriceConfidence.High, new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc), []);

    [Fact]
    public async Task TryEstimateAsync_WhenDisabled_ReturnsNull()
    {
        var provider = new TestProvider(Pricing(), _ => Task.FromResult<ProviderRawResult?>(Raw(new PricePoint(100m, "EUR"))), enabled: false);

        Assert.Null(await provider.TryEstimateAsync(Input(), CancellationToken.None));
    }

    [Fact]
    public async Task TryEstimateAsync_WhenCategoryUnsupported_ReturnsNull()
    {
        var provider = new TestProvider(Pricing(), _ => Task.FromResult<ProviderRawResult?>(Raw(new PricePoint(100m, "EUR"))),
            supports: _ => false);

        Assert.Null(await provider.TryEstimateAsync(Input(), CancellationToken.None));
    }

    [Fact]
    public async Task TryEstimateAsync_WhenFetchNull_ReturnsNull()
    {
        var provider = new TestProvider(Pricing(), _ => Task.FromResult<ProviderRawResult?>(null));

        Assert.Null(await provider.TryEstimateAsync(Input(), CancellationToken.None));
    }

    [Fact]
    public async Task TryEstimateAsync_WhenFetchReturnsNoPoints_ReturnsNull()
    {
        var provider = new TestProvider(Pricing(), _ => Task.FromResult<ProviderRawResult?>(Raw()));

        Assert.Null(await provider.TryEstimateAsync(Input(), CancellationToken.None));
    }

    [Fact]
    public async Task TryEstimateAsync_WhenFetchThrows_SwallowsAndReturnsNull()
    {
        var provider = new TestProvider(Pricing(), _ => throw new InvalidOperationException("boom"));

        Assert.Null(await provider.TryEstimateAsync(Input(), CancellationToken.None));
    }

    [Fact]
    public async Task TryEstimateAsync_WhenFetchThrowsCancellation_Rethrows()
    {
        var provider = new TestProvider(Pricing(), _ => throw new OperationCanceledException());

        await Assert.ThrowsAsync<OperationCanceledException>(() => provider.TryEstimateAsync(Input(), CancellationToken.None));
    }

    [Fact]
    public async Task TryEstimateAsync_ConvertsForeignCurrencyToBase()
    {
        var provider = new TestProvider(Pricing("EUR"), _ => Task.FromResult<ProviderRawResult?>(Raw(new PricePoint(100m, "USD"))));

        var dto = await provider.TryEstimateAsync(Input(), CancellationToken.None);

        Assert.NotNull(dto);
        Assert.Equal(92m, dto!.EstimatedPrice); // 100 USD * 0.92
        Assert.Equal("EUR", dto.Currency);
        Assert.Equal(1, dto.SampleSize);
    }

    [Fact]
    public async Task TryEstimateAsync_DropsUnknownCurrencyPoints()
    {
        // JPY is absent from the FX table and is not the base currency → the point is dropped.
        var provider = new TestProvider(Pricing("EUR"),
            _ => Task.FromResult<ProviderRawResult?>(Raw(new PricePoint(100m, "EUR"), new PricePoint(9999m, "JPY"))));

        var dto = await provider.TryEstimateAsync(Input(), CancellationToken.None);

        Assert.NotNull(dto);
        Assert.Equal(1, dto!.SampleSize);
        Assert.Equal(100m, dto.EstimatedPrice);
    }

    [Fact]
    public async Task TryEstimateAsync_WhenAllPointsUnconvertible_ReturnsNull()
    {
        var provider = new TestProvider(Pricing("EUR"),
            _ => Task.FromResult<ProviderRawResult?>(Raw(new PricePoint(100m, "JPY"))));

        Assert.Null(await provider.TryEstimateAsync(Input(), CancellationToken.None));
    }

    [Fact]
    public async Task TryEstimateAsync_DropsNonPositiveAndOutOfRangePoints()
    {
        var provider = new TestProvider(Pricing("EUR"), _ => Task.FromResult<ProviderRawResult?>(
            Raw(new PricePoint(0m, "EUR"), new PricePoint(2_000_000m, "EUR"), new PricePoint(150m, "EUR"))));

        var dto = await provider.TryEstimateAsync(Input(), CancellationToken.None);

        Assert.NotNull(dto);
        Assert.Equal(1, dto!.SampleSize);
        Assert.Equal(150m, dto.EstimatedPrice);
    }

    [Fact]
    public async Task TryEstimateAsync_AppliesPercentileBounds()
    {
        var provider = new TestProvider(Pricing("EUR"),
            _ => Task.FromResult<ProviderRawResult?>(Raw(
                new PricePoint(100m, "EUR"), new PricePoint(200m, "EUR"),
                new PricePoint(300m, "EUR"), new PricePoint(400m, "EUR"))),
            bounds: (25d, 75d));

        var dto = await provider.TryEstimateAsync(Input(), CancellationToken.None);

        Assert.NotNull(dto);
        Assert.Equal(250m, dto!.EstimatedPrice); // p50
        Assert.Equal(175m, dto.LowEstimate);     // p25
        Assert.Equal(325m, dto.HighEstimate);    // p75
        Assert.Equal(4, dto.SampleSize);
    }

    [Fact]
    public async Task TryEstimateAsync_WhenSinglePoint_PercentileReturnsThatValue()
    {
        var provider = new TestProvider(Pricing("EUR"), _ => Task.FromResult<ProviderRawResult?>(Raw(new PricePoint(123m, "EUR"))));

        var dto = await provider.TryEstimateAsync(Input(), CancellationToken.None);

        Assert.NotNull(dto);
        Assert.Equal(123m, dto!.EstimatedPrice);
        Assert.Equal(123m, dto.LowEstimate);
        Assert.Equal(123m, dto.HighEstimate);
    }

    [Fact]
    public async Task TryEstimateAsync_PassesThroughSourcesConfidenceAndSource()
    {
        var citation = new PriceCitation("https://a.example/1", "Auction A");
        var raw = new ProviderRawResult(
            [new PricePoint(100m, "EUR")],
            PriceConfidence.Medium,
            new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            [citation]);
        var provider = new TestProvider(Pricing("EUR"), _ => Task.FromResult<ProviderRawResult?>(raw), source: PriceSource.ClaudeResearch);

        var dto = await provider.TryEstimateAsync(Input(), CancellationToken.None);

        Assert.NotNull(dto);
        Assert.Equal(PriceSource.ClaudeResearch, dto!.Source);
        Assert.Equal(PriceConfidence.Medium, dto.Confidence);
        Assert.Single(dto.Sources);
        Assert.Equal("https://a.example/1", dto.Sources[0].Url);
    }

    [Fact]
    public async Task TryEstimateAsync_WhenWhitespaceCurrency_TreatsAsBaseCurrency()
    {
        var provider = new TestProvider(Pricing("EUR"), _ => Task.FromResult<ProviderRawResult?>(Raw(new PricePoint(80m, "   "))));

        var dto = await provider.TryEstimateAsync(Input(), CancellationToken.None);

        Assert.NotNull(dto);
        Assert.Equal(80m, dto!.EstimatedPrice);
    }

    private sealed class TestProvider(
        IOptions<PricingOptions> pricing,
        Func<PriceProviderInput, Task<ProviderRawResult?>> fetch,
        bool enabled = true,
        (double Low, double High)? bounds = null,
        PriceSource source = PriceSource.Internal,
        Func<SpiritCategory, bool>? supports = null)
        : PriceProviderBase(pricing, NullLogger.Instance)
    {
        public override PriceSource Source => source;

        protected override bool Enabled => enabled;

        protected override (double Low, double High) PercentileBounds => bounds ?? base.PercentileBounds;

        public override bool Supports(SpiritCategory category) => supports?.Invoke(category) ?? true;

        protected override Task<ProviderRawResult?> FetchAsync(PriceProviderInput input, CancellationToken cancellationToken) =>
            fetch(input);
    }
}
