using System.Net.Http.Json;
using System.Text.Json;
using VirtualBar.HttpTests.Support;

namespace VirtualBar.HttpTests.Feed;

/// <summary>
/// The home feed over HTTP on SQL Server (#57). The service tests run on InMemory, which proves neither that the
/// review query (its flavors and its product, through nullable navigations) translates to SQL, nor the camelCase shape
/// the home page reads.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class FeedTests(ApiHosts hosts)
{
    private ApiFactory Api => hosts.ConfirmationOff;

    [SqlServerFact]
    public async Task GetFeed_WhenAFollowedCollectorReviewsABottle_ShowsTheReviewWithReviewerAndOwner()
    {
        var reader = await Collector.RegisterAsync(Api, "feed-reader");
        var reviewer = await Collector.RegisterAsync(Api, "feed-reviewer");
        var owner = await Collector.RegisterAsync(Api, "feed-owner");
        await reader.FollowAsync(reviewer.Id);
        var bottleId = await owner.AddBottleAsync();
        var reviewId = await CollectorApi.IdAsync(await reviewer.Client.PostAsJsonAsync(
            $"/api/bottles/{bottleId}/reviews",
            new { score = 91, summary = "Dried fruit and oak.", flavors = new[] { "Oak", "Vanilla", "Smoky", "Peaty" } }));

        var response = await reader.Client.GetAsync("/api/feed?take=100");

        await CollectorApi.AssertSucceededAsync(response);
        var item = (await response.JsonAsync()).EnumerateArray()
            .Single(i => i.GetProperty("type").GetString() == "Reviewed");
        Assert.Equal(reviewId, item.GetProperty("reviewId").GetGuid());
        Assert.Equal(91, item.GetProperty("reviewScore").GetInt32());
        Assert.Equal("Dried fruit and oak.", item.GetProperty("reviewSummary").GetString());
        Assert.Equal(
            ["Smoky", "Peaty", "Vanilla"],
            item.GetProperty("reviewFlavors").EnumerateArray().Select(f => f.GetString()));
        Assert.Equal(reviewer.Id, item.GetProperty("actorUserId").GetGuid());
        Assert.Equal("HTTP test collector", item.GetProperty("actorDisplayName").GetString());
        Assert.Equal(bottleId, item.GetProperty("bottleId").GetGuid());
        Assert.Equal(owner.Id, item.GetProperty("bottleUserId").GetGuid());
        Assert.Equal(JsonValueKind.Null, item.GetProperty("productId").ValueKind);
    }

    /// <summary>
    /// Each followed-activity query loads its newest <c>skip + take</c> items; added as ints, the largest skip would
    /// overflow into a negative row count, which SQL Server refuses.
    /// </summary>
    [SqlServerFact]
    public async Task GetFeed_WhenSkipIsTheLargestInteger_AnswersAnEmptyPage()
    {
        var reader = await Collector.RegisterAsync(Api, "feed-reader");
        var followed = await Collector.RegisterAsync(Api, "feed-followed");
        await reader.FollowAsync(followed.Id);
        await followed.AddBottleAsync();

        var response = await reader.Client.GetAsync($"/api/feed?skip={int.MaxValue}&take=20");

        await CollectorApi.AssertSucceededAsync(response);
        Assert.Empty((await response.JsonAsync()).EnumerateArray());
    }
}
