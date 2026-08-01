# Product Catalog & Add-Requests (Каталог от продукти + заявки за добавяне) — OVERVIEW & SHARED CONTEXT

> **Read this first, before any slice.** Single source of truth for the decisions, the architecture,
> the conventions, and the risks. Each `NN-*.md` slice assumes you read this. Format mirrors
> `docs/badges/`, `docs/collection-value/`, `docs/bottle-reviews/`, `docs/barcode-scanning/`.

> **Approach — a canonical, admin-curated catalog that enriches but never blocks.** The collector
> platforms that solved this (Whiskybase, Untappd, Discogs) all use community submissions + moderation,
> and none of them block the add flow while a submission is pending. VirtualBar keeps its instant
> free-text add-bottle flow; picking a catalog product is an **accelerator** (pre-fill + link), and a
> miss silently files a **`ProductRequest`** the admin resolves later. Approval back-links the
> requester's bottle (and identical unlinked bottles). Seed data comes from open datasets (Iowa ABD
> product portfolio — public data with UPCs; Open Food Facts — ODbL barcode DB), imported **offline**
> by a standalone tool — never at runtime. This feature also un-parks the "crowdsourced catalog
> phase 2" item from `docs/barcode-scanning/00-OVERVIEW.md` §8: the catalog becomes **L0** of the
> layered barcode lookup.

---

## 1. Goal
A master table of canonical spirits products (**`Products`**). Adding a bottle offers autocomplete
against it; a pick pre-fills the form and links the bottle (`Bottle.ProductId`). A bottle whose exact
canonical key already exists is **auto-linked**; anything else **auto-creates a pending
`ProductRequest`** (deduped, capped, never failing the add). The admin reviews requests on a new
admin page — **approve** (creates the product, links the source bottle, retro-links identical
unlinked bottles, notifies the requester) or **reject** (with a note, notifies). The catalog starts
from a curated seed (~200–400 iconic bottles) and can be regenerated at scale (5–15k) from open
datasets by an offline import tool.

## 2. Why this shape (research + codebase — short version)
- **Never block the #1 flow.** A collector entering 50 bottles on day one will not wait for approval
  on 10 of them; the long tail (single casks, small batches) means the catalog is never complete —
  and limited editions are exactly VirtualBar's audience. Free-text stays; the catalog is a bonus.
- **The codebase already wants this.** `ProductKey.For(...)` (Application/Common) canonicalizes
  bottles for `PriceSnapshot` caching — typos fragment that cache today. `Distilleries` proves the
  seeded-master-list pattern. `Bottle.Barcode` + the barcode lookup (`ProductLookupService`) want a
  first-party answer before calling the external UPC API.
- **One review queue, one admin.** Evaluate-on-submit with a global pending-key dedupe keeps the
  queue small: N users adding the same missing bottle produce **one** request.
- **Open data exists** (verified 2026-07): Iowa Alcoholic Beverages Division product portfolio
  (public data, has UPC + volume + proof + age), Open Food Facts (ODbL, barcode-keyed, images),
  TTB COLA registry (every US label since 1999). No licensable path to Whiskybase — do not scrape.

## 3. Locked decisions
1. **`Product` entity** (DbSet `Products`, extends `BaseEntity`): `Name` (required), `Brand?`
   (free-text producer when no distillery FK), `DistilleryId?` (FK → Distillery, Restrict),
   `Category` (`SpiritCategory`), `Country?`, `Region?`, `Age?` (int), `AbvPercent?` (double),
   `VolumeMl?` (int), `Barcode?` (EAN/UPC digits), `ImageUrl?`, `Description?`,
   `CanonicalKey` (required), `Origin` (**new enum `ProductOrigin { Seeded, Approved }`**).
   **No `VintageYear`** — vintage/batch/cask specifics stay on the bottle instance (see §7).
2. **`CanonicalKey` = `ProductKey.For(distilleryName ?? brand, name, category, age, null, volumeMl)`**
   — the *same* helper the pricing cache uses (vintage always null for catalog entries). Unique
   **filtered index** `WHERE [IsDeleted] = 0` (the Review/Offer pattern). `ProductKey` normalization
   is hereby **frozen** — changing it would orphan both `PriceSnapshots` and catalog matching.
