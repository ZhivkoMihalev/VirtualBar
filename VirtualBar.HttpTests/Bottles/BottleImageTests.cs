using System.Net;
using System.Net.Http.Json;
using VirtualBar.HttpTests.Support;

namespace VirtualBar.HttpTests.Bottles;

/// <summary>
/// Deleting a bottle image over HTTP. The route names both the bottle and the image, and the API must act only on an
/// image of that bottle (#72). Both ids are GUIDs, so these tests also catch the controller passing them the wrong way
/// round, which the service tests cannot see.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class BottleImageTests(ApiHosts hosts)
{
    private ApiFactory Api => hosts.ConfirmationOff;

    [SqlServerFact]
    public async Task DeleteImage_WhenTheImageBelongsToAnotherBottleOfTheSameOwner_Returns404AndKeepsTheImage()
    {
        var owner = await Collector.RegisterAsync(Api, "owner");
        var namedBottle = await owner.AddBottleAsync();
        var imagesBottle = await owner.AddBottleAsync();
        var imageId = await owner.LinkImageAsync(imagesBottle);

        var response = await owner.Client.DeleteAsync($"/api/bottles/{namedBottle}/images/{imageId}");

        await CollectorApi.AssertRefusedAsync(response, HttpStatusCode.NotFound);
        Assert.Contains(imageId, await ImageIdsAsync(imagesBottle));
    }

    [SqlServerFact]
    public async Task DeleteImage_WhenTheImageBelongsToTheNamedBottle_Returns200AndRemovesIt()
    {
        var owner = await Collector.RegisterAsync(Api, "owner");
        var bottleId = await owner.AddBottleAsync();
        var imageId = await owner.LinkImageAsync(bottleId);

        var response = await owner.Client.DeleteAsync($"/api/bottles/{bottleId}/images/{imageId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain(imageId, await ImageIdsAsync(bottleId));
    }

    /// <summary>
    /// Linking used to store any address it was sent, so every viewer of the bottle loaded it (#131). The request now
    /// names a catalog product; an address sent the old way is refused and the bottle gets no image.
    /// </summary>
    [SqlServerFact]
    public async Task LinkImage_WhenTheRequestSendsAnAddressInsteadOfAProduct_Returns400AndStoresNothing()
    {
        var owner = await Collector.RegisterAsync(Api, "owner");
        var bottleId = await owner.AddBottleAsync();

        var response = await owner.Client.PostAsJsonAsync(
            $"/api/bottles/{bottleId}/images/link", new { url = "https://example.com/any.png" });

        await CollectorApi.AssertRefusedAsync(response, HttpStatusCode.BadRequest);
        Assert.Empty(await ImageIdsAsync(bottleId));
    }

    private async Task<List<Guid>> ImageIdsAsync(Guid bottleId)
    {
        var response = await Api.CreateClient().GetAsync($"/api/bottles/{bottleId}");
        await CollectorApi.AssertSucceededAsync(response);
        return (await response.JsonAsync()).GetProperty("images").EnumerateArray()
            .Select(image => image.GetProperty("id").GetGuid())
            .ToList();
    }
}
