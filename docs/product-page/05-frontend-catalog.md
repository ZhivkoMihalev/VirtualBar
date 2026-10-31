# Slice 05 — Frontend: catalog page, NavBar, API layer

> Prereq: slice 04. Gate: `npm run lint` 0 problems, `npm run build` clean.

## Types — `src/types/index.ts`
`PagedResult<T> { items: T[]; hasMore: boolean }`, `ProductCard`, `ProductDetail`, `ProductOwner`,
`ProductListing` (camelCase mirrors of the slice 02 DTOs; enums as the existing string unions).

## API
- `src/api/productsApi.ts`: `browseProducts({ search?, category?, page, pageSize? })`,
  `getProduct(id)`, `getProductReviews(id, page, pageSize?)`. Do not send empty `search`.
- `src/api/pricesApi.ts`: `getProductEstimate(productId)` → `PriceEstimate | null` (204 → `null`,
  same handling as the bottle estimate function).
- `src/api/bottlesApi.ts`: `getBottle(id)` → `GET /api/bottles/{id}` (already public).

## `/catalog` — `src/pages/CatalogPage.tsx`
- Public route in `App.tsx` next to `/browse`.
- State lives in the URL (`useSearchParams`: `q`, `category`) so back/forward and shared links work.
- Search input with a 300 ms debounce; the query runs when the term is empty **or** ≥ 2 chars
  (1 char → keep the previous results and show a small "type at least 2 characters" hint).
- Category filter: the existing `SpiritCategory` values, "All" first; reuse the label keys already
  used by the add-bottle form.
- `useInfiniteQuery({ queryKey: ['products', 'browse', q, category], getNextPageParam: (last, all) => last.hasMore ? all.length + 1 : undefined })`.
- Product card (shadcn `Card` + speakeasy styles): image (fallback: the category bottle silhouette /
  `CATEGORY_COLORS` from `src/lib/bottlePalette.ts`), name, brand · distillery, category, age, volume,
  and a footer row: `★ 87.5 (12)` when `reviewsCount > 0`, owners count, "N for sale" when
  `listingsCount > 0`. Whole card is a `Link` to `/product/:id`.
- "Load more" button (not infinite scroll — simpler and keyboard-friendly); loading skeletons;
  empty state ("No products match"); error state.

## NavBar
`nav.catalog` link to `/catalog` for **everyone**, after "Browse", in both the desktop bar and the
mobile sheet.

## i18n — `bg.json` + `en.json`
- `nav.catalog`.
- New namespace `catalog`: `title`, `subtitle`, `searchPlaceholder`, `minChars`, `allCategories`,
  `loadMore`, `empty`, `error`, `owners` (plural forms), `forSale` (plural), `reviews` (plural).
  Use i18next `_one`/`_other` keys for the plurals (check how existing namespaces do plurals and copy).

## Gate
`npm run lint` → 0 problems; `npm run build` clean; open `/catalog` logged out in bg and en: list
without a term, search, category filter, load more, open a card.