3. **`ProductRequest` entity** (DbSet `ProductRequests`, extends `BaseEntity`): `UserId` (requester,
   FK Restrict), proposed fields mirroring `Product` (`Name`, `Brand?`, `DistilleryId?`, `Category`,
   `Age?`, `AbvPercent?`, `VolumeMl?`, `Barcode?`, `Country?`, `Region?`), `UserNote?`,
   `CanonicalKey`, `Status` (**new enum `ProductRequestStatus { Pending, Approved, Rejected }`**),
   `AdminNote?`, `ResolvedProductId?` (FK → Product, Restrict), `SourceBottleId?` (FK → Bottle,
   Restrict — the bottle whose add triggered it), `RespondedAt?`.
4. **One pending request per canonical key, globally** — filtered unique index on `CanonicalKey`
   `WHERE [Status] = 0 AND [IsDeleted] = 0` (the `Offer` pattern: the decorator pre-check is a
   friendly fast path; the service maps the `DbUpdateException` race loser to `Conflict`).
   Plus a **per-user cap: 25 open requests** (decorator guard against spam).
5. **`Bottle.ProductId`** (Guid?, FK → Product, Restrict) on `AddBottleRequest`/`UpdateBottleRequest`/
   `BottleDto`. The bottle's own free-text fields remain the display truth; the link only enriches.
6. **Add-bottle resolution order** (in `BottleService.AddBottleAsync`):
   (a) explicit `ProductId` from the client (autocomplete pick; decorator validates existence) → link,
   done; (b) else exact `CanonicalKey` match against `Products` → **auto-link silently**; (c) else
   call `IProductRequestService.CreateAsync` with `SourceBottleId` and **ignore a non-success
   `Result`** (dup/cap are expected); the call is wrapped `try/catch → LogError` (badge-engine
   philosophy: a request bug must never fail adding a bottle).
7. **Approve enriches, never blocks** (`ApproveAsync`, admin-only): payload = optional field
   overrides + optional `ExistingProductId` (link instead of create) + `UseSourceBottleImage`
   (copies the source bottle's primary image URL onto the new product — URL reuse, not file copy,
   the barcode-scanning L1 precedent; an explicit `ImageUrl` override wins; silently skipped when
   there is no source bottle/usable image or on the `ExistingProductId` path). Creates the `Product`
   (`Origin = Approved`; duplicate-key race → `Conflict`), marks the request `Approved` +
   `ResolvedProductId` + `RespondedAt`, links the source bottle if still unlinked, then a
   **retro-link pass**: candidate bottles (`ProductId == null`, same `Category`/`Age`/`VolumeMl` —
   all SQL-filterable) are compared in memory by `ProductKey.For` and linked on exact match
   (silently — no notification to their owners). Requester gets `ProductRequestApproved` — and, added
   after this slice, the `FirstCatalogProduct` badge (`BadgeTrigger.ProductRequestApproved`, evaluated
   for the **requester** as the last line of `ApproveAsync`; see `docs/badges/00-OVERVIEW.md` §3
   "Added after this slice").
8. **Reject** sets `Status = Rejected` + `AdminNote?` + `RespondedAt`, notifies
   `ProductRequestRejected`. **Withdraw** = the requester soft-deletes an own **pending** request
   (`DELETE /api/product-requests/{id}`). No `Withdrawn` status — 3 members, **append-only**.
9. **Two new `NotificationType` members appended at the END**: `ProductRequestApproved`,
   `ProductRequestRejected`. Fired via plain `CreateAsync` (actor = the admin; the existing
   self-skip is correct here — an admin approving their own request needs no notification).
   `ResourceId = ResolvedProductId` (approve) / request `Id` (reject); `ResourceName` = product name.
10. **Catalog search API** on the existing `ProductsController`: `GET /api/products?search=&category=&limit=`
    — `[AllowAnonymous]` (public catalog data, like `/api/distilleries`). Decorator: `search`
    trimmed, **min 2 chars**, `limit` clamped 1–50 (default 20). Match on `Name`/`Brand`/
    `Distillery.Name` (contains, case-insensitive); order: starts-with first, then name.
