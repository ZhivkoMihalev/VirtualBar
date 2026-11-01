---
paths:
  - "**/*Badge*.cs"
  - "**/BadgeChip*"
  - "**/badges*"
---

# UserBadge
Composite PK: `(UserId, Badge)`. No `BaseEntity`. FK → AppUser (`DeleteBehavior.Restrict`); index on `UserId` for profile/public-bar reads.
| Property | Type |
|---|---|
| `UserId` | `Guid` |
| `Badge` | `BadgeType` |
| `AwardedAt` | `DateTime` |

**Design (badges / achievements):** The catalog lives in **code**, not the DB (the `FlavorTag` pattern): `BadgeType` enum (**19 members, append-only — never rename/reorder**; stored int values and i18n keys depend on the names) + `BadgeTrigger` (6) + `BadgeCountKind` (9) enums + static `BadgeCatalog` in `VirtualBar.Domain/Common/` (`BadgeDefinition(Type, Trigger, CountKind, Threshold)` records, `All`, `ForTrigger`). Names/descriptions are frontend-translated. Awards are **permanent** — never revoked when counts later drop (unlike/unfollow/delete/unlist).

The 19 badges (every bottle count below sums `Bottle.Quantity`, so one ×5 entry counts as five bottles): `FirstBottle` + `Collector5/10/25/50/100` (bottles ≥ 1/5/10/25/50/100), `Explorer3/5` (distinct categories), `LimitedHunter` (5 limited bottles), `Liked10/50/100` (likes received on own bottles), `FirstFollower`/`Popular10`/`Influencer50` (followers 1/10/50), `FirstListing` (bottles currently on offer ≥ 1 — sums `ForSaleQuantity`, so a partly-listed stack counts only what is actually for sale), `FirstSale`/`FirstPurchase` (accepted offers as seller/buyer), `FirstCatalogProduct` (own product requests approved by an admin ≥ 1 — counts `Status = Approved`, so linking to an existing catalog product counts exactly like creating a new one).

**Award engine — `IBadgeService.EvaluateAsync(userId, trigger, cancellationToken)`** (`BadgeService` + `BadgeValidationDecorator`): evaluate-on-trigger, uniform `count >= threshold`, no background job — any relevant action awards **everything missed** (lazy catch-up). Returns `Task` (not `Result<T>`), not controller-exposed; the entire body is `try/catch` → `LogError` — a badge bug never breaks the host operation. Called as one awaited line **after** the operation's own `SaveChangesAsync` + notification at **seven hook sites**: `AddBottleAsync` (`BottleAdded`), `ListForSaleAsync` (`BottleListed`), `LikeAsync` (`LikeReceived` → the bottle **owner**), `FollowAsync` (`FollowerGained` → the **followed** user), `AcceptAsync` (`OfferAccepted` ×2 — seller and buyer, winning path only), `ProductRequestService.ApproveAsync` (`ProductRequestApproved` → the **requester**, never the admin; the `Conflict` race path returns before it). Idempotency: awards go **one at a time** (`Add` → `SaveChangesAsync` → notification); a concurrent duplicate loses on the composite PK → `DbUpdateException` → detach, skip its notification, continue. Earning fires `NotificationType.BadgeEarned` via `CreateSystemAsync` with `ResourceId = null`, `ResourceName = badgeType.ToString()` (the frontend translates the badge name from it).

**API endpoints (`/api/badges`):**
- `GET /api/badges/user/{userId}` — `[AllowAnonymous]` (public bars are public); earned badges only, newest first; `404` unknown user
- `GET /api/badges/progress` — `[Authorize]`; all 19 catalog entries with `current`/`threshold`/`earned`/`awardedAt` for the **current** user (own-only — never exposed for other users)
