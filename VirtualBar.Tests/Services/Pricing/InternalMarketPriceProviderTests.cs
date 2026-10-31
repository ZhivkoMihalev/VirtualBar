using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using VirtualBar.Application.Common;
using VirtualBar.Application.DTOs.Pricing;
using VirtualBar.Application.Options;
using VirtualBar.Domain.Entities;
using VirtualBar.Domain.Enums;
using VirtualBar.Infrastructure.Persistence;
using VirtualBar.Infrastructure.Services.Pricing;

namespace VirtualBar.Tests.Services.Pricing;

public sealed class InternalMarketPriceProviderTests
{
    private const string MatchKey = "whisky|macallan 18|18yo|700";

    private static AppDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static InternalMarketPriceProvider Create(AppDbContext db, bool enabled = true, int minSamples = 3, int minApprox = 2)
    {
        var internalOpts = Options.Create(new InternalProviderOptions
        {
            UseProviderStats = enabled,
            MinSamples = minSamples,
            MinApproxSamples = minApprox,
        });
        var pricing = Options.Create(new PricingOptions
        {
            BaseCurrency = "EUR",
            FxToBase = new Dictionary<string, decimal> { ["EUR"] = 1m, ["USD"] = 0.92m, ["GBP"] = 1.17m },
            SnapshotTtlDays = 5,
        });
        return new InternalMarketPriceProvider(db, internalOpts, pricing, NullLogger<InternalMarketPriceProvider>.Instance);
    }

    private static PriceProviderInput MatchingInput() =>
        new("Macallan 18", null, SpiritCategory.Whisky, 18, null, 700, null, MatchKey);

    private static Bottle SeedBottle(
        AppDbContext db,
        string name = "Macallan 18",
        SpiritCategory category = SpiritCategory.Whisky,
        int? age = 18,
        int? volumeMl = 700,
        decimal? askingPrice = null,
        string? currency = null,
        bool isDeleted = false)
    {
        var bottle = new Bottle
        {
            UserId = Guid.NewGuid(),
            Name = name,
            Category = category,
            Age = age,
            VolumeMl = volumeMl,
            Condition = BottleCondition.Sealed,
            AskingPrice = askingPrice,
            Currency = currency,
            IsDeleted = isDeleted,
        };
        db.Bottles.Add(bottle);
        db.SaveChanges();
        return bottle;
    }

    private static void SeedAcceptedOffer(AppDbContext db, Guid bottleId, decimal price, string currency, OfferStatus status = OfferStatus.Accepted)
    {
        db.Offers.Add(new Offer
        {
            BottleId = bottleId,
            BuyerId = Guid.NewGuid(),
            SellerId = Guid.NewGuid(),
            OfferedPrice = price,
            Currency = currency,
            Status = status,
        });
        db.SaveChanges();
    }

    private const string ProductCanonicalKey = "whisky|macallan|sherry oak|18yo|700";

    private static Product SeedProduct(AppDbContext db, bool isDeleted = false)
    {
        var product = new Product
        {
            Name = "Sherry Oak 18",
            Brand = "Macallan",
            Category = SpiritCategory.Whisky,
            Age = 18,
            VolumeMl = 700,
            CanonicalKey = ProductCanonicalKey,
            Origin = ProductOrigin.Seeded,
            IsDeleted = isDeleted,
        };
        db.Products.Add(product);
        db.SaveChanges();
        return product;
    }

    private static Bottle SeedLinkedBottle(AppDbContext db, Guid productId, string name, int? vintageYear, decimal? askingPrice = null)
    {
        var bottle = SeedBottle(db, name: name, askingPrice: askingPrice, currency: askingPrice is null ? null : "EUR");
        bottle.VintageYear = vintageYear;
        bottle.ProductId = productId;
        db.SaveChanges();
        return bottle;
    }

    private static PriceProviderInput ProductInput() =>
        new("Sherry Oak 18", "Macallan", SpiritCategory.Whisky, 18, null, 700, null, ProductCanonicalKey);

    [Fact]
    public async Task TryEstimateAsync_WhenListingsOnLinkedBottles_MatchTheProductKeyWhateverTheVintage()
    {
        await using var db = CreateDbContext();
        var product = SeedProduct(db);
        SeedLinkedBottle(db, product.Id, "Mac 18 1998", vintageYear: 1998, askingPrice: 100m);
        SeedLinkedBottle(db, product.Id, "Macallan Sherry", vintageYear: null, askingPrice: 200m);
        var provider = Create(db, minSamples: 2);

        var estimate = await provider.TryEstimateAsync(ProductInput(), CancellationToken.None);

        Assert.NotNull(estimate);
        Assert.Equal(2, estimate!.SampleSize);
    }

    [Fact]
    public async Task TryEstimateAsync_WhenAcceptedOfferOnLinkedBottle_MatchesTheProductKey()
    {
        await using var db = CreateDbContext();
        var product = SeedProduct(db);
        var bottle = SeedLinkedBottle(db, product.Id, "Mac 18 2001", vintageYear: 2001);
        SeedAcceptedOffer(db, bottle.Id, 300m, "EUR");
        var provider = Create(db);

        var estimate = await provider.TryEstimateAsync(ProductInput(), CancellationToken.None);

        Assert.NotNull(estimate);
        Assert.Equal(1, estimate!.SampleSize);
    }

    [Fact]
    public async Task TryEstimateAsync_WhenLinkedProductDeleted_BottleMatchesByItsOwnKey()
    {
        await using var db = CreateDbContext();
        var product = SeedProduct(db, isDeleted: true);
        SeedLinkedBottle(db, product.Id, "Macallan 18", vintageYear: null, askingPrice: 100m);
        var provider = Create(db);

        Assert.Null(await provider.TryEstimateAsync(ProductInput(), CancellationToken.None));
        Assert.NotNull(await provider.TryEstimateAsync(MatchingInput(), CancellationToken.None));
    }

