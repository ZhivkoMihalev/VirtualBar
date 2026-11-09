---
paths:
  - "**/*Bottle*"
  - "**/*bottle*"
  - "**/BarShelf*"
  - "**/DashboardPage*"
  - "**/MarketplacePage*"
  - "**/PublicBarPage*"
---

# Bottles

## Bottle
| Property | Type | Notes |
|---|---|---|
| `UserId` | `Guid` | FK → AppUser (owner) |
| `Name` | `string` | |
| `DistilleryId` | `Guid?` | FK → Distillery (nullable; soft-delete aware in MapToDto) |
| `Region`, `Country` | `string?` | |
| `Category` | `SpiritCategory` | Whisky/Rum/Cognac/Vodka/Gin/Tequila/Brandy/Other |
| `Age` | `int?` | Age statement in years |
| `VintageYear` | `int?` | Year of distillation |
| `AbvPercent` | `double?` | Alcohol by volume |
| `VolumeMl` | `int?` | Bottle volume |
| `Condition` | `BottleCondition` | Sealed/Opened/Empty |
| `Quantity` | `int` | How many identical bottles this entry stands for — see below |
| `ForSaleQuantity` | `int` | How many of the owned bottles are actually on offer — see below |
| `Description` | `string?` | |
| `IsLimited` | `bool` | Limited edition flag |
| `IsForSale` | `bool` | Listed in marketplace |
| `AskingPrice` | `decimal?` | `decimal(18,2)` |
| `Currency` | `string?` | `EUR`, `GBP` or `USD`, stored upper-case (`.claude/rules/domain/currencies.md`) |
| `ForSaleAt` | `DateTime?` | When the bottle was listed for sale (set by `ListForSaleAsync`, cleared by `UnlistFromSaleAsync`) |
| `Barcode` | `string?` | The EAN/UPC on the bottle, 8–14 digits; `nvarchar(14)`, indexed — see below |
| `ProductId` | `Guid?` | FK → Product (nullable) — catalog link; see below |
| `DisplayOrder` | `int` | The bottle's place on its owner's shelf — see below |

### Quantity
- Default `1` (a store default backfills old rows), validated 1–99 in the decorator.
- One entry = the whole stack, so likes/comments/reviews stay aggregated on it.
- **Every total counts physical bottles:** `BadgeCountKind.Bottles`/`LimitedBottles` sum it,
  `UserProfileService.BottleCount` sums it, and Collection Value multiplies the per-bottle estimate by it.
- Set on add; the owner can adjust it from the bottle detail panel (a quantity-only `PUT` must still echo every other
  field).
- **Never below `ForSaleQuantity`:** `UpdateBottleAsync`'s decorator returns `409` rather than leaving the marketplace
  offering bottles the owner no longer has.

### ForSaleQuantity
- A stack can be listed **in part**.
- Invariant maintained by the service, not the DB:
  - `ListForSaleAsync` sets it to `request.Quantity` (validated 1..`Quantity` in the decorator; omitted → **1**, so an
    older client lists a single bottle rather than the whole stack);
  - `UnlistFromSaleAsync` zeroes it.
- Only meaningful alongside `IsForSale`, which stays the authoritative listed/not-listed flag every query filters on.
- `BadgeCountKind.ActiveListings` sums **this**, not `Quantity`; the Dashboard "for sale" stat and the marketplace `×N`
  badge read it too.
- `AskingPrice` stays **per bottle** — the UI labels it so once more than one is offered.
- Changing the offered count means unlist + re-list (the already-for-sale `409` guard still blocks a silent re-list).

