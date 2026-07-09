# 02 — Contracts + Canonical ProductKey

> Phase **A** · Depends on: **01** · Read `00-OVERVIEW.md` first.

## Goal
The canonical product signature (identical bottles → one shared lookup), the provider/orchestrator interfaces,
the DTOs (now carrying **sources**), and the per-provider options — including the new **`AnthropicOptions`**.
**Option classes are shape-only; values live in `appsettings.json`** (memory: config-values-in-appsettings).

## Recover from stash
- `VirtualBar.Application/Common/ProductKey.cs` (+ `VirtualBar.Tests/Common/ProductKeyTests.cs`)
- `VirtualBar.Application/Interfaces/IPriceProvider.cs`, `IPriceEstimationService.cs`
- `VirtualBar.Application/DTOs/Pricing/*` — `PriceEstimateDto`, `CollectionValueDto`, `BottlePriceLineDto`, `PriceProviderInput`
- `VirtualBar.Application/Options/PricingOptions.cs`, `InternalProviderOptions.cs`

## Build new / extend
1. **`PriceEstimateDto`** — `EstimatedPrice`, `LowEstimate`, `HighEstimate` (the min–max), `Currency` (base),
   `Confidence`, `Source`, `SampleSize`, `AsOf`, and **`IReadOnlyList<PriceCitation> Sources`** (url + title).
   *(No `SignalType`, no native fields.)*
2. **`PriceCitation`** record `(string Url, string Title)` — own file (Application/DTOs/Pricing).
3. **`PriceProviderInput`** — carries name / distillery / category / age / vintage / volume / barcode / canonical key.
4. **`AnthropicOptions` (NEW, shape-only)** — `UseProviderStats`, `BaseUrl`, `ApiKey`, `Model`, `AnthropicVersion`,
   `MaxSearchesPerBottle`, `DailyCallBudget`, `AllowedDomains[]`, `BlockedDomains[]`. Bound from the `"Anthropic"`
   section; `ApiKey` from `appsettings.Development.json` / user-secrets.
5. **`PricingOptions`** — add `PreWarmTopNBottles`, `RefreshIntervalHours`, `RefreshEnabled`.
   **`InternalProviderOptions`** — `MinSamples`, `MinApproxSamples`.

## Test targets (written in slice 10)
`ProductKeyTests` (recover): canonicalization, equality of identical bottles, barcode-over-text precedence,
null/empty handling.

## Gate
`dotnet build` → 0 errors. *(Tests deferred to slice 10 — testing-last.)*
