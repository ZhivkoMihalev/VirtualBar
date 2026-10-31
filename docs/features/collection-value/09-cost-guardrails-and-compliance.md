# 09 — Cost guardrails & compliance

> Phase **A** · Depends on: **03, 05, 06** · Read `00-OVERVIEW.md` first.
> Cross-cutting hardening for the Claude integration — the operational/legal safety net.

## Goal
Keep the AI integration **cheap, safe, and compliant**: bound spend, enforce citation display, and contain
prompt-injection from fetched web pages (overview §3, §7).

## Items
1. **Daily call budget** — a shared counter (`AnthropicOptions.DailyCallBudget`) checked by the provider (slice 03)
   and the pre-warm job (slice 06); when spent, `FetchAsync` short-circuits to `null` (no spend). Reset per UTC day.
2. **Per-call cap** — `MaxSearchesPerBottle` → the tool's `max_uses`; `max_tokens` bounded. Default model
   `claude-sonnet-4-6` (config); document Opus (quality) / Haiku (cost) trade-off.
3. **Cache TTL is the primary cost lever** — 5-day `PriceSnapshot`; never call Claude on a fresh hit (slice 05).
   Pre-warm only top-N (slice 06).
4. **Admin enable** — web search must be enabled for the org in the Claude Console; document this in setup +
   fail soft (provider → `null` + log) if the API rejects the tool.
5. **Citation compliance** — the estimate is only surfaced **with** its `Sources`; the API returns them (slice 07)
   and the UI displays them (slice 08). An estimate with no citations is treated as `None`.
6. **Prompt-injection containment** — the system prompt instructs the model to treat page content as untrusted
   data, never as instructions; the provider accepts only the strict JSON shape + citations and **validates**
   (sanity bounds) — a page cannot make us store an arbitrary number or call other tools.
7. **Indicative framing** — never present as exact; the "indicative" label + range + as-of are always shown.
8. **Secrets** — `ApiKey` only in `appsettings.Development.json` / user-secrets; never committed.

## Test targets (written in slice 10)
budget counter (spend → short-circuit, daily reset), citation-required gate (no sources → None), sanity-bound
rejection, fail-soft on tool-disabled error. 100% branch where logic lives (provider/job).

## Gate
`dotnet build` → 0 errors. Manual: confirm a fresh cache hit makes **no** Anthropic call. *(Tests deferred to slice 10.)*
