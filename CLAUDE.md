# CLAUDE.md

This file provides guidance to Claude Code when working with code in this repository.

---

## What is VirtualBar

VirtualBar is a platform for collectors of premium spirits (whisky, rum, cognac, vodka and more). Every user gets a **virtual bar** — a public profile showcasing their collection. Others can browse collections, follow collectors, like and comment on bottles, leave **structured reviews** (0–100 score + tasting notes + flavor tags), send direct messages, and buy/sell limited editions through a built-in marketplace. The home page (`/`) is a social news feed showing admin-authored articles and activity from followed users. Users receive **in-app notifications** when someone likes/comments their bottle, follows them, sends a message, or when someone they follow adds a bottle or lists one for sale. Users can add bottles to a **wish list** and get notified when a matching bottle appears for sale. Any user can **make an offer** on any bottle in another collector's bar (regardless of whether it is listed for sale); the seller receives a notification and can accept, decline, or ignore. Direct messaging is handled via a **floating chat widget** (Facebook Messenger style) accessible from every page.

**User roles:** Collector (all registered users). Platform administrators have `IsAdmin = true` on their `AppUser` record — seeded via `AdminEmail` in `appsettings.Development.json` on startup.

---

## Solution Structure

```
VirtualBar/
├── VirtualBar.Domain          # Entities, enums — zero outward dependencies
├── VirtualBar.Application     # Interfaces, DTOs, Result<T>
├── VirtualBar.Infrastructure  # EF Core, service implementations
├── VirtualBar.Api             # ASP.NET Core controllers, Program.cs
├── VirtualBar.Tests/          # xUnit unit tests (one file per service)
└── VirtualBar.Web/            # React + Vite 7 + Tailwind CSS 4 frontend

**Target framework:** .NET 9 (`net9.0`)
```

**Dependency rule:** `Api → Application ← Infrastructure`. Domain has no outward dependencies.

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

```bash
cd VirtualBar.Web
npm install        # first time only
npm run dev        # dev server → http://localhost:5173
npm run build      # production build — must output "✓ built in ..."
```

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

---

## Code Style

- One blank line between every property in entity classes.

---

## CRITICAL RULES — Never Violate

- `CancellationToken` parameter is always named `cancellationToken`. Never `ct`, never `token`.
- Never delete or modify database records without asking the user first. EF Core migrations that ADD schema are fine. For any `DELETE`, `UPDATE`, `ExecuteDeleteAsync`, `ExecuteUpdateAsync`, or drop-column migration — stop and ask.
- After every backend change: run `dotnet build` (0 errors required) and the relevant test class.
- After every frontend change: run `npm run build` (clean output required).
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

All entities extend `BaseEntity` which adds `Id (Guid)`, `CreatedAt`, `UpdatedAt`, `IsDeleted`, `DeletedAt`.

`BottleLike`, `UserFollow`, `DistilleryCategory`, and `BottleReviewFlavor` are junction tables with composite PKs — no `BaseEntity`.

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
Junction relations: `BottleLike` (liked), `UserFollow` (followers/following).

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
| `Description` | `string?` | |
| `IsLimited` | `bool` | Limited edition flag |
| `IsForSale` | `bool` | Listed in marketplace |
| `AskingPrice` | `decimal?` | `decimal(18,2)` |
| `Currency` | `string?` | ISO code e.g. "USD", "EUR" |
| `ForSaleAt` | `DateTime?` | When the bottle was listed for sale (set by `ListForSaleAsync`, cleared by `UnlistFromSaleAsync`) |
| `Barcode` | `string?` | Scanned/known EAN-UPC; sharpens canonical price-estimate matching (Collection Value) |

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

**Design:** One review per `(BottleId, UserId)` — DB-enforced via a **filtered unique index** (`WHERE [IsDeleted] = 0`), same pattern as the `Offer` pending index: the decorator's already-reviewed pre-check is only a friendly fast path; `BottleReviewService.AddReviewAsync` maps the index violation (`DbUpdateException`) to `Conflict` when a concurrent create loses the race. The owner CAN review their own bottle (tasting journal use case) — no self-notification (decorator skips it). Update replaces flavors wholesale (clear junction rows + add new); the review row soft-deletes via `BaseEntity.IsDeleted`. Reviews attach to the bottle instance (like likes/comments), not to a canonical product. Additional index on `(BottleId, IsDeleted)` for the summary query.

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

**INotificationService methods:**
- `CreateAsync(recipientId, type, resourceId, resourceName, ct)` — single recipient; decorator skips if `recipientId == currentUser.UserId` (no self-notifications).
- `CreateBulkAsync(recipientIds, type, resourceId, resourceName, ct)` — fan-out; queries actor display name once, `AddRange` + single `SaveChangesAsync`. Decorator filters out self from the list.
- Both are fire-and-forget calls from trigger services (not controller-exposed). They return `Task` (not `Result<T>`).
- `GET /api/notifications` — returns last 30 + global unread count.
- `PATCH /api/notifications/{id}/read` and `POST /api/notifications/read-all`.

