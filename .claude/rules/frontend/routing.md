---
paths:
  - "VirtualBar.Web/src/App.tsx"
  - "VirtualBar.Web/src/contexts/AuthContext*"
  - "VirtualBar.Web/src/pages/**"
  - "VirtualBar.Web.Tests/unit/pages/**"
  - "VirtualBar.Web.Tests/e2e/**"
---

# Routing

## Routes
- `/` is `HomePage.tsx` (public news/social feed).
- **Public:**
  - `/browse` (`BrowsePage`), `/bar/:userId` (`PublicBarPage`, a collector's public bar). A visitor sees which bottles
    are for sale, but not their asking price: the API sends none, and `BottleDetailPanel` shows a login link in its
    place (#103);
  - `/marketplace` (`MarketplacePage`) is reachable by anyone, but its content is for logged-in collectors: a visitor
    sees a login gate (`LoginGate`, `marketplace.loginGate*` keys) with "log in" and "register" links, and the page sends
    no marketplace or wish list request (#100). `GET /api/bottles/marketplace` and `GET /api/wishlist/all` are
    `[Authorize]`;
  - `/catalog` (`CatalogPage`) and `/product/:id` (`ProductPage`);
  - `/confirm-email` (`ConfirmEmailPage`);
  - `/forgot-password` and `/reset-password`, the password reset pages (`.claude/rules/domain/users.md`).
- `/login` and `/register` send a logged-in user to `/`. Login and register redirect to `/` after success.
- **Protected:** `/dashboard` (the user's own virtual bar), `/offers` (the Offers page), `/profile`.
- **Admins only:** `/admin/product-requests` (`ProductRequestsAdminPage`); `AdminRoute` sends anyone else to `/`.
- There is **no `/messages` page** (the path redirects to `/`): messaging is handled entirely by the floating
  `ChatWidget`.
- Any other path redirects to `/`.

## Email confirmation
`Identity:RequireConfirmedEmail` is on by default outside Development.
- Register then answers with an empty token and `requiresEmailConfirmation: true`, so `AuthContext.register` stores
  nothing and returns `'emailConfirmationRequired'`, and `RegisterPage` shows "check your email".
- The link in the email opens `/confirm-email?email=…&token=…` (`ConfirmEmailPage`, public), which calls
  `POST /api/auth/confirm-email` once on load.
- `ResendConfirmation` (`POST /api/auth/resend-confirmation`) is offered there when the link is invalid or expired, on
  the check-your-email screen, and under a login refused with `EmailNotConfirmed`.
- If the confirmation email fails to send, register still answers `requiresEmailConfirmation: true` (the account
  already exists; `AuthService` logs the error). The user lands on "check your email" and recovers with
  `ResendConfirmation` once email works again.

## Catalog (`/catalog`)
- The catalog keeps its search term, category and order in the URL (`?q=&category=&sort=`; typing replaces the history
  entry, a category or sort choice pushes one), so back/forward and shared links reproduce the view.
- The sort control (#56) is a shadcn `Select` next to the search box, labelled `catalog.sortLabel`, with one
  `catalog.sort<Value>` option per `ProductSort`.
  - The default order for the current term (`Name`, or `Relevance` with a term) is left out of the URL; an unknown
    `sort` value is ignored, and `Relevance` without a term reads as `Name`, as on the server.
  - "Best match" (`Relevance`) is offered only while there is a term.
  - The sort is part of the query key, so a new order starts again from page 1.
- It pages with `useInfiniteQuery` and a "show more" button.
- The search box keeps what is typed in its own state and writes it to the URL. It is **not** bound to
  `searchParams` directly.
  - Why: React Router commits a URL change in a transition, so a box bound to the URL is re-rendered with the old value
    before the new one lands, and fast typing lost letters ("Lagavulin" became "gvlin" at 80 ms a key).
  - The box takes the URL's term only on a POP (back/forward); `catalog-browse.spec.ts` types at full speed to keep it
    that way.

## Product page (`/product/:id`)
- Shows the price estimate only to logged-in users (`/api/prices` is `[Authorize]`; an anonymous call would
  401-redirect to `/login`).
- Shows the listings ("За продан") only to logged-in collectors, as on the marketplace. A visitor sees only how many
  there are (`product.listingsCount_one`/`_other`, from `ProductDetailDto.ListingsCount`) and a "Влез, за да ги видиш"
  link to `/login` (`ListingsLoginGate`); with none for sale, the same empty text as a collector (#102). The API sends a
  visitor no listings anyway.
- Opens a listing in `BottleDetailPanel`, queried as `['bottles', ownerId, bottleId]`, so the panel's own
  `['bottles', ownerId]` invalidations refresh it.
- Renders a "not found" state for an unknown id instead of redirecting.
- Offers a logged-in collector a "Want it" button that adds the product to their wish list
  (`.claude/rules/domain/wish-list.md`).
- A dated product (#59) shows its year as a badge next to its category: `product.vintageBadge` ("Реколта 1988") for a
  `vintageYear`, `product.releaseBadge` ("Издание 2021") for a `releaseYear`.
- When its line has other years, `LineYears` lists them under the header as "Други реколти" / "Други издания"
  (`product.otherVintages` / `product.otherReleases`): every year of the line oldest first, this one in its place marked
  `aria-current="page"` and "тази" (`product.thisYear`), the others linked to their pages with their score and listings
  count. The line itself comes from the API (`.claude/rules/domain/catalog.md`, Years).
