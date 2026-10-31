using System.Net;
using System.Text;
using System.Text.Json;
using VirtualBar.HttpTests.Support;

namespace VirtualBar.HttpTests.Uploads;

/// <summary>
/// The four upload endpoints (#68), sent multipart as the frontend sends them. A valid image is saved in the web root
/// (a temporary folder, <see cref="ApiFactory.WebRoot"/>) and served under <c>/uploads</c>. An SVG, or a file one byte
/// over the endpoint's limit, is refused with 400 and leaves no file behind.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class UploadTests(ApiHosts hosts)
{
    public enum Endpoint
    {
        BottleImage,
        Avatar,
        NewsCover,
        WishListImage,
    }

    private const int Megabyte = 1024 * 1024;

    // Markup the browser would run if it were ever served from our origin: the reason SVG is not accepted.
    private static readonly byte[] Svg =
        Encoding.UTF8.GetBytes("<svg xmlns=\"http://www.w3.org/2000/svg\"><script>alert(1)</script></svg>");

    private ApiFactory Api => hosts.ConfirmationOff;

    [SqlServerTheory]
    [InlineData(Endpoint.BottleImage)]
    [InlineData(Endpoint.Avatar)]
    [InlineData(Endpoint.NewsCover)]
    [InlineData(Endpoint.WishListImage)]
    public async Task Upload_WhenTheFileIsAPngImage_SavesItAndServesItUnderUploads(Endpoint endpoint)
    {
        // The file name claims .html: the stored extension has to come from the checked content type, never from it.
        var response = await UploadAsync(endpoint, CollectorApi.ImageForm(CollectorApi.Png, fileName: "image.html"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var url = UrlOf(endpoint, await response.JsonAsync());
        Assert.StartsWith("/uploads/", url);
        Assert.EndsWith(".png", url);
        Assert.Equal(CollectorApi.Png, await File.ReadAllBytesAsync(Path.Combine(Api.WebRoot, url.TrimStart('/'))));

        var served = await Api.CreateClient().GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, served.StatusCode);
        Assert.Equal("image/png", served.Content.Headers.ContentType?.MediaType);
        Assert.Equal(CollectorApi.Png, await served.Content.ReadAsByteArrayAsync());
    }

    [SqlServerTheory]
    [InlineData(Endpoint.BottleImage)]
    [InlineData(Endpoint.Avatar)]
    [InlineData(Endpoint.NewsCover)]
    [InlineData(Endpoint.WishListImage)]
    public async Task Upload_WhenTheFileIsAnSvg_Returns400AndSavesNothing(Endpoint endpoint)
    {
        var filesBefore = FilesInWebRoot();

        var response = await UploadAsync(endpoint, CollectorApi.ImageForm(Svg, "image/svg+xml", "image.svg"));

        await CollectorApi.AssertRefusedAsync(response, HttpStatusCode.BadRequest);
        Assert.Equal(filesBefore, FilesInWebRoot());
    }

    [SqlServerTheory]
    [InlineData(Endpoint.BottleImage)]
    [InlineData(Endpoint.Avatar)]
    [InlineData(Endpoint.NewsCover)]
    [InlineData(Endpoint.WishListImage)]
    public async Task Upload_WhenTheFileIsOneByteOverTheLimit_Returns400AndSavesNothing(Endpoint endpoint)
    {
        var oversized = new byte[MaxBytes(endpoint) + 1];
        CollectorApi.Png.CopyTo(oversized, 0);
        var filesBefore = FilesInWebRoot();

        var response = await UploadAsync(endpoint, CollectorApi.ImageForm(oversized));

        await CollectorApi.AssertRefusedAsync(response, HttpStatusCode.BadRequest);
        Assert.Equal(filesBefore, FilesInWebRoot());
    }

    /// <summary>Sends <paramref name="form"/> to the endpoint as someone allowed to use it: an owner, or an admin.</summary>
    private async Task<HttpResponseMessage> UploadAsync(Endpoint endpoint, HttpContent form)
    {
        switch (endpoint)
        {
            case Endpoint.BottleImage:
                var owner = await Collector.RegisterAsync(Api, "uploader");
                return await owner.Client.PostAsync($"/api/bottles/{await owner.AddBottleAsync()}/images", form);
            case Endpoint.Avatar:
                return await (await Collector.RegisterAsync(Api, "uploader")).Client.PostAsync("/api/users/me/avatar", form);
            case Endpoint.NewsCover:
                return await (await Collector.RegisterAdminAsync(Api, "admin")).Client.PostAsync("/api/news/upload-cover", form);
            case Endpoint.WishListImage:
                return await (await Collector.RegisterAsync(Api, "uploader")).Client.PostAsync("/api/wishlist/image", form);
            default:
                throw new ArgumentOutOfRangeException(nameof(endpoint), endpoint, null);
        }
    }

    // The avatar endpoint answers with the updated profile; the other three with just the URL.
    private static string UrlOf(Endpoint endpoint, JsonElement body) =>
        body.GetProperty(endpoint == Endpoint.Avatar ? "avatarUrl" : "url").GetString()!;

    // The limits the endpoints document: 10 MB for bottle images and news covers, 5 MB for avatars and wishes.
    private static int MaxBytes(Endpoint endpoint) =>
        endpoint is Endpoint.BottleImage or Endpoint.NewsCover ? 10 * Megabyte : 5 * Megabyte;

    private List<string> FilesInWebRoot() =>
        Directory.GetFiles(Api.WebRoot, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal).ToList();
}
