using System.Net;
using Microsoft.Extensions.DependencyInjection;
using VirtualBar.Domain.Entities;
using VirtualBar.Domain.Enums;
using VirtualBar.HttpTests.Support;
using VirtualBar.Infrastructure.Persistence;

namespace VirtualBar.HttpTests.Products;

/// <summary>
/// The public catalog, <c>GET /api/products/browse?sort=</c> (#56), on SQL Server: every order translates and pages
/// over the whole seeded catalog, and "top rated" ranks products with enough reviews above a single perfect score.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class CatalogBrowseTests(ApiHosts hosts)
{
    private ApiFactory Api => hosts.ConfirmationOff;

    [SqlServerTheory]
    [InlineData("Name")]
    [InlineData("Relevance")]
    [InlineData("TopRated")]
    [InlineData("MostReviewed")]
    [InlineData("Popular")]
    [InlineData("ForSale")]
    public async Task Browse_WhenSorted_PagesTheWholeCatalogWithoutRepeats(string sort)
    {
        var client = Api.CreateClient();

        var first = await (await client.GetAsync($"/api/products/browse?sort={sort}&page=1&pageSize=48")).JsonAsync();
        var second = await (await client.GetAsync($"/api/products/browse?sort={sort}&page=2&pageSize=48")).JsonAsync();

        Assert.True(first.GetProperty("hasMore").GetBoolean());
        var ids = first.GetProperty("items").EnumerateArray()
            .Concat(second.GetProperty("items").EnumerateArray())
            .Select(card => card.GetProperty("id").GetGuid())
            .ToList();
        Assert.Equal(96, ids.Count);
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    [SqlServerFact]
    public async Task Browse_WhenSortedTopRated_RanksEnoughReviewsAboveASinglePerfectScore()
    {
        var prefix = CollectorApi.UniqueName("top rated");
        var reviewer = await Collector.RegisterAsync(Api, "reviewer");
        await SeedReviewedProductAsync(reviewer, $"{prefix} single perfect", 100);
        await SeedReviewedProductAsync(reviewer, $"{prefix} three reviews", 90, 90, 90);
        await SeedReviewedProductAsync(reviewer, $"{prefix} unreviewed");

        var response = await Api.CreateClient().GetAsync(
            $"/api/products/browse?search={Uri.EscapeDataString(prefix)}&sort=TopRated");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var names = (await response.JsonAsync()).GetProperty("items").EnumerateArray()
            .Select(card => card.GetProperty("name").GetString())
            .ToArray();
        Assert.Equal(
            new[] { $"{prefix} three reviews", $"{prefix} single perfect", $"{prefix} unreviewed" },
            names);
    }

    [SqlServerTheory]
    [InlineData("Cheapest")]
    [InlineData("99")]
    public async Task Browse_WhenSortUnknown_Returns400(string sort)
    {
        var response = await Api.CreateClient().GetAsync($"/api/products/browse?sort={sort}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// A new catalog product with one bottle per score, each linked to it and reviewed with that score. They go
    /// straight into the app's database: one collector cannot review a bottle twice through the API, and there is
    /// no endpoint that creates a product without the admin request flow.
    /// </summary>
    private async Task SeedReviewedProductAsync(Collector owner, string name, params int[] scores)
    {
        await using var scope = Api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var product = new Product
        {
            Name = name,
            Category = SpiritCategory.Rum,
            CanonicalKey = name,
            Origin = ProductOrigin.Approved,
        };
        db.Products.Add(product);

        foreach (var score in scores)
        {
            var bottle = new Bottle { UserId = owner.Id, Name = name, Category = SpiritCategory.Rum, Product = product };
            db.Bottles.Add(bottle);
            db.BottleReviews.Add(new BottleReview { Bottle = bottle, UserId = owner.Id, Score = score });
        }

        await db.SaveChangesAsync();
    }
}
