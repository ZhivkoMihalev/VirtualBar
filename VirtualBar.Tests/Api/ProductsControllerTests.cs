using Microsoft.AspNetCore.Mvc;
using Moq;
using VirtualBar.Api.Controllers;
using VirtualBar.Application.Common;
using VirtualBar.Application.DTOs.Products;
using VirtualBar.Application.DTOs.Reviews;
using VirtualBar.Application.Interfaces;
using VirtualBar.Domain.Enums;

namespace VirtualBar.Tests.Api;

/// <summary>
/// Every action only forwards their arguments and map the <see cref="Result{T}"/> to a status code;
/// the guards and queries behind them are covered in the service test classes.
/// </summary>
public sealed class ProductsControllerTests
{
    private readonly Mock<IProductCatalogService> catalog = new();

    private readonly Mock<IProductLookupService> lookup = new();

    private ProductsController CreateController() => new(lookup.Object, catalog.Object);

    [Fact]
    public async Task LookupBarcode_WhenFound_ReturnsOkWithProduct()
    {
        var product = new BarcodeProductDto();
        lookup
            .Setup(s => s.LookupByBarcodeAsync("5000281005409", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<BarcodeProductDto>.Ok(product));

        var result = await CreateController().LookupBarcode("5000281005409", CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Same(product, ok.Value);
    }

    [Fact]
    public async Task LookupBarcode_WhenNotFound_ReturnsNotFound()
    {
        lookup
            .Setup(s => s.LookupByBarcodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<BarcodeProductDto>.NotFound("Product not found."));

        var result = await CreateController().LookupBarcode("00000000", CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task Search_WhenServiceSucceeds_ReturnsOkAndForwardsArguments()
    {
        List<ProductDto> products = [new ProductDto { Name = "Lagavulin 16" }];
        catalog
            .Setup(s => s.SearchAsync("lag", SpiritCategory.Whisky, 51, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<List<ProductDto>>.Ok(products));

        var result = await CreateController().Search("lag", SpiritCategory.Whisky, 51, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Same(products, ok.Value);
    }

    [Fact]
    public async Task Search_WhenServiceFails_ReturnsBadRequest()
    {
        catalog
            .Setup(s => s.SearchAsync(It.IsAny<string>(), It.IsAny<SpiritCategory?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<List<ProductDto>>.Fail("Search term must be at least 2 characters."));

        var result = await CreateController().Search("a", null, 20, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Browse_WhenServiceSucceeds_ReturnsOkWithPageAndForwardsArguments()
    {
        var page = new PagedResult<ProductCardDto> { Items = [new ProductCardDto { Name = "Lagavulin 16" }], HasMore = true };
        catalog
            .Setup(s => s.BrowseAsync("lag", SpiritCategory.Whisky, 2, 12, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PagedResult<ProductCardDto>>.Ok(page));

        var result = await CreateController().Browse("lag", SpiritCategory.Whisky, 2, 12, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Same(page, ok.Value);
    }

    [Fact]
    public async Task Browse_WhenServiceFails_ReturnsBadRequest()
    {
        catalog
            .Setup(s => s.BrowseAsync(It.IsAny<string?>(), It.IsAny<SpiritCategory?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PagedResult<ProductCardDto>>.Fail("Search term must be at least 2 characters."));

        var result = await CreateController().Browse("a", null, 1, 24, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task GetById_WhenServiceSucceeds_ReturnsOkWithProduct()
    {
        var id = Guid.NewGuid();
        var detail = new ProductDetailDto { Id = id, Name = "Lagavulin 16" };
        catalog
            .Setup(s => s.GetProductAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ProductDetailDto>.Ok(detail));

        var result = await CreateController().GetById(id, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Same(detail, ok.Value);
    }

    [Fact]
    public async Task GetById_WhenProductMissing_ReturnsNotFound()
    {
        catalog
            .Setup(s => s.GetProductAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ProductDetailDto>.NotFound("Product not found."));

        var result = await CreateController().GetById(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task GetReviews_WhenServiceSucceeds_ReturnsOkWithPageAndForwardsArguments()
    {
        var id = Guid.NewGuid();
        var page = new PagedResult<BottleReviewDto> { Items = [new BottleReviewDto { Score = 90 }] };
        catalog
            .Setup(s => s.GetProductReviewsAsync(id, 3, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PagedResult<BottleReviewDto>>.Ok(page));

        var result = await CreateController().GetReviews(id, 3, 5, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Same(page, ok.Value);
    }

    [Fact]
    public async Task GetReviews_WhenProductMissing_ReturnsNotFound()
    {
        catalog
            .Setup(s => s.GetProductReviewsAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PagedResult<BottleReviewDto>>.NotFound("Product not found."));

        var result = await CreateController().GetReviews(Guid.NewGuid(), 1, 10, CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task GetReviews_WhenPageOutOfRange_ReturnsBadRequest()
    {
        catalog
            .Setup(s => s.GetProductReviewsAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PagedResult<BottleReviewDto>>.Fail("Page is out of range."));

        var result = await CreateController().GetReviews(Guid.NewGuid(), 1001, 10, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }
}