    [Fact]
    public async Task TryEstimateAsync_WhenDisabled_ReturnsNull()
    {
        await using var db = CreateDbContext();
        SeedBottle(db, askingPrice: 100m, currency: "EUR");
        var provider = Create(db, enabled: false);

        Assert.Null(await provider.TryEstimateAsync(MatchingInput(), CancellationToken.None));
    }

    [Fact]
    public async Task TryEstimateAsync_WhenNoMatchingData_ReturnsNull()
    {
        await using var db = CreateDbContext();
        SeedBottle(db, name: "Something Else", askingPrice: 100m, currency: "EUR");
        var provider = Create(db);

        Assert.Null(await provider.TryEstimateAsync(MatchingInput(), CancellationToken.None));
    }

    [Fact]
    public async Task TryEstimateAsync_WhenThreeMatchingListings_HighConfidence()
    {
        await using var db = CreateDbContext();
        SeedBottle(db, askingPrice: 100m, currency: "EUR");
        SeedBottle(db, askingPrice: 150m, currency: "EUR");
        SeedBottle(db, askingPrice: 200m, currency: "EUR");
        var provider = Create(db);

        var dto = await provider.TryEstimateAsync(MatchingInput(), CancellationToken.None);

        Assert.NotNull(dto);
        Assert.Equal(PriceSource.Internal, dto!.Source);
        Assert.Equal(PriceConfidence.High, dto.Confidence);
        Assert.Equal(3, dto.SampleSize);
    }

    [Fact]
    public async Task TryEstimateAsync_WhenTwoSamples_MediumConfidence()
    {
        await using var db = CreateDbContext();
        SeedBottle(db, askingPrice: 100m, currency: "EUR");
        SeedBottle(db, askingPrice: 200m, currency: "EUR");
        var provider = Create(db);

        var dto = await provider.TryEstimateAsync(MatchingInput(), CancellationToken.None);

        Assert.NotNull(dto);
        Assert.Equal(PriceConfidence.Medium, dto!.Confidence);
        Assert.Equal(2, dto.SampleSize);
    }

    [Fact]
    public async Task TryEstimateAsync_WhenSingleSample_LowConfidence()
    {
        await using var db = CreateDbContext();
        SeedBottle(db, askingPrice: 120m, currency: "EUR");
        var provider = Create(db);

        var dto = await provider.TryEstimateAsync(MatchingInput(), CancellationToken.None);

        Assert.NotNull(dto);
        Assert.Equal(PriceConfidence.Low, dto!.Confidence);
        Assert.Equal(1, dto.SampleSize);
        Assert.Equal(120m, dto.EstimatedPrice);
    }

    [Fact]
    public async Task TryEstimateAsync_IncludesAcceptedOffersAndExcludesOthers()
    {
        await using var db = CreateDbContext();
        SeedBottle(db, askingPrice: 100m, currency: "EUR");                       // listing → 1 point

        var offerBottle = SeedBottle(db);                                          // matching, not a listing
        SeedAcceptedOffer(db, offerBottle.Id, 140m, "EUR");                        // accepted → 1 point

        var pendingBottle = SeedBottle(db);
        SeedAcceptedOffer(db, pendingBottle.Id, 999m, "EUR", OfferStatus.Pending); // excluded (not accepted)

        var deletedBottle = SeedBottle(db, isDeleted: true);
        SeedAcceptedOffer(db, deletedBottle.Id, 999m, "EUR");                       // excluded (bottle deleted)

        var provider = Create(db);

        var dto = await provider.TryEstimateAsync(MatchingInput(), CancellationToken.None);

        Assert.NotNull(dto);
        Assert.Equal(2, dto!.SampleSize); // one listing + one accepted offer
    }

    [Fact]
    public async Task TryEstimateAsync_ExcludesSoftDeletedListings()
    {
        await using var db = CreateDbContext();
        SeedBottle(db, askingPrice: 100m, currency: "EUR");
        SeedBottle(db, askingPrice: 200m, currency: "EUR", isDeleted: true); // excluded
        var provider = Create(db);

        var dto = await provider.TryEstimateAsync(MatchingInput(), CancellationToken.None);

        Assert.NotNull(dto);
        Assert.Equal(1, dto!.SampleSize);
    }

    [Fact]
    public async Task TryEstimateAsync_ExcludesNonMatchingListings()
    {
        await using var db = CreateDbContext();
        SeedBottle(db, askingPrice: 100m, currency: "EUR");                       // matches
        SeedBottle(db, name: "Glenfiddich 12", age: 12, askingPrice: 50m, currency: "EUR"); // different key
        var provider = Create(db);

        var dto = await provider.TryEstimateAsync(MatchingInput(), CancellationToken.None);

        Assert.NotNull(dto);
        Assert.Equal(1, dto!.SampleSize);
    }

    [Fact]
    public async Task TryEstimateAsync_ConvertsCurrencyMix()
    {
        await using var db = CreateDbContext();
        SeedBottle(db, askingPrice: 100m, currency: "EUR"); // 100
        SeedBottle(db, askingPrice: 100m, currency: "USD"); // 92
        var provider = Create(db);

        var dto = await provider.TryEstimateAsync(MatchingInput(), CancellationToken.None);

        Assert.NotNull(dto);
        Assert.Equal(2, dto!.SampleSize);
        Assert.True(dto.LowEstimate <= dto.EstimatedPrice);
        Assert.True(dto.EstimatedPrice <= dto.HighEstimate);
        Assert.InRange(dto.EstimatedPrice, 92m, 100m);
    }
}
