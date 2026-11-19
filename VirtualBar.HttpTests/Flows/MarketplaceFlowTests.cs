using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using VirtualBar.HttpTests.Support;

namespace VirtualBar.HttpTests.Flows;

/// <summary>
/// A sale through an offer, end to end over HTTP with two tokens (#68, #130). The seller lists a bottle, the buyer
/// makes an offer, the seller accepts and the bottle is reserved, then the buyer confirms receiving it, or one side
/// cancels. Each step has to reach the other collector as a notification, the bottle has to move from one bar to the
/// other, and the completed deal has to earn each side their badge: the chain trigger → notification → badge across
/// the whole app, which <c>VirtualBar.Tests/Integration</c> proves at service level.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class MarketplaceFlowTests(ApiHosts hosts)
{
    private ApiFactory Api => hosts.ConfirmationOff;

    [SqlServerFact]
    public async Task ConfirmReceipt_WhenTheBuyerConfirmsAnAcceptedOffer_MovesTheBottleAndAwardsFirstSaleAndFirstPurchase()
    {
        var seller = await Collector.RegisterAsync(Api, "seller");
        var buyer = await Collector.RegisterAsync(Api, "buyer");
        var bottleId = await seller.AddBottleAsync();
        await seller.ListForSaleAsync(bottleId);

        var offerId = await CollectorApi.IdAsync(await buyer.Client.PostAsJsonAsync(
            "/api/offers", new { bottleId, offeredPrice = 100m, currency = "EUR", message = "Would you take 100?" }));

        AssertAbout(Assert.Single(await seller.NotificationsAsync("OfferReceived")), offerId, actor: buyer);

        // 1. The seller accepts: the deal awaits the buyer, and the bottle is reserved and off sale.
        var accept = await seller.Client.PatchAsync($"/api/offers/{offerId}/accept", content: null);

        Assert.Equal(HttpStatusCode.OK, accept.StatusCode);
        Assert.Equal("AwaitingReceipt", (await accept.JsonAsync()).GetProperty("status").GetString());
        AssertAbout(Assert.Single(await buyer.NotificationsAsync("OfferAccepted")), offerId, actor: seller);
        Assert.Empty(await buyer.NotificationsAsync("BadgeEarned"));

        var reserved = await (await seller.Client.GetAsync($"/api/bottles/{bottleId}")).JsonAsync();
        Assert.False(reserved.GetProperty("isForSale").GetBoolean());
        Assert.Equal(1, reserved.GetProperty("reservedQuantity").GetInt32());
        Assert.Equal("AwaitingReceipt", await SentStatusAsync(buyer, offerId));

        // 2. The buyer confirms receiving it: the deal is complete.
        var confirm = await buyer.Client.PatchAsync($"/api/offers/{offerId}/confirm-receipt", content: null);

        Assert.Equal(HttpStatusCode.OK, confirm.StatusCode);
        var completed = await confirm.JsonAsync();
        Assert.Equal("Accepted", completed.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.String, completed.GetProperty("completedAt").ValueKind);
        AssertAbout(Assert.Single(await seller.NotificationsAsync("SaleCompleted")), offerId, actor: buyer);
        Assert.Equal("Accepted", await SentStatusAsync(buyer, offerId));

        // 3. The bottle left the seller's bar and joined the buyer's, which remembers the deal.
        Assert.Equal(HttpStatusCode.NotFound, (await seller.Client.GetAsync($"/api/bottles/{bottleId}")).StatusCode);
        var bar = await (await buyer.Client.GetAsync($"/api/bottles?userId={buyer.Id}")).JsonAsync();
        var bought = Assert.Single(bar.EnumerateArray());
        Assert.Equal(seller.Id, bought.GetProperty("acquiredFromUserId").GetGuid());
        Assert.Equal(JsonValueKind.String, bought.GetProperty("acquiredFromDisplayName").ValueKind);
        Assert.Equal(1, bought.GetProperty("quantity").GetInt32());
        Assert.False(bought.GetProperty("isForSale").GetBoolean());

        // 4. Each side earns their own badge, and only theirs.
        Assert.Contains(await seller.NotificationsAsync("BadgeEarned"), IsBadge("FirstSale"));
        Assert.Contains(await buyer.NotificationsAsync("BadgeEarned"), IsBadge("FirstPurchase"));
        var sellerBadges = await seller.EarnedBadgesAsync();
        var buyerBadges = await buyer.EarnedBadgesAsync();
        Assert.True(sellerBadges["FirstSale"]);
        Assert.False(sellerBadges["FirstPurchase"]);
        Assert.True(buyerBadges["FirstPurchase"]);
        Assert.False(buyerBadges["FirstSale"]);
    }

    [SqlServerFact]
    public async Task CancelDeal_WhenTheSellerCancelsAnAcceptedOffer_KeepsTheBottleAndTellsTheBuyer()
    {
        var seller = await Collector.RegisterAsync(Api, "seller");
        var buyer = await Collector.RegisterAsync(Api, "buyer");
        var bottleId = await seller.AddBottleAsync();
        var offerId = await buyer.MakeOfferAsync(bottleId);
        await CollectorApi.AssertSucceededAsync(await seller.Client.PatchAsync($"/api/offers/{offerId}/accept", content: null));

        var cancel = await seller.Client.PatchAsync($"/api/offers/{offerId}/cancel", content: null);

        Assert.Equal(HttpStatusCode.OK, cancel.StatusCode);
        Assert.Equal("Cancelled", (await cancel.JsonAsync()).GetProperty("status").GetString());
        AssertAbout(Assert.Single(await buyer.NotificationsAsync("DealCancelled")), offerId, actor: seller);
        var bottle = await (await seller.Client.GetAsync($"/api/bottles/{bottleId}")).JsonAsync();
        Assert.Equal(0, bottle.GetProperty("reservedQuantity").GetInt32());
        Assert.Empty((await (await buyer.Client.GetAsync($"/api/bottles?userId={buyer.Id}")).JsonAsync()).EnumerateArray());
    }

    [SqlServerFact]
    public async Task AcceptOffer_WhenNoBottleIsLeft_Returns409AndDeclinesTheOtherOffers()
    {
        var seller = await Collector.RegisterAsync(Api, "seller");
        var first = await Collector.RegisterAsync(Api, "first-buyer");
        var second = await Collector.RegisterAsync(Api, "second-buyer");
        var bottleId = await seller.AddBottleAsync();
        var firstOffer = await first.MakeOfferAsync(bottleId);
        var secondOffer = await second.MakeOfferAsync(bottleId);

        await CollectorApi.AssertSucceededAsync(await seller.Client.PatchAsync($"/api/offers/{firstOffer}/accept", content: null));

        Assert.Equal("Declined", await SentStatusAsync(second, secondOffer));
        AssertAbout(Assert.Single(await second.NotificationsAsync("OfferDeclined")), secondOffer, actor: seller);
        var third = await Collector.RegisterAsync(Api, "third-buyer");
        var refused = await third.Client.PostAsJsonAsync("/api/offers", new { bottleId, offeredPrice = 100m, currency = "EUR" });
        await CollectorApi.AssertRefusedAsync(refused, HttpStatusCode.Conflict);
    }

    private static async Task<string?> SentStatusAsync(Collector buyer, Guid offerId)
    {
        var sent = await buyer.Client.GetAsync("/api/offers/sent");
        await CollectorApi.AssertSucceededAsync(sent);
        return (await sent.JsonAsync()).EnumerateArray()
            .Single(offer => offer.GetProperty("id").GetGuid() == offerId)
            .GetProperty("status").GetString();
    }

    private static void AssertAbout(JsonElement notification, Guid offerId, Collector actor)
    {
        Assert.Equal(offerId, notification.GetProperty("resourceId").GetGuid());
        Assert.Equal(actor.Id, notification.GetProperty("actorId").GetGuid());
    }

    private static Predicate<JsonElement> IsBadge(string badge) =>
        notification => notification.GetProperty("resourceName").GetString() == badge;
}
