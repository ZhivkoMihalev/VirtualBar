# Collection Value (Bottle Price Estimation) — OVERVIEW & SHARED CONTEXT

> **Read this first, before any slice.** Single source of truth for the decisions, the architecture,
> the conventions, and the risks. Each `NN-*.md` slice assumes you read this. Format mirrors
> `docs/shadcn-migration/`.

> **Approach — AI-research, simplified.** A ToS check of every free external price source showed that
> **caching + displaying their prices in our app is prohibited without a paid licence / written consent**,
> so direct integration is out. Market value is produced by a **Claude web-search research call** (Anthropic
> Messages API) returning an **indicative min–max with mandatory citations**, complemented by our own internal
> marketplace data. Two signals only — kept deliberately lean (no per-source provider zoo, no signal-type
> hierarchy, no native-basis bookkeeping).

---

## 1. Goal
An **indicative min–max market value per bottle** and a **total collection value** on the virtual bar. Always
**indicative (ориентировъчно)** — never an exact appraisal — and always shown with **range + confidence +
sources + "as of" date**.

## 2. Why this shape (ToS investigation — short version)
A verbatim ToS check of 6 free sources (Whisky.Auction, Scotch Whisky Auctions, Whisky Auctioneer, WhiskyHunter,
Systembolaget, Wine-Searcher trial) found that **every one prohibits caching + displaying its prices in a
third-party / commercial app** without a paid licence or written consent (and Wine-Searcher's only viable tier
is paid, $320+/mo). **The bottleneck is display/cache, not fetch.** So instead of integrating any single feed we
ask **Claude (with web search)** to research public sources and return a **synthesized indicative min–max + the
citations it used** — market research/synthesis, not republishing one source's feed, and Anthropic **mandates
citation display**. *Not legal advice; keep estimates indicative and always show sources.*

## 3. Verified Anthropic web-search facts (re-confirmed from official docs)
- **Tool:** `{ "type": "web_search_20250305", "name": "web_search", "max_uses": N }` (basic). Options:
  `max_uses`, `allowed_domains`, `blocked_domains`, `user_location`. (`web_search_20260318` adds dynamic
  filtering via the code-execution tool — optional later optimization.)
- **Endpoint:** `POST https://api.anthropic.com/v1/messages`, headers `x-api-key`, `anthropic-version: 2023-06-01`.
- **Models:** Opus 4.8 / Sonnet 4.6 / etc. **Default `claude-sonnet-4-6`** (cost/quality), configurable.
- **Pricing:** **$10 per 1,000 searches** + token costs; search-result content counts as input tokens; citation
  fields are free; errors not billed. ≈ **$0.05–0.12 per uncached bottle**, bounded hard by the 5-day cache.
- **Citations — always returned AND mandatory to display:** *"When displaying API outputs directly to end users,
  citations must be included to the original source."* → the UI **must** show sources.
- **Ops:** an org admin enables web search in the Claude Console; the `pause_turn` stop reason must be handled
  (server-side loop); the **Batch API** supports web search at the same price — handy for pre-warm.

## 4. Locked decisions
1. **Two signals, source-labelled.** `ClaudeMarketResearchProvider` (`PriceSource.ClaudeResearch`) → indicative
   min–max + confidence + **sources**; `InternalMarketProvider` (`PriceSource.Internal`) → our own data. The
   `Source` is also the UI label ("researched" vs "community") — **no separate signal-type enum**.
2. **Simple selection.** Prefer **Internal whenever it returns data** (it has ≥ `MinApproxSamples` of our own
   listings/offers); otherwise use **Claude**; otherwise **None**. No confidence-capping hierarchy.
3. **Mandatory citations.** Every displayed estimate shows its sources (Anthropic requirement + honest UX); an
   estimate with no citations is treated as `None`.
4. **Aggressive caching is mandatory (cost).** Read-through `PriceSnapshot`, **5-day TTL** — each canonical bottle
   hits Claude **at most once per TTL**; identical bottles share one lookup via the canonical key.
5. **Cost guardrails.** `max_uses` cap, model config, a **daily call budget**, pre-warm only **top-N**, optional
   **Batch API**. (Slice 09.)
