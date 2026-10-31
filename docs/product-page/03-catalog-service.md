# Slice 03 — Catalog service: browse, product detail, product reviews

> Prereq: slice 02. Build gate only; test targets are listed at the end and written in slice 07.

## Shared query pieces (private helpers in `ProductCatalogService`)
- `LinkedBottles(productId)` = `db.Bottles.Where(b => b.ProductId == productId && !b.IsDeleted)`.
- `LinkedReviews(productId)` = `db.BottleReviews.Where(r => !r.IsDeleted && !r.Bottle.IsDeleted && r.Bottle.ProductId == productId)`.
- Product filter everywhere: `!p.IsDeleted`. Distillery name / brand fallback exactly as `SearchAsync`
  (copy the comment's reasoning, not the comment).

## `BrowseAsync(search?, category?, page, pageSize)` — inner
1. `q = Products.AsNoTracking().Where(!IsDeleted)`; category filter if set.
2. If `search` is not null (decorator already trimmed/validated): same `Contains` on
   `Name`/`Brand`/`Distillery.Name`, order `StartsWith desc, Name, Id`. Else order `Name, Id`
   (`Id` makes paging stable when names repeat).
3. `.Skip((page - 1) * pageSize).Take(pageSize + 1)` → project to `ProductCardDto` with correlated
   counts:
   - `OwnersCount = p.Bottles.Where(b => !b.IsDeleted).Select(b => b.UserId).Distinct().Count()`
     (`Product.Bottles` navigation exists);
   - `ReviewsCount`, `AverageScore` (`(double?)Average(r => r.Score)`, round in memory after the
     query to 1 decimal — `Math.Round` on a nullable aggregate is not reliably translated);
   - `ListingsCount` = linked bottles with `IsForSale`.
4. `HasMore = rows.Count > pageSize`; `Items = rows.Take(pageSize)`.

## `GetProductAsync(productId)` — inner
Several small queries (clearer than one giant projection; all `AsNoTracking`):
1. Product row → `ProductDetailDto` product fields (+ `Description`). The decorator has already
   checked existence (see below), so the inner uses `FirstAsync`.
2. Aggregate: `ReviewsCount = LinkedReviews.Count()`; `AverageScore = LinkedReviews.Average(score)`
   rounded to 1 decimal, `null` when count is 0.
3. Top flavors: `db.BottleReviewFlavors.Where(f => LinkedReviews contains f.ReviewId)`
   `.GroupBy(f => f.Flavor).OrderByDescending(count).ThenBy(key).Take(3)` — same tie-break as
   `BottleReviewService`.
4. Owners: `LinkedBottles.GroupBy(b => b.UserId)` → `{ UserId, BottleCount = Sum(Quantity) }`,
   join `Users` for `DisplayName`/`AvatarUrl`; `OwnersCount` = group count; take 20 ordered by
   `BottleCount desc, DisplayName`.
5. Listings: `LinkedBottles.Where(IsForSale)` ordered `ForSaleAt desc, Id`, projected to
   `ProductListingDto` with the seller's name/avatar.
6. Latest reviews: `LinkedReviews` ordered `CreatedAt desc, Id`, `Take(5)`, projected to
   `BottleReviewDto` **the same way `BottleReviewService` projects it** (extract a shared static
   `Expression<Func<BottleReview, BottleReviewDto>>` into `VirtualBar.Infrastructure/Services/`
   `ReviewProjections.cs` and use it from both services, so the two cannot drift; flavors ordered
   by enum value as today).

## `GetProductReviewsAsync(productId, page, pageSize)` — inner
`LinkedReviews` ordered `CreatedAt desc, Id`, `Skip/Take(pageSize + 1)`, shared projection,
`HasMore` as in browse.

## Decorator — `ProductCatalogValidationDecorator`
Every method: `ThrowIfCancellationRequested()` first.
- `BrowseAsync`: `search` → trim; blank → `null`; non-null and `< 2` → `Fail("Search term must be at
  least 2 characters.")`; `> 100` → `Fail("Search term is too long.")`. `page < 1` → 1;
  `page > 1000` → `Fail("Page is out of range.")`. `pageSize` < 1 → 24, > 48 → 48.
- `GetProductAsync` / `GetProductReviewsAsync`: product must exist and not be deleted →
  else `NotFound("Product not found.")` (the decorator gains `AppDbContext` in its
  ctor, as `BottleReviewValidationDecorator` and others already do). Reviews
  `page`/`pageSize` clamp: page ≥ 1 (≤ 1000), pageSize 1–50, default 10.
- Update the DI factory in `DependencyInjection.cs` if the decorator ctor changes.

## Test targets (write in slice 07)
Every branch above: search null/blank/short/long/valid; category on/off; page/pageSize clamps;
`HasMore` true/false; ordering with and without search; counts ignore deleted bottles, deleted reviews
and reviews on deleted bottles; `AverageScore` null with no reviews; owners grouped and summed by
`Quantity`, capped at 20 with correct `OwnersCount`; listings only `IsForSale`; latest reviews ≤ 5;
top-flavor tie-break; unknown and deleted product → `NotFound`.

## Gate
`dotnet build` → 0 errors.
