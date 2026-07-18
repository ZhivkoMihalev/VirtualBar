# 07 — Backend tests (written LAST)

> Depends on: **01–05** · Read `00-OVERVIEW.md` first.

## Goal
**All** backend unit tests for Badges, written **after the whole backend is implemented** (testing-last
protocol). **100% branch** for every new/changed service method. EF InMemory everywhere — the composite
**PK** is enforced by InMemory (unlike secondary unique indexes), so **no SQLite is needed** for the race
test; `OfferServiceTests` stays on its existing SQLite setup for its own `ExecuteUpdateAsync` reasons.

## `BadgeCatalogTests` (new, plain unit)
All 18 `BadgeType`s defined exactly once; `ForTrigger` returns the exact subsets from overview §3;
every threshold ≥ 1.

## `BadgeServiceTests` (new; test through `BadgeValidationDecorator`, mirror the comment-tests style)
Mock only `ICurrentUser` + `INotificationService` (`Mock<INotificationService>` when verifying calls).
Seed helpers: `SeedUser`, `SeedBottle`, `SeedLike`, `SeedFollow`, `SeedOffer`, `SeedBadge`.
- **EvaluateAsync / engine:**
  - exact threshold hit (5th bottle → `Collector5`) and one-below (4th → nothing new);
  - cumulative catch-up: user with 50 pre-seeded bottles, zero badges → one `BottleAdded` evaluation
    awards `FirstBottle`+`Collector5/10/25/50` (not `Collector100`);
  - already-earned → no new row, no second notification;
  - **PK race:** pre-seed the `UserBadge` row, then evaluate via an inner-service instance whose
    pre-loaded earned-set is stale (or insert between load and save) → `DbUpdateException` swallowed,
    entity detached, **no** notification, later candidates in the same run still awarded;
  - every `CountKind` branch: `Bottles` (soft-deleted excluded), `Categories` distinct, `LimitedBottles`,
    `LikesReceived` (likes on soft-deleted bottles excluded), `Followers`, `ActiveListings`
    (`IsForSale` only), `SalesAccepted` / `PurchasesAccepted` (`Accepted` + non-deleted only);
  - notification args: `CreateSystemAsync(userId, NotificationType.BadgeEarned, null, "<BadgeType>")`;
  - engine exception → swallowed (fault a dependency, assert no throw + `LogError` path);
  - decorator: `Guid.Empty` / undefined trigger → silent return (inner never called).
- **GetUserBadgesAsync:** unknown user → `NotFound`; earned list newest-first; empty → `Ok([])`.
- **GetMyProgressAsync:** 18 rows in catalog order; `Current` per count-kind; `Earned`/`AwardedAt` set
  for seeded awards; zero-state user → all `Earned = false`, `Current = 0`.

## `NotificationServiceTests` — additions
`CreateSystemAsync`: recipient == current user → row IS created (the contrast with `CreateAsync`'s
skip); `ActorId` == recipient + `ActorDisplayName` == recipient's own name; missing recipient → no row,
no throw; decorator passes through without the self-skip.

## Touched trigger suites (slice 04 ripple)
`BottleServiceTests`, `BottleLikeServiceTests`, `UserFollowServiceTests`, `OfferServiceTests` —
`CreateXxxService` helpers gain `IBadgeService? badgeService = null` → `?? Mock.Of<IBadgeService>()`
(the `INotificationService` convention). New verifies: add-bottle → `(currentUser, BottleAdded)`;
list-for-sale → `(currentUser, BottleListed)`; like → `(ownerId, LikeReceived)` (not the liker);
follow → `(followedId, FollowerGained)`; accept → both `(sellerId, OfferAccepted)` and
`(buyerId, OfferAccepted)`; accept race loser → `EvaluateAsync` never called.

## Conventions (CLAUDE.md)
Naming `<Method>_When<Condition>_<Outcome>`; isolated InMemory DB per test (`Guid.NewGuid()` name);
mock only `ICurrentUser`, `INotificationService`, `IBadgeService` (trigger suites), `HttpMessageHandler`;
`private static` seed helpers; cover every `if`, `switch` arm, `&&`, `||`, `?.`, `??`.

## Gate
`dotnet test VirtualBar.Tests/VirtualBar.Tests.csproj` → **`Failed: 0`**; **100% branch** on
`BadgeService`, `BadgeValidationDecorator`, `BadgeCatalog`, `CreateSystemAsync`, and the touched trigger
paths (coverage run: `--collect:"XPlat Code Coverage"`).
