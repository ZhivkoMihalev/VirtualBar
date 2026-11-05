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
| `ImageUrl` | `string?` | Optional display image |

- At least one of `DistilleryId` or `Category` must be set (enforced by `WishListValidationDecorator`).
- Matching in `BottleService.ListForSaleAsync` uses AND of all non-null criteria.
- Visible to logged-in collectors in the Marketplace "LOOKING FOR" tab (`GET /api/wishlist/all`, `[Authorize]`): other
  users can click "СЪОБЩЕНИЕ" to open a chat with the searcher. A visitor sees the marketplace login gate instead
  (`.claude/rules/frontend/routing.md`, #100).

## Related rules
- The matching runs in `BottleService.ListForSaleAsync` (`.claude/rules/domain/bottles.md`) and sends `WishListMatch`
  (`.claude/rules/domain/notifications.md`).
- `DistilleryId` refers to the seeded distilleries (`.claude/rules/domain/distilleries.md`).