6. **Honest UX.** Range + confidence + **sources** + as-of; `None` → "—", never a fabricated number; always
   labelled indicative.
7. **Collection value counts only SEALED bottles.** Per-bottle estimate is shown for all conditions; the **total**
   sums only `Condition == Sealed`.
8. **Reuse the existing barcode layer** (`IProductLookupService`) + `Bottle.Barcode` to sharpen matching (and to
   pass a UPC into the Claude prompt).
9. **Currency.** The provider converts the researched min–max to a single **base currency** (config, default
   `EUR`) via a configurable FX table; the snapshot stores the base-currency values only.
10. **No forbidden integrations.** No scraping, no third-party price-API used against its ToS. A licensed direct
    integration (e.g. paid Wine-Searcher) is a **future, parked** option.
11. **Per-provider config + `UseProviderStats`.** Each provider binds its own options; the **Anthropic API key**
    lives in `appsettings.Development.json` / user-secrets — never committed.
12. **Shared `PriceProviderBase`** owns the on/off gate, FX→base conversion, min/median/percentile aggregation,
    error swallowing → `null`, and DTO building; each provider overrides only `FetchAsync`.

**Config layout** (shared in `appsettings.json`; secrets in `appsettings.Development.json`):
```jsonc
"Pricing":     { "BaseCurrency": "EUR", "SnapshotTtlDays": 5,
                 "FxToBase": { "EUR": 1.0, "GBP": 1.17, "USD": 0.92 },
                 "RefreshIntervalHours": 24, "PreWarmTopNBottles": 200, "RefreshEnabled": true },
"Anthropic":   { "UseProviderStats": true, "BaseUrl": "https://api.anthropic.com",
                 "ApiKey": "",                 // appsettings.Development.json / user-secrets
                 "Model": "claude-sonnet-4-6", "AnthropicVersion": "2023-06-01",
                 "MaxSearchesPerBottle": 5, "DailyCallBudget": 200,
                 "AllowedDomains": [], "BlockedDomains": [] },
"InternalProvider": { "UseProviderStats": true, "MinSamples": 3, "MinApproxSamples": 2 }
```

## 4a. Architecture at a glance
```
Bottle ─(canonical ProductKey | Barcode)─▶ PriceEstimationService  (orchestrator + read-through cache)
                                                  │
        ┌─────────────────────────────────────────┴───────────────────────────────┐
        ▼                                                                           ▼
 InternalMarketProvider (PriceSource.Internal)              ClaudeMarketResearchProvider (PriceSource.ClaudeResearch)
 our listings/offers/sales; confidence by volume            Anthropic Messages API + web_search;
                                                            indicative min–max + CITATIONS
        └───────────────────────────┬───────────────────────────────────────────────┘
                                    ▼
                  PriceSnapshot  (EF, 5-day TTL cache — also stores Sources[])
                                    ▲
        PreWarmRefreshJob (top-N canonical bottles via Claude; optional Batch API)
                                    │
                                    ▼
           CollectionValue  (sum of latest per-bottle estimates — SEALED bottles only)
```
**Selection:** Internal **if it returns data** → else Claude → else `None`. Each result carries a `Confidence` +
`Source` for the UI. (No signal-type ordering, no confidence capping.)

## 5. What already exists (reuse, don't rebuild)
- **From the stash** (`git checkout stash@{0} -- <path>`): `PriceProviderBase`, `ProductKey`,
  `InternalMarketPriceProvider`, the orchestrator + read-through cache skeleton (`PriceEstimationService`),
  `PriceSnapshot`, the pricing DTOs, `PricesController`, the frontend (`pricesApi`/Dashboard card/bottle row/i18n),
  and the test scaffolds (`FakeHttpHandler`, stubs).
- **Barcode lookup** (`ProductLookupService`) — the `AddHttpClient<T>` + options + decorator **pattern to copy**
  for the new `ClaudeMarketResearchProvider` HTTP client.
- **Verified Anthropic web-search spec** (§3).

