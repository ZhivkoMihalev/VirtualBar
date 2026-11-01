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
| `Quantity` | `int` | How many identical bottles this entry stands for (default `1`, store default backfills old rows, validated 1–99 in the decorator). One entry = the whole stack, so likes/comments/reviews stay aggregated on it — **every total counts physical bottles**: `BadgeCountKind.Bottles`/`LimitedBottles` sum it, `UserProfileService.BottleCount` sums it, and Collection Value multiplies the per-bottle estimate by it. Set on add; the owner can adjust it from the bottle detail panel (a quantity-only `PUT` must still echo every other field), but **never below `ForSaleQuantity`** — `UpdateBottleAsync`'s decorator returns `409` rather than leaving the marketplace offering bottles the owner no longer has |
| `ForSaleQuantity` | `int` | How many of the owned bottles are actually on offer — a stack can be listed **in part**. Invariant maintained by the service, not the DB: `ListForSaleAsync` sets it to `request.Quantity` (validated 1..`Quantity` in the decorator; omitted → **1**, so an older client lists a single bottle rather than the whole stack), `UnlistFromSaleAsync` zeroes it. Only meaningful alongside `IsForSale`, which stays the authoritative listed/not-listed flag every query filters on. `BadgeCountKind.ActiveListings` sums **this**, not `Quantity`; the Dashboard "for sale" stat and the marketplace `×N` badge read it too. `AskingPrice` stays **per bottle** — the UI labels it so once more than one is offered. Changing the offered count means unlist + re-list (the already-for-sale `409` guard still blocks a silent re-list) |
| `Description` | `string?` | |
| `IsLimited` | `bool` | Limited edition flag |
| `IsForSale` | `bool` | Listed in marketplace |
| `AskingPrice` | `decimal?` | `decimal(18,2)` |
| `Currency` | `string?` | ISO code e.g. "USD", "EUR" |
| `ForSaleAt` | `DateTime?` | When the bottle was listed for sale (set by `ListForSaleAsync`, cleared by `UnlistFromSaleAsync`) |
| `Barcode` | `string?` | Scanned/known EAN-UPC; sharpens canonical price-estimate matching (Collection Value) |
| `ProductId` | `Guid?` | FK → Product (nullable) — catalog link. Set by explicit pick or exact-`CanonicalKey` auto-link in `AddBottleAsync`; `UpdateBottleAsync` assigns it blindly from the request (null = unlink — clients must echo the current value). The bottle's own fields remain the display truth |

Relations: one `Bottle` → many `BottleImage`, `BottleLike`, `BottleComment`, `BottleReview`.

`BottleDto` additionally carries `AverageScore` (1 decimal, null when no reviews) + `ReviewsCount` — **computed in queries, never denormalized** (projected the same way `LikesCount`/`CommentsCount` are in `BottleService`).

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
