using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VirtualBar.Domain.Entities;
using VirtualBar.HttpTests.Support;
using VirtualBar.Infrastructure.Migrations;
using VirtualBar.Infrastructure.Persistence;

namespace VirtualBar.HttpTests.Bottles;

/// <summary>
/// The data fix of #131 (<see cref="HideBottleImagesWithArbitraryUrls"/>), run on SQL Server against rows it must hide
/// or keep. Its T-SQL runs on deploy against production data, and the throwaway database only ever sees it on an empty
/// table, so these tests seed the rows (straight into the database: the API no longer stores such addresses), run the
/// migration's own SQL and read the bottles back over HTTP, as a viewer would see them.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class BottleImageCleanupTests(ApiHosts hosts)
{
    private ApiFactory Api => hosts.ConfirmationOff;

    [SqlServerTheory]
    [InlineData("https://tracker.example/pixel.png?bottle=1")]
    [InlineData("/uploads/bottles/../../appsettings.json")]
    [InlineData("/uploads/bottles/nested/image.png")]
    [InlineData("/uploads/bottles/a\\b.png")]
    [InlineData("/uploads/bottles/")]
    [InlineData("/uploads/wishlist/someone-else.png")]
    [InlineData("data:image/png;base64,iVBORw0KGgo=")]
    [InlineData("javascript:alert(1)")]
    public async Task Cleanup_WhenAnImageIsNeitherAnUploadNorAProductImage_HidesIt(string url)
    {
        var bottleId = await NewBottleAsync();
        await AddImagesAsync(bottleId, (url, true));

        await RunCleanupAsync();

        Assert.Empty(await ImagesAsync(bottleId));
    }

    [SqlServerFact]
    public async Task Cleanup_KeepsTheApisOwnUploadsAndProductImages()
    {
        var productImage = $"https://images.example.test/{Guid.NewGuid():N}.png";
        await CatalogData.SeedProductAsync(Api, productImage);
        var upload = $"/uploads/bottles/{Guid.NewGuid()}.jpg";
        var bottleId = await NewBottleAsync();
        await AddImagesAsync(bottleId, (upload, true), (productImage, false));

        await RunCleanupAsync();

        Assert.Equal(new (string, bool)[] { (upload, true), (productImage, false) }, await ImagesAsync(bottleId));
    }

    /// <summary>The feed shows only a primary image, so a bottle whose cover was hidden gets its next image as cover.</summary>
    [SqlServerFact]
    public async Task Cleanup_WhenTheCoverIsHidden_MakesTheNextRemainingImageTheCover()
    {
        var first = $"/uploads/bottles/{Guid.NewGuid()}.png";
        var second = $"/uploads/bottles/{Guid.NewGuid()}.png";
        var bottleId = await NewBottleAsync();
        await AddImagesAsync(bottleId, ("https://tracker.example/pixel.png", true), (first, false), (second, false));

        await RunCleanupAsync();

        Assert.Equal(new (string, bool)[] { (first, true), (second, false) }, await ImagesAsync(bottleId));
    }

    /// <summary>Only a cover the cleanup hid is replaced; a bottle that had no live cover before is left as it was.</summary>
    [SqlServerFact]
    public async Task Cleanup_WhenABottleHadNoCoverBefore_LeavesItWithoutOne()
    {
        var upload = $"/uploads/bottles/{Guid.NewGuid()}.png";
        var bottleId = await NewBottleAsync();
        await AddImagesAsync(bottleId, (upload, false), ("https://tracker.example/pixel.png", false));

        await RunCleanupAsync();

        Assert.Equal(new (string, bool)[] { (upload, false) }, await ImagesAsync(bottleId));
    }

    private async Task<Guid> NewBottleAsync() =>
        await (await Collector.RegisterAsync(Api, "collector")).AddBottleAsync();

    /// <summary>Adds the images in this gallery order, the way rows stored before the fix look.</summary>
    private async Task AddImagesAsync(Guid bottleId, params (string Url, bool IsPrimary)[] images)
    {
        await using var scope = Api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.BottleImages.AddRange(images.Select((image, index) => new BottleImage
        {
            BottleId = bottleId,
            Url = image.Url,
            IsPrimary = image.IsPrimary,
            SortOrder = index,
        }));
        await db.SaveChangesAsync();
    }

    private async Task RunCleanupAsync()
    {
        await using var scope = Api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.ExecuteSqlRawAsync(HideBottleImagesWithArbitraryUrls.CleanupSql);
    }

    /// <summary>The bottle's images as a visitor gets them, in gallery order.</summary>
    private async Task<List<(string Url, bool IsPrimary)>> ImagesAsync(Guid bottleId)
    {
        var response = await Api.CreateClient().GetAsync($"/api/bottles/{bottleId}");
        await CollectorApi.AssertSucceededAsync(response);
        return (await response.JsonAsync()).GetProperty("images").EnumerateArray()
            .Select(image => (image.GetProperty("url").GetString()!, image.GetProperty("isPrimary").GetBoolean()))
            .ToList();
    }
}