---

### Distillery
| Property | Type | Notes |
|---|---|---|
| `Name` | `string` | Unique index |
| `Country` | `string?` | |
| `Region` | `string?` | |

Has `ICollection<DistilleryCategory> Categories` and `ICollection<Bottle> Bottles`. Seeded on startup by `DistillerySeeder` (~710 entries across Whisky/Rum/Vodka/Cognac/Brandy/Tequila/Gin). **No `POST /api/distilleries`** — seeder is the single source of truth. Public read via `GET /api/distilleries?category=Rum` (AllowAnonymous).

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
| `ProductKey` | `string` | Canonical key from `ProductKey.For(...)`; **unique index** |
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

## Collection Value (bottle price estimation)

Shows an **indicative min–max market value per bottle** and a **total collection value** (Sealed bottles only) on the Dashboard. Always indicative — shown with range + confidence + **sources** + "as of" date; `None` renders "—", never a fabricated number.

**Provider architecture** (`VirtualBar.Infrastructure/Services/Pricing/`):
- `IPriceProvider` + `PriceProviderBase` (abstract) — shared plumbing: on/off gate (`UseProviderStats`), FX→base conversion, percentile aggregation, sanity bounds (`MaxSanePrice`), error-swallow → `null` + log, DTO build. Each provider overrides only `FetchAsync`.
- `ClaudeMarketResearchProvider` (`PriceSource.ClaudeResearch`) — Anthropic Messages API + `web_search` tool; returns an indicative min–max + **mandatory citations** (a result with no citations is treated as `None`). Typed `HttpClient`.
- `InternalMarketPriceProvider` (`PriceSource.Internal`) — our own listings + accepted offers matched by `ProductKey`; confidence by sample count.
- `PriceEstimationService` (orchestrator) — read-through `PriceSnapshot` cache (5-day TTL). On a miss/stale snapshot it tries **Claude first, then Internal** as fallback, and caches the chosen estimate. Collection value sums **Sealed** bottles only.
- `PreWarmWorker` + `PreWarmRefreshJob` (`IHostedService`) — periodically research the top-N most-owned bottles with a missing/stale snapshot, within budget.
- `AnthropicDailyCallBudget` (singleton) — process-wide cap on billed Claude calls, resets per UTC day; consumed by both the provider and the pre-warm job.

**Selection order (shipped):** Claude → Internal fallback → `None`.

**API (`/api/prices`, `[Authorize]`):**
- `GET /api/prices/bottle/{bottleId}` → `PriceEstimateDto` — **cache-only** read (never triggers a synchronous Claude call on the request path); `204` when no estimate is cached yet, `404` when the bottle is missing.
- `GET /api/prices/collection` → `CollectionValueDto` — Sealed-only total + per-bottle lines; owner-scoped (decorator enforces `userId == currentUser.UserId`).

**Config** (`appsettings.json`; `Anthropic:ApiKey` is empty there — set it in `appsettings.Development.json` / user-secrets, never committed):
- `Pricing` — `BaseCurrency`, `SnapshotTtlDays` (5), `FxToBase`, `RefreshIntervalHours`, `PreWarmTopNBottles`, `RefreshEnabled`.
- `Anthropic` — `UseProviderStats` (on/off), `Model` (`claude-sonnet-4-6`), `AnthropicVersion`, `MaxSearchesPerBottle`, `DailyCallBudget`, `AllowedDomains` / `BlockedDomains`.
- `InternalProvider` — `UseProviderStats`, `MinSamples`, `MinApproxSamples`.

Full slice-by-slice specs live in `docs/collection-value/`.

---

## Frontend Architecture

**Auth:** JWT stored in `localStorage` (`token` + `user`). `AuthContext` exposes `user`, `login`, `logout`. The Axios `client` in `src/api/client.ts` attaches the token as a Bearer header on every request and redirects to `/login` on 401. The `user` object includes `isAdmin: boolean` — set from the JWT claim emitted by `AuthService`.

**API layer (`src/api/`):** One file per domain (e.g., `bottlesApi.ts`, `usersApi.ts`, `notificationsApi.ts`). All use named import `{ client }` from `./client`. Return typed data directly (axios `.data` unwrapping).

**Data fetching:** TanStack Query (`useQuery` / `useMutation`). `queryClient.invalidateQueries` after mutations. Global `staleTime: 30_000`.

**Types:** All shared TypeScript types in `src/types/index.ts`. No per-file type definitions.

