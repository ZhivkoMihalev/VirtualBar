# 01 — Domain & Migration

> Depends on: **—** · Read `00-OVERVIEW.md` first.

## Goal
The data model: `BadgeType` / `BadgeTrigger` / `BadgeCountKind` enums, the static `BadgeCatalog`, the
`UserBadge` junction entity, `NotificationType.BadgeEarned`, EF configuration, one ADD-only migration.

## Build new / extend
1. **`BadgeType` enum** — `VirtualBar.Domain/Enums/BadgeType.cs`, the 18 members from overview §3
   (catalog table), **append-only**: `FirstBottle`, `Collector5`, `Collector10`, `Collector25`,
   `Collector50`, `Collector100`, `Explorer3`, `Explorer5`, `LimitedHunter`, `Liked10`, `Liked50`,
   `Liked100`, `FirstFollower`, `Popular10`, `Influencer50`, `FirstListing`, `FirstSale`,
   `FirstPurchase`.
2. **`BadgeTrigger` enum** — `BottleAdded`, `LikeReceived`, `FollowerGained`, `BottleListed`,
   `OfferAccepted`.
3. **`BadgeCountKind` enum** — `Bottles`, `Categories`, `LimitedBottles`, `LikesReceived`, `Followers`,
   `ActiveListings`, `SalesAccepted`, `PurchasesAccepted`.
4. **`BadgeCatalog`** — static class in `VirtualBar.Domain/Common/` (or `Enums/` sibling; one top-level
   type per file): `public sealed record BadgeDefinition(BadgeType Type, BadgeTrigger Trigger,
   BadgeCountKind CountKind, int Threshold);` + `public static readonly IReadOnlyList<BadgeDefinition>
   All` (the 18 rows) + `public static IReadOnlyList<BadgeDefinition> ForTrigger(BadgeTrigger trigger)`.
   Pure data, no I/O — unit-testable that all 18 `BadgeType`s appear exactly once.
5. **`UserBadge` entity** — `VirtualBar.Domain/Entities/UserBadge.cs`, composite PK, **no `BaseEntity`**
   (mirror `BottleLike`; one blank line between properties):
   - `Guid UserId` + `AppUser User`
   - `BadgeType Badge`
   - `DateTime AwardedAt`
6. **`AppUser`** — add `ICollection<UserBadge> Badges = []`.
7. **`NotificationType`** — append **`BadgeEarned`** at the END.
8. **`AppDbContext`** — `DbSet<UserBadge> UserBadges`; config (mirror the `BottleLike` block):
   ```csharp
   builder.Entity<UserBadge>(e =>
   {
       e.HasKey(b => new { b.UserId, b.Badge });

       e.HasIndex(b => b.UserId);   // profile/public-bar reads

       e.HasOne(b => b.User)
           .WithMany(u => u.Badges)
           .HasForeignKey(b => b.UserId)
           .OnDelete(DeleteBehavior.Restrict);
   });
   ```
   (The PK doubles as the §3.7 idempotency backstop — no extra unique index needed.)
9. **Migration** — `dotnet ef migrations add AddUserBadges --project VirtualBar.Infrastructure
   --startup-project VirtualBar.Api`. ADD-only: one table + index; touches nothing existing.

## Test targets (written in slice 07)
`BadgeCatalog` — every `BadgeType` defined exactly once; `ForTrigger` returns the right subsets;
thresholds positive. (Entities/enums carry no logic.)

## Gate
`dotnet build` → **0 errors**; migration applies on `dotnet run`.
