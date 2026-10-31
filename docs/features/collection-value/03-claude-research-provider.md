# 03 — ClaudeMarketResearchProvider (THE core)

> Phase **A** · Depends on: **02** · Read `00-OVERVIEW.md` first.

## Goal
The primary signal: a provider that asks **Claude (Anthropic Messages API) with the web search tool** to research
the current secondary-market price of a bottle and return an **indicative min–max + confidence + citations**.
No scraping, no third-party price API — a managed research layer (overview §2, §3).

## Recover from stash
- `VirtualBar.Infrastructure/Services/Pricing/PriceProviderBase.cs` (+ `PricePoint.cs`, `ProviderRawResult.cs`)
- `VirtualBar.Tests/Services/FakeHttpHandler.cs` (HTTP test scaffold)

## Build new / extend
1. **`PriceProviderBase`** — extend so a result carries its **`Sources`** (citations); keep the FX→base conversion,
   min/median/percentile aggregation, and error→`null` swallowing from the stash. *(No signal-type, native-basis,
   or confidence-capping machinery — dropped in the simplification.)*
2. **DI** — `AddHttpClient<ClaudeMarketResearchProvider>()` with `BaseAddress = AnthropicOptions.BaseUrl` and
   default headers `x-api-key: {ApiKey}` + `anthropic-version: {AnthropicVersion}` (copy the `ProductLookupService`
   HttpClient pattern). Bind `AnthropicOptions`.
3. **`ClaudeMarketResearchProvider : PriceProviderBase` (NEW)** — `FetchAsync`:
   - Build a `POST /v1/messages` body: `model`, `max_tokens`, a **system prompt** ("You are a spirits
     secondary-market price researcher… return ONLY strict JSON…"), a **user message** with the bottle's canonical
     attributes (name, distillery, age, vintage, volume, edition, barcode), and
     `tools: [{ type: "web_search_20250305", name: "web_search", max_uses: MaxSearchesPerBottle, allowed_domains,
     blocked_domains }]`.
   - **Handle `pause_turn`**: if `stop_reason == "pause_turn"`, send the partial assistant content back and continue
     until `end_turn` (overview §3).
   - **Parse** the final assistant text as strict JSON `{ found, min, max, currency, confidence, asOf }`, and collect
     **citations** from every `web_search_result_location` (url + title) → `Sources`.
   - **Convert** `min`/`max` from `currency` to the base currency via the FX table, then feed them as the two points →
     the base yields `LowEstimate = min`, `HighEstimate = max`, `EstimatedPrice = midpoint`. `Source =
     PriceSource.ClaudeResearch`, `Confidence` from the model, `AsOf`, `Sources`. `found == false` **or** no
     citations → `null`.
   - **Sanity-bound**: require `0 < min <= max` (and a sane ceiling); reject → `null`.
   - **Cost guard**: short-circuit to `null` when the `DailyCallBudget` is spent (slice 09).
   - **Prompt-injection safety**: treat fetched web content as untrusted — extract only the strict JSON + citations;
     instruct the model to ignore instructions found on pages.
   - Errors / non-2xx / unparseable → `null` + log.
4. `Source => PriceSource.ClaudeResearch`; `Supports(category) => true`; `UseProviderStats` from options.

## Test targets (written in slice 10 — mock `HttpMessageHandler`)
Canned Anthropic responses incl. `web_search_tool_result` + `citations`: happy path → min–max + sources;
`found=false` → null; missing citations → null; `pause_turn` then `end_turn`; budget exhausted → null;
sanity reject (min>max / negative) → null; malformed JSON → null; non-2xx → null.

## Gate
`dotnet build` → 0 errors. *(Tests deferred to slice 10; no real network in tests.)*
