# Offer
| Property | Type | Notes |
|---|---|---|
| `BottleId` | `Guid` | FK → Bottle (`DeleteBehavior.Restrict`) |
| `BuyerId` | `Guid` | FK → AppUser |
| `SellerId` | `Guid` | FK → AppUser |
| `OfferedPrice` | `decimal` | `decimal(18,2)` |
| `Currency` | `string` | ISO code e.g. "USD" |
| `Message` | `string?` | Optional note from buyer |
| `Status` | `OfferStatus` | Pending/Accepted/Declined/Withdrawn |
| `RespondedAt` | `DateTime?` | Set when seller accepts or declines |

**Design:** Offers can be made on **any** non-deleted bottle, regardless of `IsForSale`. One pending offer per buyer per bottle — DB-enforced via a **filtered unique index** on `(BottleId, BuyerId)` where `Status = Pending AND IsDeleted = 0`; the decorator's pre-check is only a friendly fast path, and `OfferService.CreateAsync` maps the index violation to `Conflict` when a concurrent create loses the race. Accept/Decline/Withdraw are **atomic conditional updates** (`ExecuteUpdateAsync … WHERE Status = Pending`): the race loser gets `Conflict` and fires no notification, so an offer can never be resolved twice. Because of `ExecuteUpdateAsync`, `OfferServiceTests` for these paths use SQLite in-memory. All FK relations `DeleteBehavior.Restrict`.

**OfferStatus enum:** `Pending`, `Accepted`, `Declined`, `Withdrawn`

**API endpoints (`/api/offers`, all require `[Authorize]`):**
- `POST /api/offers` — buyer creates offer
- `GET /api/offers/received` — seller views received offers
- `GET /api/offers/sent` — buyer views sent offers
- `PATCH /api/offers/{id}/accept` — seller accepts
- `PATCH /api/offers/{id}/decline` — seller declines
- `PATCH /api/offers/{id}/withdraw` — buyer withdraws
