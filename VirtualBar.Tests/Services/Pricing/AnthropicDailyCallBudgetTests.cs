using Microsoft.Extensions.Options;
using VirtualBar.Application.Options;
using VirtualBar.Infrastructure.Services.Pricing;

namespace VirtualBar.Tests.Services.Pricing;

public sealed class AnthropicDailyCallBudgetTests
{
    private static AnthropicDailyCallBudget Create(int dailyBudget, TimeProvider timeProvider) =>
        new(Options.Create(new AnthropicOptions { DailyCallBudget = dailyBudget }), timeProvider);

    [Fact]
    public void TryConsume_WhenUnderBudget_ReturnsTrueAndReducesRemaining()
    {
        var budget = Create(2, new MutableTimeProvider(DateTimeOffset.UnixEpoch));

        Assert.Equal(2, budget.Remaining());
        Assert.True(budget.TryConsume());
        Assert.Equal(1, budget.Remaining());
        Assert.True(budget.TryConsume());
        Assert.Equal(0, budget.Remaining());
    }

    [Fact]
    public void TryConsume_WhenBudgetSpent_ReturnsFalse()
    {
        var budget = Create(1, new MutableTimeProvider(DateTimeOffset.UnixEpoch));

        Assert.True(budget.TryConsume());
        Assert.False(budget.TryConsume());
        Assert.Equal(0, budget.Remaining());
    }

    [Fact]
    public void TryConsume_WhenBudgetZero_ReturnsFalse()
    {
        var budget = Create(0, new MutableTimeProvider(DateTimeOffset.UnixEpoch));

        Assert.False(budget.TryConsume());
        Assert.Equal(0, budget.Remaining());
    }

    [Fact]
    public void TryConsume_AfterUtcDayRollover_ResetsCount()
    {
        var clock = new MutableTimeProvider(new DateTimeOffset(2026, 6, 30, 23, 0, 0, TimeSpan.Zero));
        var budget = Create(1, clock);

        Assert.True(budget.TryConsume());
        Assert.False(budget.TryConsume());

        clock.Advance(TimeSpan.FromDays(1));

        Assert.Equal(1, budget.Remaining());
        Assert.True(budget.TryConsume());
    }

    [Fact]
    public void Remaining_WhenOverConsumedConceptually_NeverNegative()
    {
        var budget = Create(1, new MutableTimeProvider(DateTimeOffset.UnixEpoch));

        budget.TryConsume();
        budget.TryConsume();

        Assert.Equal(0, budget.Remaining());
    }
}
