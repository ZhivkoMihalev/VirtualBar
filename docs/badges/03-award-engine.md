# 03 — Award Engine & System Notification (the core)

> Depends on: **02** · Read `00-OVERVIEW.md` first.

## Goal
`BadgeService.EvaluateAsync` — the uniform `count >= threshold` engine with PK-backed idempotency — plus
`NotificationService.CreateSystemAsync` (the no-self-skip variant), both wrapped by their decorators and
wired in DI.

## Build / extend — `NotificationService.CreateSystemAsync` (overview §3.8)
- **Inner (`NotificationService`)**: look up the **recipient's** display name
  (`db.Users.Where(u => u.Id == recipientId).Select(u => u.DisplayName)`); missing → return. Add the
  `Notification` with `ActorId = recipientId`, `ActorDisplayName = <recipient's own name>`, the passed
  `type`/`resourceId`/`resourceName`, `IsRead = false`; `SaveChangesAsync`. (Same shape as `CreateAsync`
  minus the current-user actor.)
- **Decorator (`NotificationValidationDecorator`)**: `ThrowIfCancellationRequested()` then delegate —
  **deliberately NO `recipientId == currentUser.UserId` skip** (that guard stays untouched on
  `CreateAsync`/`CreateBulkAsync`).

## Build new — `BadgeValidationDecorator` (`VirtualBar.Infrastructure/Decorators/`)
`ThrowIfCancellationRequested()` first line of every method.
- **`EvaluateAsync`**: `userId == Guid.Empty` OR `!Enum.IsDefined(trigger)` → return silently (mirror
  the notification decorator's quiet-skip style — trigger paths must never fail on bad input).
- **`GetUserBadgesAsync`**: user exists → else `NotFound("User not found.")`.
- **`GetMyProgressAsync`**: no guards (authenticated via `[Authorize]`; `currentUser` is the scope) —
  cancellation check only.

## Build new — `BadgeService` (`VirtualBar.Infrastructure/Services/`)
Primary constructor: `(AppDbContext db, ICurrentUser currentUser,
INotificationService notificationService, ILogger<BadgeService> logger)`.

- **`EvaluateAsync(userId, trigger, ct)`** — entire body in `try/catch (Exception ex)` →
  `logger.LogError(ex, ...)` + return (overview §3.6 — never breaks the host operation):
  1. `defs = BadgeCatalog.ForTrigger(trigger)`; load the user's already-earned types once
     (`db.UserBadges.Where(b => b.UserId == userId).Select(b => b.Badge)`); `missing = defs` not yet
     earned; empty → return (the hot-path fast exit).
  2. Compute each **distinct `CountKind`** among `missing` exactly once (overview §3.4) via a private
     `Task<int> CountAsync(BadgeCountKind, userId, ct)` switch:
     - `Bottles` — non-deleted bottles of the user
     - `Categories` — `Select(b => b.Category).Distinct().Count()` over the same set
     - `LimitedBottles` — same set, `IsLimited`
     - `LikesReceived` — `db.BottleLikes.Count(l => l.Bottle.UserId == userId && !l.Bottle.IsDeleted)`
     - `Followers` — `db.UserFollows.Count(f => f.FollowedId == userId)`
     - `ActiveListings` — non-deleted bottles with `IsForSale`
     - `SalesAccepted` — `db.Offers` where `SellerId == userId && Status == Accepted && !IsDeleted`
     - `PurchasesAccepted` — the `BuyerId` variant
  3. For each `missing` def with `count >= Threshold`, **award one at a time** (overview §3.7):
     `Add(new UserBadge { UserId, Badge, AwardedAt = DateTime.UtcNow })` → `try SaveChangesAsync` →
     success → `notificationService.CreateSystemAsync(userId, NotificationType.BadgeEarned, null,
     def.Type.ToString(), ct)`; `catch (DbUpdateException)` → detach the entry
     (`db.Entry(badge).State = EntityState.Detached`), skip its notification, continue — a concurrent
     trigger won the race; at-most-once award AND notification.
- **`GetUserBadgesAsync(userId, ct)`** — earned badges, `AwardedAt` **descending** → `UserBadgeDto` list.
- **`GetMyProgressAsync(ct)`** — for `currentUser.UserId`: earned set + one count per **distinct
  `CountKind` in the whole catalog**, then project all 18 defs → `BadgeProgressDto` in catalog order
  (`Earned`/`AwardedAt` from the earned set, `Current` from the counts).

## DI (`DependencyInjection.cs` — next to the notification pair)
```csharp
services.AddScoped<BadgeService>();
services.AddScoped<IBadgeService>(sp => new BadgeValidationDecorator(
    sp.GetRequiredService<BadgeService>(),
    sp.GetRequiredService<AppDbContext>()));
```
*(Slice 02's interface member lands together with this slice so the build stays green — see 02's gate.)*

## Test targets (written in slice 07)
Engine: per-trigger awards at exact threshold and cumulative catch-up (50 pre-existing bottles → one add
awards all six collection badges); below threshold → nothing; already-earned → nothing + no duplicate
notification; PK race (`DbUpdateException`) → swallowed, no notification, remaining badges still
processed; every `CountKind` branch incl. soft-delete exclusions; engine exception (e.g. disposed
context) → swallowed + logged; `CreateSystemAsync` called with `(userId, BadgeEarned, null,
"<BadgeType>")`. Queries: earned ordering; progress rows for all 18 with correct `Current`/`Earned`;
decorator guards (empty userId / undefined trigger → silent; unknown user → `NotFound`).
`NotificationServiceTests`: `CreateSystemAsync` — self-recipient IS created (the difference from
`CreateAsync`), actor = recipient, missing recipient → no row.

## Gate
`dotnet build` → **0 errors**. No tests yet.
