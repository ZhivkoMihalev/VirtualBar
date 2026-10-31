# Public Catalog & Product Page (Каталог + страница на продукт) — OVERVIEW & SHARED CONTEXT

> **Read this first, before any slice.** Single source of truth for the decisions, the architecture,
> the conventions and the risks. Each `NN-*.md` slice assumes you read this. Format mirrors
> `docs/product-catalog/` and `docs/bottle-reviews/`. Tracking issue: **#50**.

> **Approach — the product becomes a page, the bottle stays the truth.** Reviews, listings and
> ownership stay attached to the bottle instance. A product page is a set of **read-only queries**
> over every non-deleted bottle linked to that product. No new tables, no stored counters, **no
> migration**. The one behaviour change is in pricing: a linked bottle is priced by its product's
> canonical key, so all copies of one product share one `PriceSnapshot`.

---

## 1. Goal
- `/catalog` — anyone (logged in or not) can browse the ~3,400 catalog products, with or without a
  search term, filter by category, and open a product.
- `/product/:id` — product header, community score + top flavors across every linked bottle, who owns
  it, who sells it right now, the latest reviews (with "show more"), and — for logged-in users — the
  cached price estimate.
- Two bottles linked to the same product share **one** price snapshot (one Claude call, one cache row).

## 2. What is parked today (and is un-parked by this feature)
- `docs/product-catalog/00-OVERVIEW.md` §7: "Public catalog browse page", "Per-product review
  aggregation", "Pricing convergence".
- `docs/bottle-reviews/00-OVERVIEW.md` decision 3 (reviews attach to the bottle; aggregation parked).
  Decision 3 itself **stays**: reviews are still written per bottle. Only the read side aggregates.

## 3. Locked decisions
1. **No migration.** Every aggregate (score, counts, top flavors, owners, listings) is computed in
   queries, never stored — the `BottleDto.AverageScore` rule. `Bottle.ProductId` already has an FK
   index (EF creates one per FK), so "all bottles of product X" is an index seek.
2. **"Linked bottle"** = `Bottle.ProductId == productId && !Bottle.IsDeleted`. A soft-deleted product
   returns `404` and is never shown, even if bottles still point to it.
3. **Browsing without a term — decided: paged list.** New endpoint
   `GET /api/products/browse?search=&category=&page=&pageSize=` (`[AllowAnonymous]`). `search` is
   optional: blank → whole catalog ordered by `Name`; present → same 2–100 char rule and the same
   starts-with-first ordering as the existing search. Paging uses the **`pageSize + 1` trick** (the
   same idea as the "ask for 51" rule): the service fetches one extra row and returns
   `{ items, hasMore }` — no `COUNT(*)` query. `pageSize` clamped 1–48, default 24; `page` ≥ 1.
   The existing `GET /api/products?search=` is **unchanged** — `ProductSelect` keeps using it.
4. **Reviews on a popular product — decided: cap + separate paged endpoint.**
   `GET /api/products/{id}` returns the **latest 5** reviews; the full list comes from
   `GET /api/products/{id}/reviews?page=&pageSize=` (`[AllowAnonymous]`, same `{ items, hasMore }`
   shape, `pageSize` clamped 1–50, default 10). The aggregate (`AverageScore`, `ReviewsCount`,
   `TopFlavors`) always covers **all** reviews, not just the 5 returned.
5. **Owners** are grouped per user: `UserId`, `DisplayName`, `AvatarUrl`, `BottleCount`
   (= sum of `Quantity`, the "every total counts physical bottles" rule). The response carries
   `OwnersCount` (all) and the top **20** owners (by `BottleCount` desc, then `DisplayName`).
6. **Listings** = linked bottles with `IsForSale`. Each row: `BottleId`, seller (id, name, avatar),
   `ForSaleQuantity`, `AskingPrice`, `Currency`, `Condition`, `VintageYear`, `ForSaleAt`. Ordered by
   `ForSaleAt` desc. The frontend opens the existing `BottleDetailPanel` for a row (it already has
   `MakeOfferSection`) — no new offer flow.
7. **Pricing convergence.** One helper decides the price key of a bottle:
   `PriceKey.ForBottle(productCanonicalKey, distilleryName, name, category, age, vintageYear, volumeMl)`
   → `productCanonicalKey ?? ProductKey.For(...)`. `productCanonicalKey` is the stored
   `Product.CanonicalKey` of a **non-deleted** linked product, else `null`. **Every** place that
   computes a bottle's key calls this helper — 3 in `PriceEstimationService`, 1 in `PreWarmWorker`,
   1 in `InternalMarketPriceProvider` (listings + accepted offers). `ProductKey` normalization stays
   **frozen**.
   - The Claude input for a linked bottle uses the **product's** fields: `Name`, distillery name ??
     `Brand`, `Category`, `Age`, `VintageYear = null`, `VolumeMl`, `Barcode = product ?? bottle`.
   - Old snapshots under bottle-based keys are a cache: they stop being read, are **not deleted**,
     and simply age out of use. No data change → no `db-destructive-approved` needed.