11. **Catalog is L0 of the barcode lookup.** `ProductLookupService.LookupByBarcodeAsync` checks
    `Products` by barcode **before** the external UPC call and returns the catalog hit (with a new
    additive `BarcodeProductDto.ProductId`) — zero external cost, and the form links the product.
    Coexists with the (not yet shipped) L1/L2 layering planned in `docs/barcode-scanning/`.
12. **`ProductSeeder`** (mirror `DistillerySeeder`): runs on startup only when `Products` is empty,
    reads an **embedded** `products.seed.json`, resolves `DistilleryId` by name, computes keys,
    dedupes. The starter seed is **curated (~200–400 iconic core-range bottles), no barcodes, no
    guessed numbers** — unknown fields are `null` (the Collection-Value honesty rule). The offline
    **import tool** (slice 08) regenerates the file at 5–15k scale from Iowa/OFF when the user
    decides; it never runs inside the API process.
13. **Frontend:** `ProductSelect` autocomplete (shadcn Command/Popover — new files use shadcn) at the
    top of the Dashboard add-form; a pick pre-fills the form + sets `productId` + shows a "linked"
    chip; editing an identity field (name/category/age/volume) after a pick **clears the link**.
    Option rows show a thumbnail when the product has an image; a pick whose product carries an
    image links it to the new bottle when the user uploads no photo of their own (the existing
    `linkBottleImage` barcode flow). New admin page `/admin/product-requests` behind an `AdminRoute` guard (isAuthenticated +
    `user.isAdmin`; server enforces regardless). "My requests" list on `ProfilePage`. NavBar item
    visible to admins only. Two new bell types. i18n namespaces `products` + `productRequests` (bg+en).

## 3a. Architecture at a glance
```
Dashboard add-form ── ProductSelect ──▶ GET /api/products?search= (L0 catalog, AllowAnonymous)
        │ pick → prefill + productId          barcode input ──▶ GET /api/products/barcode/{code}
        ▼                                                        (L0 Products → external UPC API)
POST /api/bottles (AddBottleRequest + ProductId?)
        ▼
BottleService.AddBottleAsync
  explicit ProductId → link ─┬─ else CanonicalKey match → auto-link
                             └─ else IProductRequestService.CreateAsync (SourceBottleId,
                                   dedupe: unique pending key │ cap 25 │ failures ignored)
                                          ▼
                              ProductRequests (Pending)
                                          ▼
/admin/product-requests ──▶ PATCH approve ──▶ Product created (Origin=Approved) → request Approved
   (AdminRoute, isAdmin)  │                    → link source bottle → retro-link identical bottles
                          │                    → notify ProductRequestApproved
                          └▶ PATCH reject  ──▶ AdminNote → notify ProductRequestRejected

Startup: ProductSeeder (embedded products.<category>.seed.json, top-up by CanonicalKey)
Curation: the seed files are hand-edited; ProductSeedDataTests guards their invariants on every build
          (slice 08's offline import tool was built, never used, and removed — see 08 for why)
```

## 4. What already exists (reuse, don't rebuild)
- **`ProductKey.For(...)`** (`VirtualBar.Application/Common/ProductKey.cs`) — the canonical key; reuse
  as-is, never re-implement normalization.
- **`ProductsController` + `ProductLookupService` + `ProductValidationDecorator`** — the barcode
  lookup trio; slice 03 adds the search endpoint to the same controller, slice 05 adds the L0 check
  inside the lookup service (ctor gains `AppDbContext` — its tests get the extra dependency).
- **`DistillerySeeder` + `Program.cs` seed block** — the seeder template and its call site.
- **`Offer`/`BottleReview` filtered-unique-index pattern** (`AppDbContext` lines ~157/~203) — copy the
  `HasFilter` style; indexed strings get `nvarchar(450)` by EF convention (no `HasMaxLength` anywhere
  in this codebase — keep it that way).
