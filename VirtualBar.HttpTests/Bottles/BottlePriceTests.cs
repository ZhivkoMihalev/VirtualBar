using System.Net;
using System.Text.Json;
using VirtualBar.HttpTests.Support;

namespace VirtualBar.HttpTests.Bottles;

/// <summary>
/// The asking price on a public bar is for logged-in collectors only, like the marketplace (#103). The bottle
/// endpoints stay open to a visitor, who still sees that a bottle is for sale and how many, but gets
/// <c>askingPrice</c> and <c>currency</c> as null.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class BottlePriceTests(ApiHosts hosts)
{
    private ApiFactory Api => hosts.ConfirmationOff;

    [SqlServerFact]
    public async Task GetBottlesByUser_WhenTheCallerIsNotLoggedIn_ReturnsTheListingWithoutItsPrice()
    {
        var (seller, bottleId) = await SeedListedBottleAsync();

        var response = await Api.CreateClient().GetAsync($"/api/bottles?userId={seller.Id}");

        AssertWithoutPrice(await SingleBottleAsync(response, bottleId));
    }

    [SqlServerFact]
    public async Task GetBottlesByUser_WhenTheCallerIsLoggedIn_ReturnsThePrice()
    {
        var (seller, bottleId) = await SeedListedBottleAsync();
        var viewer = await Collector.RegisterAsync(Api, "viewer");

        var response = await viewer.Client.GetAsync($"/api/bottles?userId={seller.Id}");

        AssertWithPrice(await SingleBottleAsync(response, bottleId));
    }

    [SqlServerFact]
    public async Task GetBottleById_WhenTheCallerIsNotLoggedIn_ReturnsTheListingWithoutItsPrice()
    {
        var (_, bottleId) = await SeedListedBottleAsync();

        var response = await Api.CreateClient().GetAsync($"/api/bottles/{bottleId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertWithoutPrice(await response.JsonAsync());
    }

    [SqlServerFact]
    public async Task GetBottleById_WhenTheCallerIsLoggedIn_ReturnsThePrice()
    {
        var (_, bottleId) = await SeedListedBottleAsync();
        var viewer = await Collector.RegisterAsync(Api, "viewer");

        var response = await viewer.Client.GetAsync($"/api/bottles/{bottleId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertWithPrice(await response.JsonAsync());
    }

    /// <summary>A seller with one bottle listed for sale at 120 EUR (<see cref="CollectorApi.ListForSaleAsync"/>).</summary>
    private async Task<(Collector Seller, Guid BottleId)> SeedListedBottleAsync()
    {
        var seller = await Collector.RegisterAsync(Api, "seller");
        var bottleId = await seller.AddBottleAsync();
        await seller.ListForSaleAsync(bottleId);
        return (seller, bottleId);
    }

    private static async Task<JsonElement> SingleBottleAsync(HttpResponseMessage response, Guid bottleId)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return Assert.Single(
            (await response.JsonAsync()).EnumerateArray(),
            bottle => bottle.GetProperty("id").GetGuid() == bottleId);
    }

    private static void AssertWithoutPrice(JsonElement bottle)
    {
        Assert.True(bottle.GetProperty("isForSale").GetBoolean());
        Assert.Equal(1, bottle.GetProperty("forSaleQuantity").GetInt32());
        Assert.Equal(JsonValueKind.Null, bottle.GetProperty("askingPrice").ValueKind);
        Assert.Equal(JsonValueKind.Null, bottle.GetProperty("currency").ValueKind);
    }

    private static void AssertWithPrice(JsonElement bottle)
    {
        Assert.True(bottle.GetProperty("isForSale").GetBoolean());
        Assert.Equal(120m, bottle.GetProperty("askingPrice").GetDecimal());
        Assert.Equal("EUR", bottle.GetProperty("currency").GetString());
    }
}
