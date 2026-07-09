using Microsoft.Extensions.Options;
using VirtualBar.Application.Options;

namespace VirtualBar.Infrastructure.Services.Pricing;

/// <summary>
/// The shared, thread-safe daily spend cap for billed Anthropic calls (slice 09 · cost guardrails).
/// Both the research provider (slice 03) and the pre-warm job (slice 06) reserve from this single counter,
/// so total spend never exceeds <see cref="AnthropicOptions.DailyCallBudget"/> within one UTC day. The
/// counter resets automatically at UTC midnight. Registered as a singleton so the count is process-wide.
/// </summary>
public sealed class AnthropicDailyCallBudget(
    IOptions<AnthropicOptions> anthropicOptions,
    TimeProvider timeProvider)
{
    private readonly object gate = new();

    private DateOnly currentDay;

    private int callsToday;

    /// <summary>
    /// Atomically reserves one call against today's budget. Returns <c>true</c> when a call may proceed
    /// (recording the spend), or <c>false</c> when the budget for the current UTC day is exhausted — in
    /// which case the caller must short-circuit without making a (billed) request.
    /// </summary>
    public bool TryConsume()
    {
        var budget = anthropicOptions.Value.DailyCallBudget;

        lock (gate)
        {
            RollOverIfNewDay();

            if (callsToday >= budget)
                return false;

            callsToday++;
            return true;
        }
    }

    /// <summary>The number of calls still available for the current UTC day (never negative).</summary>
    public int Remaining()
    {
        var budget = anthropicOptions.Value.DailyCallBudget;

        lock (gate)
        {
            RollOverIfNewDay();
            var remaining = budget - callsToday;
            return remaining > 0 ? remaining : 0;
        }
    }

    private void RollOverIfNewDay()
    {
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        if (today != currentDay)
        {
            currentDay = today;
            callsToday = 0;
        }
    }
}
