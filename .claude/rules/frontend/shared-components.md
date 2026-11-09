---
paths:
  - "VirtualBar.Web/src/components/**"
  - "VirtualBar.Web.Tests/unit/components/**"
  - "VirtualBar.Web/src/pages/**"
  - "VirtualBar.Web.Tests/unit/pages/**"
---

# Shared components

- `src/components/NavBar.tsx` — imported by every page.
  - Contains `<NotificationBell />` and `<LanguageSwitcher />` in the right slot (authenticated only).
  - The "СЪОБЩЕНИЯ" nav item is a button that calls `toggleInbox()` from `ChatContext` — not a route link.
- `src/components/Avatar.tsx` — props: `displayName`, `avatarUrl?`, `size`. Renders `<img>` when avatarUrl present,
  initials div otherwise.
- `src/components/NotificationBell.tsx` — bell icon with gold badge (unread count), dropdown with last 30
  notifications, mark-read / mark-all-read.
  - Polls every 30 s via `refetchInterval`.
  - `NewMessage` notifications call `openChat(actorId)` from `ChatContext` instead of navigating.
  - `BadgeEarned` notifications render the translated badge name from `resourceName` and navigate to `/profile` (no
    actor emphasis — actor == recipient).
- `src/components/Footer.tsx` — mounted once in `App.tsx`; fully opaque `#07030A` background.
- `src/components/BottleDetailPanel.tsx` — full-screen overlay for bottle details.
  - Shows `SaleSection` + `DeleteSection` for the owner.
  - Shows `MakeOfferSection` for non-owners (any authenticated user, regardless of `IsForSale`).
  - Shows a listed bottle to non-owners in `ListingSection`: the asking price (per bottle when several are offered) and
    the available count. A visitor gets no price from the API, so the section shows a "Влез, за да видиш цената" link
    to `/login` (`bottle.loginToSeePrice`) in its place, with the count still visible (#103).
  - `EstimateSection` (price estimate) for authenticated users only — the `/api/prices` endpoint is `[Authorize]`, so
    rendering it anonymously would 401-redirect to `/login`.
  - `LikesSection`, `ReviewsSection` (aggregate header + one-per-user review form + review list; form
    authenticated-only), and `CommentsSection` (below reviews) for all.
  - `onDelete` prop (DashboardPage only) invalidates bottles query and closes panel.
  - Bottles reserved for buyers (#130, `reservedQuantity`): `QuantitySection` cannot go below the listed plus reserved
    count and says how many are reserved; `SaleSection` offers only the free bottles (`bottle.allReserved` when none);
    `DeleteSection` shows `bottle.removeReserved` instead of the remove button.
  - A bottle that came through a deal shows "Купена от" (`bottle.acquiredFrom`) with the seller linked to their bar.
  - The sale and offer forms offer only `CURRENCIES` from `src/lib/currencies.ts`, EUR by default
    (`.claude/rules/domain/currencies.md`).
  - The owner gets `BarcodeSection`: the bottle's barcode, typed or scanned, saved through an update that echoes the
    whole bottle (`echoBottle`, which `QuantitySection` uses too) (#49).
  - A bottle linked to a catalog product (`productId`) shows a "View product" link (`bottle.viewProduct`, visible to
    everyone) that closes the panel and opens `/product/:id`.
- Bottle cards (`BarShelf`, Marketplace) show a gold `★ <avg>` badge when `reviewsCount > 0`.
- `src/components/reviews/ReviewsAggregate.tsx` (average/100, count, top flavors; takes the three values, plus an
  optional `emptyLabel`) and `src/components/reviews/ReviewCard.tsx` — shared by the bottle panel and the product page.
  - Their helpers (`NOTE_FIELDS`, `hasAnyNote`, `formatRelativeTime`) live in `src/lib/reviewFormat.ts`.
- `src/components/PriceEstimateView.tsx` — the presentational estimate block (range, confidence, sources, "as of", "—"
  when nothing is cached).
  - The caller owns the query (`EstimateSection` in the panel for a bottle, `ProductPage` for a product).
- `src/components/CategoryPill.tsx` — the round category filter button (`aria-pressed`) used by Marketplace and the
  catalog.
- `src/components/ReceiptReminder.tsx` — mounted once in `App.tsx` (#130). For a logged-in buyer with offers
  `AwaitingReceipt` (it reads `GET /api/offers/sent`, query key `['offers', 'sent']`) it opens a dialog "Получи ли
  бутилката?" with "Получих бутилката" per purchase and "Още не".
  - "Още не" stores the session's token in `sessionStorage` (`vbar_receipt_reminder_dismissed`), so it asks again after
    the next login and in a new tab or browser session; storage errors are ignored.
  - It renders nothing for a visitor or when no purchase waits.
- `src/components/ChatWidget.tsx` — floating Facebook Messenger-style chat.
  - `position: fixed; bottom: 0; right: 20px; zIndex: 1000`. Renders only for authenticated users.
  - Layout right-to-left: gold circular toggle button (52px, unread badge) → inbox panel (300×480px) → active
    conversation window (320×480px).
  - Polls inbox every 30 s. Mark-as-read runs automatically when a thread is viewed.
- `src/components/BarcodeScannerDialog.tsx` — reads a bottle's barcode with the camera (#49); used by the add form and
  the bottle panel's `BarcodeSection`.
  - `@zxing/browser` over `getUserMedia`, the rear camera (`facingMode: environment`), EAN-13, EAN-8, UPC-A and UPC-E
    only; the library verifies the check digit. It is loaded with a dynamic `import()` when a scan starts, so it is a
    chunk of its own, not part of the main bundle.
  - It hands the first barcode read to `onDetected`; the caller closes it. The camera is stopped on that read and on
    every close.
  - It says why it cannot scan: `insecure` (no `navigator.mediaDevices`: the page is not HTTPS), `denied`
    (`NotAllowedError`, `SecurityError`), `noCamera` (`NotFoundError`, `OverconstrainedError`), `unavailable` (any
    other, e.g. an in-app browser's `NotSupportedError`). The caller's barcode field stays the way in.
- `src/components/DistillerySelect.tsx` — autocomplete dropdown for selecting a distillery.
  - Props: `value`, `onChange`, `category?`.
  - ARIA roles (`combobox`/`listbox`/`option`), keyboard navigation (ArrowUp/Down/Enter/Escape), scroll-into-view.
  - Fetches from `GET /api/distilleries?category=...`, `queryKey: ['distilleries', category ?? 'all']`, staleTime 5 min.
- `src/components/BadgeChip.tsx` — circular gold-bordered achievement medallion.
  - Static `badgeType → lucide icon` map (19 entries) + translated name (`badges` namespace); `size` prop; dimmed
    variant for `earned: false`.
  - Used by `ProfilePage`: achievements section, earned chips first, then unearned with thin gold progress bars +
    "7/10" labels; `queryKey: ['badges','progress']`.
  - Used by `PublicBarPage`: earned-only strip under the bar header; `queryKey: ['badges', userId]`;
    empty/loading/error → renders nothing.
