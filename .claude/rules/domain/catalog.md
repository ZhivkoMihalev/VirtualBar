---
paths:
  - "**/*Product*"
  - "**/product*"
  - "**/CatalogPage*"
  - "**/catalog*"
  - "**/barcode*"
---

# Product catalog

## Product
Canonical catalog entry for a spirits product. Not user-owned.

| Property | Type | Notes |
|---|---|---|
| `Name` | `string` | Required |
| `Brand` | `string?` | The name on the label. **Independent of `DistilleryId`, not an alternative to it** — every seeded row carries one, and it may differ from the distillery (Caorunn @ Balmenach, Meikle Toir @ GlenAllachie, The One @ Lakes Distillery). Nullable only because an admin-approved product may omit it; both read paths then fall back to the non-deleted distillery name |
| `DistilleryId` | `Guid?` | FK → Distillery (`DeleteBehavior.Restrict`) — **where it was made**, known only for some products (blends like Johnnie Walker have none) |
| `Category` | `SpiritCategory` | |
| `Country`, `Region` | `string?` | |
| `Age` | `int?` | |
| `AbvPercent` | `double?` | |
| `VolumeMl` | `int?` | |
| `Barcode` | `string?` | EAN/UPC digits |
| `ImageUrl` | `string?` | |
| `Description` | `string?` | |
| `CanonicalKey` | `string` | `ProductKey.For(distilleryName ?? brand, name, category, age, null, volumeMl)` — **filtered unique index** `WHERE [IsDeleted] = 0` |
| `Origin` | `ProductOrigin` | `Seeded` / `Approved` (append-only enum) |

**No `VintageYear`** — vintage/batch/cask specifics stay on the bottle instance. `ProductKey` normalization is **frozen** (shared with the `PriceSnapshot` cache — changing it would orphan both). Seeded on startup by `ProductSeeder` with **top-up semantics** (the `DistillerySeeder` pattern): one embedded seed file **per category** — `SeedData/products.<category>.seed.json` (`whisky`, `rum`, `cognac`, `brandy`, `vodka`, `gin`, `tequila`, `other`; user-curated and growing — ~3400 rows; no fabricated data — unknown fields are null). The csproj embeds them with the glob `Persistence\SeedData\products.*.seed.json`, and `SeedProductsAsync` reads every matching manifest resource in ordinal order, so adding a new category file needs no code change. Each row still carries its own `category` field. It inserts only rows whose `CanonicalKey` is not yet present — strictly insert-only, never updates or re-inserts existing rows (soft-deleted included); rows with unparseable categories are skipped, in-file duplicate keys collapse to the first, distillery names resolve case-insensitively (miss → `Brand` fallback + null FK). That fallback is a **silent data loss** — the row keeps its name but loses the FK, so it disappears from `/api/distilleries`, the add-bottle picker and wish-list matching. It is therefore both **logged** (`SeedProductsAsync` takes an optional `ILogger` and warns with the offending names) and **enforced**: `ProductSeedDataTests.EverySeedDistillery_ExistsInDistillerySeeder` fails the build when a seed file names a distillery `DistillerySeeder` does not know. When that test fires, either add the name to `DistillerySeeder` (a real production site) or move it to `brand` (a range or label — e.g. Port Charlotte is a Bruichladdich line, not a distillery). `ProductSeedDataTests.EverySeedRow_CarriesABrand` additionally pins that every row names its label brand. Tests assert seed invariants, never an exact row count.

**Add-bottle resolution order** (`BottleService.AddBottleAsync`): (a) explicit `ProductId` from the client (decorator validates existence) → link; (b) else exact `CanonicalKey` match → **auto-link silently**; (c) else auto-file a `ProductRequest` with `SourceBottleId` — wrapped `try/catch → LogError`, non-success `Result` ignored (the badge-engine philosophy: a request bug never fails adding a bottle).

**Barcode L0:** `ProductLookupService.LookupByBarcodeAsync` answers from `Products` **before** the external UPC API (non-deleted match by `Barcode`, `OrderBy(CreatedAt).ThenBy(Id)` — oldest wins; `Brand` falls back to the non-deleted distillery name). Hit → `BarcodeProductDto.ProductId` set, zero external calls; miss → external path unchanged. The lookup decorator trims the barcode once and logs swallowed exceptions.

