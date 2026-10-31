using System.Net;
using System.Net.Http.Json;
using VirtualBar.HttpTests.Support;

namespace VirtualBar.HttpTests.Access;

/// <summary>
/// The admin-only actions (#68): writing news and resolving product requests. A collector is refused with 403; an
/// admin, made through the app's database and logged in again (<see cref="Collector.RegisterAdminAsync"/>), succeeds.
/// So the tests also prove that the <c>isAdmin</c> claim travels from login, through the JWT, to the decorators.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class AdminTests(ApiHosts hosts)
{
    private ApiFactory Api => hosts.ConfirmationOff;

    [SqlServerFact]
    public async Task CreateNews_WhenTheCallerIsNotAnAdmin_Returns403()
    {
        var collector = await Collector.RegisterAsync(Api, "collector");

        var response = await collector.Client.PostAsJsonAsync("/api/news", CollectorApi.NewsPost("Not allowed"));

        await CollectorApi.AssertRefusedAsync(response, HttpStatusCode.Forbidden);
    }

    [SqlServerFact]
    public async Task CreateNews_WhenTheCallerIsAnAdmin_Returns201AndPublishesThePost()
    {
        var admin = await Collector.RegisterAdminAsync(Api, "admin");
        var title = CollectorApi.UniqueName("news");

        var response = await admin.Client.PostAsJsonAsync("/api/news", CollectorApi.NewsPost(title));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var id = (await response.JsonAsync()).GetProperty("id").GetGuid();
        var published = await Api.CreateClient().GetAsync($"/api/news/{id}?lang=bg");
        Assert.Equal(HttpStatusCode.OK, published.StatusCode);
        Assert.Equal(title, (await published.JsonAsync()).GetProperty("title").GetString());
    }

    [SqlServerFact]
    public async Task UpdateNews_WhenTheCallerIsNotAnAdmin_Returns403()
    {
        var postId = await (await Collector.RegisterAdminAsync(Api, "admin")).CreateNewsPostAsync();
        var collector = await Collector.RegisterAsync(Api, "collector");

        var response = await collector.Client.PutAsJsonAsync($"/api/news/{postId}", CollectorApi.NewsPost("Not allowed"));

        await CollectorApi.AssertRefusedAsync(response, HttpStatusCode.Forbidden);
    }

    [SqlServerFact]
    public async Task UpdateNews_WhenTheCallerIsAnAdmin_Returns200WithTheNewTitle()
    {
        var admin = await Collector.RegisterAdminAsync(Api, "admin");
        var postId = await admin.CreateNewsPostAsync();
        var title = CollectorApi.UniqueName("updated news");

        var response = await admin.Client.PutAsJsonAsync($"/api/news/{postId}", CollectorApi.NewsPost(title));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(title, (await response.JsonAsync()).GetProperty("title").GetString());
    }

    [SqlServerFact]
    public async Task DeleteNews_WhenTheCallerIsNotAnAdmin_Returns403()
    {
        var postId = await (await Collector.RegisterAdminAsync(Api, "admin")).CreateNewsPostAsync();
        var collector = await Collector.RegisterAsync(Api, "collector");

        var response = await collector.Client.DeleteAsync($"/api/news/{postId}");

        await CollectorApi.AssertRefusedAsync(response, HttpStatusCode.Forbidden);
    }

    [SqlServerFact]
    public async Task DeleteNews_WhenTheCallerIsAnAdmin_Returns204AndThePostIsGone()
    {
        var admin = await Collector.RegisterAdminAsync(Api, "admin");
        var postId = await admin.CreateNewsPostAsync();

        var response = await admin.Client.DeleteAsync($"/api/news/{postId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Api.CreateClient().GetAsync($"/api/news/{postId}")).StatusCode);
    }

    [SqlServerFact]
    public async Task UploadNewsCover_WhenTheCallerIsNotAnAdmin_Returns403()
    {
        var collector = await Collector.RegisterAsync(Api, "collector");

        var response = await collector.Client.PostAsync("/api/news/upload-cover", CollectorApi.ImageForm(CollectorApi.Png));

        await CollectorApi.AssertRefusedAsync(response, HttpStatusCode.Forbidden);
    }

    [SqlServerFact]
    public async Task UploadNewsCover_WhenTheCallerIsAnAdmin_SavesTheImageInTheWebRoot()
    {
        var admin = await Collector.RegisterAdminAsync(Api, "admin");

        var response = await admin.Client.PostAsync("/api/news/upload-cover", CollectorApi.ImageForm(CollectorApi.Png));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var url = (await response.JsonAsync()).GetProperty("url").GetString()!;
        Assert.StartsWith("/uploads/news/", url);
        Assert.Equal(CollectorApi.Png, await File.ReadAllBytesAsync(Path.Combine(Api.WebRoot, url.TrimStart('/'))));
    }

    [SqlServerFact]
    public async Task ProductRequestQueue_WhenTheCallerIsNotAnAdmin_Returns403()
    {
        var collector = await Collector.RegisterAsync(Api, "collector");

        var response = await collector.Client.GetAsync("/api/product-requests?status=Pending");

        await CollectorApi.AssertRefusedAsync(response, HttpStatusCode.Forbidden);
    }

    [SqlServerFact]
    public async Task ProductRequestQueue_WhenTheCallerIsAnAdmin_ListsAPendingRequest()
    {
        var requestId = await (await Collector.RegisterAsync(Api, "requester")).RequestProductAsync();
        var admin = await Collector.RegisterAdminAsync(Api, "admin");

        var response = await admin.Client.GetAsync("/api/product-requests?status=Pending");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(
            (await response.JsonAsync()).EnumerateArray(),
            request => request.GetProperty("id").GetGuid() == requestId);
    }

    [SqlServerFact]
    public async Task ApproveProductRequest_WhenTheCallerIsNotAnAdmin_Returns403()
    {
        var requester = await Collector.RegisterAsync(Api, "requester");
        var requestId = await requester.RequestProductAsync();

        // The requester approving their own request is the case that matters most.
        var response = await requester.Client.PatchAsJsonAsync($"/api/product-requests/{requestId}/approve", new { });

        await CollectorApi.AssertRefusedAsync(response, HttpStatusCode.Forbidden);
    }

    [SqlServerFact]
    public async Task ApproveProductRequest_WhenTheCallerIsAnAdmin_ApprovesItAndCreatesTheProduct()
    {
        var requestId = await (await Collector.RegisterAsync(Api, "requester")).RequestProductAsync();
        var admin = await Collector.RegisterAdminAsync(Api, "admin");

        var response = await admin.Client.PatchAsJsonAsync($"/api/product-requests/{requestId}/approve", new { });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var request = await response.JsonAsync();
        Assert.Equal("Approved", request.GetProperty("status").GetString());
        var productId = request.GetProperty("resolvedProductId").GetGuid();
        Assert.Equal(HttpStatusCode.OK, (await Api.CreateClient().GetAsync($"/api/products/{productId}")).StatusCode);
    }

    [SqlServerFact]
    public async Task RejectProductRequest_WhenTheCallerIsNotAnAdmin_Returns403()
    {
        var requester = await Collector.RegisterAsync(Api, "requester");
        var requestId = await requester.RequestProductAsync();

        var response = await requester.Client.PatchAsJsonAsync($"/api/product-requests/{requestId}/reject", new { });

        await CollectorApi.AssertRefusedAsync(response, HttpStatusCode.Forbidden);
    }

    [SqlServerFact]
    public async Task RejectProductRequest_WhenTheCallerIsAnAdmin_RejectsIt()
    {
        var requestId = await (await Collector.RegisterAsync(Api, "requester")).RequestProductAsync();
        var admin = await Collector.RegisterAdminAsync(Api, "admin");

        var response = await admin.Client.PatchAsJsonAsync(
            $"/api/product-requests/{requestId}/reject", new { adminNote = "Not a real product." });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Rejected", (await response.JsonAsync()).GetProperty("status").GetString());
    }
}
