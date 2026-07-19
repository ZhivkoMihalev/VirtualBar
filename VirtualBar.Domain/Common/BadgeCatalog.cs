using VirtualBar.Domain.Enums;

namespace VirtualBar.Domain.Common;

public static class BadgeCatalog
{
    public static readonly IReadOnlyList<BadgeDefinition> All =
    [
        new(BadgeType.FirstBottle, BadgeTrigger.BottleAdded, BadgeCountKind.Bottles, 1),
        new(BadgeType.Collector5, BadgeTrigger.BottleAdded, BadgeCountKind.Bottles, 5),
        new(BadgeType.Collector10, BadgeTrigger.BottleAdded, BadgeCountKind.Bottles, 10),
        new(BadgeType.Collector25, BadgeTrigger.BottleAdded, BadgeCountKind.Bottles, 25),
        new(BadgeType.Collector50, BadgeTrigger.BottleAdded, BadgeCountKind.Bottles, 50),
        new(BadgeType.Collector100, BadgeTrigger.BottleAdded, BadgeCountKind.Bottles, 100),
        new(BadgeType.Explorer3, BadgeTrigger.BottleAdded, BadgeCountKind.Categories, 3),
        new(BadgeType.Explorer5, BadgeTrigger.BottleAdded, BadgeCountKind.Categories, 5),
        new(BadgeType.LimitedHunter, BadgeTrigger.BottleAdded, BadgeCountKind.LimitedBottles, 5),
        new(BadgeType.Liked10, BadgeTrigger.LikeReceived, BadgeCountKind.LikesReceived, 10),
        new(BadgeType.Liked50, BadgeTrigger.LikeReceived, BadgeCountKind.LikesReceived, 50),
        new(BadgeType.Liked100, BadgeTrigger.LikeReceived, BadgeCountKind.LikesReceived, 100),
        new(BadgeType.FirstFollower, BadgeTrigger.FollowerGained, BadgeCountKind.Followers, 1),
        new(BadgeType.Popular10, BadgeTrigger.FollowerGained, BadgeCountKind.Followers, 10),
        new(BadgeType.Influencer50, BadgeTrigger.FollowerGained, BadgeCountKind.Followers, 50),
        new(BadgeType.FirstListing, BadgeTrigger.BottleListed, BadgeCountKind.ActiveListings, 1),
        new(BadgeType.FirstSale, BadgeTrigger.OfferAccepted, BadgeCountKind.SalesAccepted, 1),
        new(BadgeType.FirstPurchase, BadgeTrigger.OfferAccepted, BadgeCountKind.PurchasesAccepted, 1),
    ];

    public static IReadOnlyList<BadgeDefinition> ForTrigger(BadgeTrigger trigger) =>
        All.Where(d => d.Trigger == trigger).ToList();
}
