using System.Net;
using VirtualBar.Application.Common;
using VirtualBar.Application.DTOs.Pricing;
using VirtualBar.Application.Interfaces;

namespace VirtualBar.Tests.Services.Pricing;

/// <summary>
/// A test <see cref="HttpMessageHandler"/> that returns canned responses. The responder receives the
/// request and a zero-based call index so a test can vary the reply per call (e.g. pause_turn then end_turn).
/// </summary>
public sealed class FakeHttpHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, int, HttpResponseMessage> respond;

    public int CallCount { get; private set; }

    public FakeHttpHandler(Func<HttpRequestMessage, int, HttpResponseMessage> respond) => this.respond = respond;

    public FakeHttpHandler(Func<HttpRequestMessage, HttpResponseMessage> respond)
        : this((request, _) => respond(request))
    {
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = respond(request, CallCount);
        CallCount++;
        return Task.FromResult(response);
    }

    public static HttpResponseMessage JsonOk(string json) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json"),
        };
}

/// <summary>A <see cref="TimeProvider"/> whose UTC clock can be set/advanced to test day rollover.</summary>
public sealed class MutableTimeProvider(DateTimeOffset now) : TimeProvider
{
    private DateTimeOffset utcNow = now;

    public override DateTimeOffset GetUtcNow() => utcNow;

    public void Advance(TimeSpan by) => utcNow = utcNow.Add(by);
}

/// <summary>
/// A recording stub for <see cref="IPriceEstimationService"/>, used to test the decorator and controller
/// in isolation. Records call counts and the last user id, and can be set to throw if called.
/// </summary>
public sealed class StubPriceEstimationService : IPriceEstimationService
{
    public bool ThrowIfCalled { get; set; }

    public int BottleCalls { get; private set; }

    public int CachedCalls { get; private set; }

    public int CollectionCalls { get; private set; }

    public Guid LastCollectionUserId { get; private set; }

    public Result<PriceEstimateDto> BottleResult { get; set; } = Result<PriceEstimateDto>.Ok(new PriceEstimateDto());

    public Result<PriceEstimateDto> CachedResult { get; set; } = Result<PriceEstimateDto>.Ok(new PriceEstimateDto());

    public Result<CollectionValueDto> CollectionResult { get; set; } = Result<CollectionValueDto>.Ok(new CollectionValueDto());

    /// <summary>When set, <see cref="GetBottleEstimateAsync"/> throws this for the matching bottle id.</summary>
    public Guid? ThrowForBottleId { get; set; }

    /// <summary>The exception thrown for <see cref="ThrowForBottleId"/> (defaults to a generic failure).</summary>
    public Exception BottleException { get; set; } = new InvalidOperationException("Simulated research failure.");

    public Task<Result<PriceEstimateDto>> GetBottleEstimateAsync(Guid bottleId, CancellationToken cancellationToken)
    {
        Guard();
        BottleCalls++;
        if (ThrowForBottleId == bottleId)
            throw BottleException;
        return Task.FromResult(BottleResult);
    }

    public Task<Result<PriceEstimateDto>> GetCachedBottleEstimateAsync(Guid bottleId, CancellationToken cancellationToken)
    {
        Guard();
        CachedCalls++;
        return Task.FromResult(CachedResult);
    }

    public Task<Result<CollectionValueDto>> GetCollectionValueAsync(Guid userId, CancellationToken cancellationToken)
    {
        Guard();
        CollectionCalls++;
        LastCollectionUserId = userId;
        return Task.FromResult(CollectionResult);
    }

    private void Guard()
    {
        if (ThrowIfCalled)
            throw new InvalidOperationException("The inner service should not have been called.");
    }
}