## 6. Backend conventions (from CLAUDE.md — follow exactly)
- `Result<T>` everywhere; typed factories; never throw for expected failures.
- **Decorator pattern** (`XxxValidationDecorator` guards + `cancellationToken.ThrowIfCancellationRequested()`)
  wrapping a pure `XxxService`; register both in `DependencyInjection.cs`.
- Primary-constructor DI; `CancellationToken cancellationToken` on every async + EF call.
- Controllers `[Authorize]` by default; full XML docs; `result.Success ? Ok(...) : result.ToActionResult(this)`.
- **Migrations are ADD-only.** Never drop/rename without asking.
- **One top-level type per file** (private nested helper records OK). Mock only `ICurrentUser`,
  `INotificationService`, and `HttpMessageHandler`.

## 7. Risks
- **Accuracy / hallucination** — an LLM can be wrong or stale for niche bottles. Mitigate: web-grounded calls,
  **require citations**, return **confidence** + `found=false → None`, sanity-bound the numbers, label indicative.
- **Cost** — pay-per-call. The 5-day cache + `max_uses` + a daily budget cap are mandatory, not optional.
- **Latency** — a web-search call takes seconds → always **async + cached**, never blocking a page load.
- **Prompt injection** — fetched web pages are untrusted; the provider asks only for a strict JSON price + sources
  and validates; the model must not act on instructions embedded in pages.
- **Citation compliance** — sources **must** be displayed (Anthropic requirement).
- **Legal — reduced, not zero.** Synthesis + citations is far safer than republishing a feed, but keep estimates
  indicative, show sources, and get legal sign-off before a public/commercial launch.

## 8. Open questions (decide during build)
- **Model & `max_uses`** — `claude-sonnet-4-6` default; Opus for quality, Haiku for cost (confirm web-search support); `max_uses` 3 (cheap) vs 5 (balanced).
- **Domain filtering** — restrict `allowed_domains` to trusted retailer/marketplace sites for quality, or leave open?
- **Trigger model** — on-demand (first view → async research → cache) vs **pre-warm only top-N**; daily budget size.
- **Extra signal** — also let the user enter their own purchase price / self-estimate? (optional add-on.)
- **Public visibility** — estimates on public bars too, or owner-only? (MVP: owner Dashboard + bottle panel.)

## 9. Slice index, dependencies & order
| Slice | Doc | Depends on |
|---|---|---|
| 1 | `01-domain-migration.md` (+`Barcode`, +`Sources` on snapshot) | — |
| 2 | `02-contracts-product-key.md` (+ DTOs w/ sources, `AnthropicOptions`) | 1 |
| 3 | `03-claude-research-provider.md` **(the core)** | 2 |
| 4 | `04-internal-provider.md` | 2 |
| 5 | `05-orchestrator-caching.md` (source-priority + cost-critical cache) | 3, 4 |
| 6 | `06-prewarm-job.md` (top-N via Claude; Batch API option) | 5 |
| 7 | `07-api.md` | 5 |
| 8 | `08-frontend.md` (range + confidence + **sources** + as-of) | 7 |
| 9 | `09-cost-guardrails-and-compliance.md` (budget caps, admin enable, citations, injection safety) | 3, 5, 6 |
| 10 | `10-backend-tests.md` **(ALL backend unit tests — written last)** | 1–7, 9 |

**Execution order (tests last):** implement backend slices **1→2→3→4→5→6→7→9 with build-only gates (no tests
yet)**, then write **all** backend unit tests in **slice 10**. The frontend slice **8** runs after slice 7.

## 10. Verification protocol (REQUIRED)
- **During each backend slice (1–7, 9):** `dotnet build VirtualBar.Api/VirtualBar.Api.csproj --no-restore -v q`
  → **0 errors**. Each slice lists **Test targets** — record them, but **do NOT write tests yet**.
- **Slice 10 (after the whole backend is implemented):** write every `<Service>Tests` class — **100% branch**
  for every new service method — then `dotnet test VirtualBar.Tests/VirtualBar.Tests.csproj` → `Failed: 0`.
- **Frontend slice (8):** `npm --prefix VirtualBar.Web run build` clean + exercise in **bg + en**.
- Do **not** commit unless the user asks.
