# 04 — Trigger Wiring (six hook sites)

> Depends on: **03** · Read `00-OVERVIEW.md` first.

## Goal
Wire `EvaluateAsync` into the five services at the six exact points where the corresponding
notifications already fire. Each hook is one awaited line **after** the operation's own
`SaveChangesAsync` + notification (badge evaluation reads committed state; its failures are already
swallowed inside the engine — §3.6).

## Extend (constructor: add `IBadgeService badgeService` via primary constructor)
| Service · method | Hook (after the existing notification call) |
|---|---|
| `BottleService.AddBottleAsync` | `await badgeService.EvaluateAsync(currentUser.UserId, BadgeTrigger.BottleAdded, cancellationToken);` |
| `BottleService.ListForSaleAsync` | `await badgeService.EvaluateAsync(currentUser.UserId, BadgeTrigger.BottleListed, cancellationToken);` |
| `BottleLikeService.LikeAsync` | `await badgeService.EvaluateAsync(bottle.UserId, BadgeTrigger.LikeReceived, cancellationToken);` — the **owner** earns, on like only (unlike never revokes — §3.2) |
| `UserFollowService.FollowAsync` | `await badgeService.EvaluateAsync(targetUserId, BadgeTrigger.FollowerGained, cancellationToken);` — the **followed** user earns |
| `OfferService.AcceptAsync` | **two** calls: `EvaluateAsync(offer.SellerId, BadgeTrigger.OfferAccepted, ...)` and `EvaluateAsync(offer.BuyerId, BadgeTrigger.OfferAccepted, ...)` — seller checks `SalesAccepted`, buyer `PurchasesAccepted` (same trigger, different count-kinds resolve per user) |

Notes:
- `AcceptAsync` places the hooks **after** the atomic `ExecuteUpdateAsync` succeeded and only on the
  winning path (the `Conflict` race loser fires nothing — mirror its notification placement).
- No hooks in `UnlikeAsync`/`UnfollowAsync`/`DeleteBottleAsync`/decline/withdraw — awards are permanent.
- Do NOT hook `AddCommentAsync`/`SendAsync` — no comment/message badges in the initial catalog.

## Constructor ripple (build breaks loud — fix mechanically)
The five services gain a parameter → update:
- `DependencyInjection.cs` — nothing to do (constructor injection resolves automatically for the inner
  services; verify the decorator lambdas that construct inner services manually, if any, still compile).
- **Existing test classes** (`BottleServiceTests`, `BottleLikeServiceTests`, `UserFollowServiceTests`,
  `OfferServiceTests`): their `CreateXxxService` helpers gain
  `IBadgeService? badgeService = null` → `badgeService ?? Mock.Of<IBadgeService>()` — the exact
  convention already used for `INotificationService`. (Done in slice 07; the build gate here only needs
  the API project to compile — `dotnet build` on the Api csproj does not compile tests.)

## Test targets (written in slice 07)
Per touched service: the hook fires with the right `(userId, trigger)` (Moq `Verify`); like → **owner**
not liker; follow → **followed** not follower; accept → **both** seller and buyer; accept race loser →
no badge call.

## Gate
`dotnet build` → **0 errors** (Api project). No tests yet.