- **`INotificationService.CreateAsync`** + the decorator self-skip — correct for both new types.
- **Decorator + DI wiring style** (`DependencyInjection.cs`), `Result<T>` typed factories,
  `ToActionResult`.
- **Frontend:** `DistillerySelect` (autocomplete UX reference), shadcn `command`/`popover`/`dialog`
  primitives (already in `components/ui/`), the Dashboard add-form barcode prefill flow,
  `ProtectedRoute` in `App.tsx` (template for `AdminRoute`), TanStack Query conventions,
  `NotificationBell` type map, i18n bg/en files.
- **Tests:** SQLite in-memory pattern for unique-index races (`OfferServiceTests`,
  `BottleReviewServiceTests`), `Mock.Of<T>()` optional ctor parameters convention.

## 5. Backend conventions (from CLAUDE.md — follow exactly)
- `Result<T>` + typed factories (`NotFound`/`Forbidden`/`Conflict`/`Fail`); decorator owns ALL guards
  + `ThrowIfCancellationRequested()`; inner service pure; both registered in `DependencyInjection.cs`.
- Admin checks live in the **decorator** (`if (!currentUser.IsAdmin) return Forbidden(...)`).
- Primary-constructor DI; `CancellationToken cancellationToken` everywhere; controllers `[Authorize]`
  by default, `[AllowAnonymous]` explicit; full XML docs; `result.Success ? Ok(...) : result.ToActionResult(this)`.
- Migrations **ADD-only** (two tables + one nullable column + indexes — safe); one blank line between
  entity properties; one top-level type per file; no `// ── section ──` divider comments.
- Enums serialize as strings (global `JsonStringEnumConverter`); all three new enums **append-only**.
- Mock only `ICurrentUser`, `INotificationService`, `IBadgeService` — **extended for this feature**:
  `BottleServiceTests` also mocks **`IProductRequestService`** (optional ctor parameter,
  `Mock.Of<IProductRequestService>()` default, same convention as `IBadgeService`). The one sanctioned
  exception is `VirtualBar.Tests/Integration/`, which wires the real services on purpose — see
  CLAUDE.md §Testing Conventions.

## 6. Risks
- **Wrong auto-link** — mitigated by linking ONLY on exact `CanonicalKey` equality (category + name +
  age + volume all agree after normalization). Never fuzzy-link; fuzzy stays human (the admin).
- **Request spam** — global pending-key dedupe + per-user cap of 25 + auto path silently absorbing
  failures. No CAPTCHA-grade defense needed behind `[Authorize]`.
- **Naming overlap** — `ProductLookupService`/`ProductValidationDecorator` (external barcode lookup)
  vs the new `ProductCatalogService`/`ProductRequestService`. Names chosen to avoid collision;
  renaming the existing decorator to `ProductLookupValidationDecorator` is a nice-to-have — **parked**
  (§7), do NOT bundle it into this feature.
- **Seed quality** — a fabricated ABV/barcode is worse than a null. Starter seed: only well-known
  core-range facts, `null` when unsure, **no barcodes**. Import tool marks provenance and keeps
  attribution (ODbL for OFF).
- **Product image reuse** — a product image may be another user's uploaded bottle photo (approve
  copies the URL) or an OFF hotlink (import). Upload files are never physically deleted today, so
  reused local links stay valid — the same accepted risk as barcode-scanning L1; copy-on-link
  hardening stays parked there. OFF hotlinks may rot over time — acceptable for seed data; a null
  image renders fine everywhere.
- **Startup insert time** — 10k+ rows insert once (empty-table guard), single `AddRange` +
  `SaveChangesAsync` batches fine; keep the seed ≤ ~15k and measure before growing.
- **Approve/withdraw race** (admin approves while requester withdraws) — single admin makes this
  rare; approve re-reads the request and the `Pending` + `!IsDeleted` check in the decorator plus the
  duplicate-product `Conflict` mapping bound the damage. No `ExecuteUpdateAsync` needed. Accepted.
- **`ProductLookupService` ctor ripple** — gains `AppDbContext`; its existing tests get the InMemory
  context (mechanical, loud at build time).