**Internationalisation:** `react-i18next` + `i18next-browser-languagedetector`. Bulgarian is the default language (`lng: 'bg'`). English is optional. Language choice is persisted in `localStorage` under key `vbar_lang`. Translation files: `src/i18n/bg.json` and `src/i18n/en.json`. i18next is initialised in `src/i18n/index.ts` and imported as the first line of `src/main.tsx`. Every page and component uses `const { t } = useTranslation()`. The `LanguageSwitcher` component (`src/components/LanguageSwitcher.tsx`) renders a speakeasy-styled dropdown (БГ/EN) placed inside the NavBar of every page.

Current translation namespaces: `nav`, `lang`, `login`, `register`, `dashboard`, `addBottle`, `browse`, `marketplace`, `publicBar`, `bottle`, `messages`, `profile`, `footer`, `hero`, `home`, `barShelf`, `notifications`, `wishList`, `distillerySelect`, `offers`, `collectionValue`, `reviews`, `flavors` (28 keys = `FlavorTag` enum member names).

**Routing:** `/` is `HomePage.tsx` (public news/social feed). Login and register redirect to `/` after success. `/dashboard` is the user's own virtual bar (protected). `/offers` is the Offers page (protected). There is **no `/messages` route** — messaging is handled entirely by the floating `ChatWidget`.

**Shared components:**
- `src/components/NavBar.tsx` — imported by every page; contains `<NotificationBell />` and `<LanguageSwitcher />` in the right slot (authenticated only). The "СЪОБЩЕНИЯ" nav item is a button that calls `toggleInbox()` from `ChatContext` — not a route link.
- `src/components/Avatar.tsx` — props: `displayName`, `avatarUrl?`, `size`. Renders `<img>` when avatarUrl present, initials div otherwise.
- `src/components/NotificationBell.tsx` — bell icon with gold badge (unread count), dropdown with last 30 notifications, mark-read / mark-all-read. Polls every 30 s via `refetchInterval`. `NewMessage` notifications call `openChat(actorId)` from `ChatContext` instead of navigating.
- `src/components/Footer.tsx` — mounted once in `App.tsx`; fully opaque `#07030A` background.
- `src/components/BottleDetailPanel.tsx` — full-screen overlay for bottle details. Shows `SaleSection` + `DeleteSection` for the owner. Shows `MakeOfferSection` for non-owners (any authenticated user, regardless of `IsForSale`). `EstimateSection` (price estimate) for authenticated users only — the `/api/prices` endpoint is `[Authorize]`, so rendering it anonymously would 401-redirect to `/login`. `LikesSection`, `ReviewsSection` (aggregate header + one-per-user review form + review list; form authenticated-only), and `CommentsSection` (below reviews) for all. `onDelete` prop (DashboardPage only) invalidates bottles query and closes panel. Bottle cards (`BarShelf`, Marketplace) show a gold `★ <avg>` badge when `reviewsCount > 0`.
- `src/components/ChatWidget.tsx` — floating Facebook Messenger-style chat. `position: fixed; bottom: 0; right: 20px; zIndex: 1000`. Renders only for authenticated users. Layout right-to-left: gold circular toggle button (52px, unread badge) → inbox panel (300×480px) → active conversation window (320×480px). Polls inbox every 30 s. Mark-as-read runs automatically when a thread is viewed.
- `src/components/DistillerySelect.tsx` — autocomplete dropdown for selecting a distillery. Props: `value`, `onChange`, `category?`. ARIA roles (`combobox`/`listbox`/`option`), keyboard navigation (ArrowUp/Down/Enter/Escape), scroll-into-view. Fetches from `GET /api/distilleries?category=...`, `queryKey: ['distilleries', category ?? 'all']`, staleTime 5 min.

**Contexts:**
- `src/contexts/AuthContext.tsx` — `user`, `login`, `logout`, `isAuthenticated`, `isLoading`.
- `src/contexts/ChatContext.tsx` — `inboxOpen`, `openInbox`, `closeInbox`, `toggleInbox`, `activeUserId`, `openChat(userId)`, `closeChat`. Used by NavBar, NotificationBell, MarketplacePage, ChatWidget.

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
- Use **EF Core InMemory** by default. Switch to **SQLite in-memory** only when the method calls `ExecuteUpdateAsync` / `ExecuteDeleteAsync`, or when the test must enforce a **unique index** (InMemory doesn't — e.g. the `BottleReview` duplicate-race test).
- Mock only `ICurrentUser` and `INotificationService` — never mock `AppDbContext`.
- Services that depend on `INotificationService` receive `Mock.Of<INotificationService>()` in their `CreateXxxService` helper (optional parameter with default).
- Seed helpers are `private static` methods in the test class: `SeedUser`, `SeedBottle`, `SeedComment`, etc.
- Cover every branch: every `if`, every `switch` arm, every `?.`, `??`, `&&`, `||`.

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
3. Swagger/OpenAPI available at `/openapi/v1.json` (development only).
