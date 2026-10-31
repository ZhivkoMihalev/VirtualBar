# 10 — Backend tests (written LAST)

> Phase **A** · Depends on: **01–07, 09** · Read `00-OVERVIEW.md` first.

## Goal
**All** backend unit tests for Collection Value, written **after the whole backend is implemented** (per the
user's testing-last decision). One `<Service>Tests` class per service, **100% branch** for every new service
method (CLAUDE.md). No backend tests are written during slices 1–7/9 — only here.

## Recover from stash (test scaffolds)
`VirtualBar.Tests/Services/FakeHttpHandler.cs`, `StubPriceProvider.cs`, `StubPriceEstimationService.cs`,
`ThrowingPriceEstimationService.cs`, `TestProvider.cs`, and the `*Tests` skeletons.

## Coverage checklist (the "Test targets" gathered from each slice)
- **`ProductKey`** (slice 02) — canonicalization, equality of identical bottles, barcode-over-text precedence, null/empty.
- **`PriceProviderBase`** (slice 03) — FX→base conversion, median/percentile aggregation, error → null swallow.
- **`ClaudeMarketResearchProvider`** (slice 03, mock `HttpMessageHandler`) — happy path → min–max + sources;
  `found=false` → null; missing citations → null; `pause_turn` then `end_turn`; budget exhausted → null;
  sanity reject (min>max / negative) → null; malformed JSON → null; non-2xx → null. Canned Anthropic responses
  with `web_search_tool_result` + `citations`.
- **`InternalMarketPriceProvider`** (slice 04) — sample-count branches (High / Medium / Low / none), currency mix,
  soft-delete exclusion, no-data → null.
- **`PriceEstimationService`** + decorator (slice 05) — cache hit / miss / stale (**no Claude call on a fresh
  hit**), source-priority (Internal-with-samples chosen over Claude; Claude when Internal empty), sources
  persisted, both-null → None, Sealed-only total, provider exception swallowed.
- **`PreWarmRefreshJob`** (slice 06) — top-N skips fresh snapshots, daily-budget stop, TTL respect, cancellation,
  batch vs sequential path.
- **Cost guardrails** (slice 09) — budget counter (spend → short-circuit, daily reset), citation-required gate
  (no sources → None), sanity-bound rejection, fail-soft on tool-disabled error.
- **`PricesController`** (slice 07) — smoke test per action where logic warrants.

## Conventions (CLAUDE.md)
One `<Service>Tests` per service in `VirtualBar.Tests/Services`; naming `<Method>_When<Condition>_<Outcome>`;
isolated InMemory DB per test (`Guid.NewGuid()` name); mock only `ICurrentUser`, `INotificationService`,
`HttpMessageHandler`. EF InMemory by default; SQLite in-memory only if `ExecuteUpdate/DeleteAsync` is used.

## Gate
`dotnet test VirtualBar.Tests/VirtualBar.Tests.csproj` → **`Failed: 0`**; **100% branch** for every new service
method (coverage run: `--collect:"XPlat Code Coverage"`).