**API endpoints (`/api/products`):**
- `GET /api/products?search=&category=&limit=` — `[AllowAnonymous]` catalog search; `search` trimmed, min 2 chars, max 100; `limit` clamped 1–**51** (default 20) — the cap is deliberately one over the 50 a client renders, so asking for 51 and getting 51 back proves there are more matches without a count query; `ProductSelect` uses exactly that to decide whether to show its "refine your search" hint; matches `Name`/`Brand`/`Distillery.Name` (contains; case-insensitivity comes from the SQL Server collation, not the code); starts-with ranked first; the returned `Brand` falls back to the non-deleted distillery name exactly as the barcode endpoint does, so both paths agree on the same product
- `GET /api/products/barcode/{barcode}` — `[Authorize]`; catalog L0 → external UPC fallback
- `GET /api/products/browse?search=&category=&page=&pageSize=` — `[AllowAnonymous]`; the public `/catalog` → `PagedResult<ProductCardDto> { Items, HasMore }`. `search` is optional (omitted/blank lists the whole catalog ordered `Name, Id`; a term filters and ranks exactly like the search endpoint, 2–100 chars); `page` ≥ 1 (≤ 1000, beyond → `400`); `pageSize` clamped 1–48 (default 24). Each card adds `OwnersCount` (distinct users), `ReviewsCount`, `AverageScore` and `ListingsCount` over the product's non-deleted linked bottles
- `GET /api/products/{id}` — `[AllowAnonymous]`; the `/product/:id` page → `ProductDetailDto`: the product (+ `Description`) plus, over every non-deleted linked bottle, the review aggregate (`AverageScore`, `ReviewsCount`, top-3 `TopFlavors` with the same tie-break as a bottle), `OwnersCount` + the top 20 `Owners` ranked by physical bottles (`Sum(Quantity)`), every for-sale `Listings` entry (newest first) and the 5 `LatestReviews`; `404` unknown/deleted product
- `GET /api/products/{id}/reviews?page=&pageSize=` — `[AllowAnonymous]`; the rest of the product's reviews, newest first → `PagedResult<BottleReviewDto>`; `pageSize` clamped 1–50 (default 10); `404` unknown/deleted product

All three are **computed on read** (no stored aggregates, no migration) and use `pageSize + 1` for `HasMore` instead of a count query. Reviews on the product page are mapped through the same `ReviewMapping.ToDto` as the bottle reviews, so the two cannot drift.

## ProductRequest
A user's request to add a missing product to the catalog.

| Property | Type | Notes |
|---|---|---|
| `UserId` | `Guid` | FK → AppUser (requester, `DeleteBehavior.Restrict`) |
| `Name`, `Brand?`, `DistilleryId?`, `Category`, `Age?`, `AbvPercent?`, `VolumeMl?`, `Barcode?`, `Country?`, `Region?` | | Proposed fields mirroring `Product` |
| `UserNote` | `string?` | ≤ 500 chars (`Brand` ≤ 200) |
| `CanonicalKey` | `string` | **Filtered unique index** `WHERE [Status] = 0 AND [IsDeleted] = 0` — one pending request per key, globally |
| `Status` | `ProductRequestStatus` | `Pending` / `Approved` / `Rejected` (append-only — withdraw is a soft-delete, no `Withdrawn` member) |
| `AdminNote` | `string?` | |
| `ResolvedProductId` | `Guid?` | FK → Product (`Restrict`) |
| `SourceBottleId` | `Guid?` | FK → Bottle (`Restrict`) — the bottle whose add triggered it; ownership enforced in the decorator |
| `RespondedAt` | `DateTime?` | Set on approve/reject |

**Design:** the `Offer` index pattern — the decorator's dedupe pre-checks (existing product / existing pending request with the same key) are a friendly fast path; `ProductRequestService` maps the `DbUpdateException` race loser to `Conflict` (on create AND on the approve-time product insert). Per-user cap: **25 open requests**. Sanity bounds (create + approve effective values): barcode digits-only 8–14, age 1–100, ABV 1–96, volume 20–6000; approve additionally validates the effective `Category` (enum-defined) and effective `DistilleryId` (exists non-deleted). **Approve** (admin-only): optional field overrides (`override ?? request value`) or `ExistingProductId` (link instead of create); `UseSourceBottleImage` copies the source bottle's primary image URL (explicit `ImageUrl` override wins; ignored on the `ExistingProductId` path; missing bottle/images → null, never an error); creates the `Product` (`Origin = Approved`, key recomputed from effective values), marks the request, links the source bottle if still unlinked, then **retro-links** identical unlinked bottles (SQL pre-filter on `Category`/`Age`/`VolumeMl`, in-memory exact `ProductKey` comparison without vintage — silent, no owner notifications), and finally evaluates the **requester's** badges (`BadgeTrigger.ProductRequestApproved` → `FirstCatalogProduct`). **Reject** sets `AdminNote` + `RespondedAt` — no badge evaluation. **Withdraw** = the requester soft-deletes an own **pending** request. A rejected key may be requested again (the index only guards `Pending`).

**API endpoints (`/api/product-requests`, all `[Authorize]`):**
- `POST /api/product-requests` — manual request (`409` duplicate)
- `GET /api/product-requests/mine` — own requests, newest first
- `DELETE /api/product-requests/{id}` — withdraw own **pending** request
- `GET /api/product-requests?status=Pending` — admin queue (`403` non-admin)
- `PATCH /api/product-requests/{id}/approve` — admin; body `ResolveProductRequestRequest` (**required** — send at least `{}`)
- `PATCH /api/product-requests/{id}/reject` — admin; body `RejectProductRequestRequest` (**required** — send at least `{}`)

Design records (the specs, written before the code): `docs/features/product-catalog/` and, for the public catalog and the product page, `docs/features/product-page/`.
