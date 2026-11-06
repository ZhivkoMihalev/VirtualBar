using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using VirtualBar.Domain.Entities;
using VirtualBar.Domain.Enums;
using VirtualBar.HttpTests.Support;
using VirtualBar.Infrastructure.Persistence;

namespace VirtualBar.HttpTests.Products;

/// <summary>
/// The public product page, <c>GET /api/products/{id}</c>: anyone may open it, but the listings (seller, price) are
/// for logged-in collectors only, like the marketplace. A visitor gets only how many there are (#102).
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class ProductPageTests(ApiHosts hosts)
{
    private ApiFactory Api => hosts.ConfirmationOff;

    [SqlServerFact]
    public async Task GetProduct_WhenTheCallerIsNotLoggedIn_ReturnsTheListingsCountButNoListings()
    {
        var (productId, _) = await SeedListedProductAsync();

        var response = await Api.CreateClient().GetAsync($"/api/products/{productId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var product = await response.JsonAsync();
        Assert.Equal(1, product.GetProperty("listingsCount").GetInt32());
        Assert.Empty(product.GetProperty("listings").EnumerateArray());
    }

    [SqlServerFact]
    public async Task GetProduct_WhenTheCallerIsLoggedIn_ReturnsTheListings()
    {
        var (productId, bottleId) = await SeedListedProductAsync();
        var viewer = await Collector.RegisterAsync(Api, "viewer");

        var response = await viewer.Client.GetAsync($"/api/products/{productId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var product = await response.JsonAsync();
        Assert.Equal(1, product.GetProperty("listingsCount").GetInt32());
        var listing = Assert.Single(product.GetProperty("listings").EnumerateArray());
        Assert.Equal(bottleId, listing.GetProperty("bottleId").GetGuid());
    }

    /// <summary>
    /// The seeded catalog carries the approved years (#59): a vintage-dated product has its vintage year and no other
    /// vintage of its line, and an edition lists the other editions of its series, each with its own release year.
    /// </summary>
    [SqlServerFact]
    public async Task GetProduct_WhenSeededProductIsDated_ReturnsItsYearAndTheOtherYearsOfItsLine()
    {
        var client = Api.CreateClient();

        var millesime = await GetSeededProductAsync(client, "Millésime 1988", "Cognac");
        Assert.Equal(1988, millesime.GetProperty("vintageYear").GetInt32());
        Assert.Equal(JsonValueKind.Null, millesime.GetProperty("releaseYear").ValueKind);

        var classicCut = await GetSeededProductAsync(client, "Classic Cut 2020 Edition", "Whisky");
        Assert.Equal(2020, classicCut.GetProperty("releaseYear").GetInt32());
        var years = classicCut.GetProperty("otherYears").EnumerateArray()
            .Select(y => (Name: y.GetProperty("name").GetString()!, Year: y.GetProperty("year").GetInt32()))
            .ToList();
        Assert.Contains(("Classic Cut 2019 Edition", 2019), years);
        Assert.Contains(("Classic Cut 2021 Edition", 2021), years);
        Assert.DoesNotContain(years, y => y.Year == 2020);
        Assert.Equal(years.OrderBy(y => y.Year).ToList(), years);
    }

    private static async Task<JsonElement> GetSeededProductAsync(HttpClient client, string name, string category)
    {
        var page = await (await client.GetAsync(
            $"/api/products/browse?search={Uri.EscapeDataString(name)}&category={category}")).JsonAsync();
        var id = page.GetProperty("items").EnumerateArray()
            .First(p => p.GetProperty("name").GetString() == name)
            .GetProperty("id").GetGuid();

        var response = await client.GetAsync($"/api/products/{id}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.JsonAsync();
    }

    /// <summary>
    /// A new catalog product with one listed bottle linked to it. The product goes straight into the app's database
    /// (there is no endpoint that creates one without the admin request flow); the bottle goes through the API.
    /// </summary>
    private async Task<(Guid ProductId, Guid BottleId)> SeedListedProductAsync()
    {
        var name = CollectorApi.UniqueName("product");
        var product = new Product
        {
            Name = name,
            Category = SpiritCategory.Rum,
            CanonicalKey = name,
            Origin = ProductOrigin.Approved,
        };

        await using (var scope = Api.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Products.Add(product);
            await db.SaveChangesAsync();
        }

        var seller = await Collector.RegisterAsync(Api, "seller");
        var bottleId = await CollectorApi.IdAsync(await seller.Client.PostAsJsonAsync("/api/bottles", new
        {
            name,
            category = "Rum",
            productId = product.Id,
        }));
        await seller.ListForSaleAsync(bottleId);

        return (product.Id, bottleId);
    }
}