- **Retro-link cost** — candidates are pre-filtered in SQL by `Category`+`Age`+`VolumeMl` equality
  (nullable comparisons translate to `IS NULL` correctly in EF Core), so the in-memory key comparison
  set is tiny. Runs only on approve.

## 7. Open questions (decide later — all parked, none block this feature)
- **Pricing convergence** — when a bottle is linked, build `PriceProviderInput` from the *product's*
  canonical fields (+ its barcode) so identical bottles share one `PriceSnapshot` row reliably.
  Natural next slice after ship; touches `PriceEstimationService` only.
- **Wish list by product** — `WishListItem.ProductId?` as a third, precise matching criterion.
- **Per-product review aggregation** — "all reviews of Macallan 12 across the platform" (reviews stay
  bottle-attached; aggregation is a query, not a migration).
- **Vintage-specific catalog entries** — revisit if requests show real demand; would add
  `VintageYear` to `Product` + key computation.
- **Periodic retro-link job** — a background pass linking old unlinked bottles as the catalog grows
  (approve-time retro-link covers the common case).
- **Community moderation** (trusted-user approvals) — solo admin is fine at current scale.
- **Rename `ProductValidationDecorator` → `ProductLookupValidationDecorator`** — clarity refactor.
- **Public catalog browse page** (`/catalog`) — the search API already allows it; UI only.

## 8. Slice index, dependencies & order
| Slice | Doc | Depends on |
|---|---|---|
| 1 | `01-domain-migration.md` (entities, 2 enums, `Bottle.ProductId`, indexes, migration) | — |
| 2 | `02-seeder.md` (`ProductSeeder`, embedded starter seed JSON, `Program.cs` hook) | 1 |
| 3 | `03-catalog-api.md` (`IProductCatalogService` search, `ProductDto`, controller) | 1 (2 for demo data) |
| 4 | `04-product-requests.md` **(the core — request service, admin resolve, notifications)** | 1 |
| 5 | `05-bottle-integration.md` (bottle DTOs + auto-link + auto-request + barcode L0) | 3, 4 |
| 6 | `06-frontend.md` (ProductSelect, add-form, admin page, profile, bell, i18n bg+en) | 3, 4, 5 |
| 7 | `07-backend-tests.md` **(ALL backend unit tests — written last)** | 1–5 |
| 8 | `08-data-import-pipeline.md` (offline Iowa/OFF → regenerated seed; optional for MVP) | 2 (independent of 3–7) |

**Execution order (tests last):** backend slices **1→2→3→4→5 with build-only gates (no tests yet)**,
then frontend **6**, then **all** backend unit tests in **7** (6 and 7 may swap freely). Slice **8**
is an offline deliverable — run it whenever the user wants the big catalog; the feature ships without it.

## 9. Verification protocol (REQUIRED)
- **During each backend slice (1–5):** `dotnet build VirtualBar.Api/VirtualBar.Api.csproj --no-restore -v q`
  → **0 errors**. Each slice lists **Test targets** — record them, but **do NOT write tests yet**.
- **Slice 7:** write `ProductCatalogServiceTests`, `ProductRequestServiceTests`, `ProductSeederTests`,
  extend `BottleServiceTests` + `ProductLookupServiceTests` — **100% branch** for every new/changed
  service method — then `dotnet test VirtualBar.Tests/VirtualBar.Tests.csproj` → `Failed: 0`.
- **Frontend slice (6):** `npm --prefix VirtualBar.Web run build` clean + exercise in **bg + en**
  (pick-from-catalog, no-match auto-request, admin approve/reject round-trip, bell navigation).
- Do **not** commit unless the user asks.

## 10. Docs & CLAUDE.md (final step, after slice 7)
Update `CLAUDE.md`: `Products` + `ProductRequests` in the DbSet table; entity-details sections for
both (+ the three new enums); the two `NotificationType` rows; the `Bottle.ProductId` row; the
`/api/products` search + `/api/product-requests` endpoints; the seeder note under Startup Behavior;
`products`/`productRequests` in the i18n namespace list; the extended mock convention
(`IProductRequestService`) under Testing Conventions; a short "Product Catalog" feature paragraph in
the intro (mirror the badges/wish-list mentions).
