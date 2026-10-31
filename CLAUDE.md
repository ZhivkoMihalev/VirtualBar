# CLAUDE.md

This file provides guidance to Claude Code when working with code in this repository.

---

## What is VirtualBar

VirtualBar is a platform for collectors of premium spirits (whisky, rum, cognac, vodka and more). Every user gets a **virtual bar** — a public profile showcasing their collection. Others can browse collections, follow collectors, like and comment on bottles, leave **structured reviews** (0–100 score + tasting notes + flavor tags), send direct messages, and buy/sell limited editions through a built-in marketplace. The home page (`/`) is a social news feed showing admin-authored articles and activity from followed users. Users receive **in-app notifications** when someone likes/comments their bottle, follows them, sends a message, or when someone they follow adds a bottle or lists one for sale. Users can add bottles to a **wish list** and get notified when a matching bottle appears for sale. Any user can **make an offer** on any bottle in another collector's bar (regardless of whether it is listed for sale); the seller receives a notification and can accept, decline, or ignore. Collectors earn **permanent achievement badges** for milestones (first bottle, 10 bottles, 5 categories, 50 likes received, first sale…) — shown with progress on the own profile and as an earned-only strip on the public bar. The platform maintains an admin-curated **product catalog** of canonical spirits: adding a bottle links it to a catalog product (explicit pick or silent exact-match auto-link), and an unknown bottle silently files a **product request** the admin approves (creating the product and retro-linking identical bottles) or rejects — the add flow is never blocked. Anyone, logged in or not, can browse that catalog at **`/catalog`** and open a **product page** (`/product/:id`) that gathers every collector's bottle of it: the community score and top flavors, the owners, the current listings and all the reviews, computed on read. Direct messaging is handled via a **floating chat widget** (Facebook Messenger style) accessible from every page.

**User roles:** Collector (all registered users). Platform administrators have `IsAdmin = true` on their `AppUser` record — seeded via `AdminEmail` in `appsettings.Development.json` on startup.

---

## Solution Structure

```
VirtualBar/
├── VirtualBar.Domain          # Entities, enums — zero outward dependencies
├── VirtualBar.Application     # Interfaces, DTOs, Result<T>
├── VirtualBar.Infrastructure  # EF Core, service implementations
├── VirtualBar.Api             # ASP.NET Core controllers, Program.cs
├── VirtualBar.Tests/
│   ├── Services/              # xUnit unit tests (one file per service)
│   ├── Integration/           # cross-service flow tests (real service graph, no mocks)
│   ├── Persistence/           # EF model conventions (UTC DateTime round-trip)
│   └── Api/                   # API helpers (ResultExtensions: status code + error body) + controller tests
├── VirtualBar.HttpTests/      # the whole API over HTTP (WebApplicationFactory) on SQL Server
│   ├── Support/               # test host, shared throwaway database, email pickup reader, [SqlServerFact]
│   ├── Auth/                  # email confirmation, email failure, error reasons, JWT + logout
│   └── Startup/               # configuration Program.cs refuses to start with
├── VirtualBar.Web/            # React + Vite 8 + Tailwind CSS 4 frontend
├── VirtualBar.Web.Tests/      # frontend tests — its own npm workspace, depends on VirtualBar.Web
│   ├── unit/                  # Vitest + React Testing Library (jsdom); mirrors VirtualBar.Web/src
│   └── e2e/                   # Playwright specs, run against the real stack
└── package.json               # npm workspaces root: VirtualBar.Web + VirtualBar.Web.Tests, one lockfile

**Target framework:** .NET 9 (`net9.0`)
```

The frontend is two **npm workspaces**, the JS counterpart of a solution with a test project. One `npm install`
in the repo root installs both into the root `node_modules`, from the single root `package-lock.json`.
`VirtualBar.Web.Tests` declares `virtualbar.web` as a dependency, the way `VirtualBar.Tests` references the backend
projects. It deliberately does **not** list the app's runtime packages (`react`, `react-dom`, `@tanstack/react-query`,
`react-router-dom`, `i18next`, …): it uses the app's own copies, so a component and its test share one React and one
query-client context. Listing one of them separately could install a second copy, which breaks hooks.

**Dependency rule:** `Api → Application ← Infrastructure`. Domain has no outward dependencies.

The product catalog is **hand-curated**, not generated. There was an offline import CLI
(`tools/ProductImport`, slice 08); it was removed because it never produced a single shipped row and
re-running it would have destroyed the curated files. The invariants it used to check on demand now run
on every build in `ProductSeedDataTests` — see `docs/product-catalog/08-data-import-pipeline.md`.

---

## Build & Run Commands

### Backend

```bash
# Build
dotnet build VirtualBar.Api/VirtualBar.Api.csproj --no-restore -v q
# Expected: 0 Error(s). Pre-existing warnings OK; new warnings are not.

# Run the API (auto-applies migrations on startup)
cd VirtualBar.Api && dotnet run
# API: http://localhost:5000  |  OpenAPI: http://localhost:5000/openapi/v1.json (development only)

# Add EF Core migration
dotnet ef migrations add <Name> --project VirtualBar.Infrastructure --startup-project VirtualBar.Api

# Apply migrations manually
dotnet ef database update --project VirtualBar.Infrastructure --startup-project VirtualBar.Api
```

### Frontend

Run from the **repo root** (each script forwards to its workspace):

```bash
npm install        # first time only — installs both workspaces into the root node_modules
npm run dev        # VirtualBar.Web dev server → http://localhost:5173
npm run lint       # ESLint in both workspaces — must report 0 problems (CI blocks on it)
npm test           # Vitest unit + component tests (VirtualBar.Web.Tests/unit) — must report 0 failed
npm run typecheck  # tsc over VirtualBar.Web.Tests (unit + e2e + configs)
npm run build      # VirtualBar.Web production build — must output "✓ built in ..."
npm run test:e2e   # Playwright (VirtualBar.Web.Tests/e2e) — needs the API running, see CI → E2E
```

Inside a workspace folder the same scripts work too (`cd VirtualBar.Web && npm run dev`; in `VirtualBar.Web.Tests`
also `npm run test:watch` and `npm run test:e2e:ui` / `test:e2e:debug` / `test:e2e:report`).

The Vite dev server proxies `/api` and `/uploads` to `http://localhost:5000`.

### Tests

```bash
# Full suite
dotnet test VirtualBar.Tests/VirtualBar.Tests.csproj --verbosity minimal

# One service class only
dotnet test VirtualBar.Tests/VirtualBar.Tests.csproj \
  --filter "FullyQualifiedName~BottleServiceTests" --verbosity minimal

# Branch coverage report
dotnet test VirtualBar.Tests/VirtualBar.Tests.csproj \
  --collect:"XPlat Code Coverage" --results-directory ./coverage-results
```

Expected output: `Failed: 0`. The `coverage-results/` directory is gitignored.

```bash
# HTTP tests: the whole API on SQL Server (about 30 s; the first host migrates and seeds a fresh database)
dotnet test VirtualBar.HttpTests/VirtualBar.HttpTests.csproj --verbosity minimal
```

They need no setup: the server comes from `VIRTUALBAR_TEST_SQL` if set, else from the API's own
`appsettings.Development.json`. On a machine where no server answers, the SQL tests are **skipped** with the reason
(the startup-guard tests still run); in CI (`CI=true`) they **fail** instead, so a missing server never passes as green.
See Testing Conventions → HTTP tests.

### CI (GitHub Actions)

`.github/workflows/ci.yml` runs on every PR to `master`, every push to `master`, and on demand. Two parallel jobs:
- **Backend** — restore → build (Release) → unit tests with coverage → **HTTP tests** (`VirtualBar.HttpTests`) with coverage, against a SQL Server 2022 service container (`VIRTUALBAR_TEST_SQL`; they run even when the unit tests failed, so one push shows every failure; raw results of both in the `backend-test-results` artifact) → **coverage report** (merges both test projects) (ReportGenerator, `VirtualBar.Infrastructure.Migrations.*` excluded): written to the job summary, posted as one PR comment that is edited in place on every push (skipped on Dependabot and fork PRs, whose token is read-only), and uploaded as the `coverage-report` HTML artifact. Informational: it never fails the build → `dotnet ef migrations has-pending-model-changes`, which fails when an entity changed without a matching migration. It needs no secrets: the SQL Server password is a CI-only literal for a container that lives only as long as the job, and a throwaway `Jwt:Key` is generated where one is needed.
- **Frontend** — from the repo root: `npm ci` (both workspaces, root lockfile) → `npm run lint` (both workspaces; blocking — must report 0 problems) → `npm run typecheck` (the tests) → `npm test` (Vitest, no backend needed) → `npm run build`.

`.github/workflows/migration-guard.yml` (the **Migration guard** check) runs on every PR to `master`. Migrations apply automatically on startup, so it scans the `Up()` method of every migration the PR adds or changes (`.github/scripts/check-destructive-migrations.sh`) and fails on `DropTable`, `DropColumn`, `DeleteData`, `UpdateData` or any raw `Sql(...)`: operations that delete or rewrite existing data on the next deploy. `Down()` is ignored, since it legitimately drops what `Up()` created. The check passes once the PR carries the **`db-destructive-approved`** label; add it only after the data change was agreed (see CRITICAL RULES).

