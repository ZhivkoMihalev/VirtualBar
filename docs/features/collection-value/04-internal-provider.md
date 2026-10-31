# 04 — InternalMarketProvider (community signal)

> Phase **A** · Depends on: **02** · Read `00-OVERVIEW.md` first.

## Goal
Value derived from **VirtualBar's own data** — listings (`AskingPrice`), accepted `Offers`, and future internal
sales — for matching bottles. 100% legal (it's our data), free, and **grows with the community** (overview §4.1).
Confidence is sample-count-driven.

## Recover from stash
- `VirtualBar.Infrastructure/Services/Pricing/InternalMarketPriceProvider.cs`
- `VirtualBar.Tests/Services/InternalMarketPriceProviderTests.cs`

## Build new / extend
1. EF query over non-deleted `Bottles` (with `AskingPrice`) and accepted `Offers` matching the canonical
   `ProductKey` (name / distillery / category / age / vintage / volume).
2. **Aggregate** the matched prices: convert to base via FX, then let `PriceProviderBase` compute median +
   percentiles → `LowEstimate` / `HighEstimate` / `EstimatedPrice`. `Source = PriceSource.Internal`; `Sources`
   empty (our own data, no external citation). *(No realized-vs-asking signal split — internal data is treated
   uniformly; volume drives confidence.)*
3. **Confidence by sample count:** `>= MinSamples` → `High`; `>= MinApproxSamples` → `Medium`; else `Low` (or
   `null` when there is no data at all).

## Test targets (written in slice 10)
sample-count branches (High / Medium / Low / none), currency mix, soft-delete exclusion, no-data → `null`.

## Gate
`dotnet build` → 0 errors. *(Tests deferred to slice 10.)*
