---
paths:
  - "**/*WishList*"
  - "**/wishList*"
  - "**/MarketplacePage*"
---

# WishListItem
| Property | Type | Notes |
|---|---|---|
| `UserId` | `Guid` | FK → AppUser (`DeleteBehavior.Restrict`) |
| `BottleName` | `string?` | User label only — NOT used in matching |
| `DistilleryId` | `Guid?` | FK → Distillery — matching criterion (exact FK match) |
| `Category` | `SpiritCategory?` | Matching criterion (exact enum match) |
| `ProductId` | `Guid?` | FK → Product (`DeleteBehavior.Restrict`, indexed) — matching criterion against `Bottle.ProductId` |
| `ImageUrl` | `string?` | Optional display image |

- At least one of `ProductId`, `DistilleryId` or `Category` must be set (enforced by `WishListValidationDecorator`,
  `400`).
- A given `DistilleryId` or `ProductId` must exist and not be deleted, else `404` (`Distillery not found.` /
  `Product not found.`).
- `WishListItemDto` and `PublicWishListItemDto` carry `ProductId` plus `ProductName`, which is null once the product is
  soft-deleted (the same rule as `DistilleryName`).

## Matching
- Runs in `BottleService.ListForSaleAsync` and uses the AND of all non-null criteria:
  `(w.Category == null || w.Category == bottle.Category)`, the same for `DistilleryId`, and
  `(w.ProductId == null || w.ProductId == bottle.ProductId)`.
- Never the seller's own wishes; each matching user is notified once (`Distinct`), however many wishes match.
- A product wish is the precise one (#51): a wish for "Lagavulin 16" matches only a listing of a bottle **linked** to
  that product, not every Lagavulin.
- Known limitation, out of scope of #51: a bottle that gets linked only **after** it was listed (an admin approval
  retro-links it) notifies nobody. Add-bottle auto-links an exact catalog match, so most bottles are linked when listed.

## API endpoints (`/api/wishlist`, all `[Authorize]`)
- `GET /api/wishlist` — the caller's own items, newest first.
- `GET /api/wishlist/all` — every collector's items, newest first (the Marketplace "LOOKING FOR" tab).
- `POST /api/wishlist` — add an item (`AddWishListItemRequest`); `400` no criterion, `404` unknown distillery or product.
- `POST /api/wishlist/image` — upload an image (JPEG/PNG/WebP, max 5 MB) → `{ url }` under `/uploads/wishlist/`.
- `DELETE /api/wishlist/{id}` — soft-delete an own item; `403` another collector's, `404` missing.

## Frontend
- Visible to logged-in collectors in the Marketplace "LOOKING FOR" tab (`GET /api/wishlist/all`, `[Authorize]`): other
  users can click "СЪОБЩЕНИЕ" to open a chat with the searcher. A visitor sees the marketplace login gate instead
  (`.claude/rules/frontend/routing.md`, #100).
  - A product wish with no `BottleName` is titled by the product name, linked to `/product/:id`; with a label of its
    own, the product shows as a linked badge next to it. A deleted product (`ProductName` null) gets no link.
- The "post a search" form on `MarketplacePage` offers `ProductSelect` above the other fields.
  - Picking a product clears and hides the distillery and category fields: next to a product they could only rule out
    listings of that very bottle. Removing the product brings them back.
- The product page (`ProductPage`) has a "Want it" button for a logged-in collector (`WantItButton`).
  - It adds `{ productId }` to the wish list, and shows "on your wish list" instead once the collector's own list
    (`GET /api/wishlist`, query `['wishlist', 'mine']`) holds a wish for that product.
  - Visitors do not see it: `/api/wishlist` is `[Authorize]`, and an anonymous call would redirect to `/login`.
- Adding or removing a wish invalidates `['wishlist']`, which covers both the public list (`['wishlist', 'all']`) and
  the collector's own.

## Related rules
- The matching runs in `BottleService.ListForSaleAsync` (`.claude/rules/domain/bottles.md`) and sends `WishListMatch`
  (`.claude/rules/domain/notifications.md`).
- `DistilleryId` refers to the seeded distilleries (`.claude/rules/domain/distilleries.md`).
- `ProductId` refers to the catalog; a bottle's own `ProductId` is set when it is added
  (`.claude/rules/domain/catalog.md`, Add-bottle resolution order).
