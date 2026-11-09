---
paths:
  - "**/*Offer*"
  - "**/offer*"
---

# Offer
| Property | Type | Notes |
|---|---|---|
| `BottleId` | `Guid` | FK → Bottle (`DeleteBehavior.Restrict`) |
| `BuyerId` | `Guid` | FK → AppUser |
| `SellerId` | `Guid` | FK → AppUser |
| `OfferedPrice` | `decimal` | `decimal(18,2)` |
| `Currency` | `string` | `EUR`, `GBP` or `USD`, stored upper-case (`.claude/rules/domain/currencies.md`) |
| `Message` | `string?` | Optional note from buyer |
| `Status` | `OfferStatus` | See the deal below |
| `RespondedAt` | `DateTime?` | Set when the seller accepts or declines |
| `CompletedAt` | `DateTime?` | Set when the buyer confirms receiving the bottle (#130) |
| `CancelledAt` | `DateTime?` | Set when one side cancels a deal awaiting receipt (#130) |

**OfferStatus enum** (append-only, stored as int):
`Pending`, `Accepted`, `Declined`, `Withdrawn`, `AwaitingReceipt`, `Cancelled`.
- `Accepted` means a **completed deal**: the buyer confirmed receiving the bottle. Offers accepted before #130 are
  completed deals too, so they needed no update. `SalesAccepted` / `PurchasesAccepted` (badges) and the internal price
  estimate count `Accepted` only.
- `AwaitingReceipt`: the seller accepted, and one bottle is reserved for the buyer.
- `Cancelled`: a deal one side ended while it awaited receipt.

## Design
- Offers can be made on **any** non-deleted bottle, regardless of `IsForSale`, as long as one of its bottles is free
  (`Quantity - ReservedQuantity >= 1`); otherwise `409` with reason `NoBottleLeft`.
- One pending offer per buyer per bottle — DB-enforced via a **filtered unique index** on `(BottleId, BuyerId)` where
  `Status = Pending AND IsDeleted = 0`.
  - The decorator's pre-check is only a friendly fast path.
  - `OfferService.CreateAsync` maps the index violation to `Conflict` when a concurrent create loses the race.
- Every state change is an **atomic conditional update** (`ExecuteUpdateAsync … WHERE Status = …`): the race loser gets
  `Conflict` and fires no notification, so an offer can never be resolved twice.
- Because of `ExecuteUpdateAsync`, `OfferServiceTests` for these paths use SQLite in-memory.
- All FK relations `DeleteBehavior.Restrict`.

### A deal in two steps (#130)
Owner decisions of 2026-11-09: the bottle moves when the buyer confirms receiving it; it then leaves the seller's bar;
either side may cancel while the deal waits, and nothing completes a deal by itself.
1. **The seller accepts** (`AcceptAsync`, one transaction): `Pending` → `AwaitingReceipt`.
   - It reserves a free bottle with a conditional update on the bottle row (`WHERE Quantity - ReservedQuantity >= 1`,
     `ReservedQuantity + 1`), so two accepts racing for the last free bottle have one winner; the loser rolls its claim
     back and answers `409` (`NoBottleLeft`), its offer still pending.
   - A listed bottle comes off sale: `ForSaleQuantity - 1`, and the last listed one unlists the entry as
     `UnlistFromSaleAsync` does.
   - With no free bottle left, the other pending offers on it are declined in one update, and the buyers it declined
     (read back by their `RespondedAt`) get `OfferDeclined`.
   - The buyer gets `OfferAccepted`. No badges yet.
2. **The buyer confirms** (`ConfirmReceiptAsync`, one transaction): `AwaitingReceipt` → `Accepted`, `CompletedAt`.
   - The seller's entry loses the bottle (`Quantity - 1`, `ReservedQuantity - 1`) and is soft-deleted with its last one,
     so its likes, comments and reviews leave the product page with it.
   - The buyer gets a copy: name, distillery and product (left out if deleted), category, region, country, age,
     vintage, ABV, volume, barcode (recorded for the product, `ProductBarcodeRecorder`), condition and limited flag;
     `Quantity` 1, not for sale, `AcquiredFromOfferId` = the offer. The seller's `Description` stays with the seller.
   - After the commit, the cover image: an upload under `/uploads/bottles/` is copied to a new file, so the seller
     deleting theirs cannot take the buyer's; any other URL is kept. A path that leaves the uploads folder, or a
     missing file, gives no image; a failure is logged, the deal stays complete.
   - The seller gets `SaleCompleted`, the buyer's followers `NewBottleFromFollowing`, and the badges are evaluated:
     `OfferAccepted` for both, `BottleAdded` for the buyer.
3. **Either side cancels** (`CancelAsync`) while it awaits receipt: `AwaitingReceipt` → `Cancelled`, `CancelledAt`,
   `ReservedQuantity - 1`. The bottle stays with the seller, off sale; the other side gets `DealCancelled`.

### Error reason
`OfferErrorReasons.NoBottleLeft` (`VirtualBar.Application/Common/OfferErrorReasons.cs`) is the `reason` of the `409`
"No bottle left to sell." (`OfferService.NoBottleLeftMessage`), on create and on accept; the offers page shows
`offers.errorNoBottleLeft` for it.

## API endpoints (`/api/offers`, all require `[Authorize]`)
- `POST /api/offers` — buyer creates offer; `409` a pending offer exists, or no bottle is free (`NoBottleLeft`)
- `GET /api/offers/received` — seller views received offers
- `GET /api/offers/sent` — buyer views sent offers (the receipt reminder reads it too)
- `PATCH /api/offers/{id}/accept` — seller accepts; `409` not pending, or no bottle is free (`NoBottleLeft`)
- `PATCH /api/offers/{id}/decline` — seller declines
- `PATCH /api/offers/{id}/withdraw` — buyer withdraws
- `PATCH /api/offers/{id}/confirm-receipt` — the buyer only (`403` otherwise); `409` not awaiting receipt
- `PATCH /api/offers/{id}/cancel` — the buyer or the seller (`403` otherwise); `409` not awaiting receipt

## Frontend
- **"ОФЕРТИ"** (`OffersPage`): received `Pending` offers have "Приеми" with what accepting does (`offers.acceptHint`)
  and "Откажи"; `AwaitingReceipt` shows "Откажи сделката" to the seller, and "Получих бутилката" plus "Откажи сделката"
  to the buyer. Each step refreshes the offers, the bars, the marketplace and the collection values.
- **The receipt reminder** (`ReceiptReminder`, `.claude/rules/frontend/shared-components.md`): after login, and when the
  app opens with a saved session, a buyer with offers `AwaitingReceipt` is asked "Получи ли бутилката?".

## Related rules
- `OfferService` sends `OfferReceived`, `OfferAccepted`, `OfferDeclined`, `SaleCompleted` and `DealCancelled`
  (`.claude/rules/domain/notifications.md`).
- `ConfirmReceiptAsync` evaluates the badges of seller and buyer (`OfferAccepted`, plus `BottleAdded` for the buyer,
  `.claude/rules/domain/badges.md`).
- The reservation and the moved bottle: `ReservedQuantity` and `AcquiredFromOfferId` (`.claude/rules/domain/bottles.md`).
- Completed deals feed the internal price estimate (`.claude/rules/domain/pricing.md`).
- `CreateAsync` accepts only the supported currencies (`.claude/rules/domain/currencies.md`).
