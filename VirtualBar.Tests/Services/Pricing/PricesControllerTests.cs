using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using VirtualBar.Api.Controllers;
using VirtualBar.Application.Common;
using VirtualBar.Application.DTOs.Pricing;
using VirtualBar.Application.Interfaces;

namespace VirtualBar.Tests.Services.Pricing;

public sealed class PricesControllerTests
{
    private static ICurrentUser CurrentUser(Guid userId)
    {
        var mock = new Mock<ICurrentUser>();
        mock.Setup(u => u.UserId).Returns(userId);
        return mock.Object;
    }

    [Fact]
    public async Task GetBottleEstimate_WhenCached_ReturnsOk()
    {
        var dto = new PriceEstimateDto { EstimatedPrice = 150m };
        var stub = new StubPriceEstimationService { CachedResult = Result<PriceEstimateDto>.Ok(dto) };
        var controller = new PricesController(stub, CurrentUser(Guid.NewGuid()));

        var result = await controller.GetBottleEstimate(Guid.NewGuid(), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Same(dto, ok.Value);
    }

    [Fact]
    public async Task GetBottleEstimate_WhenNoEstimateCached_ReturnsNoContent()
    {
        var stub = new StubPriceEstimationService { CachedResult = Result<PriceEstimateDto>.Ok(null!) };
        var controller = new PricesController(stub, CurrentUser(Guid.NewGuid()));

        var result = await controller.GetBottleEstimate(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task GetBottleEstimate_WhenBottleMissing_ReturnsNotFound()
    {
        var stub = new StubPriceEstimationService { CachedResult = Result<PriceEstimateDto>.NotFound("Bottle not found.") };
        var controller = new PricesController(stub, CurrentUser(Guid.NewGuid()));

        var result = await controller.GetBottleEstimate(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task GetCollectionValue_ReturnsOkForCurrentUser()
    {
        var userId = Guid.NewGuid();
        var value = new CollectionValueDto { Currency = "EUR", TotalValue = 500m };
        var stub = new StubPriceEstimationService { CollectionResult = Result<CollectionValueDto>.Ok(value) };
        var controller = new PricesController(stub, CurrentUser(userId));

        var result = await controller.GetCollectionValue(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Same(value, ok.Value);
        Assert.Equal(userId, stub.LastCollectionUserId);
    }

    [Fact]
    public async Task GetCollectionValue_WhenForbidden_ReturnsForbidden()
    {
        var stub = new StubPriceEstimationService { CollectionResult = Result<CollectionValueDto>.Forbidden("nope") };
        var controller = new PricesController(stub, CurrentUser(Guid.NewGuid()));

        var result = await controller.GetCollectionValue(CancellationToken.None);

        var status = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, status.StatusCode);
    }
}
