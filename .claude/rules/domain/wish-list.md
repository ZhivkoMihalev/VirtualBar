# WishListItem
| Property | Type | Notes |
|---|---|---|
| `UserId` | `Guid` | FK → AppUser (`DeleteBehavior.Restrict`) |
| `BottleName` | `string?` | User label only — NOT used in matching |
| `DistilleryId` | `Guid?` | FK → Distillery — matching criterion (exact FK match) |
| `Category` | `SpiritCategory?` | Matching criterion (exact enum match) |
| `ImageUrl` | `string?` | Optional display image |

At least one of `DistilleryId` or `Category` must be set (enforced by `WishListValidationDecorator`). Matching in `BottleService.ListForSaleAsync` uses AND of all non-null criteria. Publicly visible in Marketplace "LOOKING FOR" tab — other users can click "СЪОБЩЕНИЕ" to open a chat with the searcher.
