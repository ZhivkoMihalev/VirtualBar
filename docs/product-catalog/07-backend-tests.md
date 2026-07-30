# Slice 07 — Backend unit tests (ALL of them — written last)

> Prereq: slices 01–05 built. Conventions: one test class per service in
> `VirtualBar.Tests/Services/`, `<MethodName>_When<Condition>_<ExpectedOutcome>` naming, isolated
> InMemory DB per test (`Guid.NewGuid().ToString()`), **SQLite in-memory only where a unique-index
> race must surface as `DbUpdateException`**, private static seed helpers, 100% branch coverage on
> every new/changed method. Mock only `ICurrentUser`, `INotificationService`, `IBadgeService`,
> `IProductRequestService` (new), `HttpMessageHandler`.

## New test classes

### `ProductCatalogServiceTests`
Decorator: cancellation throws; search null / whitespace / 1 char → `Fail`; term > 100 chars →
`Fail` ("too long"); `"  ab  "` trimmed → passes and inner receives `"ab"`; limit 0 → 20 applied;
limit negative → 20; limit 999 → clamped 50; valid limit passed through unchanged. Inner: matches
by name / brand / distillery name; category filter on / off / no-match; starts-with ranked before
contains; deleted product excluded; limit respected; product with `Brand == null` matched via name
(short-circuit branch); `DistilleryName` null when distillery soft-deleted (slice 03 implemented
the `BottleService.MapToDto` semantics: match does NOT check `IsDeleted`, projection does).
**⚠ Case-sensitivity:** the API's case-insensitive matching comes from the SQL Server collation,
NOT from the code — EF InMemory (`string.Contains`, ordinal) and SQLite are case-SENSITIVE. Use
exact-case substrings in test terms (e.g. `"acallan"`, not `"maca"` vs `"Macallan"`) and do NOT
assert case-insensitivity; a "case differences" test would falsely fail correct code.

### `ProductRequestServiceTests`
- **CreateAsync** (decorator): name missing/whitespace → `Fail`; undefined enum category → `Fail`;
  barcode "abc" / 7 digits / 15 digits → `Fail`, 8 and 14 digits → pass; age 0/101, abv 0.5/97,
  volume 19/6001 → `Fail`; brand/userNote over cap → `Fail`; unknown `DistilleryId` → `Fail`;
  `SourceBottleId` pointing to a missing/deleted bottle → `Fail`; foreign-owned source bottle →
  `Forbidden`; 25 open requests → `Fail`; existing product same key →
  `Conflict`; existing pending same key → `Conflict`.
- **CreateAsync** (inner): happy path persists trimmed fields + `Pending` + `CanonicalKey`
  (distillery-name variant AND brand-fallback variant asserted literally against `ProductKey.For`);
  concurrent duplicate → **SQLite**: pre-insert a pending row with the same key bypassing the
  decorator, call inner, assert `Conflict` and the failed entity detached.
- **GetMineAsync**: own only, no deleted, newest first.
- **WithdrawAsync**: pending own → soft-deleted (`IsDeleted`, `DeletedAt`); missing → `NotFound`;
  foreign → `Forbidden`; approved/rejected → `Conflict`.
- **GetAllAsync**: non-admin → `Forbidden`; status filter each value + null; includes requester
  display name.
- **ApproveAsync**: non-admin → `Forbidden`; missing → `NotFound`; already approved → `Conflict`;
  new product created with `Origin=Approved` + overrides applied + key recomputed from effective
  values; `ExistingProductId` happy path (no new product row); `ExistingProductId` unknown → `Fail`;
  source bottle linked; source bottle already linked → untouched; source bottle soft-deleted →
  skipped; image copy: `UseSourceBottleImage` + primary image → `ImageUrl` copied; no primary →
  lowest `SortOrder` used; no images / deleted source bottle / no `SourceBottleId` → null; explicit
  `ImageUrl` override wins over the flag; flag ignored on the `ExistingProductId` path;
  retro-link: matching bottle linked, same-category-different-name bottle NOT linked,
  null-age/null-volume equality matching; duplicate product key → `Conflict` (**SQLite**);
  `ProductRequestApproved` notification verified (recipient = requester, resourceId = product id,
  resourceName = product name); no notification on `Conflict` paths.
- **RejectAsync**: non-admin → `Forbidden`; missing → `NotFound`; resolved → `Conflict`; happy path
  sets `Rejected`/`AdminNote`/`RespondedAt` + `ProductRequestRejected` notification verified.

### `ProductSeederTests`
Seeds when empty (count, `Origin=Seeded`, keys computed); second run no-op; distillery exact +
case-insensitive resolution; unknown distillery → `Brand` fallback + null FK; bad category row
skipped; in-file duplicate keys collapse to one.

### `DistillerySeederTests`
(Added 2026-07-26: `DistillerySeeder` was rewritten to top-up semantics — inserts distilleries
missing by name, case-insensitive, plus missing `(DistilleryId, Category)` pairs; strictly
INSERT-only.) Empty DB → full seed (distillery + category counts); distilleries present but
categories empty → categories backfilled; partially seeded → only missing distilleries inserted
AND existing rows' `Country`/`Region` NOT overwritten; second run → inserts nothing; existing name
differing only in casing → not duplicated.

## Extended test classes

### `BottleServiceTests`
`CreateBottleService` helper gains optional `IProductRequestService` (default
`Mock.Of<IProductRequestService>()`) — update all existing call sites (mechanical). New:
- Add with valid explicit `ProductId` → linked; request service **never called**.
- Add with unknown `ProductId` → decorator `Fail`.
- Add, no `ProductId`, exact catalog key match → auto-linked, request service never called.
- Add, no match → `CreateAsync` called once; captured request has `SourceBottleId == bottle.Id`
  and mirrors name/category/age/volume.
- Request service returns `Result.Fail/Conflict` → add still `Success`.
- Request service throws → add still `Success` (and does not bubble).
- Update: set valid → linked; set unknown → `Fail`; null → unlinked; `BottleDto.ProductId` mapped
  in gets/marketplace projections.

### `ProductLookupServiceTests`
Ctor gains the InMemory `AppDbContext` (update existing tests). New: barcode present in catalog →
`Ok` with `ProductId`, handler counter proves **zero** external HTTP calls; soft-deleted catalog
product ignored → external path; catalog miss → existing external behavior intact (all current
tests still green).

## Run gates
```bash
dotnet test VirtualBar.Tests/VirtualBar.Tests.csproj --verbosity minimal          # Failed: 0
dotnet test VirtualBar.Tests/VirtualBar.Tests.csproj --collect:"XPlat Code Coverage" --results-directory ./coverage-results
```
Check branch coverage on `ProductCatalogService`, `ProductRequestService` (+decorators),
`ProductSeeder`, changed `BottleService`/`ProductLookupService` methods = **100%**.
