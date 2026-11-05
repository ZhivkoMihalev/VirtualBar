---
paths:
  - "**/*Price*"
  - "**/price*"
  - "**/Pricing/**"
  - "**/*PreWarm*"
  - "**/*Anthropic*"
  - "**/*CollectionValue*"
  - "**/collection-value/**"
  - "**/ProductKey*"
---

# Pricing

## PriceSnapshot
Read-through cache of an **indicative market-value estimate** for a canonical product (see Collection Value below).
- One row per `ProductKey`, so identical bottles owned by different users share a single lookup.
- Not user-owned — no FKs.

| Property | Type | Notes |
|---|---|---|
| `ProductKey` | `string` | The bottle's price key from `PriceKey.ForBottle(...)` — `Product.CanonicalKey` for a bottle linked to a non-deleted product, else `ProductKey.For(...)` of the bottle; **unique index** |
| `Barcode` | `string?` | Last barcode seen for this product |
| `Category` | `SpiritCategory` | |
| `EstimatedPrice` | `decimal` | Median, base currency; `decimal(18,2)` |
| `LowEstimate` / `HighEstimate` | `decimal?` | Indicative min–max |
| `Currency` | `string` | Base currency (config, default `EUR`) |
| `SampleSize` | `int` | Data points behind the estimate |
| `Source` | `PriceSource` | `ClaudeResearch` / `Internal` |
| `Confidence` | `PriceConfidence` | `Low` / `Medium` / `High` |
| `SourcesJson` | `string` | JSON array of `{ Url, Title }` citations (mandatory for `ClaudeResearch`; serialized/read with default System.Text.Json — PascalCase) |
| `AsOf` | `DateTime` | "As of" date of the underlying data |
| `FetchedAt` | `DateTime` | When cached; drives the 5-day TTL |

- Indexes: unique `ProductKey`; `Barcode`; `(Category, FetchedAt)`.
- `SourcesJson` has no explicit column type (defaults to `nvarchar(max)` on SQL Server, `TEXT` on SQLite). Do not
  re-add `HasColumnType("nvarchar(max)")`: it breaks the SQLite test DB.

## Collection Value (bottle price estimation)
Shows an **indicative min–max market value per bottle** and a **total collection value** (Sealed bottles only) on the
Dashboard. Always indicative — shown with range + confidence + **sources** + "as of" date; `None` renders "—", never a
fabricated number.

### Provider architecture (`VirtualBar.Infrastructure/Services/Pricing/`)
- `IPriceProvider` + `PriceProviderBase` (abstract) — shared plumbing: on/off gate (`UseProviderStats`), FX→base
  conversion, percentile aggregation, sanity bounds (`MaxSanePrice`), error-swallow → `null` + log, DTO build. Each
  provider overrides only `FetchAsync`.
- `ClaudeMarketResearchProvider` (`PriceSource.ClaudeResearch`) — Anthropic Messages API + `web_search` tool.
  - Returns an indicative min–max + **mandatory citations** (a result with no citations is treated as `None`).
  - Typed `HttpClient`.
- `InternalMarketPriceProvider` (`PriceSource.Internal`) — our own listings + accepted offers matched by `ProductKey`;
  confidence by sample count.
- `PriceEstimationService` (orchestrator) — read-through `PriceSnapshot` cache (5-day TTL).
  - On a miss/stale snapshot it tries **Claude first, then Internal** as fallback, and caches the chosen estimate.
  - Collection value sums **Sealed** bottles only, each scaled by the bottle's `Quantity`.
- `PreWarmWorker` + `PreWarmRefreshJob` (`IHostedService`) — periodically research the top-N most-owned bottles with a
  missing/stale snapshot, within budget.
- `AnthropicDailyCallBudget` (singleton) — process-wide cap on billed Claude calls, resets per UTC day; consumed by both
  the provider and the pre-warm job.

**Selection order (shipped):** Claude → Internal fallback → `None`.

### Price key rule (#50)
- Every pricing path computes a bottle's key through `PriceKey.ForBottle` (`VirtualBar.Application/Common/PriceKey.cs`):
  `PriceEstimationService` (estimate, cached read, collection value), `PreWarmWorker` (grouping) and
  `InternalMarketPriceProvider` (listings + accepted offers).