8. **Product price on the page** — new `GET /api/prices/product/{productId}` (`[Authorize]`,
   **cache-only**, `204` when nothing is cached, `404` for an unknown/deleted product). Same
   rule as `GET /api/prices/bottle/{id}`: never a Claude call on the request path.
9. **Frontend:** `/catalog` + `/product/:id` are public routes. Shared review UI
   (`ReviewsAggregate`, `ReviewCard`) and the estimate view move out of `BottleDetailPanel` into
   their own component files so the product page reuses them. New i18n namespaces **`catalog`** and
   **`product`** (bg + en) + `nav.catalog` + `bottle.viewProduct`.
10. **Two PRs.** PR 1 = slice 01 (pricing, with its own tests — it ships alone and changes live
    behaviour). PR 2 = slices 02–07 (read endpoints + frontend + tests + CLAUDE.md).

## 3a. Architecture at a glance
```
/catalog ── useInfiniteQuery ──▶ GET /api/products/browse?search=&category=&page=   (anon)
   │ card click                                 └─ { items: ProductCardDto[], hasMore }
   ▼
/product/:id ── useQuery ──▶ GET /api/products/{id}                                  (anon)
   │                          └─ ProductDetailDto { product, community, owners, listings, latestReviews }
   ├─ "show more" reviews ──▶ GET /api/products/{id}/reviews?page=                    (anon)
   ├─ estimate (auth only) ─▶ GET /api/prices/product/{id}   (cache-only, 204 = none)
   └─ listing row ─────────▶ GET /api/bottles/{bottleId} → BottleDetailPanel (make an offer)

BottleDetailPanel ── "View product" (bottle.productId) ──▶ /product/:id
NavBar ── "Catalog" ──▶ /catalog

Pricing: bottle ─▶ PriceKey.ForBottle(product?.CanonicalKey, bottle fields…)
                   ├─ linked + product alive → Product.CanonicalKey   (shared by all copies)
                   └─ else                   → ProductKey.For(bottle fields)  (unchanged)
         used by PriceEstimationService ×3, PreWarmWorker, InternalMarketPriceProvider
```

## 4. What already exists (reuse, don't rebuild)
- `ProductsController` + `IProductCatalogService` / `ProductCatalogService` /
  `ProductCatalogValidationDecorator` — new methods go on the **same** trio.
- `ProductDto` (search result shape), `Product.CanonicalKey` (already `ProductKey.For(distillery ??
  brand, name, category, age, null, volume)` — exactly the key pricing needs).
- `BottleReviewService.GetReviewsAsync` — the aggregate formula to mirror: `Math.Round(avg, 1)`,
  top-3 flavors ordered by count desc **then enum value asc**.
- `BottleReviewDto` — reused as-is for product reviews (it already has `BottleId`).
- `PriceEstimationService.GetCachedBottleEstimateAsync` + `PricesController` + the
  `PriceEstimationValidationDecorator` — template for the product estimate.
- `GET /api/bottles/{id}` is already `[AllowAnonymous]` — the frontend only lacks a `getBottle()`.
- Frontend: `BottleDetailPanel` (`ReviewsAggregate`, `ReviewCard`, `EstimateSection`), `Avatar`,
  `NavBar`, `searchProducts`, shadcn primitives in `components/ui/`, TanStack Query conventions,
  `BrowsePage` (card-grid reference), `PublicBarPage` (opens the panel from a list).
- Tests: `ProductCatalogServiceTests`, `PriceEstimationServiceTests`, `PreWarmWorkerTests`,
  `InternalMarketPriceProviderTests`, `PriceEstimationValidationDecoratorTests`, `PricesControllerTests`.
  E2E: `VirtualBar.Web/tests/product-select.spec.ts`, `reviews.spec.ts`.

## 5. Backend conventions (from CLAUDE.md — follow exactly)
- `Result<T>` + typed factories; decorator owns all guards + `ThrowIfCancellationRequested()`;
  inner service pure; DI in `DependencyInjection.cs`.
- `[AllowAnonymous]` explicit on the three new product endpoints; prices stay `[Authorize]`.
- Full XML docs on every new action; `cancellationToken` everywhere; one type per file; one blank
  line between properties; no `ToLower()` on columns.