`.github/workflows/e2e.yml` (the **E2E** check) runs the Playwright suite in `VirtualBar.Web.Tests/e2e` on Chromium against the real stack: SQL Server 2022 in a service container, the API in `Development` on a fresh database (migrations + seeders run on startup), and the Vite dev server of `VirtualBar.Web` that `VirtualBar.Web.Tests/playwright.config.ts` starts. It is too slow for every PR, so it runs nightly on `master` (02:17 UTC), on demand (Actions → E2E → Run workflow), and on PRs labelled **`e2e`**; it is therefore not a required check. CI-only config overrides, set as environment variables in the workflow: `Identity:RequireConfirmedEmail=false` (the tests log new users straight in), `Email:PickupDirectory` (every email is written there as an `.eml` file instead of sent; `email-confirmation.spec.ts` reads the same directory through the `E2E_MAIL_DIR` environment variable and opens the link in the email), `RateLimiting:AuthPermitLimit=10000` (`Program.cs` defaults to 10 auth requests a minute per IP, which the suite exceeds from one machine), `Anthropic:UseProviderStats=false` and `Pricing:RefreshEnabled=false`. The Playwright HTML report (traces + screenshots of failures) and the API log are uploaded as artifacts. To run the suite locally, start the API with the same overrides: the **`api-e2e`** configuration in `.claude/launch.json` passes them as command-line arguments and writes the emails to `VirtualBar.Web.Tests/.e2e-mail/` (gitignored). It uses the database of `appsettings.Development.json`, and the suite fills that database with test collectors, bottles, listings, offers, wishes and messages. Then run `cd VirtualBar.Web.Tests && E2E_MAIL_DIR=<absolute path of .e2e-mail> npx playwright test --project=chromium`. CI runs one test at a time (`workers: 1`); a local run at Playwright's default parallelism makes many browsers compete for the CPU against the unbundled dev server, which can push the older specs' 5 s assertions over their limit, so `--workers=1` reproduces CI. `Email:PickupDirectory` is for development only: the API refuses to start with it in any other environment, where it would silently stop every email.

`.github/workflows/project-board.yml` (the **Project board** workflow) keeps the **Virtual Bar** board in GitHub Projects in step with the issues' `status: …` labels (`To Do` → `In Progress` → `Ready For Testing` → `In Testing` → `Ready for deployment` → `Done`). The label is the source of truth: change a status by changing the label, and `.github/scripts/sync-project-status.sh` moves the card to the board's Status column of the same name (ignoring case), adding the issue to the board first. An issue is on the board once it has a status label; dragging a card does not change the label. It runs on `opened`/`reopened` and on adding a `status: …` label; run it by hand (Actions → Project board → Run workflow) to sync every issue at once. It needs the `PROJECT_TOKEN` secret, a classic personal access token with only the `project` scope, because the workflow's own token cannot reach a project owned by a user account.

**What each status means:**

| Status | When | Moved by |
|---|---|---|
| `To Do` | The issue is written and not started. | whoever opens it |
| `In Progress` | Work has started on a branch. | the developer |
| `Ready For Testing` | The PR is open or merged and the work waits for a test. Testing happens on `master`, after the merge. | the developer, when opening the PR |
| `In Testing` | Someone is testing it. | the tester |
| `Ready for deployment` | The test passed. The code is in `master` and waits for the next deploy to Azure. | the tester |
| `Done` | It is deployed to Azure. | whoever deploys (manual; there is no deploy workflow yet) |

A test that finds a problem sends the issue back to `In Progress`. An issue stays **open until `Done`**, so a PR never closes it: write `Part of #N` in the PR description, **never** `Closes`/`Fixes`/`Resolves #N`, which would close the issue at merge, before it is tested and deployed. Because testing happens after the merge, everything in `Ready for deployment` is already in `master` and goes out with the next deploy; a future deploy workflow can rely on that to move those issues to `Done` itself.

