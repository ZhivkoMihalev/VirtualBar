using System.Net;
using System.Net.Http.Json;
using VirtualBar.HttpTests.Support;

namespace VirtualBar.HttpTests.Access;

/// <summary>
/// Every action that changes a collector's own resource, called by another collector (#68). The validation decorators
/// refuse it with 403, except <c>ReorderBottles</c>, which only ever looks at the caller's own bottles and so answers
/// 404 for anyone else's. A success or a 500 here is a bug of its own, never something to adjust the test to.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class OwnershipTests(ApiHosts hosts)
{
    private ApiFactory Api => hosts.ConfirmationOff;

    [SqlServerFact]
    public async Task UpdateBottle_WhenTheBottleBelongsToAnotherCollector_Returns403()
    {
        var (owner, other) = await TwoCollectorsAsync();
        var bottleId = await owner.AddBottleAsync();

        var response = await other.Client.PutAsJsonAsync(
            $"/api/bottles/{bottleId}", new { name = "Renamed by someone else", category = "Rum" });

        await CollectorApi.AssertRefusedAsync(response, HttpStatusCode.Forbidden);
    }

    [SqlServerFact]
    public async Task RemoveBottle_WhenTheBottleBelongsToAnotherCollector_Returns403()
    {
        var (owner, other) = await TwoCollectorsAsync();
        var bottleId = await owner.AddBottleAsync();

        var response = await other.Client.DeleteAsync($"/api/bottles/{bottleId}");

        await CollectorApi.AssertRefusedAsync(response, HttpStatusCode.Forbidden);
    }

    [SqlServerFact]
    public async Task ReorderBottles_WhenOneOfTheBottlesBelongsToAnotherCollector_Returns404()
    {
        var (owner, other) = await TwoCollectorsAsync();
        var ownersBottle = await owner.AddBottleAsync();
        var ownBottle = await other.AddBottleAsync();
        // The same request with only the caller's own bottle succeeds, so the 404 below comes from the other bottle.
        await CollectorApi.AssertSucceededAsync(
            await other.Client.PutAsJsonAsync("/api/bottles/reorder", new { bottleIds = new[] { ownBottle } }));

        var response = await other.Client.PutAsJsonAsync(
            "/api/bottles/reorder", new { bottleIds = new[] { ownBottle, ownersBottle } });

        await CollectorApi.AssertRefusedAsync(response, HttpStatusCode.NotFound);
    }

    [SqlServerFact]
    public async Task ListForSale_WhenTheBottleBelongsToAnotherCollector_Returns403()
    {
        var (owner, other) = await TwoCollectorsAsync();
        var bottleId = await owner.AddBottleAsync();

        var response = await other.Client.PostAsJsonAsync(
            $"/api/bottles/{bottleId}/list-for-sale", new { askingPrice = 1m, currency = "EUR" });

        await CollectorApi.AssertRefusedAsync(response, HttpStatusCode.Forbidden);
    }

    [SqlServerFact]
    public async Task Unlist_WhenTheBottleBelongsToAnotherCollector_Returns403()
    {
        var (owner, other) = await TwoCollectorsAsync();
        var bottleId = await owner.AddBottleAsync();
        await owner.ListForSaleAsync(bottleId);

        var response = await other.Client.PostAsync($"/api/bottles/{bottleId}/unlist", content: null);

        await CollectorApi.AssertRefusedAsync(response, HttpStatusCode.Forbidden);
    }

    [SqlServerFact]
    public async Task AddImage_WhenTheBottleBelongsToAnotherCollector_Returns403()
    {
        var (owner, other) = await TwoCollectorsAsync();
        var bottleId = await owner.AddBottleAsync();

        // A valid image: the file is checked before the owner, so anything else would be refused as invalid instead.
        var response = await other.Client.PostAsync(
            $"/api/bottles/{bottleId}/images", CollectorApi.ImageForm(CollectorApi.Png));

        await CollectorApi.AssertRefusedAsync(response, HttpStatusCode.Forbidden);
    }

    [SqlServerFact]
    public async Task LinkImage_WhenTheBottleBelongsToAnotherCollector_Returns403()
    {
        var (owner, other) = await TwoCollectorsAsync();
        var bottleId = await owner.AddBottleAsync();

        var response = await other.Client.PostAsJsonAsync(
            $"/api/bottles/{bottleId}/images/link", new { url = "https://images.example.test/other.png" });

        await CollectorApi.AssertRefusedAsync(response, HttpStatusCode.Forbidden);
    }

    [SqlServerFact]
    public async Task DeleteImage_WhenTheBottleBelongsToAnotherCollector_Returns403()
    {
        var (owner, other) = await TwoCollectorsAsync();
        var bottleId = await owner.AddBottleAsync();
        var imageId = await owner.LinkImageAsync(bottleId);

        var response = await other.Client.DeleteAsync($"/api/bottles/{bottleId}/images/{imageId}");

        await CollectorApi.AssertRefusedAsync(response, HttpStatusCode.Forbidden);
    }

    [SqlServerFact]
    public async Task DeleteComment_WhenTheCommentBelongsToAnotherCollector_Returns403()
    {
        var (owner, other) = await TwoCollectorsAsync();
        var bottleId = await owner.AddBottleAsync();
        var commentId = await owner.AddCommentAsync(bottleId);

        var response = await other.Client.DeleteAsync($"/api/bottles/{bottleId}/comments/{commentId}");

        await CollectorApi.AssertRefusedAsync(response, HttpStatusCode.Forbidden);
    }

    [SqlServerFact]
    public async Task UpdateReview_WhenTheReviewBelongsToAnotherCollector_Returns403()
    {
        var (owner, other) = await TwoCollectorsAsync();
        var bottleId = await owner.AddBottleAsync();
        var reviewId = await owner.AddReviewAsync(bottleId);

        var response = await other.Client.PutAsJsonAsync(
            $"/api/bottles/{bottleId}/reviews/{reviewId}", new { score = 10, summary = "Rewritten by someone else." });

        await CollectorApi.AssertRefusedAsync(response, HttpStatusCode.Forbidden);
    }

    [SqlServerFact]
    public async Task DeleteReview_WhenTheReviewBelongsToAnotherCollector_Returns403()
    {
        var (owner, other) = await TwoCollectorsAsync();
        var bottleId = await owner.AddBottleAsync();
        var reviewId = await owner.AddReviewAsync(bottleId);

        var response = await other.Client.DeleteAsync($"/api/bottles/{bottleId}/reviews/{reviewId}");

        await CollectorApi.AssertRefusedAsync(response, HttpStatusCode.Forbidden);
    }

    [SqlServerFact]
    public async Task AcceptOffer_WhenTheCallerIsTheBuyer_Returns403()
    {
        var (seller, buyer) = await TwoCollectorsAsync();
        var offerId = await buyer.MakeOfferAsync(await seller.AddBottleAsync());

        var response = await buyer.Client.PatchAsync($"/api/offers/{offerId}/accept", content: null);

        await CollectorApi.AssertRefusedAsync(response, HttpStatusCode.Forbidden);
    }

    [SqlServerFact]
    public async Task DeclineOffer_WhenTheCallerIsTheBuyer_Returns403()
    {
        var (seller, buyer) = await TwoCollectorsAsync();
        var offerId = await buyer.MakeOfferAsync(await seller.AddBottleAsync());

        var response = await buyer.Client.PatchAsync($"/api/offers/{offerId}/decline", content: null);

        await CollectorApi.AssertRefusedAsync(response, HttpStatusCode.Forbidden);
    }

    [SqlServerFact]
    public async Task WithdrawOffer_WhenTheCallerIsTheSeller_Returns403()
    {
        var (seller, buyer) = await TwoCollectorsAsync();
        var offerId = await buyer.MakeOfferAsync(await seller.AddBottleAsync());

        var response = await seller.Client.PatchAsync($"/api/offers/{offerId}/withdraw", content: null);

        await CollectorApi.AssertRefusedAsync(response, HttpStatusCode.Forbidden);
    }

    [SqlServerFact]
    public async Task WithdrawProductRequest_WhenTheRequestBelongsToAnotherCollector_Returns403()
    {
        var (owner, other) = await TwoCollectorsAsync();
        var requestId = await owner.RequestProductAsync();

        var response = await other.Client.DeleteAsync($"/api/product-requests/{requestId}");

        await CollectorApi.AssertRefusedAsync(response, HttpStatusCode.Forbidden);
    }

    [SqlServerFact]
    public async Task RemoveWishListItem_WhenTheItemBelongsToAnotherCollector_Returns403()
    {
        var (owner, other) = await TwoCollectorsAsync();
        var itemId = await owner.AddWishAsync();

        var response = await other.Client.DeleteAsync($"/api/wishlist/{itemId}");

        await CollectorApi.AssertRefusedAsync(response, HttpStatusCode.Forbidden);
        // The owner removes it: a wish would match every later Rum listing in this run.
        await CollectorApi.AssertSucceededAsync(await owner.Client.DeleteAsync($"/api/wishlist/{itemId}"));
    }

    [SqlServerFact]
    public async Task MarkMessageRead_WhenTheCallerIsTheSender_Returns403()
    {
        var (sender, receiver) = await TwoCollectorsAsync();
        var messageId = await sender.SendMessageAsync(receiver.Id);

        var response = await sender.Client.PostAsync($"/api/messages/{messageId}/read", content: null);

        await CollectorApi.AssertRefusedAsync(response, HttpStatusCode.Forbidden);
    }

    [SqlServerFact]
    public async Task MarkNotificationRead_WhenTheNotificationBelongsToAnotherCollector_Returns403()
    {
        var (owner, other) = await TwoCollectorsAsync();
        await other.FollowAsync(owner.Id);
        // The follow also earns the owner FirstFollower, which notifies too, so pick the follow by its type.
        var notificationId = Assert.Single(
            await owner.NotificationsAsync(),
            notification => notification.GetProperty("type").GetString() == "NewFollower").GetProperty("id").GetGuid();

        var response = await other.Client.PostAsync($"/api/notifications/{notificationId}/read", content: null);

        await CollectorApi.AssertRefusedAsync(response, HttpStatusCode.Forbidden);
    }

    private async Task<(Collector Owner, Collector Other)> TwoCollectorsAsync() =>
        (await Collector.RegisterAsync(Api, "owner"), await Collector.RegisterAsync(Api, "other"));
}