### ProductId
- Set by explicit pick or exact-`CanonicalKey` auto-link in `AddBottleAsync`.
- `UpdateBottleAsync` assigns it blindly from the request (null = unlink — clients must echo the current value).
- The bottle's own fields remain the display truth.
- On add, a bottle linked to a vintage-dated product (explicit pick or auto-link) takes the product's `VintageYear` when
  the request gives none; a different year from the collector is kept (#59).
  - The add form shows a warning then (`VintageMismatch`), offering the matching vintage of the line or
    "Request vintage <year>", which unlinks the bottle and renames it so saving files the request.

### Barcode (#49)
- Set on add and assigned on update from the request (`AddBottleRequest`/`UpdateBottleRequest.Barcode`), trimmed, blank
  → null (`BarcodeFormat.Normalize`); `BottleDto` returns it.
- Update assigns it like every other field: null clears it, so a client echoes the current value (the bottle panel's
  quantity change does).
- Not 8–14 digits → `400` from `BottleValidationDecorator` (`BarcodeFormat.IsValid`). The add form therefore sends only
  a valid one (`toBarcode` in `src/lib/barcode.ts`): a mistyped barcode never blocks adding the bottle.
- A linked bottle with a barcode records it for its product (`ProductBarcode`, `.claude/rules/domain/catalog.md`), and
  an unlinked one's product request carries it.
- Pricing uses it when the bottle's product has none (`.claude/rules/domain/pricing.md`).
- **The add form** (`DashboardPage`): a barcode typed or scanned (`BarcodeScannerDialog`) is looked up at once.
  - A product (`Catalog` or `Collectors`) fills every field from it, like a catalog pick, and links it (#120); the link
    is a barcode link, so editing the name keeps it, while changing the barcode or a later miss drops it. For
    `Collectors` the message says how many collectors back it (`addBottle.barcodeFromCollectors`).
  - A suggestion (`Bottles`) fills the fields from other collectors' bottles, links nothing, and sends its country and
    region with the bottle (`addBottle.barcodeSuggested`): saving files a product request, as for any unknown bottle.
  - A miss (`404`) asks to pick the bottle from the catalog (`addBottle.barcodeNotFound`); the barcode stays in the
    field and is saved with the picked product, which records it (`ProductBarcode`). `400` and other failures have
    their own messages.
- **The bottle panel** (`BarcodeSection`, owner only): the owner types or scans the barcode of a bottle already added,
  which is the only way an older bottle gets one; an empty field clears it.

### DisplayOrder
- A user's bottles are listed in this order.
- `PUT /api/bottles/reorder` sets it from the order of ids the owner sends; it sees only the caller's own bottles, so
  another collector's id answers `404`.

### Relations and computed fields
- One `Bottle` → many `BottleImage`, `BottleLike`, `BottleComment`, `BottleReview`.
- `BottleDto` additionally carries `AverageScore` (1 decimal, null when no reviews) + `ReviewsCount` — **computed in
  queries, never denormalized** (projected the same way `LikesCount`/`CommentsCount` are in `BottleService`).

### Asking price for visitors
- The asking price is for logged-in collectors only, like the marketplace (#103).
- `GET /api/bottles?userId=` and `GET /api/bottles/{id}` stay `[AllowAnonymous]` (public bars), but for a caller who is
  not logged in `BottleService.ForViewer` sets `AskingPrice` and `Currency` to null.
- A visitor still gets `IsForSale` and `ForSaleQuantity`: the owner decided a visitor may see **that** a bottle is for
  sale, only not its price.
- The other endpoints that return a price need no such step: the marketplace is `[Authorize]`, the home feed sends
  for-sale items only to a logged-in user, and the product page sends a visitor no listings (`.claude/rules/domain/catalog.md`).

## API endpoints (`/api/bottles`)
`BottlesController` is `[Authorize]`, except the two public-bar reads. Every refusal below comes from
`BottleValidationDecorator`; a change to another collector's bottle answers `403` (pinned in `OwnershipTests`).
- `GET /api/bottles?userId=` — `[AllowAnonymous]`; a collector's bar: the non-deleted bottles by `DisplayOrder`, then
  newest. `400` empty `userId`. A visitor gets no price (see above).
- `GET /api/bottles/{id}` — `[AllowAnonymous]`; one bottle. `404` missing or deleted. A visitor gets no price.
- `GET /api/bottles/marketplace?search=&category=&sort=` — `[Authorize]` (#100); every listed bottle (`MarketplaceQuery`).
  - `search` (trimmed, blank = none) matches the bottle name or its distillery's name; `category` filters.
  - `sort` is `price_asc`, `price_desc` or `newest`; any other value, or none, is `newest` (`CreatedAt` ↓).
- `POST /api/bottles` — add (`AddBottleRequest`) → `201` with the bottle.
  - `400` blank name, quantity outside 1–99, a barcode that is not 8–14 digits, or an unknown or deleted product
    (`Selected product does not exist.`).
  - `404` unknown or deleted distillery.
- `PUT /api/bottles/{id}` — update (`UpdateBottleRequest`, every field echoed, see ProductId above).
  - `400` blank name, quantity outside 1–99, a barcode that is not 8–14 digits, or an unknown product; `404` missing
    bottle or unknown distillery; `403` another collector's.
  - `409` a quantity below `ForSaleQuantity`.
- `POST /api/bottles/{id}/list-for-sale` — list (`ListForSaleRequest`: price, currency, quantity).
  - `404` missing; `403` another collector's; `409` already listed.
  - `400` a quantity outside 1..`Quantity`, or an unsupported currency (`.claude/rules/domain/currencies.md`).
- `POST /api/bottles/{id}/unlist` — `404` missing; `403` another collector's; `400` not listed.
- `PUT /api/bottles/reorder` — set `DisplayOrder` from the ids sent (`ReorderBottlesRequest`).
  - `400` no ids or duplicate ids; `404` any id that is not one of the caller's own bottles.
  - The caller's bottles missing from the request keep their relative order after the ones sent.
- `DELETE /api/bottles/{id}` — soft delete. `404` missing; `403` another collector's.

## BottleImage
| Property | Type | Notes |
|---|---|---|
| `BottleId` | `Guid` | FK → Bottle |
| `Url` | `string` | Relative path under `/uploads/bottles/` |
| `IsPrimary` | `bool` | Cover image |
| `SortOrder` | `int` | Gallery order |

## BottleLike
Composite PK: `(BottleId, UserId)`. No `BaseEntity`.
| Property | Type |
|---|---|
| `BottleId` | `Guid` |
| `UserId` | `Guid` |
| `LikedAt` | `DateTime` |

## BottleComment
| Property | Type | Notes |
|---|---|---|
| `BottleId` | `Guid` | FK → Bottle |
| `UserId` | `Guid` | FK → AppUser (author) |
| `Content` | `string` | |

## Related rules
Bottle code also does work that other rule files describe:
- `BottleService.AddBottleAsync` links the bottle to the catalog (`.claude/rules/domain/catalog.md`, Add-bottle resolution
  order), notifies followers (`NewBottleFromFollowing`) and evaluates badges (`BottleAdded`).
- `AddBottleAsync` and `UpdateBottleAsync` record a linked bottle's barcode for its product (`ProductBarcode`,
  `.claude/rules/domain/catalog.md`).
- `BottleService.ListForSaleAsync` notifies followers (`BottleListedForSale`), matches wish lists and notifies their
  owners (`WishListMatch`, `.claude/rules/domain/wish-list.md`) and evaluates badges (`BottleListed`).
- `BottleLikeService.LikeAsync` sends `BottleLiked` and evaluates the owner's badges (`LikeReceived`);
  `BottleCommentService.AddCommentAsync` sends `BottleCommented`.
- `ListForSaleAsync` accepts only the supported currencies (`.claude/rules/domain/currencies.md`).
- Notification types: `.claude/rules/domain/notifications.md`. Badges: `.claude/rules/domain/badges.md`. A bottle's price estimate
  and the collection value: `.claude/rules/domain/pricing.md`.
