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
  - `/browse` (`BrowsePage`), `/marketplace` (`MarketplacePage`), `/bar/:userId` (`PublicBarPage`, a collector's
    public bar);
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
- The catalog keeps its search term and category in the URL (`?q=&category=`; typing replaces the history entry, a
  category click pushes one), so back/forward and shared links reproduce the view.
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
- Opens a listing in `BottleDetailPanel`, queried as `['bottles', ownerId, bottleId]`, so the panel's own
  `['bottles', ownerId]` invalidations refresh it.
- Renders a "not found" state for an unknown id instead of redirecting.
