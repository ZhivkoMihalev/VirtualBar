# 06 — Pre-warm job

> Phase **A** · Depends on: **05** · Read `00-OVERVIEW.md` first.

## Goal
Decouple coverage from user count (overview §4.4–4.5) **within a strict cost budget**: proactively research the
most-relevant bottles so estimates exist before users ask — without blowing the Claude bill.

## Recover from stash
- `VirtualBar.Infrastructure/Services/Pricing/PriceRefreshBackgroundService.cs` (+ tests) → becomes `PreWarmRefreshJob`

## Build new / extend
1. **`PreWarmRefreshJob` (IHostedService)** — periodic every `RefreshIntervalHours` when `RefreshEnabled`:
   - Compute the **top-`PreWarmTopNBottles`** most-owned / most-searched **canonical** bottles that have a
     missing or stale (`> TTL`) snapshot.
   - Research each via the orchestrator (which caches the result), **respecting the Anthropic `DailyCallBudget`**
     and stopping when it is spent. Never refresh a fresh snapshot (TTL).
2. **Optional Batch API** — for larger top-N, submit the batch via the Messages **Batches API** (web search is
   supported there at the same price, overview §3) for throughput/cost; otherwise sequential with a small delay.
3. Idempotent and cancellation-aware; follows the `Program.cs` startup-scope pattern (overview §5).

> No external "seed import" (the old Systembolaget/auction seed is gone — those sources are off the table).

## Test targets (written in slice 10)
top-N selection (skips fresh snapshots), daily-budget stop, TTL respect, cancellation, batch vs sequential path.
100% branch.

## Gate
`dotnet build` → 0 errors; `dotnet run` pre-warms within budget without error. *(Tests deferred to slice 10.)*
