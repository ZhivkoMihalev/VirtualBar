using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using VirtualBar.HttpTests.Support;

namespace VirtualBar.HttpTests.Flows;

/// <summary>
/// A sale through an offer, end to end over HTTP with two tokens (#68). The seller lists a bottle, the buyer makes an
/// offer, the seller accepts. Each step has to reach the other collector as a notification, and the sale has to earn
/// each side their badge: the chain trigger → notification → badge across the whole app, which
/// <c>VirtualBar.Tests/Integration</c> proves at service level.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class MarketplaceFlowTests(ApiHosts hosts)
{
    private ApiFactory Api => hosts.ConfirmationOff;

    [SqlServerFact]
    public async Task AcceptOffer_WhenTheSellerAcceptsAnOfferOnAListedBottle_NotifiesBothAndAwardsFirstSaleAndFirstPurchase()
    {
        var seller = await Collector.RegisterAsync(Api, "seller");
        var buyer = await Collector.RegisterAsync(Api, "buyer");
        var bottleId = await seller.AddBottleAsync();
        await seller.ListForSaleAsync(bottleId);

        var offerId = await CollectorApi.IdAsync(await buyer.Client.PostAsJsonAsync(
            "/api/offers", new { bottleId, offeredPrice = 100m, currency = "EUR", message = "Would you take 100?" }));

        AssertAbout(Assert.Single(await seller.NotificationsAsync("OfferReceived")), offerId, actor: buyer);

        var accept = await seller.Client.PatchAsync($"/api/offers/{offerId}/accept", content: null);

        Assert.Equal(HttpStatusCode.OK, accept.StatusCode);
        Assert.Equal("Accepted", (await accept.JsonAsync()).GetProperty("status").GetString());
        AssertAbout(Assert.Single(await buyer.NotificationsAsync("OfferAccepted")), offerId, actor: seller);

        // Each side earns their own badge, and only theirs.
        Assert.Contains(await seller.NotificationsAsync("BadgeEarned"), IsBadge("FirstSale"));
        Assert.Contains(await buyer.NotificationsAsync("BadgeEarned"), IsBadge("FirstPurchase"));
        var sellerBadges = await seller.EarnedBadgesAsync();
        var buyerBadges = await buyer.EarnedBadgesAsync();
        Assert.True(sellerBadges["FirstSale"]);
        Assert.False(sellerBadges["FirstPurchase"]);
        Assert.True(buyerBadges["FirstPurchase"]);
        Assert.False(buyerBadges["FirstSale"]);

        // The buyer's own list of sent offers shows the outcome too.
        var sent = await buyer.Client.GetAsync("/api/offers/sent");
        await CollectorApi.AssertSucceededAsync(sent);
        Assert.Equal(
            "Accepted",
            (await sent.JsonAsync()).EnumerateArray()
                .Single(offer => offer.GetProperty("id").GetGuid() == offerId)
                .GetProperty("status").GetString());
    }

    private static void AssertAbout(JsonElement notification, Guid offerId, Collector actor)
    {
        Assert.Equal(offerId, notification.GetProperty("resourceId").GetGuid());
        Assert.Equal(actor.Id, notification.GetProperty("actorId").GetGuid());
    }

    private static Predicate<JsonElement> IsBadge(string badge) =>
        notification => notification.GetProperty("resourceName").GetString() == badge;
}
