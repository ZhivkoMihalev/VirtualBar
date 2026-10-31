using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace VirtualBar.HttpTests.Support;

/// <summary>
/// The endpoints the tests set data up through, as the frontend calls them, plus the checks they repeat. Every setup
/// call asserts that it succeeded and returns the id of what it created, so a test only states what it is about.
/// </summary>
internal static class CollectorApi
{
    /// <summary>A real 1×1 PNG, for the upload endpoints.</summary>
    public static readonly byte[] Png = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNkYAAAAAYAAjCB0C8AAAAASUVORK5CYII=");

    /// <summary>A name no other test, and no earlier run, has used.</summary>
    public static string UniqueName(string purpose) => $"HTTP test {purpose} {Guid.NewGuid():N}";

    /// <summary>The multipart form the frontend sends to an upload endpoint: one <c>file</c> field.</summary>
    public static MultipartFormDataContent ImageForm(byte[] content, string contentType = "image/png")
    {
        var file = new ByteArrayContent(content);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        return new MultipartFormDataContent { { file, "file", "image.png" } };
    }

    public static async Task<Guid> AddBottleAsync(this Collector collector) =>
        await IdAsync(await collector.Client.PostAsJsonAsync("/api/bottles", new
        {
            name = UniqueName("bottle"),
            category = "Rum",
        }));

    public static async Task ListForSaleAsync(this Collector collector, Guid bottleId) =>
        await AssertSucceededAsync(await collector.Client.PostAsJsonAsync(
            $"/api/bottles/{bottleId}/list-for-sale", new { askingPrice = 120m, currency = "EUR" }));

    public static async Task<Guid> LinkImageAsync(this Collector collector, Guid bottleId) =>
        await IdAsync(await collector.Client.PostAsJsonAsync(
            $"/api/bottles/{bottleId}/images/link", new { url = "https://images.example.test/bottle.png" }));

    public static async Task<Guid> AddCommentAsync(this Collector collector, Guid bottleId) =>
        await IdAsync(await collector.Client.PostAsJsonAsync(
            $"/api/bottles/{bottleId}/comments", new { content = "A fine bottle." }));

    public static async Task<Guid> AddReviewAsync(this Collector collector, Guid bottleId) =>
        await IdAsync(await collector.Client.PostAsJsonAsync(
            $"/api/bottles/{bottleId}/reviews", new { score = 88, summary = "Rich and long." }));

    public static async Task<Guid> MakeOfferAsync(this Collector collector, Guid bottleId) =>
        await IdAsync(await collector.Client.PostAsJsonAsync(
            "/api/offers", new { bottleId, offeredPrice = 100m, currency = "EUR" }));

    public static async Task<Guid> RequestProductAsync(this Collector collector) =>
        await IdAsync(await collector.Client.PostAsJsonAsync(
            "/api/product-requests", new { name = UniqueName("product"), category = "Rum" }));

    public static async Task<Guid> AddWishAsync(this Collector collector) =>
        await IdAsync(await collector.Client.PostAsJsonAsync(
            "/api/wishlist", new { bottleName = UniqueName("wish"), category = "Rum" }));

    public static async Task<Guid> SendMessageAsync(this Collector collector, Guid receiverId) =>
        await IdAsync(await collector.Client.PostAsJsonAsync(
            "/api/messages", new { receiverId, content = "Is this bottle still available?" }));

    public static async Task FollowAsync(this Collector collector, Guid userId) =>
        await AssertSucceededAsync(await collector.Client.PostAsync($"/api/users/{userId}/follow", content: null));

    /// <summary>The collector's latest notifications, newest first.</summary>
    public static async Task<List<JsonElement>> NotificationsAsync(this Collector collector)
    {
        var response = await collector.Client.GetAsync("/api/notifications");
        await AssertSucceededAsync(response);
        return (await response.JsonAsync()).GetProperty("notifications").EnumerateArray().ToList();
    }

    public static async Task<Guid> CreateNewsPostAsync(this Collector admin) =>
        await IdAsync(await admin.Client.PostAsJsonAsync("/api/news", NewsPost(UniqueName("news"))));

    /// <summary>A news post body with the Bulgarian translation the API requires.</summary>
    public static object NewsPost(string title) => new
    {
        translations = new[] { new { languageCode = "bg", title, content = "Текст на новината." } },
    };

    /// <summary>
    /// A refusal in the <c>{ error }</c> shape <c>ToActionResult</c> writes. Checking the body tells a refusal by the
    /// app apart from a 404 or 403 the framework answers on its own, which has no body.
    /// </summary>
    public static async Task AssertRefusedAsync(HttpResponseMessage response, HttpStatusCode status)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace((await response.JsonAsync()).GetProperty("error").GetString()));
    }

    /// <summary>A 2xx answer; otherwise the failure shows the status and the body.</summary>
    public static async Task AssertSucceededAsync(HttpResponseMessage response) =>
        Assert.True(
            response.IsSuccessStatusCode,
            $"{response.RequestMessage?.Method} {response.RequestMessage?.RequestUri?.AbsolutePath} answered " +
            $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");

    /// <summary>The <c>id</c> of what a successful create returned.</summary>
    public static async Task<Guid> IdAsync(HttpResponseMessage response)
    {
        await AssertSucceededAsync(response);
        return (await response.JsonAsync()).GetProperty("id").GetGuid();
    }
}
