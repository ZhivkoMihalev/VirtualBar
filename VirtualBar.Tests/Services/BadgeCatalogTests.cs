using VirtualBar.Domain.Common;
using VirtualBar.Domain.Enums;

namespace VirtualBar.Tests.Services;

public sealed class BadgeCatalogTests
{
    [Fact]
    public void All_ContainsEveryBadgeTypeExactlyOnce()
    {
        var allTypes = Enum.GetValues<BadgeType>();

        Assert.Equal(allTypes.Length, BadgeCatalog.All.Count);
        Assert.Equal(allTypes.Length, BadgeCatalog.All.Select(d => d.Type).Distinct().Count());
        foreach (var type in allTypes)
            Assert.Single(BadgeCatalog.All, d => d.Type == type);
    }

    [Fact]
    public void All_HasEighteenDefinitions()
    {
        Assert.Equal(18, BadgeCatalog.All.Count);
    }

    [Fact]
    public void All_EveryThresholdIsAtLeastOne()
    {
        Assert.All(BadgeCatalog.All, d => Assert.True(d.Threshold >= 1, $"{d.Type} has threshold {d.Threshold}"));
    }

    [Fact]
    public void ForTrigger_BottleAdded_ReturnsNineBottleCategoryAndLimitedDefinitions()
    {
        var defs = BadgeCatalog.ForTrigger(BadgeTrigger.BottleAdded);

        Assert.Equal(9, defs.Count);
        Assert.All(defs, d => Assert.Equal(BadgeTrigger.BottleAdded, d.Trigger));
        Assert.Equal(6, defs.Count(d => d.CountKind == BadgeCountKind.Bottles));
        Assert.Equal(2, defs.Count(d => d.CountKind == BadgeCountKind.Categories));
        Assert.Equal(1, defs.Count(d => d.CountKind == BadgeCountKind.LimitedBottles));
        Assert.Contains(defs, d => d.Type == BadgeType.FirstBottle);
        Assert.Contains(defs, d => d.Type == BadgeType.Collector100);
        Assert.Contains(defs, d => d.Type == BadgeType.Explorer5);
        Assert.Contains(defs, d => d.Type == BadgeType.LimitedHunter);
    }

    [Fact]
    public void ForTrigger_LikeReceived_ReturnsThreeLikeDefinitions()
    {
        var defs = BadgeCatalog.ForTrigger(BadgeTrigger.LikeReceived);

        Assert.Equal(3, defs.Count);
        Assert.All(defs, d =>
        {
            Assert.Equal(BadgeTrigger.LikeReceived, d.Trigger);
            Assert.Equal(BadgeCountKind.LikesReceived, d.CountKind);
        });
    }

    [Fact]
    public void ForTrigger_FollowerGained_ReturnsThreeFollowerDefinitions()
    {
        var defs = BadgeCatalog.ForTrigger(BadgeTrigger.FollowerGained);

        Assert.Equal(3, defs.Count);
        Assert.All(defs, d => Assert.Equal(BadgeCountKind.Followers, d.CountKind));
        Assert.Contains(defs, d => d.Type == BadgeType.FirstFollower);
        Assert.Contains(defs, d => d.Type == BadgeType.Popular10);
        Assert.Contains(defs, d => d.Type == BadgeType.Influencer50);
    }

    [Fact]
    public void ForTrigger_BottleListed_ReturnsFirstListingOnly()
    {
        var defs = BadgeCatalog.ForTrigger(BadgeTrigger.BottleListed);

        Assert.Single(defs);
        Assert.Equal(BadgeType.FirstListing, defs[0].Type);
        Assert.Equal(BadgeCountKind.ActiveListings, defs[0].CountKind);
        Assert.Equal(1, defs[0].Threshold);
    }

    [Fact]
    public void ForTrigger_OfferAccepted_ReturnsFirstSaleAndFirstPurchase()
    {
        var defs = BadgeCatalog.ForTrigger(BadgeTrigger.OfferAccepted);

        Assert.Equal(2, defs.Count);
        Assert.Contains(defs, d => d.Type == BadgeType.FirstSale && d.CountKind == BadgeCountKind.SalesAccepted);
        Assert.Contains(defs, d => d.Type == BadgeType.FirstPurchase && d.CountKind == BadgeCountKind.PurchasesAccepted);
    }

    [Fact]
    public void ForTrigger_EveryTrigger_ReturnsNonEmpty()
    {
        foreach (var trigger in Enum.GetValues<BadgeTrigger>())
            Assert.NotEmpty(BadgeCatalog.ForTrigger(trigger));
    }

    [Fact]
    public void ForTrigger_UnionOfAllTriggers_CoversAllEighteenDefinitionsExactlyOnce()
    {
        var union = Enum.GetValues<BadgeTrigger>()
            .SelectMany(BadgeCatalog.ForTrigger)
            .ToList();

        Assert.Equal(18, union.Count);
        Assert.Equal(18, union.Select(d => d.Type).Distinct().Count());
        Assert.Equal(
            BadgeCatalog.All.Select(d => d.Type).OrderBy(t => t),
            union.Select(d => d.Type).OrderBy(t => t));
    }
}