- A bottle linked to a **non-deleted** product uses the stored `Product.CanonicalKey`, so every copy of the product
  shares one snapshot whatever its spelling or vintage.
  - Claude then researches the **product's** fields (name, non-deleted distillery ?? brand, age, volume, no vintage,
    product barcode ?? bottle barcode).
- Any other bottle keeps `ProductKey.For(bottle fields)`.
- Never call `ProductKey.For` directly in pricing code: two paths computing the key differently means snapshots written
  by one are never read by the other.
- Snapshots written under the old bottle keys before this rule are no longer read; the admin prune below removes them.

### Pruning orphaned snapshots (#58)
- `PriceSnapshotPruneService` (behind `PriceSnapshotPruneValidationDecorator`, admin only) removes the snapshots no
  path reads any more: old bottle keys of linked bottles, and the keys of deleted bottles and products.
- A snapshot is **kept** when its key is:
  - the price key of a non-deleted bottle (`PriceKey.ForBottle`, loaded and computed in C# as `PreWarmWorker` does);
  - the `CanonicalKey` of a non-deleted product: `GET /api/prices/product/{productId}` reads it whatever its age, even
    with no bottle linked;
  - or it is still within `SnapshotTtlDays`. A key that comes back later costs one research call, never wrong data.
- A **one-off admin operation**, never a periodic job (owner's decision): a periodic delete would be standing data
  removal without approval.
- **Dry run by default:** it reports `TotalCount`, `OrphanCount` and up to 20 `SampleKeys` and deletes nothing.
  `dryRun=false` deletes the orphans by id in batches of 500 (`ExecuteDeleteAsync`), checking the age again so a
  snapshot refreshed meanwhile stays, and reports `DeletedCount`.
- Run the dry run first and get its numbers approved before a real run (CRITICAL RULES). No migration is involved.

### API (`/api/prices`, `[Authorize]`)
- `GET /api/prices/bottle/{bottleId}` → `PriceEstimateDto` — **cache-only** read (never triggers a synchronous Claude
  call on the request path); `204` when no estimate is cached yet, `404` when the bottle is missing.
- `GET /api/prices/product/{productId}` → `PriceEstimateDto` — **cache-only** read of the snapshot under the product's
  `CanonicalKey` (the one its linked bottles share); `204` when none is cached, `404` for an unknown or deleted product.
- `GET /api/prices/collection` → `CollectionValueDto` — Sealed-only total + per-bottle lines; owner-scoped (decorator
  enforces `userId == currentUser.UserId`).
- `POST /api/admin/prices/prune?dryRun=true` (`AdminPricesController`, `[Authorize]`, admin only → `403` otherwise) →
  `PriceSnapshotPruneResultDto`; `dryRun` defaults to `true`. See Pruning orphaned snapshots above.

### Config
`appsettings.json`; `Anthropic:ApiKey` is empty there — set it in `appsettings.Development.json` / user-secrets, never
committed:
- `Pricing` — `BaseCurrency`, `SnapshotTtlDays` (5), `FxToBase`, `RefreshIntervalHours`, `PreWarmTopNBottles`,
  `RefreshEnabled`.
- `Anthropic` — `UseProviderStats` (on/off), `Model` (`claude-sonnet-4-6`), `AnthropicVersion`, `MaxSearchesPerBottle`,
  `DailyCallBudget`, `AllowedDomains` / `BlockedDomains`, `BaseUrl`, `ApiKey`.
- `InternalProvider` — `UseProviderStats`, `MinSamples`, `MinApproxSamples`.

## Related rules
- Collection value counts Sealed bottles, each scaled by `Bottle.Quantity` (`.claude/rules/domain/bottles.md`).
- A linked bottle's key is `Product.CanonicalKey` (`.claude/rules/domain/catalog.md`).
- The internal provider reads listings and accepted offers (`.claude/rules/domain/bottles.md`, `.claude/rules/domain/offers.md`).
- Every currency the app accepts must be convertible through `FxToBase` (`.claude/rules/domain/currencies.md`).

Full slice-by-slice specs live in `docs/features/collection-value/`.
