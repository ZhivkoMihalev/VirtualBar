# 03 — Service & Validation Decorator (the core)

> Depends on: **02** · Read `00-OVERVIEW.md` first.

## Goal
`BottleReviewService` (pure logic) wrapped by `BottleReviewValidationDecorator` (all guards), DI wiring,
and the `BottleReviewed` notification. Structural template: the `BottleComment` pair; concurrency
template: `OfferService.CreateAsync`.

## Build new — `BottleReviewValidationDecorator` (`VirtualBar.Infrastructure/Decorators/`)
`cancellationToken.ThrowIfCancellationRequested()` first line of EVERY method. Guards in order:

- **`GetReviewsAsync`**: bottle exists & `!IsDeleted` → else `NotFound("Bottle not found.")`.
- **`AddReviewAsync`**:
  1. `Score` in **0–100** → else `Fail("Score must be between 0 and 100.")`
  2. each of `Nose`/`Palate`/`Finish`/`Summary`: if non-null, length ≤ **2000** → else `Fail`
  3. `Flavors` (when provided): count ≤ **5** → else `Fail`; no duplicates → else `Fail`;
     every value `Enum.IsDefined` → else `Fail`
  4. bottle exists & `!IsDeleted` → else `NotFound`
  5. **friendly duplicate pre-check** — existing non-deleted review by `currentUser.UserId` on this
     bottle → `Conflict("You have already reviewed this bottle.")` *(fast path only — the DB index is
     the real enforcement, overview §3.2)*
- **`UpdateReviewAsync`**: same input guards (1–3); review exists & `!IsDeleted` → else
  `NotFound("Review not found.")`; `review.UserId == currentUser.UserId` → else `Forbidden("Forbidden.")`.
- **`DeleteReviewAsync`**: review exists & `!IsDeleted` → else `NotFound`; ownership → else `Forbidden`.

## Build new — `BottleReviewService` (`VirtualBar.Infrastructure/Services/`)
Primary constructor: `(AppDbContext db, ICurrentUser currentUser, INotificationService notificationService)`.
No precondition `if`s, no `ThrowIfCancellationRequested` (decorator owns both).

- **`GetReviewsAsync`** — load non-deleted reviews for the bottle, `Include(User)` + `Include(Flavors)`,
  order `CreatedAt` **descending**; compute in memory:
  `AverageScore = Math.Round(avg, 1)` (null when empty), `ReviewsCount`, `TopFlavors` = flatten →
  group → top 3 by count (ties → enum order), `MyReview` = the review with
  `UserId == currentUser.UserId` (anonymous `Guid.Empty` matches nothing — by design, overview §3.8).
- **`AddReviewAsync`** — build `BottleReview` (+ `BottleReviewFlavor` rows from the distinct request
  flavors), `Add` + `SaveChangesAsync` inside `try/catch (DbUpdateException)` →
  `Conflict("You have already reviewed this bottle.")` (unique-index race loser — Offer pattern).
  Then load the `User` reference (mirror comments), query the bottle's `{ UserId, Name }`, and fire
  `notificationService.CreateAsync(bottleOwner, NotificationType.BottleReviewed, bottleId, bottleName,
  cancellationToken)` — the notification decorator suppresses self-review notifications. Return the DTO.
- **`UpdateReviewAsync`** — load the review incl. `Flavors` + `User`; overwrite `Score` + the four note
  fields; **replace flavors wholesale** (clear the junction collection, add the new distinct set —
  hard junction rows by design, overview §3.10); `SaveChangesAsync`. No notification on update.
- **`DeleteReviewAsync`** — soft-delete (`IsDeleted = true`, `DeletedAt = DateTime.UtcNow`),
  `SaveChangesAsync` (mirror `DeleteCommentAsync`). Junction rows stay (harmless; review is filtered out
  everywhere by `IsDeleted`).
- Private `static MapToDto` — includes `Flavors` ordered by enum value, `UpdatedAt`.

## DI (`DependencyInjection.cs` — next to the comment pair)
```csharp
services.AddScoped<BottleReviewService>();
services.AddScoped<IBottleReviewService>(sp => new BottleReviewValidationDecorator(
    sp.GetRequiredService<BottleReviewService>(),
    sp.GetRequiredService<AppDbContext>(),
    sp.GetRequiredService<ICurrentUser>()));
```

## Test targets (written in slice 07)
Every decorator guard branch; summary aggregation (empty / avg rounding / top-3 ties / deleted excluded /
`MyReview` for me vs anonymous); add happy + notification args; **duplicate race → Conflict (SQLite
in-memory — InMemory does not enforce unique indexes)**; update field+flavor replacement; soft delete.

## Gate
`dotnet build` → **0 errors**. No tests yet.
