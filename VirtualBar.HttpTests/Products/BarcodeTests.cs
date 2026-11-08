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
/// The platform's own barcode database (#49) on SQL Server: a barcode saved with a bottle of a catalog product is
/// recorded for that product in the same save, and the next collector's lookup of the barcode finds the product, in the
/// shape of the catalog search.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class BarcodeTests(ApiHosts hosts)
{
    private ApiFactory Api => hosts.ConfirmationOff;

    [SqlServerFact]
    public async Task AddBottle_WhenLinkedWithABarcode_AnotherCollectorsLookupFindsTheProduct()
    {
        var barcode = CollectorApi.NewBarcode();
        var productId = await SeedProductAsync(CollectorApi.UniqueName("barcode product"));
        var owner = await Collector.RegisterAsync(Api, "barcode-owner");
        var other = await Collector.RegisterAsync(Api, "barcode-lookup");

        var added = await owner.Client.PostAsJsonAsync("/api/bottles", new
        {
            name = CollectorApi.UniqueName("barcode bottle"),
            category = "Rum",
            productId,
            barcode = $" {barcode} ",
        });

        Assert.Equal(HttpStatusCode.Created, added.StatusCode);
        Assert.Equal(barcode, (await added.JsonAsync()).GetProperty("barcode").GetString());

        var lookup = await other.Client.GetAsync($"/api/products/barcode/{barcode}");

        Assert.Equal(HttpStatusCode.OK, lookup.StatusCode);
        var found = await lookup.JsonAsync();
        Assert.Equal("Collectors", found.GetProperty("source").GetString());
        Assert.Equal(1, found.GetProperty("collectors").GetInt32());
        Assert.Equal(JsonValueKind.Null, found.GetProperty("suggestion").ValueKind);
        var product = found.GetProperty("product");
        Assert.Equal(productId, product.GetProperty("id").GetGuid());
        // The whole catalog product, as the search returns it, so the form can fill every field (#120).
        Assert.Equal("Rum", product.GetProperty("category").GetString());
    }

    [SqlServerFact]
    public async Task LookupBarcode_WhenOnlyABottleNotInTheCatalogHasIt_SuggestsItsFields()
    {
        var barcode = CollectorApi.NewBarcode();
        var name = CollectorApi.UniqueName("barcode unknown bottle");
        var owner = await Collector.RegisterAsync(Api, "barcode-unknown");
        var other = await Collector.RegisterAsync(Api, "barcode-suggested");
        var added = await owner.Client.PostAsJsonAsync("/api/bottles", new { name, category = "Rum", barcode, age = 12 });
        Assert.Equal(HttpStatusCode.Created, added.StatusCode);

        var lookup = await other.Client.GetAsync($"/api/products/barcode/{barcode}");

        Assert.Equal(HttpStatusCode.OK, lookup.StatusCode);
        var found = await lookup.JsonAsync();
        Assert.Equal("Bottles", found.GetProperty("source").GetString());
        Assert.Equal(JsonValueKind.Null, found.GetProperty("product").ValueKind);
        var suggestion = found.GetProperty("suggestion");
        Assert.Equal(name, suggestion.GetProperty("name").GetString());
        Assert.Equal("Rum", suggestion.GetProperty("category").GetString());
        Assert.Equal(12, suggestion.GetProperty("age").GetInt32());
    }

    [SqlServerFact]
    public async Task AddBottle_WhenBarcodeIsNotEightToFourteenDigits_Returns400()
    {
        var collector = await Collector.RegisterAsync(Api, "bad-barcode");

        var response = await collector.Client.PostAsJsonAsync("/api/bottles", new
        {
            name = CollectorApi.UniqueName("bad barcode bottle"),
            category = "Rum",
            barcode = "50002670142AB",
        });

        await CollectorApi.AssertRefusedAsync(response, HttpStatusCode.BadRequest);
    }

    [SqlServerFact]
    public async Task LookupBarcode_WhenNobodyHasTheBarcode_Returns404()
    {
        var collector = await Collector.RegisterAsync(Api, "unknown-barcode");

        var response = await collector.Client.GetAsync($"/api/products/barcode/{CollectorApi.NewBarcode()}");

        await CollectorApi.AssertRefusedAsync(response, HttpStatusCode.NotFound);
    }

    [SqlServerFact]
    public async Task LookupBarcode_WhenNotEightToFourteenDigits_Returns400()
    {
        var collector = await Collector.RegisterAsync(Api, "invalid-barcode");

        var response = await collector.Client.GetAsync("/api/products/barcode/1234567");

        await CollectorApi.AssertRefusedAsync(response, HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// A catalog product without a barcode of its own. It goes straight into the app's database: there is no endpoint
    /// that creates a product without the admin request flow.
    /// </summary>
    private async Task<Guid> SeedProductAsync(string name)
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
        await db.SaveChangesAsync();

        return product.Id;
    }
}