**CodeQL** (security scanning of C#, JS/TS and the workflows) runs through GitHub's *default setup*, configured in the repository settings (Advanced Security → Code scanning), not in a workflow file. Findings appear in the Security tab and on PRs.

`.github/dependabot.yml` opens **monthly** update PRs for npm (one entry at the repo root, which covers both workspaces and the root lockfile), NuGet and GitHub Actions (minor + patch grouped into one PR per ecosystem, majors separate — except `vite` and `@vitejs/*`, whose majors come as one PR because neither installs without the other). Security updates are not bound to the schedule.

---

## Code Style

- One blank line between every property in entity classes.

---

## CRITICAL RULES — Never Violate

- `CancellationToken` parameter is always named `cancellationToken`. Never `ct`, never `token`.
- Never delete or modify database records without asking the user first. EF Core migrations that ADD schema are fine. For any `DELETE`, `UPDATE`, `ExecuteDeleteAsync`, `ExecuteUpdateAsync`, or drop-column migration — stop and ask. For migrations the **Migration guard** CI check enforces this: it blocks the PR until it carries the `db-destructive-approved` label.
- After every backend change: run `dotnet build` (0 errors required) and the relevant test class.
- After every frontend change: run, from the repo root, `npm run lint` (0 problems — CI blocks on it), `npm test` (0 failed) and `npm run build` (clean output required).
- Every new service method needs 100% branch coverage in tests.

---

## Backend Patterns

### Result\<T\>

All service methods return `Result<T>` (never throw for expected failures):

```csharp
public async Task<Result<BottleDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken)
{
    var bottle = await db.Bottles.FindAsync(id, cancellationToken);
    if (bottle is null) return Result<BottleDto>.Fail("Bottle not found.");
    return Result<BottleDto>.Ok(Map(bottle));
}
```

Controllers translate results to HTTP via the `ToActionResult` extension (`VirtualBar.Api/Extensions/ResultExtensions.cs`):

```csharp
return result.Success ? Ok(result.Data) : result.ToActionResult(this);
```

Decorators use typed factory methods — never `Fail()` for infrastructure errors:
```csharp
Result<T>.NotFound("Bottle not found.")   // → 404
Result<T>.Forbidden("Forbidden.")         // → 403
Result<T>.Conflict("Already exists.")     // → 409
Result<T>.Fail("Name is required.")       // → 400 (validation)
```

The error body is `{ error }`. `Fail` and `Conflict` take an optional **reason**: a stable, machine-readable value that `ToActionResult` adds as `{ error, reason }` so the frontend can show a translated message instead of the English `error` text. The auth endpoints use it (`AuthErrorReasons` in `VirtualBar.Application/Common`: `InvalidCredentials`, `LockedOut`, `EmailNotConfirmed`, `EmailTaken`; the frontend translates them in the `authErrors` i18n namespace via `src/lib/authErrors.ts`). Reason values are API contract: never rename one. Login deliberately has a single `InvalidCredentials` for unknown email and wrong password, so it does not reveal which accounts exist.

### Ownership check

Every mutation on a user-owned resource must verify ownership before acting:

```csharp
if (bottle.UserId != currentUser.UserId)
    return Result<bool>.Fail("Forbidden.");
```

### Primary Constructor + DI

```csharp
public sealed class BottleService(
    AppDbContext db,
    ICurrentUser currentUser,
    INotificationService notificationService) : IBottleService
```

### Controller XML docs

Every public action must have:

```csharp
/// <summary>One-line description.</summary>
/// <param name="cancellationToken">Cancellation token.</param>
/// <response code="200">Success.</response>
/// <response code="400">Error description.</response>
```

### CancellationToken on every EF Core call

`.ToListAsync(cancellationToken)`, `.FirstOrDefaultAsync(cancellationToken)`, `SaveChangesAsync(cancellationToken)` — always.

### Decorator Pattern for Validation

Every service that has validation logic uses the **Decorator pattern**:

- **`XxxValidationDecorator`** — contains all `if` guard checks (input validation, business rule checks like "does this resource exist?", "is this email already taken?"). Returns `Result<T>.Fail(...)` early on invalid state. Located in `VirtualBar.Infrastructure/Decorators/`. Always calls `cancellationToken.ThrowIfCancellationRequested()` at the top of every method.
- **`XxxService`** (inner) — pure business logic only. Assumes all validation has passed. No `if` guards for preconditions. No `cancellationToken.ThrowIfCancellationRequested()` — the decorator handles it; EF Core async methods honor the token automatically.

DI wiring in `DependencyInjection.cs`:
```csharp
services.AddScoped<AuthService>();
services.AddScoped<IAuthService>(sp =>
    new AuthValidationDecorator(sp.GetRequiredService<AuthService>(), ...));
```

### Service registration

All services are registered in `VirtualBar.Infrastructure/DependencyInjection.cs` and called from `Program.cs`.

---

## Database — AppDbContext

`AppDbContext` extends `IdentityDbContext<AppUser, IdentityRole<Guid>, Guid>`.

**Global rule:** all FK delete behavior defaults to `DeleteBehavior.Restrict` (set via a loop in `OnModelCreating`). Cascade deletes are added only explicitly per entity.

**Global rule — every `DateTime` round-trips as UTC.** `ConfigureConventions` maps every `DateTime` / `DateTime?` property through `UtcDateTimeConverter` / `NullableUtcDateTimeConverter` (`VirtualBar.Infrastructure/Persistence/Conversions/`). Reason: `datetime2` carries no offset, so EF materialises values as `DateTimeKind.Unspecified`, System.Text.Json then omits the `Z`, and clients parse UTC as local time — every timestamp in the API was shifted by the viewer's offset (a seconds-old notification rendered as "3h ago" in UTC+3). Reading always re-stamps `Utc`; writing converts only an explicitly `Local` value and leaves `Unspecified` untouched (assumed already UTC — shifting it would corrupt data). Keep writing `DateTime.UtcNow`; **never** compensate for the offset on the frontend. Pure conversion — no schema change, no migration. Pinned by `VirtualBar.Tests/Persistence/UtcDateTimeConverterTests.cs`, whose round-trip tests need **SQLite** (EF InMemory returns the same CLR instance and so preserves `Kind` on its own, hiding the bug).

Key DbSets:

| DbSet | Purpose |
|---|---|
| `Bottles` | A collector's bottle — the core entity |
| `BottleImages` | Images attached to a bottle (primary + gallery) |
| `BottleLikes` | Junction: user liked a bottle (composite PK) |
| `BottleComments` | Comments on a bottle |
| `UserFollows` | Junction: follower → followed (composite PK) |
| `Messages` | Direct messages between users |
| `NewsPosts` | Admin-authored news articles (multilingual via `NewsPostTranslations`) |
| `NewsPostTranslations` | Per-language title + content for a `NewsPost` |
| `Notifications` | In-app notifications for users |
| `Distilleries` | Master list of distilleries (seeded on startup via `DistillerySeeder`) |
| `DistilleryCategories` | Junction: distillery → spirit category (composite PK; one distillery can have multiple categories) |
| `WishListItems` | A collector's desired bottle criteria — matched against listings |
| `Offers` | Purchase offers from one user to another on a specific bottle |
| `PriceSnapshots` | Read-through cache of indicative per-product price estimates (5-day TTL) — powers Collection Value |
| `BottleReviews` | One structured review per user per bottle: score 0–100 + tasting notes + flavors |
| `BottleReviewFlavors` | Junction: review → flavor tag (composite PK; up to 5 per review) |
| `UserBadges` | Junction: user → earned achievement badge (composite PK; awards are permanent) |
| `Products` | Canonical spirits catalog (seeded via `ProductSeeder` + admin-approved requests) — powers add-form autocomplete, auto-linking, barcode L0 |
| `ProductRequests` | User-filed requests to add a missing catalog product — admin approves/rejects; one pending per canonical key |

All entities extend `BaseEntity` which adds `Id (Guid)`, `CreatedAt`, `UpdatedAt`, `IsDeleted`, `DeletedAt`.

`BottleLike`, `UserFollow`, `DistilleryCategory`, `BottleReviewFlavor`, and `UserBadge` are junction tables with composite PKs — no `BaseEntity`.

---

## Entity Details

### AppUser (extends IdentityUser\<Guid\>)
| Property | Type | Notes |
|---|---|---|
| `DisplayName` | `string` | Indexed |
| `Bio` | `string?` | |
| `AvatarUrl` | `string?` | Relative path under `/uploads/avatars/` |
| `Country`, `City` | `string?` | |
| `IsAdmin` | `bool` | Platform administrator flag (default `false`) |

Relations: one user → many `Bottle`, `BottleComment`, `Message` (sent/received).
Junction relations: `BottleLike` (liked), `UserFollow` (followers/following), `UserBadge` (earned badges).

---

### Bottle
| Property | Type | Notes |
|---|---|---|
| `UserId` | `Guid` | FK → AppUser (owner) |
| `Name` | `string` | |
| `DistilleryId` | `Guid?` | FK → Distillery (nullable; soft-delete aware in MapToDto) |
| `Region`, `Country` | `string?` | |
| `Category` | `SpiritCategory` | Whisky/Rum/Cognac/Vodka/Gin/Tequila/Brandy/Other |
| `Age` | `int?` | Age statement in years |
| `VintageYear` | `int?` | Year of distillation |
| `AbvPercent` | `double?` | Alcohol by volume |
| `VolumeMl` | `int?` | Bottle volume |
| `Condition` | `BottleCondition` | Sealed/Opened/Empty |
| `Quantity` | `int` | How many identical bottles this entry stands for (default `1`, store default backfills old rows, validated 1–99 in the decorator). One entry = the whole stack, so likes/comments/reviews stay aggregated on it — **every total counts physical bottles**: `BadgeCountKind.Bottles`/`LimitedBottles` sum it, `UserProfileService.BottleCount` sums it, and Collection Value multiplies the per-bottle estimate by it. Set on add; the owner can adjust it from the bottle detail panel (a quantity-only `PUT` must still echo every other field), but **never below `ForSaleQuantity`** — `UpdateBottleAsync`'s decorator returns `409` rather than leaving the marketplace offering bottles the owner no longer has |
| `ForSaleQuantity` | `int` | How many of the owned bottles are actually on offer — a stack can be listed **in part**. Invariant maintained by the service, not the DB: `ListForSaleAsync` sets it to `request.Quantity` (validated 1..`Quantity` in the decorator; omitted → **1**, so an older client lists a single bottle rather than the whole stack), `UnlistFromSaleAsync` zeroes it. Only meaningful alongside `IsForSale`, which stays the authoritative listed/not-listed flag every query filters on. `BadgeCountKind.ActiveListings` sums **this**, not `Quantity`; the Dashboard "for sale" stat and the marketplace `×N` badge read it too. `AskingPrice` stays **per bottle** — the UI labels it so once more than one is offered. Changing the offered count means unlist + re-list (the already-for-sale `409` guard still blocks a silent re-list) |
| `Description` | `string?` | |
| `IsLimited` | `bool` | Limited edition flag |
| `IsForSale` | `bool` | Listed in marketplace |
| `AskingPrice` | `decimal?` | `decimal(18,2)` |
| `Currency` | `string?` | ISO code e.g. "USD", "EUR" |
| `ForSaleAt` | `DateTime?` | When the bottle was listed for sale (set by `ListForSaleAsync`, cleared by `UnlistFromSaleAsync`) |
| `Barcode` | `string?` | Scanned/known EAN-UPC; sharpens canonical price-estimate matching (Collection Value) |
| `ProductId` | `Guid?` | FK → Product (nullable) — catalog link. Set by explicit pick or exact-`CanonicalKey` auto-link in `AddBottleAsync`; `UpdateBottleAsync` assigns it blindly from the request (null = unlink — clients must echo the current value). The bottle's own fields remain the display truth |

Relations: one `Bottle` → many `BottleImage`, `BottleLike`, `BottleComment`, `BottleReview`.

`BottleDto` additionally carries `AverageScore` (1 decimal, null when no reviews) + `ReviewsCount` — **computed in queries, never denormalized** (projected the same way `LikesCount`/`CommentsCount` are in `BottleService`).

---

### BottleImage
| Property | Type | Notes |
|---|---|---|
| `BottleId` | `Guid` | FK → Bottle |
| `Url` | `string` | Relative path under `/uploads/bottles/` |
| `IsPrimary` | `bool` | Cover image |
| `SortOrder` | `int` | Gallery order |

---

### BottleLike
Composite PK: `(BottleId, UserId)`. No `BaseEntity`.
| Property | Type |
|---|---|
| `BottleId` | `Guid` |
| `UserId` | `Guid` |
| `LikedAt` | `DateTime` |

---

### BottleComment
| Property | Type | Notes |
|---|---|---|
| `BottleId` | `Guid` | FK → Bottle |
| `UserId` | `Guid` | FK → AppUser (author) |
| `Content` | `string` | |

---

### BottleReview
| Property | Type | Notes |
|---|---|---|
| `BottleId` | `Guid` | FK → Bottle |
| `UserId` | `Guid` | FK → AppUser (reviewer) |
| `Score` | `int` | 0–100, required |
| `Nose` | `string?` | Tasting note, trimmed, ≤ 2000 chars |
| `Palate` | `string?` | Tasting note, trimmed, ≤ 2000 chars |
| `Finish` | `string?` | Tasting note, trimmed, ≤ 2000 chars |
| `Summary` | `string?` | Tasting note, trimmed, ≤ 2000 chars |

Has `ICollection<BottleReviewFlavor> Flavors` (≤ 5, distinct, enum-defined values only).

**Design:** One review per `(BottleId, UserId)` — DB-enforced via a **filtered unique index** (`WHERE [IsDeleted] = 0`), same pattern as the `Offer` pending index: the decorator's already-reviewed pre-check is only a friendly fast path; `BottleReviewService.AddReviewAsync` maps the index violation (`DbUpdateException`) to `Conflict` when a concurrent create loses the race. The owner CAN review their own bottle (tasting journal use case) — no self-notification (decorator skips it). Update replaces flavors wholesale (clear junction rows + add new); the review row soft-deletes via `BaseEntity.IsDeleted`. Reviews attach to the bottle instance (like likes/comments), not to a canonical product; the product page aggregates the reviews of every linked bottle **on read only** (`ProductCatalogService`), never stored. Additional index on `(BottleId, IsDeleted)` for the summary query.

**FlavorTag enum (Domain, append-only, 28 members):** `Smoky, Peaty, Medicinal, Maritime, Vanilla, Caramel, Toffee, Honey, Chocolate, Coffee, Nutty, Malty, Creamy, Fruity, Citrus, TropicalFruit, DriedFruit, Berry, Floral, Herbal, Grassy, Spicy, Pepper, Cinnamon, Oak, Sherry, Leather, Tobacco`. Labels are translated on the frontend only (i18n `flavors` namespace, keys = enum names) — no seeded table, no admin CRUD.

**API endpoints (`/api/bottles/{bottleId}/reviews`):**
- `GET /api/bottles/{bottleId}/reviews` — `[AllowAnonymous]`; one round-trip `BottleReviewsSummaryDto { AverageScore, ReviewsCount, TopFlavors (top-3), Reviews[], MyReview? }`; `MyReview` is null for anonymous users
- `POST /api/bottles/{bottleId}/reviews` — create own review (duplicate → `409`)
- `PUT /api/bottles/{bottleId}/reviews/{reviewId}` — update own review
- `DELETE /api/bottles/{bottleId}/reviews/{reviewId}` — soft-delete own review

### BottleReviewFlavor
Composite PK: `(ReviewId, Flavor)`. No `BaseEntity`. Cascade delete from `BottleReview` (hard junction rows, same as `DistilleryCategory`).
| Property | Type |
|---|---|
| `ReviewId` | `Guid` |
| `Flavor` | `FlavorTag` |

---

### UserFollow
Composite PK: `(FollowerId, FollowedId)`. No `BaseEntity`.
| Property | Type |
|---|---|
| `FollowerId` | `Guid` |
| `FollowedId` | `Guid` |
| `FollowedAt` | `DateTime` |

Both FK relations use `DeleteBehavior.Restrict`.

---

### Message
| Property | Type | Notes |
|---|---|---|
| `SenderId` | `Guid` | FK → AppUser |
| `ReceiverId` | `Guid` | FK → AppUser |
| `Content` | `string` | |
| `IsRead` | `bool` | |

Both FK relations use `DeleteBehavior.Restrict`.

---

### NewsPost
| Property | Type | Notes |
|---|---|---|
| `CoverImageUrl` | `string?` | Optional cover image (shared across languages) |
| `AuthorId` | `Guid` | FK → AppUser |

Has `ICollection<NewsPostTranslation> Translations`. The flat `Title`/`Excerpt`/`Content` columns were removed by migration — all text is in the translations table. Admin-only write (`IsAdmin` check in `NewsValidationDecorator`). Public read via `GET /api/news?lang=bg` and `GET /api/feed?lang=bg`.

### NewsPostTranslation
Composite PK: `(PostId, LanguageCode)`. Cascade delete from `NewsPost`.
| Property | Type | Notes |
|---|---|---|
| `PostId` | `Guid` | FK → NewsPost |
| `LanguageCode` | `string` | e.g. `"bg"`, `"en"` |
| `Title` | `string` | |
| `Content` | `string` | Full article body |

`NewsService.Resolve(post, lang)` returns the translation matching `lang`, falls back to `"bg"`. Bulgarian translation is required — enforced in `NewsValidationDecorator`.

---

### Notification
| Property | Type | Notes |
|---|---|---|
| `UserId` | `Guid` | FK → AppUser (recipient) |
| `Type` | `NotificationType` | See enum below |
| `ActorId` | `Guid` | FK → AppUser (who triggered it) |
| `ActorDisplayName` | `string` | Denormalised — avoids join on read |
| `ResourceId` | `Guid?` | Bottle ID (for bottle-related types) or Message ID |
| `ResourceName` | `string?` | Bottle name (for bottle-related types) |
| `IsRead` | `bool` | |

Both FK relations use `DeleteBehavior.Restrict`. Composite index on `(UserId, IsDeleted, CreatedAt)`.

**NotificationType enum:**
| Value | Triggered by |
|---|---|
| `BottleLiked` | `BottleLikeService.LikeAsync` |
| `BottleCommented` | `BottleCommentService.AddCommentAsync` |
| `NewFollower` | `UserFollowService.FollowAsync` |
| `NewMessage` | `MessageService.SendAsync` |
| `NewBottleFromFollowing` | `BottleService.AddBottleAsync` — fan-out to all followers |
| `BottleListedForSale` | `BottleService.ListForSaleAsync` — fan-out to all followers |
| `WishListMatch` | `BottleService.ListForSaleAsync` — fan-out to wish list owners whose criteria match the listed bottle |
| `OfferReceived` | `OfferService.CreateAsync` — sent to the seller |
| `OfferAccepted` | `OfferService.AcceptAsync` — sent to the buyer |
| `OfferDeclined` | `OfferService.DeclineAsync` — sent to the buyer |
| `BottleReviewed` | `BottleReviewService.AddReviewAsync` — sent to the bottle owner (create only, not on update/delete) |
| `BadgeEarned` | `BadgeService.EvaluateAsync` — sent to the badge earner via `CreateSystemAsync` (actor = recipient; `ResourceName` = the `BadgeType` name) |
| `ProductRequestApproved` | `ProductRequestService.ApproveAsync` — sent to the requester (`ResourceId` = new/linked product id, `ResourceName` = product name) |
| `ProductRequestRejected` | `ProductRequestService.RejectAsync` — sent to the requester (`ResourceId` = request id, `ResourceName` = requested product name) |

**INotificationService methods:**
- `CreateAsync(recipientId, type, resourceId, resourceName, ct)` — single recipient; decorator skips if `recipientId == currentUser.UserId` (no self-notifications).
- `CreateBulkAsync(recipientIds, type, resourceId, resourceName, ct)` — fan-out; queries actor display name once, `AddRange` + single `SaveChangesAsync`. Decorator filters out self from the list.
- `CreateSystemAsync(recipientId, type, resourceId, resourceName, ct)` — system notification with **no self-skip**: the recipient MAY equal the current user (own-milestone notifications); inner sets `ActorId = recipientId` + the recipient's **own** display name (missing recipient → silent return). Used only by the badge engine (`BadgeEarned`). Do NOT "fix" the self-skip on the other two — it protects every other type.
- All three are fire-and-forget calls from trigger services (not controller-exposed). They return `Task` (not `Result<T>`).
- `GET /api/notifications` — returns last 30 + global unread count.
- `PATCH /api/notifications/{id}/read` and `POST /api/notifications/read-all`.

---

### Distillery
| Property | Type | Notes |
|---|---|---|
| `Name` | `string` | Unique index |
| `Country` | `string?` | |
| `Region` | `string?` | |

Has `ICollection<DistilleryCategory> Categories` and `ICollection<Bottle> Bottles`. Seeded on startup by `DistillerySeeder` with **top-up semantics** — inserts distilleries missing by name (case-insensitive) and missing `(DistilleryId, Category)` pairs, strictly insert-only, never updates existing rows (~710 entries across Whisky/Rum/Vodka/Cognac/Brandy/Tequila/Gin). **No `POST /api/distilleries`** — seeder is the single source of truth. Public read via `GET /api/distilleries?category=Rum` (AllowAnonymous).

### DistilleryCategory
Composite PK: `(DistilleryId, Category)`. No `BaseEntity`. Cascade delete from `Distillery`.
| Property | Type |
|---|---|
| `DistilleryId` | `Guid` |
| `Category` | `SpiritCategory` |

---

### WishListItem
| Property | Type | Notes |
|---|---|---|
| `UserId` | `Guid` | FK → AppUser (`DeleteBehavior.Restrict`) |
| `BottleName` | `string?` | User label only — NOT used in matching |
| `DistilleryId` | `Guid?` | FK → Distillery — matching criterion (exact FK match) |
| `Category` | `SpiritCategory?` | Matching criterion (exact enum match) |
| `ImageUrl` | `string?` | Optional display image |

At least one of `DistilleryId` or `Category` must be set (enforced by `WishListValidationDecorator`). Matching in `BottleService.ListForSaleAsync` uses AND of all non-null criteria. Publicly visible in Marketplace "LOOKING FOR" tab — other users can click "СЪОБЩЕНИЕ" to open a chat with the searcher.

---

### Offer
| Property | Type | Notes |
|---|---|---|
| `BottleId` | `Guid` | FK → Bottle (`DeleteBehavior.Restrict`) |
| `BuyerId` | `Guid` | FK → AppUser |
| `SellerId` | `Guid` | FK → AppUser |
| `OfferedPrice` | `decimal` | `decimal(18,2)` |
| `Currency` | `string` | ISO code e.g. "USD" |
| `Message` | `string?` | Optional note from buyer |
| `Status` | `OfferStatus` | Pending/Accepted/Declined/Withdrawn |
| `RespondedAt` | `DateTime?` | Set when seller accepts or declines |

**Design:** Offers can be made on **any** non-deleted bottle, regardless of `IsForSale`. One pending offer per buyer per bottle — DB-enforced via a **filtered unique index** on `(BottleId, BuyerId)` where `Status = Pending AND IsDeleted = 0`; the decorator's pre-check is only a friendly fast path, and `OfferService.CreateAsync` maps the index violation to `Conflict` when a concurrent create loses the race. Accept/Decline/Withdraw are **atomic conditional updates** (`ExecuteUpdateAsync … WHERE Status = Pending`): the race loser gets `Conflict` and fires no notification, so an offer can never be resolved twice. Because of `ExecuteUpdateAsync`, `OfferServiceTests` for these paths use SQLite in-memory. All FK relations `DeleteBehavior.Restrict`.

**OfferStatus enum:** `Pending`, `Accepted`, `Declined`, `Withdrawn`

**API endpoints (`/api/offers`, all require `[Authorize]`):**
- `POST /api/offers` — buyer creates offer
- `GET /api/offers/received` — seller views received offers
- `GET /api/offers/sent` — buyer views sent offers
- `PATCH /api/offers/{id}/accept` — seller accepts
- `PATCH /api/offers/{id}/decline` — seller declines
- `PATCH /api/offers/{id}/withdraw` — buyer withdraws

---

### PriceSnapshot
Read-through cache of an **indicative market-value estimate** for a canonical product (see Collection Value below). One row per `ProductKey`, so identical bottles owned by different users share a single lookup. Not user-owned — no FKs.

| Property | Type | Notes |
|---|---|---|
| `ProductKey` | `string` | The bottle's price key from `PriceKey.ForBottle(...)` — `Product.CanonicalKey` for a bottle linked to a non-deleted product, else `ProductKey.For(...)` of the bottle; **unique index** |
| `Barcode` | `string?` | Last barcode seen for this product |
| `Category` | `SpiritCategory` | |
| `EstimatedPrice` | `decimal` | Median, base currency; `decimal(18,2)` |
| `LowEstimate` / `HighEstimate` | `decimal?` | Indicative min–max |
| `Currency` | `string` | Base currency (config, default `EUR`) |
| `SampleSize` | `int` | Data points behind the estimate |
| `Source` | `PriceSource` | `ClaudeResearch` / `Internal` |
| `Confidence` | `PriceConfidence` | `Low` / `Medium` / `High` |
| `SourcesJson` | `string` | JSON array of `{ Url, Title }` citations (mandatory for `ClaudeResearch`; serialized/read with default System.Text.Json — PascalCase) |
| `AsOf` | `DateTime` | "As of" date of the underlying data |
| `FetchedAt` | `DateTime` | When cached; drives the 5-day TTL |

Indexes: unique `ProductKey`; `Barcode`; `(Category, FetchedAt)`. `SourcesJson` has no explicit column type (defaults to `nvarchar(max)` on SQL Server, `TEXT` on SQLite — do not re-add `HasColumnType("nvarchar(max)")`, it breaks the SQLite test DB).

---

### UserBadge
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

---

### Product
Canonical catalog entry for a spirits product. Not user-owned.

| Property | Type | Notes |
|---|---|---|
| `Name` | `string` | Required |
| `Brand` | `string?` | The name on the label. **Independent of `DistilleryId`, not an alternative to it** — every seeded row carries one, and it may differ from the distillery (Caorunn @ Balmenach, Meikle Toir @ GlenAllachie, The One @ Lakes Distillery). Nullable only because an admin-approved product may omit it; both read paths then fall back to the non-deleted distillery name |
| `DistilleryId` | `Guid?` | FK → Distillery (`DeleteBehavior.Restrict`) — **where it was made**, known only for some products (blends like Johnnie Walker have none) |
| `Category` | `SpiritCategory` | |
| `Country`, `Region` | `string?` | |
| `Age` | `int?` | |
| `AbvPercent` | `double?` | |
| `VolumeMl` | `int?` | |
| `Barcode` | `string?` | EAN/UPC digits |
| `ImageUrl` | `string?` | |
| `Description` | `string?` | |
| `CanonicalKey` | `string` | `ProductKey.For(distilleryName ?? brand, name, category, age, null, volumeMl)` — **filtered unique index** `WHERE [IsDeleted] = 0` |
| `Origin` | `ProductOrigin` | `Seeded` / `Approved` (append-only enum) |

**No `VintageYear`** — vintage/batch/cask specifics stay on the bottle instance. `ProductKey` normalization is **frozen** (shared with the `PriceSnapshot` cache — changing it would orphan both). Seeded on startup by `ProductSeeder` with **top-up semantics** (the `DistillerySeeder` pattern): one embedded seed file **per category** — `SeedData/products.<category>.seed.json` (`whisky`, `rum`, `cognac`, `brandy`, `vodka`, `gin`, `tequila`, `other`; user-curated and growing — ~3400 rows; no fabricated data — unknown fields are null). The csproj embeds them with the glob `Persistence\SeedData\products.*.seed.json`, and `SeedProductsAsync` reads every matching manifest resource in ordinal order, so adding a new category file needs no code change. Each row still carries its own `category` field. It inserts only rows whose `CanonicalKey` is not yet present — strictly insert-only, never updates or re-inserts existing rows (soft-deleted included); rows with unparseable categories are skipped, in-file duplicate keys collapse to the first, distillery names resolve case-insensitively (miss → `Brand` fallback + null FK). That fallback is a **silent data loss** — the row keeps its name but loses the FK, so it disappears from `/api/distilleries`, the add-bottle picker and wish-list matching. It is therefore both **logged** (`SeedProductsAsync` takes an optional `ILogger` and warns with the offending names) and **enforced**: `ProductSeedDataTests.EverySeedDistillery_ExistsInDistillerySeeder` fails the build when a seed file names a distillery `DistillerySeeder` does not know. When that test fires, either add the name to `DistillerySeeder` (a real production site) or move it to `brand` (a range or label — e.g. Port Charlotte is a Bruichladdich line, not a distillery). `ProductSeedDataTests.EverySeedRow_CarriesABrand` additionally pins that every row names its label brand. Tests assert seed invariants, never an exact row count.

**Add-bottle resolution order** (`BottleService.AddBottleAsync`): (a) explicit `ProductId` from the client (decorator validates existence) → link; (b) else exact `CanonicalKey` match → **auto-link silently**; (c) else auto-file a `ProductRequest` with `SourceBottleId` — wrapped `try/catch → LogError`, non-success `Result` ignored (the badge-engine philosophy: a request bug never fails adding a bottle).

**Barcode L0:** `ProductLookupService.LookupByBarcodeAsync` answers from `Products` **before** the external UPC API (non-deleted match by `Barcode`, `OrderBy(CreatedAt).ThenBy(Id)` — oldest wins; `Brand` falls back to the non-deleted distillery name). Hit → `BarcodeProductDto.ProductId` set, zero external calls; miss → external path unchanged. The lookup decorator trims the barcode once and logs swallowed exceptions.

**API endpoints (`/api/products`):**
- `GET /api/products?search=&category=&limit=` — `[AllowAnonymous]` catalog search; `search` trimmed, min 2 chars, max 100; `limit` clamped 1–**51** (default 20) — the cap is deliberately one over the 50 a client renders, so asking for 51 and getting 51 back proves there are more matches without a count query; `ProductSelect` uses exactly that to decide whether to show its "refine your search" hint; matches `Name`/`Brand`/`Distillery.Name` (contains; case-insensitivity comes from the SQL Server collation, not the code); starts-with ranked first; the returned `Brand` falls back to the non-deleted distillery name exactly as the barcode endpoint does, so both paths agree on the same product
- `GET /api/products/barcode/{barcode}` — `[Authorize]`; catalog L0 → external UPC fallback
- `GET /api/products/browse?search=&category=&page=&pageSize=` — `[AllowAnonymous]`; the public `/catalog` → `PagedResult<ProductCardDto> { Items, HasMore }`. `search` is optional (omitted/blank lists the whole catalog ordered `Name, Id`; a term filters and ranks exactly like the search endpoint, 2–100 chars); `page` ≥ 1 (≤ 1000, beyond → `400`); `pageSize` clamped 1–48 (default 24). Each card adds `OwnersCount` (distinct users), `ReviewsCount`, `AverageScore` and `ListingsCount` over the product's non-deleted linked bottles
- `GET /api/products/{id}` — `[AllowAnonymous]`; the `/product/:id` page → `ProductDetailDto`: the product (+ `Description`) plus, over every non-deleted linked bottle, the review aggregate (`AverageScore`, `ReviewsCount`, top-3 `TopFlavors` with the same tie-break as a bottle), `OwnersCount` + the top 20 `Owners` ranked by physical bottles (`Sum(Quantity)`), every for-sale `Listings` entry (newest first) and the 5 `LatestReviews`; `404` unknown/deleted product
- `GET /api/products/{id}/reviews?page=&pageSize=` — `[AllowAnonymous]`; the rest of the product's reviews, newest first → `PagedResult<BottleReviewDto>`; `pageSize` clamped 1–50 (default 10); `404` unknown/deleted product

All three are **computed on read** (no stored aggregates, no migration) and use `pageSize + 1` for `HasMore` instead of a count query. Reviews on the product page are mapped through the same `ReviewMapping.ToDto` as the bottle reviews, so the two cannot drift.

### ProductRequest
A user's request to add a missing product to the catalog.

| Property | Type | Notes |
|---|---|---|
| `UserId` | `Guid` | FK → AppUser (requester, `DeleteBehavior.Restrict`) |
| `Name`, `Brand?`, `DistilleryId?`, `Category`, `Age?`, `AbvPercent?`, `VolumeMl?`, `Barcode?`, `Country?`, `Region?` | | Proposed fields mirroring `Product` |
| `UserNote` | `string?` | ≤ 500 chars (`Brand` ≤ 200) |
| `CanonicalKey` | `string` | **Filtered unique index** `WHERE [Status] = 0 AND [IsDeleted] = 0` — one pending request per key, globally |
| `Status` | `ProductRequestStatus` | `Pending` / `Approved` / `Rejected` (append-only — withdraw is a soft-delete, no `Withdrawn` member) |
| `AdminNote` | `string?` | |
| `ResolvedProductId` | `Guid?` | FK → Product (`Restrict`) |
| `SourceBottleId` | `Guid?` | FK → Bottle (`Restrict`) — the bottle whose add triggered it; ownership enforced in the decorator |
| `RespondedAt` | `DateTime?` | Set on approve/reject |

**Design:** the `Offer` index pattern — the decorator's dedupe pre-checks (existing product / existing pending request with the same key) are a friendly fast path; `ProductRequestService` maps the `DbUpdateException` race loser to `Conflict` (on create AND on the approve-time product insert). Per-user cap: **25 open requests**. Sanity bounds (create + approve effective values): barcode digits-only 8–14, age 1–100, ABV 1–96, volume 20–6000; approve additionally validates the effective `Category` (enum-defined) and effective `DistilleryId` (exists non-deleted). **Approve** (admin-only): optional field overrides (`override ?? request value`) or `ExistingProductId` (link instead of create); `UseSourceBottleImage` copies the source bottle's primary image URL (explicit `ImageUrl` override wins; ignored on the `ExistingProductId` path; missing bottle/images → null, never an error); creates the `Product` (`Origin = Approved`, key recomputed from effective values), marks the request, links the source bottle if still unlinked, then **retro-links** identical unlinked bottles (SQL pre-filter on `Category`/`Age`/`VolumeMl`, in-memory exact `ProductKey` comparison without vintage — silent, no owner notifications), and finally evaluates the **requester's** badges (`BadgeTrigger.ProductRequestApproved` → `FirstCatalogProduct`). **Reject** sets `AdminNote` + `RespondedAt` — no badge evaluation. **Withdraw** = the requester soft-deletes an own **pending** request. A rejected key may be requested again (the index only guards `Pending`).

**API endpoints (`/api/product-requests`, all `[Authorize]`):**
- `POST /api/product-requests` — manual request (`409` duplicate)
- `GET /api/product-requests/mine` — own requests, newest first
- `DELETE /api/product-requests/{id}` — withdraw own **pending** request
- `GET /api/product-requests?status=Pending` — admin queue (`403` non-admin)
- `PATCH /api/product-requests/{id}/approve` — admin; body `ResolveProductRequestRequest` (**required** — send at least `{}`)
- `PATCH /api/product-requests/{id}/reject` — admin; body `RejectProductRequestRequest` (**required** — send at least `{}`)

---

## Collection Value (bottle price estimation)

Shows an **indicative min–max market value per bottle** and a **total collection value** (Sealed bottles only) on the Dashboard. Always indicative — shown with range + confidence + **sources** + "as of" date; `None` renders "—", never a fabricated number.

**Provider architecture** (`VirtualBar.Infrastructure/Services/Pricing/`):
- `IPriceProvider` + `PriceProviderBase` (abstract) — shared plumbing: on/off gate (`UseProviderStats`), FX→base conversion, percentile aggregation, sanity bounds (`MaxSanePrice`), error-swallow → `null` + log, DTO build. Each provider overrides only `FetchAsync`.
- `ClaudeMarketResearchProvider` (`PriceSource.ClaudeResearch`) — Anthropic Messages API + `web_search` tool; returns an indicative min–max + **mandatory citations** (a result with no citations is treated as `None`). Typed `HttpClient`.
- `InternalMarketPriceProvider` (`PriceSource.Internal`) — our own listings + accepted offers matched by `ProductKey`; confidence by sample count.
- `PriceEstimationService` (orchestrator) — read-through `PriceSnapshot` cache (5-day TTL). On a miss/stale snapshot it tries **Claude first, then Internal** as fallback, and caches the chosen estimate. Collection value sums **Sealed** bottles only, each scaled by the bottle's `Quantity`.
- `PreWarmWorker` + `PreWarmRefreshJob` (`IHostedService`) — periodically research the top-N most-owned bottles with a missing/stale snapshot, within budget.
- `AnthropicDailyCallBudget` (singleton) — process-wide cap on billed Claude calls, resets per UTC day; consumed by both the provider and the pre-warm job.

**Selection order (shipped):** Claude → Internal fallback → `None`.

**Price key rule (#50):** every pricing path computes a bottle's key through `PriceKey.ForBottle` (`VirtualBar.Application/Common/PriceKey.cs`) — `PriceEstimationService` (estimate, cached read, collection value), `PreWarmWorker` (grouping) and `InternalMarketPriceProvider` (listings + accepted offers). A bottle linked to a **non-deleted** product uses the stored `Product.CanonicalKey`, so every copy of the product shares one snapshot whatever its spelling or vintage, and Claude researches the **product's** fields (name, non-deleted distillery ?? brand, age, volume, no vintage, product barcode ?? bottle barcode). Any other bottle keeps `ProductKey.For(bottle fields)`. Never call `ProductKey.For` directly in pricing code: two paths computing the key differently means snapshots written by one are never read by the other. Snapshots written under the old bottle keys before this rule are left in place (a cache; they are simply no longer read).

**API (`/api/prices`, `[Authorize]`):**
- `GET /api/prices/bottle/{bottleId}` → `PriceEstimateDto` — **cache-only** read (never triggers a synchronous Claude call on the request path); `204` when no estimate is cached yet, `404` when the bottle is missing.
- `GET /api/prices/product/{productId}` → `PriceEstimateDto` — **cache-only** read of the snapshot under the product's `CanonicalKey` (the one its linked bottles share); `204` when none is cached, `404` for an unknown or deleted product.
- `GET /api/prices/collection` → `CollectionValueDto` — Sealed-only total + per-bottle lines; owner-scoped (decorator enforces `userId == currentUser.UserId`).

**Config** (`appsettings.json`; `Anthropic:ApiKey` is empty there — set it in `appsettings.Development.json` / user-secrets, never committed):
- `Pricing` — `BaseCurrency`, `SnapshotTtlDays` (5), `FxToBase`, `RefreshIntervalHours`, `PreWarmTopNBottles`, `RefreshEnabled`.
- `Anthropic` — `UseProviderStats` (on/off), `Model` (`claude-sonnet-4-6`), `AnthropicVersion`, `MaxSearchesPerBottle`, `DailyCallBudget`, `AllowedDomains` / `BlockedDomains`.
- `InternalProvider` — `UseProviderStats`, `MinSamples`, `MinApproxSamples`.

Full slice-by-slice specs live in `docs/collection-value/`.

---

## Frontend Architecture

**Auth:** JWT stored in `localStorage` (`vbar_token` + `vbar_user`). `useAuth()` exposes `user`, `login`, `logout`. The Axios `client` in `src/api/client.ts` attaches the token as a Bearer header on every request and redirects to `/login` on 401. The `user` object includes `isAdmin: boolean` — set from the JWT claim emitted by `AuthService`.

**API layer (`src/api/`):** One file per domain (e.g., `bottlesApi.ts`, `usersApi.ts`, `notificationsApi.ts`). All use named import `{ client }` from `./client`. Return typed data directly (axios `.data` unwrapping).

**Data fetching:** TanStack Query (`useQuery` / `useMutation`). `queryClient.invalidateQueries` after mutations. Global `staleTime: 30_000`.

**Types:** All shared TypeScript types in `src/types/index.ts`. No per-file type definitions.

**Internationalisation:** `react-i18next` + `i18next-browser-languagedetector`. Bulgarian is the default language (`fallbackLng: 'bg'`). English is optional. The choice made in the `LanguageSwitcher` is saved in `localStorage` under `vbar_lang` and wins on every later load; detection reads only that key (never the browser language), so a first visit is always Bulgarian, and `supportedLngs` sends an unknown saved value to Bulgarian too. Do not add `lng` to the init options: it overrides detection, which is how the choice used to be forgotten on reload (#34). Translation files: `src/i18n/bg.json` and `src/i18n/en.json`. i18next is initialised in `src/i18n/index.ts` and imported as the first line of `src/main.tsx`. Every page and component uses `const { t } = useTranslation()`. The `LanguageSwitcher` component (`src/components/LanguageSwitcher.tsx`) renders a speakeasy-styled dropdown (БГ/EN) placed inside the NavBar of every page.

Current translation namespaces: `nav`, `lang`, `login`, `register`, `dashboard`, `addBottle`, `browse`, `marketplace`, `publicBar`, `bottle`, `messages`, `profile`, `footer`, `hero`, `home`, `barShelf`, `notifications`, `wishList`, `distillerySelect`, `offers`, `collectionValue`, `reviews`, `flavors` (28 keys = `FlavorTag` enum member names), `badges` (`title`/`progressTitle`/`earnedOn` + 19 × `<EnumName>.{name,description}`, keys = `BadgeType` enum member names), `authErrors` (4 keys = `AuthErrorReasons` values; login/register show these instead of the API's English `error` text), `confirmEmail` (the "check your email" screen, the `/confirm-email` page and the "send a new link" button), `catalog` (the `/catalog` page; `owners`/`forSale`/`reviews` are `_one`/`_other` plurals), `product` (the `/product/:id` page).

**Routing:** `/` is `HomePage.tsx` (public news/social feed). Login and register redirect to `/` after success. **Email confirmation** (`Identity:RequireConfirmedEmail`, on by default outside Development): register then answers with an empty token and `requiresEmailConfirmation: true`, so `AuthContext.register` stores nothing and returns `'emailConfirmationRequired'`, and `RegisterPage` shows "check your email". The link in the email opens `/confirm-email?email=…&token=…` (`ConfirmEmailPage`, public), which calls `POST /api/auth/confirm-email` once on load. `ResendConfirmation` (`POST /api/auth/resend-confirmation`) is offered there when the link is invalid or expired, on the check-your-email screen, and under a login refused with `EmailNotConfirmed`. If the confirmation email fails to send, register still answers `requiresEmailConfirmation: true` (the account already exists; `AuthService` logs the error), so the user lands on "check your email" and recovers with `ResendConfirmation` once email works again. `/dashboard` is the user's own virtual bar (protected). `/catalog` (`CatalogPage`) and `/product/:id` (`ProductPage`) are **public**. The catalog keeps its search term and category in the URL (`?q=&category=`, typing replaces the history entry, a category click pushes one), so back/forward and shared links reproduce the view; it pages with `useInfiniteQuery` and a "show more" button. The search box keeps what is typed in its own state and writes it to the URL. It is **not** bound to `searchParams` directly: React Router commits a URL change in a transition, so a box bound to the URL is re-rendered with the old value before the new one lands, and fast typing lost letters ("Lagavulin" became "gvlin" at 80 ms a key). The box takes the URL's term only on a POP (back/forward); `catalog-browse.spec.ts` types at full speed to keep it that way. The product page shows the price estimate only to logged-in users (`/api/prices` is `[Authorize]`; an anonymous call would 401-redirect to `/login`), opens a listing in `BottleDetailPanel` (queried as `['bottles', ownerId, bottleId]`, so the panel's own `['bottles', ownerId]` invalidations refresh it), and renders a "not found" state for an unknown id instead of redirecting. `/offers` is the Offers page (protected). There is **no `/messages` route** — messaging is handled entirely by the floating `ChatWidget`.

**Shared components:**
- `src/components/NavBar.tsx` — imported by every page; contains `<NotificationBell />` and `<LanguageSwitcher />` in the right slot (authenticated only). The "СЪОБЩЕНИЯ" nav item is a button that calls `toggleInbox()` from `ChatContext` — not a route link.
- `src/components/Avatar.tsx` — props: `displayName`, `avatarUrl?`, `size`. Renders `<img>` when avatarUrl present, initials div otherwise.
- `src/components/NotificationBell.tsx` — bell icon with gold badge (unread count), dropdown with last 30 notifications, mark-read / mark-all-read. Polls every 30 s via `refetchInterval`. `NewMessage` notifications call `openChat(actorId)` from `ChatContext` instead of navigating. `BadgeEarned` notifications render the translated badge name from `resourceName` and navigate to `/profile` (no actor emphasis — actor == recipient).
- `src/components/Footer.tsx` — mounted once in `App.tsx`; fully opaque `#07030A` background.
- `src/components/BottleDetailPanel.tsx` — full-screen overlay for bottle details. Shows `SaleSection` + `DeleteSection` for the owner. Shows `MakeOfferSection` for non-owners (any authenticated user, regardless of `IsForSale`). `EstimateSection` (price estimate) for authenticated users only — the `/api/prices` endpoint is `[Authorize]`, so rendering it anonymously would 401-redirect to `/login`. `LikesSection`, `ReviewsSection` (aggregate header + one-per-user review form + review list; form authenticated-only), and `CommentsSection` (below reviews) for all. `onDelete` prop (DashboardPage only) invalidates bottles query and closes panel. Bottle cards (`BarShelf`, Marketplace) show a gold `★ <avg>` badge when `reviewsCount > 0`. A bottle linked to a catalog product (`productId`) shows a "View product" link (`bottle.viewProduct`, visible to everyone) that closes the panel and opens `/product/:id`.
- `src/components/reviews/ReviewsAggregate.tsx` (average/100, count, top flavors; takes the three values, plus an optional `emptyLabel`) and `src/components/reviews/ReviewCard.tsx` — shared by the bottle panel and the product page; their helpers (`NOTE_FIELDS`, `hasAnyNote`, `formatRelativeTime`) live in `src/lib/reviewFormat.ts`.
- `src/components/PriceEstimateView.tsx` — the presentational estimate block (range, confidence, sources, "as of", "—" when nothing is cached); the caller owns the query (`EstimateSection` in the panel for a bottle, `ProductPage` for a product).
- `src/components/CategoryPill.tsx` — the round category filter button (`aria-pressed`) used by Marketplace and the catalog.
- `src/components/ChatWidget.tsx` — floating Facebook Messenger-style chat. `position: fixed; bottom: 0; right: 20px; zIndex: 1000`. Renders only for authenticated users. Layout right-to-left: gold circular toggle button (52px, unread badge) → inbox panel (300×480px) → active conversation window (320×480px). Polls inbox every 30 s. Mark-as-read runs automatically when a thread is viewed.
- `src/components/DistillerySelect.tsx` — autocomplete dropdown for selecting a distillery. Props: `value`, `onChange`, `category?`. ARIA roles (`combobox`/`listbox`/`option`), keyboard navigation (ArrowUp/Down/Enter/Escape), scroll-into-view. Fetches from `GET /api/distilleries?category=...`, `queryKey: ['distilleries', category ?? 'all']`, staleTime 5 min.
- `src/components/BadgeChip.tsx` — circular gold-bordered achievement medallion: static `badgeType → lucide icon` map (19 entries) + translated name (`badges` namespace); `size` prop; dimmed variant for `earned: false`. Used by `ProfilePage` (achievements section: earned chips first, then unearned with thin gold progress bars + "7/10" labels; `queryKey: ['badges','progress']`) and `PublicBarPage` (earned-only strip under the bar header; `queryKey: ['badges', userId]`; empty/loading/error → renders nothing).

**Contexts:** each provider file exports only its component; the context object and hook live in a separate `.ts` file (Vite fast refresh needs component-only files — ESLint `react-refresh/only-export-components`). Value types (`AuthContextValue`, `ChatContextValue`) are in `src/types/index.ts`.
- `src/contexts/AuthContext.tsx` (`AuthProvider`) + `src/contexts/useAuth.ts` (`useAuth()`) — `user`, `token`, `isAuthenticated`, `login`, `register`, `logout`, `updateUser`. The saved session is read from `localStorage` while the state is created, so the first render already knows who is logged in and there is no app-wide loading state. `login`/`register` must not add one: swapping the routes for a spinner unmounts the form and loses the error a failed attempt sets on it (the E2E "Login Validation" tests pin this). The forms show their own pending state through `useMutation`.
- `src/contexts/ChatContext.tsx` (`ChatProvider`) + `src/contexts/useChat.ts` (`useChat()`) — `inboxOpen`, `openInbox`, `closeInbox`, `toggleInbox`, `activeUserId`, `openChat(userId)`, `closeChat`. Used by NavBar, NotificationBell, MarketplacePage, PublicBarPage, ChatWidget.

The same rule applies to any component file: shared constants go to their own module (e.g. the bottle colors `CATEGORY_COLORS` / `LIQUID_LEVEL` / `CAP_COLOR` in `src/lib/bottlePalette.ts`). Generated shadcn files in `src/components/ui/` are exempt in `eslint.config.js`.

**App shell (`src/App.tsx`):** Fixed background image (`/public/bg-room.png`) + darkening overlay both `position: fixed; z-index: -1/-2`, content wrapper wrapped in `ChatProvider`, `<ChatWidget />` before `<Footer />`.

**Visual theme:** Tailwind CSS 4 + inline styles for the speakeasy aesthetic.
- Background: `#07030A` / `stone-950` / `stone-900`
- Accent gold: `#C9A84C` / `#E8C870`
- Text: `stone-100` / `stone-300`
- Cards/surfaces: dark semi-transparent with amber borders
- Fonts: Playfair Display (headings), Cormorant Garamond, Cinzel (labels/nav)

**Static inline styles:** Module-level `CSSProperties` constants (never created inside render). Dynamic values (hover state, `item.isRead` background) stay inline.

---

## Testing Conventions

- One test class per service: `<ServiceName>Tests` in `VirtualBar.Tests/Services/`.
- Method naming: `<MethodName>_When<Condition>_<ExpectedOutcome>`.
- Each test creates an isolated InMemory DB: `Guid.NewGuid().ToString()` as the DB name.
- Use **EF Core InMemory** by default. Switch to **SQLite in-memory** only when the method calls `ExecuteUpdateAsync` / `ExecuteDeleteAsync`, or when the test must surface a **unique-index or duplicate-PK violation as `DbUpdateException`** (InMemory doesn't enforce secondary unique indexes and throws `ArgumentException`, not `DbUpdateException`, for PK duplicates — e.g. the `BottleReview` duplicate-race and `BadgeService` PK-race tests).
- Mock only `ICurrentUser`, `INotificationService`, `IBadgeService`, and `IProductRequestService` (trigger-service tests) — never mock `AppDbContext`.
- Services that depend on `INotificationService` / `IBadgeService` / `IProductRequestService` receive `Mock.Of<T>()` in their `CreateXxxService` helper (optional parameters with defaults).
- Seed helpers are `private static` methods in the test class: `SeedUser`, `SeedBottle`, `SeedComment`, etc.
- Cover every branch: every `if`, every `switch` arm, every `?.`, `??`, `&&`, `||`.

**Integration tests (`VirtualBar.Tests/Integration/`)** — the deliberate exception to the mocking rule above. A flow that spans services (trigger service → notification → badge) can only be proven end to end by wiring the **real** collaborators, so these classes assemble the service graph exactly as `DependencyInjection.cs` does — real `NotificationService` and `BadgeService` behind their decorators, one graph per acting user (a request scope), only `ICurrentUser` mocked. Do NOT "fix" them back to `Mock.Of<T>()`: mocks would hide the very chain under test. They complement the per-service classes, never replace them — branch coverage still belongs in `Services/`. Because the badge engine swallows and logs its own exceptions, these tests also assert `ILogger<BadgeService>` logged no error, so a silent failure cannot pass as success. Existing file: `ProductRequestApprovalFlowTests` (approve → requester notified + `FirstCatalogProduct` awarded).

### HTTP tests (`VirtualBar.HttpTests`)

A project of its own, because it is the only backend test layer that needs SQL Server: `VirtualBar.Tests` stays
self-contained and fast. These tests start the real `Program.cs` in memory (`WebApplicationFactory<Program>`; hence
`public partial class Program { }` at the end of `Program.cs`) and send it real HTTP requests, so they cover what the
service tests cannot: DI wiring, middleware, `[Authorize]`/`[AllowAnonymous]`, model validation, JSON shape, status
codes, JWT validation and the startup guards. They are not a place for branch coverage: that stays in `VirtualBar.Tests`.
- **SQL Server, not InMemory/SQLite:** startup applies the SQL Server migrations, and the app relies on filtered unique
  indexes and the case-insensitive collation.
- **One throwaway database per run** (`VirtualBar_HttpTests_<guid>`, `Support/ApiHosts.cs`): the first host's startup
  migrates and seeds it exactly as production does (about 16 s), and it is dropped at the end. It is never the
  development database: only the server is taken from the connection string. Tests keep apart by registering
  collectors with unique emails (`AuthApi.NewEmail`), as the E2E specs do.
- **Every class joins `[Collection(ApiCollection.Name)]`** and the assembly runs one test at a time: the classes share
  `ApiHosts`, and hosts built in parallel can catch each other's startup.
- **Hosts per configuration**, each started once on first use: `ConfirmationOn` (production behaviour),
  `ConfirmationOff` (Development / E2E behaviour), `EmailUnreachable` (confirmation on, SMTP refuses every connection).
  A new configuration is a new lazy host in `ApiHosts`, not a new database.
- **Settings go in through `UseSetting`** (`Support/ApiFactory.cs`), never `ConfigureAppConfiguration`: Program.cs reads
  `Jwt:Key`, `Email:PickupDirectory` and `Identity:RequireConfirmedEmail` before `Build()`, and only `UseSetting` reaches
  it that early. The defaults match the E2E workflow: emails to a temporary pickup folder (`Mailbox` reads them back
  with MimeKit), no Claude calls, no price pre-warm, auth rate limit raised, a random `Jwt:Key`.
- **`[SqlServerFact]`** for anything that touches the database; plain `[Fact]` only for tests that never get past the
  startup guards.
- **Assert the wire format:** read bodies as `JsonElement` (`AuthApi.JsonAsync`), whose `GetProperty` is
  case-sensitive, so a test also pins the camelCase names and the `{ error, reason }` shape the frontend reads.
- Method naming as in the service tests: `<Endpoint>_When<Condition>_<ExpectedOutcome>`.

### Frontend tests (`VirtualBar.Web.Tests`)

**Unit and component tests (`unit/`)** — Vitest + React Testing Library in jsdom, run by `npm test` on every PR.
- One file per module under test, in the folder that mirrors `VirtualBar.Web/src`: `src/lib/authErrors.ts` → `unit/lib/authErrors.test.ts`, `src/pages/ConfirmEmailPage.tsx` → `unit/pages/ConfirmEmailPage.test.tsx`.
- Import the code under test through the app's `@/` alias (`vitest.config.ts` and `tsconfig.json` point it at `../VirtualBar.Web/src`), exactly as the app imports it.
- Mock only the **API layer** (`vi.mock('@/api/authApi')`), never React Query, the router or i18n: components render with the real providers through `renderWithProviders` in `unit/testUtils.tsx`:
  - the same providers as `App.tsx` (`AuthProvider`, `MemoryRouter` at `route`, `ChatProvider`, `TooltipProvider`) and real i18n in Bulgarian;
  - a fresh `QueryClient` that does not retry, and retries at once when a query asks to (so `ProductPage`'s "retry anything but a 404" rule is checked by counting calls);
  - `path` (e.g. `/product/:id`) for a page that reads route parameters, and `location()` for the path and query string the page wrote;
  - `signIn()` before rendering saves a session, so the page renders logged in. A logged-in NavBar polls `@/api/notificationsApi`, so mock that too.
  - `apiError(status, body)` / `networkError()` build real `AxiosError`s, so `isAxiosError` and the `reason` handling run as in production; `deferred()` holds a request pending to check loading states.
  - Test data comes from builders in `unit/fixtures/` (`productCard`, `productDetail`, `review`, …): a valid default, and the test overrides only what it is about.
- Assert translated text from the translation files (`bg.confirmEmail.invalidLink`, or `i18n.t(...)` for interpolated text), never a hard-coded string.
- Query by role and accessible name first (`getByRole('button', { name: … })`), then text.
- `unit/setup.ts` unmounts after every test and clears `localStorage`; mocks are reset between tests (`mockReset`, `restoreMocks`).
- `unit/i18n/translations.test.ts` pins that `bg.json` and `en.json` have the same keys, the same `{{placeholders}}` per key, and no empty value. A new i18n key therefore goes into **both** files in the same change.
- Code under test is never changed to make a test pass. A test exists to fail on the bug it names, so check that it does once: break the code, watch it go red, restore.

**E2E tests (`e2e/`)** — Playwright against the real stack (see CI → E2E): one spec per feature flow, role/text selectors first, no fixed waits.
- The newer specs (`marketplace`, `offers`, `messages`, `catalog-browse`, `product-page`) build on `e2e/support.ts`:
  - `registerCollector` registers fresh users through the API;
  - the `openAs(who)` fixture (use the `test` exported by `support.ts`) gives each collector a browser context of its own, `null` an anonymous visitor, and closes them all after the test, failed or not. A context left open keeps polling the API every 30 s for the rest of the worker's run;
  - `api` / `createBottle` set up data through the API;
  - `untouchedProduct` picks a catalog product no bottle is linked to yet, so a product page or card shows only what the test created;
  - `card`, `openBottlePanel`, `notification`, `chatToggle`, `chatMessage` and `searchMarketplace` locate the UI, and `switchToEnglish` uses the NavBar's language switcher.
- **Set up through the API, assert through the UI**: a spec drives only the flow it is about, and creates everything else directly.
- Expected text comes from the app's `bg.json` / `en.json` (exported by `support.ts` as `bg` and `en`), never copied into the spec. The older specs predate this and keep their own string constants.
- A multi-user flow uses `FLOW_TIMEOUT` and the `expect` exported by `support.ts`, which waits up to 15 s. Each step crosses browser contexts and page loads.
- Names come from `uniqueName`, so a run never collides with data from an earlier run, and a spec that leaves something other collectors would see removes it: a wish matches every later listing of its category.

---

## Security Rules

- All controllers require `[Authorize]`. Public endpoints (browse public bars, view bottle details) use `[AllowAnonymous]` explicitly.
- Every mutation on a user-owned resource checks `resource.UserId == currentUser.UserId`.
- `appsettings.Development.json` is gitignored. Connection strings and JWT keys never go in committed files.
- Only EF Core LINQ queries — no raw SQL. If raw SQL is ever added, parameterize it.
- `DeleteBehavior.Restrict` globally. Cascade only where explicitly intentional.

---

## Admin

Admin is determined by `AppUser.IsAdmin` (bool, default `false`). It is:
- Exposed via `ICurrentUser.IsAdmin` (reads the `"isAdmin"` JWT claim)
- Emitted in the JWT by `AuthService.GenerateJwtToken`
- Included in the auth response as `AuthUserDto.IsAdmin`
- **Seeded on startup:** if `AdminEmail` is set in config (typically `appsettings.Development.json`, gitignored), that user's `IsAdmin` is set to `true` automatically on `dotnet run`

Admin checks always live in the **`XxxValidationDecorator`** — never in controllers or inner services:
```csharp
if (!currentUser.IsAdmin)
    return Result<T>.Forbidden("Only administrators can do this.");
```

---

## Middleware

Located in `VirtualBar.Api/Middleware/`. Registered in `Program.cs` in this exact order:

```
UseCors
→ RequestResponseLoggingMiddleware   (outermost logger)
  → GlobalExceptionMiddleware        (catches unhandled exceptions)
    → UseStaticFiles
    → UseAuthentication
    → UseAuthorization
    → MapControllers
```

### RequestResponseLoggingMiddleware

Logs every HTTP request and response using Serilog (`ILogger<RequestResponseLoggingMiddleware>`):
- **`→`** on arrival — method, path, query string, remote IP
- **`←`** in `finally` — method, path, status code, elapsed ms, authenticated user ID

Log level varies by status code: `Information` (2xx/3xx), `Warning` (4xx), `Error` (5xx).

### GlobalExceptionMiddleware

Catches all unhandled exceptions that escape the controller/service layer:
- `OperationCanceledException` when client disconnected → `LogDebug`, no response written
- Any other `Exception` → `LogError` with full stack trace, returns `500` JSON:
  ```json
  { "status": 500, "title": "An unexpected error occurred.", "traceId": "..." }
  ```
- If response has already started streaming → does nothing (can't change headers)

**Order matters:** `GlobalExceptionMiddleware` is placed *inside* `RequestResponseLoggingMiddleware` so the logger reads the correct status code (500) after the exception middleware sets it.

### Serilog

Configured in `appsettings.json` under the `"Serilog"` key. Two sinks:
- **Console** — development output
- **File** — `logs/virtualbar-YYYYMMDD.log`, daily rotation, 7-day retention

Microsoft and EF Core namespaces are overridden to `Warning` to suppress noise.
`builder.Host.UseSerilog(...)` in `Program.cs` replaces the default .NET logging provider.

---

## Startup Behavior

On `dotnet run`:
1. Pending EF Core migrations are applied automatically (`MigrateAsync`).
2. If `AdminEmail` is configured, that user's `IsAdmin` flag is set to `true`.
3. `DistillerySeeder` tops up missing distilleries/category pairs (insert-only), then `ProductSeeder` tops up the product catalog from the embedded per-category `products.<category>.seed.json` files (insert-only by `CanonicalKey`).
4. Swagger/OpenAPI available at `/openapi/v1.json` (development only).
