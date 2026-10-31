# Slice 06 — Frontend: product page, shared components, "View product" link

> Prereq: slices 01, 04, 05. Gate: `npm run lint` 0 problems, `npm run build` clean.

## Step 1 — extract shared components (no behaviour change)
Move out of `BottleDetailPanel.tsx` into their own files (component-only files, the
`react-refresh/only-export-components` rule):
- `src/components/reviews/ReviewsAggregate.tsx` — takes `{ averageScore, reviewsCount, topFlavors }`
  instead of the whole bottle summary, so both callers can pass their data.
- `src/components/reviews/ReviewCard.tsx` (+ `hasAnyNote`, `formatRelativeTime` into
  `src/lib/reviewFormat.ts`).
- `src/components/PriceEstimateView.tsx` — the presentational part of `EstimateSection` (range,
  confidence, sources, "as of", "—" for none). `EstimateSection` keeps its query and renders the view.
Run lint + build and click a bottle panel to confirm nothing changed before going on.

## Step 2 — `/product/:id` — `src/pages/ProductPage.tsx`
Public route in `App.tsx`. `useQuery(['products', id], getProduct)`; a `404` renders a "Product not
found" state with a link back to `/catalog` (do not redirect).

Sections, top to bottom:
1. **Header** — image, name, brand · distillery, category, age, ABV, volume, country/region,
   description. "Back to catalog" link.
2. **Community** — `ReviewsAggregate` (score, count, top-3 flavors). No reviews → "No reviews yet".
3. **Estimate** — logged-in only (`useAuth().isAuthenticated`), `useQuery(['prices', 'product', id])`,
   `PriceEstimateView`. Anonymous users see nothing (an anonymous call would 401-redirect to `/login`).
4. **For sale** — one row per listing: seller avatar + name (link `/bar/:sellerId`), `×N` when
   `forSaleQuantity > 1`, asking price per bottle + currency, condition, vintage. Row button "Open" →
   `getBottle(bottleId)` (`useQuery` enabled on click, or `queryClient.fetchQuery`) → render
   `BottleDetailPanel` with that bottle (same props as `PublicBarPage`), which already shows
   `MakeOfferSection` to non-owners. Empty → "Nobody is selling it right now".
5. **Owners** — avatar grid, each a link to `/bar/:userId`, `×N` when `bottleCount > 1`,
   "and N more" when `ownersCount > owners.length`.
6. **Reviews** — first the `latestReviews` from the detail; "Show more" switches to
   `useInfiniteQuery(['products', id, 'reviews'], page => getProductReviews(id, page, 10))`
   starting at page 1 (replace the 5, don't append duplicates). Each `ReviewCard` gets a small
   "on <owner>'s bottle" link opening the panel the same way as listings (optional — skip if it
   clutters the card).

Invalidation: when `BottleDetailPanel` closes after an offer/review, invalidate `['products', id]`.

## Step 3 — "View product" in `BottleDetailPanel`
When `bottle.productId` is set: a `Link` to `/product/${bottle.productId}` in the header area,
`t('bottle.viewProduct')`, that also calls `onClose`. Visible to everyone.

## i18n — `bg.json` + `en.json`
- `bottle.viewProduct`.
- New namespace `product`: `backToCatalog`, `notFound`, `community`, `noReviews`, `estimate`,
  `forSale`, `nobodySelling`, `open`, `perBottle`, `owners`, `andMore`, `reviews`, `showMore`,
  `vintage`, `condition` (reuse existing condition labels if a namespace already has them).

## Gate
`npm run lint` → 0 problems; `npm run build` clean. Click through in bg + en:
- logged out: catalog → product → no estimate, listings open the panel read-only;
- logged in as a non-owner: estimate visible (or "—"), open a listing → make an offer;
- a bottle panel of a linked bottle → "View product" → the product page;
- a product with > 5 reviews → "Show more" pages correctly.
