# 01 — Domain & Migration

> Depends on: **—** · Read `00-OVERVIEW.md` first.

## Goal
The data model: `BottleReview` entity, `BottleReviewFlavor` junction, `FlavorTag` enum, the new
`NotificationType.BottleReviewed` member, EF configuration (filtered unique index), one ADD-only migration.

## Build new / extend
1. **`FlavorTag` enum** — `VirtualBar.Domain/Enums/FlavorTag.cs`, 28 members, **append-only**:
   ```
   Smoky, Peaty, Medicinal, Maritime,
   Vanilla, Caramel, Toffee, Honey, Chocolate, Coffee,
   Nutty, Malty, Creamy,
   Fruity, Citrus, TropicalFruit, DriedFruit, Berry,
   Floral, Herbal, Grassy,
   Spicy, Pepper, Cinnamon,
   Oak, Sherry, Leather, Tobacco
   ```
   Covers whisky/rum/cognac/gin/tequila vocabularies. Frontend translates by enum name (slice 06).
2. **`BottleReview` entity** — `VirtualBar.Domain/Entities/BottleReview.cs`, extends `BaseEntity`
   (one blank line between properties — CLAUDE.md):
   - `Guid BottleId` + `Bottle Bottle`
   - `Guid UserId` + `AppUser User`
   - `int Score` (0–100; range enforced in the decorator, not the DB)
   - `string? Nose`, `string? Palate`, `string? Finish`, `string? Summary`
   - `ICollection<BottleReviewFlavor> Flavors = []`
3. **`BottleReviewFlavor` junction** — composite PK `(ReviewId, Flavor)`, **no `BaseEntity`**
   (mirror `DistilleryCategory`):
   - `Guid ReviewId` + `BottleReview Review`
   - `FlavorTag Flavor`
4. **`Bottle`** — add `ICollection<BottleReview> Reviews = []` navigation.
5. **`NotificationType`** — append **`BottleReviewed`** at the END (stored int values stay stable).
6. **`AppDbContext`**:
   - `DbSet<BottleReview> BottleReviews`, `DbSet<BottleReviewFlavor> BottleReviewFlavors`.
   - `BottleReview`: filtered **unique** index — the concurrency backstop (overview §3.2, `Offer` pattern):
     ```csharp
     e.HasIndex(r => new { r.BottleId, r.UserId })
         .IsUnique()
         .HasFilter("[IsDeleted] = 0");   // [] quoting works on SQL Server AND SQLite (tests)
     ```
     plus a read index `e.HasIndex(r => new { r.BottleId, r.IsDeleted });`. FK delete behavior stays on
     the global `Restrict` default (both `Bottle` and `User`).
   - `BottleReviewFlavor`: `HasKey(f => new { f.ReviewId, f.Flavor })`; relation to `BottleReview` with
     **explicit `DeleteBehavior.Cascade`** (mirror `DistilleryCategory` ← `Distillery`).
7. **Migration** — `dotnet ef migrations add AddBottleReviews --project VirtualBar.Infrastructure
   --startup-project VirtualBar.Api`. ADD-only: two tables + indexes; touches nothing existing.

## Test targets (written in slice 07)
Entities/enums carry no logic → none directly; the filtered index behavior is covered by the
duplicate-race service test (SQLite in-memory).

## Gate
`dotnet build` → **0 errors**; migration applies on `dotnet run`.
