# 02 — Contracts & DTOs

> Depends on: **01** · Read `00-OVERVIEW.md` first.

## Goal
The Application-layer surface: `IBadgeService`, the badge DTOs, and the `CreateSystemAsync` addition to
`INotificationService`. New folder `VirtualBar.Application/DTOs/Badges/` — one top-level type per file.

## Build new / extend
1. **`UserBadgeDto`** — `BadgeType Badge`, `DateTime AwardedAt`. (Name/description/icon are frontend
   i18n/static-map concerns — the API ships only the enum, which serializes as a string.)
2. **`BadgeProgressDto`** — one row per catalog entry for the current user:
   - `BadgeType Badge`
   - `int Threshold`
   - `int Current` — the live count for its `CountKind` (capped display is a frontend concern)
   - `bool Earned`
   - `DateTime? AwardedAt`
3. **`IBadgeService`** — `VirtualBar.Application/Interfaces/IBadgeService.cs` (mirror
   `INotificationService`'s dual shape — overview §3.5):
   ```csharp
   Task EvaluateAsync(Guid userId, BadgeTrigger trigger, CancellationToken cancellationToken);

   Task<Result<List<UserBadgeDto>>> GetUserBadgesAsync(Guid userId, CancellationToken cancellationToken);

   Task<Result<List<BadgeProgressDto>>> GetMyProgressAsync(CancellationToken cancellationToken);
   ```
   `EvaluateAsync` is fire-and-forget from trigger services (returns `Task`, never throws — §3.6);
   the two query methods feed `BadgesController`.
4. **`INotificationService`** — add (overview §3.8):
   ```csharp
   Task CreateSystemAsync(Guid recipientId, NotificationType type, Guid? resourceId, string? resourceName, CancellationToken cancellationToken);
   ```
   Contract note in XML docs: unlike `CreateAsync`, the recipient MAY equal the current user (system
   notifications about one's own milestones); the actor recorded is the **recipient** themselves.

## Test targets (written in slice 07)
DTOs carry no logic → none here (interface behavior is tested via the implementations in slices 03–05).

## Gate
`dotnet build` → **0 errors** *(expected to fail only if `NotificationService`/decorator don't yet
implement the new member — implement them in the same commit as slice 03, or add the interface member
there; keep the two slices adjacent).*
