# 07 — Backend tests (written LAST)

> Depends on: **01–05** · Read `00-OVERVIEW.md` first.

## Goal
**All** backend unit tests for Bottle Reviews, written **after the whole backend is implemented** (per the
testing-last protocol from `docs/collection-value/`). **100% branch** for every new service method.

## `BottleReviewServiceTests` (new class, `VirtualBar.Tests/Services/`)
Test **through the decorator** (the `CreateService` helper returns
`new BottleReviewValidationDecorator(inner, db, currentUser)` — mirror `BottleCommentServiceTests`).
Mock only `ICurrentUser` and `INotificationService` (`Mock.Of<INotificationService>()` default parameter).
Seed helpers: `SeedUser`, `SeedBottle`, `SeedReview`.

- **GetReviews:** bottle missing / soft-deleted → `NotFound`; zero reviews → `AverageScore null`,
  `ReviewsCount 0`, empty `TopFlavors`, `MyReview null`; multiple reviews → rounded average (pick scores
  that exercise rounding, e.g. 85+90+91 → 88.7), newest-first order, soft-deleted review excluded from
  list AND aggregates; top-3 flavors by frequency incl. a tie (→ enum order); `MyReview` set when the
  current user has one, `null` for `Guid.Empty` (anonymous); user display name/avatar mapped.
- **AddReview:** happy path → `Ok`, persisted with flavors, `CreateAsync` verified with
  (bottle owner id, `NotificationType.BottleReviewed`, bottleId, bottle name); score `-1` → `Fail`;
  score `101` → `Fail`; boundary `0` and `100` → `Ok`; each note field at 2001 chars → `Fail` (2000 → ok);
  6 flavors → `Fail`; duplicate flavors → `Fail`; undefined enum value → `Fail`; null vs empty flavor
  list → `Ok`; bottle missing → `NotFound`; existing review by same user → `Conflict` (decorator path);
  **concurrent-duplicate race → `Conflict` via `DbUpdateException` — SQLite in-memory** (EF InMemory does
  not enforce unique indexes; mirror the `OfferServiceTests` SQLite setup): insert a review, then call
  `AddReviewAsync` through a decorator-bypassing inner instance so the DB index (not the pre-check) fires.
- **UpdateReview:** happy → fields overwritten, flavors replaced wholesale (old junction rows gone, new
  present); review missing / soft-deleted → `NotFound`; another user's review → `Forbidden`; input-guard
  branches (score range, note length, flavor count/dup/undefined) → `Fail`; no notification fired.
- **DeleteReview:** happy → `IsDeleted` + `DeletedAt` set (row still present); missing → `NotFound`;
  another user's → `Forbidden`.

## `BottleServiceTests` — additions (slice 05)
Bottle with reviews → correct rounded `AverageScore` + `ReviewsCount` in the returned `BottleDto`
(list + by-id paths); bottle without reviews → `null` / `0`; soft-deleted review excluded from both.

## Conventions (CLAUDE.md)
Naming `<Method>_When<Condition>_<Outcome>`; isolated InMemory DB per test (`Guid.NewGuid()` name);
**EF InMemory by default, SQLite in-memory ONLY for the unique-index race test**; mock only
`ICurrentUser` / `INotificationService`; `private static` seed helpers; cover every `if`, `&&`, `||`,
`?.`, `??` branch.

## Gate
`dotnet test VirtualBar.Tests/VirtualBar.Tests.csproj` → **`Failed: 0`**; **100% branch** on
`BottleReviewService`, `BottleReviewValidationDecorator`, and the touched `BottleService` paths
(coverage run: `--collect:"XPlat Code Coverage"`).