- Projections run in SQL (`AsNoTracking` + `Select`), never load entities then map.

## 6. Risks
- **Missing one key site** — if one of the 5 places keeps the old formula, the snapshot written by
  one path is never read by another (silent cache misses → more Claude calls). Mitigation: the single
  `PriceKey.ForBottle` helper + a grep check in slice 01 (`ProductKey.For(` must appear only in
  `PriceKey`, `ProductSeeder`, `ProductRequestService`, `BottleService` and tests) + a cross-path test
  (estimate written via `GetBottleEstimateAsync` is read by `GetCachedBottleEstimateAsync`,
  `GetCollectionValueAsync` and skipped as fresh by `PreWarmWorker`).
- **Vintage loses price precision** — a linked 1998-vintage bottle is priced as the core-range
  product. Accepted (decision of #50); a vintage-specific catalog stays parked.
- **Cold cache right after deploy** — linked bottles move to new keys, so their estimates show "—"
  until the pre-warm job researches the product keys. Bounded by `DailyCallBudget`; the number of
  distinct keys **drops**, so steady-state cost goes down. Mention it in the PR 1 description.
- **Wrong link = wrong price** — a bottle wrongly linked now inherits another product's price. The
  link was already exact-key only or an explicit user pick; accepted.
- **Correlated sub-queries on browse cards** (owners/score/listings per card) — 24 rows per page,
  each an index seek on `Bottle.ProductId`; fine. If it ever shows in logs, drop the counts from the
  card, not the paging.
- **Privacy** — owners and sellers are already public on `/bar/:userId` and the marketplace; the
  product page shows nothing new about a user.

## 7. Parked (not in this feature)
- Wish list by product (`WishListItem.ProductId`).
- Sorting the catalog by score / popularity.
- Showing "X rated product Y" in the feed.
- Deleting orphaned old-key `PriceSnapshots` (would need explicit approval — CRITICAL RULES).
- Vintage-specific catalog entries.

## 8. Slice index, dependencies & order
| Slice | Doc | Depends on | PR |
|---|---|---|---|
| 1 | `01-pricing-convergence.md` (key helper, 5 sites, product estimate endpoint, **its tests**) | — | 1 |
| 2 | `02-contracts-dtos.md` (DTOs + interface methods) | — | 2 |
| 3 | `03-catalog-service.md` (`BrowseAsync`, `GetProductAsync`, `GetProductReviewsAsync` + decorator) | 2 | 2 |
| 4 | `04-api.md` (3 actions on `ProductsController`) | 3 | 2 |
| 5 | `05-frontend-catalog.md` (types, API, `/catalog`, NavBar, i18n) | 4 | 2 |
| 6 | `06-frontend-product-page.md` (`/product/:id`, shared review/estimate components, panel link) | 1, 4, 5 | 2 |
| 7 | `07-tests-and-docs.md` (backend tests for 2–4, E2E spec, CLAUDE.md) | 1–6 | 2 |

**Order:** 1 → (PR 1 merged) → 2 → 3 → 4 → 5 → 6 → 7. Slices 2–4 have build-only gates, tests in 7
(the existing convention). Slice 1 is the exception: it changes live behaviour and ships on its own,
so it carries its own tests.

## 9. Verification protocol (REQUIRED)
- Backend slices: `dotnet build VirtualBar.Api/VirtualBar.Api.csproj --no-restore -v q` → 0 errors,
  no new warnings.
- Slice 1 and 7: `dotnet test VirtualBar.Tests/VirtualBar.Tests.csproj --verbosity minimal` →
  `Failed: 0`; 100% branch coverage on every new/changed service and decorator method.
- Frontend slices: `npm run lint` → 0 problems; `npm run build` clean; click through in **bg + en**,
  logged out and logged in.
- Issue status: set `status: In Progress` when slice 1 starts, `status: Ready For Testing` when PR 2
  is open.
- Do **not** commit or push unless the user asks.

## 10. Done when (from #50)
- [x] The product page shows the score, flavors, owners, listings and reviews of every bottle linked
      to the product. → slices 3, 6
- [x] `/catalog` lets a visitor who is not logged in find a product and open its page. → slices 3, 5, 7 (E2E)
- [x] Two linked bottles of the same product share one price snapshot. → slice 1
- [x] Tests at 100% branch coverage for the new service methods; `dotnet test` → `Failed: 0`. → slices 1, 7
- [x] `npm run lint` → 0 problems; `npm run build` clean. → slices 5, 6
- [x] CLAUDE.md updated (new endpoints, routes, pricing key note, i18n namespaces). → slice 7
