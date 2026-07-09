using Moq;
using VirtualBar.Application.Common;
using VirtualBar.Application.DTOs.Pricing;
using VirtualBar.Application.Interfaces;
using VirtualBar.Infrastructure.Decorators;

namespace VirtualBar.Tests.Services.Pricing;

public sealed class PriceEstimationValidationDecoratorTests
{
    private static ICurrentUser CurrentUser(Guid userId)
    {
        var mock = new Mock<ICurrentUser>();
        mock.Setup(u => u.UserId).Returns(userId);
        return mock.Object;
    }

    private static CancellationToken Cancelled()
    {
        var cts = new CancellationTokenSource();
        cts.Cancel();
        return cts.Token;
    }

    [Fact]
    public async Task GetCollectionValueAsync_WhenDifferentUser_ReturnsForbiddenWithoutCallingInner()
    {
        var inner = new StubPriceEstimationService();
        var decorator = new PriceEstimationValidationDecorator(inner, CurrentUser(Guid.NewGuid()));

        var result = await decorator.GetCollectionValueAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ErrorCode.Forbidden, result.ErrorCode);
        Assert.Equal(0, inner.CollectionCalls);
    }

    [Fact]
    public async Task GetCollectionValueAsync_WhenSameUser_CallsInner()
    {
        var userId = Guid.NewGuid();
        var inner = new StubPriceEstimationService
        {
            CollectionResult = Result<CollectionValueDto>.Ok(new CollectionValueDto { Currency = "EUR" }),
        };
        var decorator = new PriceEstimationValidationDecorator(inner, CurrentUser(userId));

        var result = await decorator.GetCollectionValueAsync(userId, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(1, inner.CollectionCalls);
        Assert.Equal(userId, inner.LastCollectionUserId);
    }

    [Fact]
    public async Task GetCollectionValueAsync_WhenCancelled_ThrowsWithoutCallingInner()
    {
        var inner = new StubPriceEstimationService { ThrowIfCalled = true };
        var decorator = new PriceEstimationValidationDecorator(inner, CurrentUser(Guid.NewGuid()));

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => decorator.GetCollectionValueAsync(Guid.NewGuid(), Cancelled()));
    }

    [Fact]
    public async Task GetBottleEstimateAsync_PassesThroughToInner()
    {
        var inner = new StubPriceEstimationService
        {
            BottleResult = Result<PriceEstimateDto>.Ok(new PriceEstimateDto { EstimatedPrice = 42m }),
        };
        var decorator = new PriceEstimationValidationDecorator(inner, CurrentUser(Guid.NewGuid()));

        var result = await decorator.GetBottleEstimateAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(42m, result.Data!.EstimatedPrice);
        Assert.Equal(1, inner.BottleCalls);
    }

    [Fact]
    public async Task GetBottleEstimateAsync_WhenCancelled_ThrowsWithoutCallingInner()
    {
        var inner = new StubPriceEstimationService { ThrowIfCalled = true };
        var decorator = new PriceEstimationValidationDecorator(inner, CurrentUser(Guid.NewGuid()));

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => decorator.GetBottleEstimateAsync(Guid.NewGuid(), Cancelled()));
    }

    [Fact]
    public async Task GetCachedBottleEstimateAsync_PassesThroughToInner()
    {
        var inner = new StubPriceEstimationService
        {
            CachedResult = Result<PriceEstimateDto>.Ok(new PriceEstimateDto { EstimatedPrice = 7m }),
        };
        var decorator = new PriceEstimationValidationDecorator(inner, CurrentUser(Guid.NewGuid()));

        var result = await decorator.GetCachedBottleEstimateAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(7m, result.Data!.EstimatedPrice);
        Assert.Equal(1, inner.CachedCalls);
    }

    [Fact]
    public async Task GetCachedBottleEstimateAsync_WhenCancelled_ThrowsWithoutCallingInner()
    {
        var inner = new StubPriceEstimationService { ThrowIfCalled = true };
        var decorator = new PriceEstimationValidationDecorator(inner, CurrentUser(Guid.NewGuid()));

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => decorator.GetCachedBottleEstimateAsync(Guid.NewGuid(), Cancelled()));
    }
}
