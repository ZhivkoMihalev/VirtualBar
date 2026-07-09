# 05 — Orchestrator + read-through cache

> Phase **A** · Depends on: **03, 04** · Read `00-OVERVIEW.md` first.

## Goal
`PriceEstimationService` — the read-through `PriceSnapshot` cache (5-day TTL) plus a **simple source-priority**
choice between the two providers, persisting the chosen estimate **with its sources**. The cache is
**cost-critical**: it caps how often we pay for a Claude call (overview §4.4). Collection value sums **Sealed
bottles only** (overview §4.7).

## Recover from stash
- `VirtualBar.Infrastructure/Services/Pricing/PriceEstimationService.cs`
- `VirtualBar.Infrastructure/Decorators/PriceEstimationValidationDecorator.cs`
- `VirtualBar.Tests/Services/PriceEstimationServiceTests.cs`, `PriceEstimationValidationDecoratorTests.cs`,
  `StubPriceProvider.cs`, `StubPriceEstimationService.cs`, `ThrowingPriceEstimationService.cs`, `TestProvider.cs`

## Build new / extend
1. **Cache first:** look up `PriceSnapshot` by `ProductKey`; a fresh (≤ TTL) hit returns immediately **without
   calling Claude**. Miss or stale → run the enabled providers (`UseProviderStats`).
2. **Source-priority selection** (overview §4a): if `InternalMarketProvider` returns a non-null result (it had
   ≥ `MinApproxSamples` of our own data) → **use it**; otherwise use `ClaudeMarketResearchProvider`; otherwise
   `None`. *(No signal-type ordering, no confidence capping.)*
3. **Persist** the chosen estimate + `Source` + `Confidence` + `AsOf` + **`SourcesJson`** to `PriceSnapshot`.
   No provider hit → `None` (UI shows "—").
4. **`CollectionValueDto`** — sum the latest snapshot per bottle for the user where `Condition == Sealed`;
   per-bottle estimates returned for all conditions, only the **total** is Sealed-only.

## Test targets (written in slice 10)
cache hit / miss / stale (**Claude NOT called on a fresh hit**); source-priority (Internal-with-samples chosen
over Claude; Claude used when Internal is empty); sources persisted; both-providers-null → `None`; Sealed-only
total; provider exception swallowed → skipped.

## Gate
`dotnet build` → 0 errors. *(Tests deferred to slice 10.)*
