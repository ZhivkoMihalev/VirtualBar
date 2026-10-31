# Slice 07 — Backend tests, E2E, CLAUDE.md

> Prereq: slices 01–06. Gate: `dotnet test` → `Failed: 0`; 100% branch coverage on every new method.

## Backend unit tests
### `ProductCatalogServiceTests` (extend; InMemory, `Guid.NewGuid()` DB per test)
Seed helpers (`SeedProduct`, `SeedUser`, `SeedBottle(productId, quantity, isForSale…)`,
`SeedReview(bottle, score, flavors)`) — add the ones missing. Cover the slice 03 test targets:
- `BrowseAsync_WhenNoSearch_ReturnsAllOrderedByName`
- `BrowseAsync_WhenSearch_RanksStartsWithFirst`
- `BrowseAsync_WhenCategory_FiltersByCategory`
- `BrowseAsync_WhenMoreThanPageSize_SetsHasMore` / `…_WhenLastPage_HasMoreFalse`
- `BrowseAsync_WhenDeletedProduct_Excluded`
- `BrowseAsync_Counts_IgnoreDeletedBottlesAndReviews`
- `GetProductAsync_WhenNoReviews_AverageIsNull`
- `GetProductAsync_AggregatesAcrossAllLinkedBottles`
- `GetProductAsync_TopFlavors_TieBreakByEnumValue`
- `GetProductAsync_Owners_GroupedBySumOfQuantity_CappedAt20`
- `GetProductAsync_Listings_OnlyForSale_NewestFirst`
- `GetProductAsync_LatestReviews_AtMostFive`
- `GetProductReviewsAsync_Pages_HasMore`

If InMemory cannot translate a projection (correlated `Distinct().Count()`, grouped joins), switch
**that** test to SQLite in-memory (the `OfferServiceTests` setup) rather than changing the query.

### `ProductCatalogValidationDecoratorTests` (create if missing, else extend)
Every guard: null/blank search → passes `null`; 1 char → 400; 101 chars → 400; page 0 → 1;
page 1001 → 400; pageSize 0 → 24, 100 → 48; reviews pageSize clamps; unknown/deleted product →
404 for detail and reviews; cancelled token throws.

### Controller
If `PricesControllerTests` is the pattern for controllers, add `ProductsControllerTests` for the
three actions (200 / 400 / 404 mapping). Otherwise skip — `ToActionResult` is already covered.

### Shared review projection
`BottleReviewServiceTests` must still pass unchanged after extracting `ReviewProjections` (that is
the regression test for the move).

## E2E — `VirtualBar.Web/tests/catalog.spec.ts`
Logged out: open `/catalog` → results without a term → search a seeded product name → open it →
heading shows the name, no estimate section. Logged in (reuse the helpers from `reviews.spec.ts`):
add a bottle linked to that product, review it → the product page shows the score and the review.
Run locally per CLAUDE.md, or add the `e2e` label to PR 2.

## CLAUDE.md (final step)
- Intro paragraph: one sentence on the public catalog and product pages.
- `Product` section → API endpoints: `browse`, `{id}`, `{id}/reviews` (anonymous, paging rule,
  `pageSize + 1`, caps 48 / 50, latest-5 + owners-20 caps).
- `BottleReview` design note: "Reviews attach to the bottle instance … aggregated per product on
  read only (`ProductCatalogService`), never stored."
- Collection Value section: the **price key rule** — linked + live product → `Product.CanonicalKey`,
  else `ProductKey.For(bottle…)`; always via `PriceKey.ForBottle`; old snapshots are left in place.
  Add `GET /api/prices/product/{productId}` to the `/api/prices` list.
- Frontend routing: `/catalog`, `/product/:id` (public). Shared components: `reviews/ReviewsAggregate`,
  `reviews/ReviewCard`, `PriceEstimateView`; `BottleDetailPanel` "View product" link.
- i18n namespace list: `catalog`, `product`.
- `docs/product-catalog/00-OVERVIEW.md` §7 and `docs/bottle-reviews/00-OVERVIEW.md` decision 3:
  mark the three parked items as done, pointing to `docs/product-page/`.

## Gate
`dotnet build` 0 errors; `dotnet test` → `Failed: 0`; coverage report shows 100% branches on
`ProductCatalogService`, `ProductCatalogValidationDecorator`, `PriceKey`, and the changed pricing
methods; `npm run lint` + `npm run build` clean.
