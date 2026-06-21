using System.Net;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Options;
using Moq;
using VirtualBar.Application.Common;
using VirtualBar.Application.Interfaces;
using VirtualBar.Infrastructure.Decorators;
using VirtualBar.Infrastructure.Options;
using VirtualBar.Infrastructure.Services;

namespace VirtualBar.Tests.Services;

public sealed class ProductLookupServiceTests
{
    private static IProductLookupService CreateService(HttpMessageHandler handler, string? webRootPath = null)
    {
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.example.com") };
        var mockEnv = new Mock<IWebHostEnvironment>();
        mockEnv.Setup(e => e.WebRootPath).Returns(webRootPath ?? Path.GetTempPath());
        mockEnv.Setup(e => e.ContentRootPath).Returns(Path.GetTempPath());
        var opts = Options.Create(new ProductLookupOptions { LookupUrl = "https://api.example.com/lookup" });
        var inner = new ProductLookupService(http, mockEnv.Object, opts);

        return new ProductValidationDecorator(inner);
    }

    private sealed class FakeHttpHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;
        public FakeHttpHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) => _respond = respond;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(_respond(request));
    }

    private static HttpResponseMessage JsonOk(string json)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }

    private static IProductLookupService ServiceReturning(string json, string? webRootPath = null)
        => CreateService(new FakeHttpHandler(_ => JsonOk(json)), webRootPath);

    private const string ProductImageUrl = "https://img.example.com/whisky.jpg";

    [Fact]
    public async Task LookupByBarcodeAsync_WhenBarcodeEmpty_ReturnsFail()
    {
        var service = ServiceReturning("{}");

        var result = await service.LookupByBarcodeAsync("", CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Barcode is required.", result.Error);
    }

    [Fact]
    public async Task LookupByBarcodeAsync_WhenBarcodeWhitespace_ReturnsFail()
    {
        var service = ServiceReturning("{}");

        var result = await service.LookupByBarcodeAsync("   ", CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Barcode is required.", result.Error);
    }

    [Fact]
    public async Task LookupByBarcodeAsync_WhenInnerThrowsNonCancellation_ReturnsFail()
    {
        var handler = new FakeHttpHandler(_ => throw new InvalidOperationException("boom"));
        var service = CreateService(handler);

        var result = await service.LookupByBarcodeAsync("12345", CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Barcode lookup failed. Please try again.", result.Error);
    }

    [Fact]
    public async Task LookupByBarcodeAsync_WhenInnerThrowsCancellation_Rethrows()
    {
        var handler = new FakeHttpHandler(_ => throw new OperationCanceledException());
        var service = CreateService(handler);

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => service.LookupByBarcodeAsync("12345", CancellationToken.None));
    }

    [Fact]
    public async Task LookupByBarcodeAsync_WhenTokenCancelled_Throws()
    {
        var service = ServiceReturning("{}");
        var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => service.LookupByBarcodeAsync("12345", cts.Token));
    }

    [Fact]
    public async Task LookupByBarcodeAsync_WhenHttpFailure_ReturnsNotFound()
    {
        var handler = new FakeHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        var service = CreateService(handler);

        var result = await service.LookupByBarcodeAsync("12345", CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Product not found.", result.Error);
        Assert.Equal(ErrorCode.NotFound, result.ErrorCode);
    }

    [Fact]
    public async Task LookupByBarcodeAsync_WhenItemsNull_ReturnsNotFound()
    {
        var service = ServiceReturning("""{"code":"x","items":null}""");

        var result = await service.LookupByBarcodeAsync("12345", CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Product not found.", result.Error);
        Assert.Equal(ErrorCode.NotFound, result.ErrorCode);
    }

    [Fact]
    public async Task LookupByBarcodeAsync_WhenItemsEmpty_ReturnsNotFound()
    {
        var service = ServiceReturning("""{"code":"x","items":[]}""");

        var result = await service.LookupByBarcodeAsync("12345", CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Product not found.", result.Error);
        Assert.Equal(ErrorCode.NotFound, result.ErrorCode);
    }

    [Fact]
    public async Task LookupByBarcodeAsync_WhenValidNoImages_ReturnsProductWithoutImage()
    {
        var service = ServiceReturning(
            """{"code":"x","items":[{"title":"Glenfiddich 12","brand":"Glenfiddich","description":"Single malt 40% abv","size":"700ml","images":[]}]}""");

        var result = await service.LookupByBarcodeAsync("12345", CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("Glenfiddich 12", result.Data!.Name);
        Assert.Equal("Glenfiddich", result.Data.Brand);
        Assert.Equal(700, result.Data.VolumeMl);
        Assert.Equal(40.0, result.Data.AbvPercent);
        Assert.Null(result.Data.ImageUrl);
    }

    [Fact]
    public async Task LookupByBarcodeAsync_WhenImagesNull_ReturnsProductWithoutImage()
    {
        var service = ServiceReturning(
            """{"code":"x","items":[{"title":"Whisky","brand":"B","description":"d","size":"700ml","images":null}]}""");

        var result = await service.LookupByBarcodeAsync("12345", CancellationToken.None);

        Assert.True(result.Success);
        Assert.Null(result.Data!.ImageUrl);
    }

    [Fact]
    public async Task LookupByBarcodeAsync_WhenImageDownloadFails_ReturnsProductWithoutImage()
    {
        var productJson =
            $$"""{"code":"x","items":[{"title":"Whisky 40%","brand":"B","description":"d","size":"700ml","images":["{{ProductImageUrl}}"]}]}""";
        var handler = new FakeHttpHandler(req =>
            req.RequestUri!.Host == "img.example.com"
                ? new HttpResponseMessage(HttpStatusCode.NotFound)
                : JsonOk(productJson));
        var service = CreateService(handler);

        var result = await service.LookupByBarcodeAsync("12345", CancellationToken.None);

        Assert.True(result.Success);
        Assert.Null(result.Data!.ImageUrl);
    }

    [Fact]
    public async Task LookupByBarcodeAsync_WhenImageDownloadSucceeds_ReturnsLocalImageUrl()
    {
        var productJson =
            $$"""{"code":"x","items":[{"title":"Whisky 40%","brand":"B","description":"d","size":"700ml","images":["{{ProductImageUrl}}"]}]}""";
        var handler = new FakeHttpHandler(req =>
            req.RequestUri!.Host == "img.example.com"
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(new byte[] { 1, 2, 3 }) }
                : JsonOk(productJson));
        var service = CreateService(handler);

        var result = await service.LookupByBarcodeAsync("12345", CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotNull(result.Data!.ImageUrl);
        Assert.StartsWith("/uploads/bottles/", result.Data.ImageUrl);
        Assert.EndsWith(".jpg", result.Data.ImageUrl);
    }

    [Fact]
    public async Task LookupByBarcodeAsync_WhenImageHasNoExtension_DefaultsToJpg()
    {
        const string noExtUrl = "https://img.example.com/image";
        var productJson =
            $$"""{"code":"x","items":[{"title":"Whisky 40%","brand":"B","description":"d","size":"700ml","images":["{{noExtUrl}}"]}]}""";
        var handler = new FakeHttpHandler(req =>
            req.RequestUri!.Host == "img.example.com"
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(new byte[] { 9 }) }
                : JsonOk(productJson));

        var service = CreateService(handler);

        var result = await service.LookupByBarcodeAsync("12345", CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotNull(result.Data!.ImageUrl);
        Assert.EndsWith(".jpg", result.Data.ImageUrl);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("unknown", null)]
    [InlineData("700ml", 700)]
    [InlineData("70cl", 700)]
    [InlineData("0.7l", 700)]
    [InlineData("1,75l", 1750)]
    public async Task LookupByBarcodeAsync_ParsesVolumeMl(string? size, int? expected)
    {
        var sizeJson = size is null ? "null" : $"\"{size}\"";
        var service = ServiceReturning(
            $$"""{"code":"x","items":[{"title":"Whisky","brand":"B","description":"d","size":{{sizeJson}},"images":[]}]}""");

        var result = await service.LookupByBarcodeAsync("12345", CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(expected, result.Data!.VolumeMl);
    }

    [Theory]
    [InlineData("Whisky", null)]
    [InlineData("Whisky 40%", 40.0)]
    [InlineData("Whisky 40.5% ABV", 40.5)]
    [InlineData("Whisky 4%", null)]
    [InlineData("Whisky 97%", null)]
    public async Task LookupByBarcodeAsync_ParsesAbv(string title, double? expected)
    {
        var service = ServiceReturning(
            $$"""{"code":"x","items":[{"title":"{{title}}","brand":"B","description":"","size":"700ml","images":[]}]}""");

        var result = await service.LookupByBarcodeAsync("12345", CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(expected, result.Data!.AbvPercent);
    }
}
