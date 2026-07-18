# Badges / Achievements (Геймификация — значки) — OVERVIEW & SHARED CONTEXT

> **Read this first, before any slice.** Single source of truth for the decisions, the architecture,
> the conventions, and the risks. Each `NN-*.md` slice assumes you read this. Format mirrors
> `docs/collection-value/`, `docs/bottle-reviews/`, `docs/barcode-scanning/`.

> **Approach — a pure-code award engine on the existing event stream.** Deep research (Drammer's 50+
> earnable badges — the "Untappd for whisky" retention mechanic) ranked gamification as the cheapest
> high-fit feature: VirtualBar already fires every interesting event (bottle added, like received,
> follower gained, listing, accepted offer) as an inline notification call — badges hook the **same six
> call sites**. The catalog lives in **code** (enum + static definitions, frontend-translated like
> `FlavorTag` in bottle-reviews), only **awards** live in the DB (one composite-PK junction table).
> Zero config, zero external calls, zero cost — the simplest of the planned features.

---

## 1. Goal
Collectors earn **permanent badges** for milestones (first bottle, 10 bottles, 5 categories, 50 likes,
first sale…). Earned badges show on the **own profile** (with progress toward the next ones,
"7/10 бутилки") and on the **public bar**. Earning one triggers a **`BadgeEarned` notification**. The
initial catalog is **18 badges across 8 count-kinds**; growing it later = one enum member + one
definition line + two i18n strings.

## 2. Why this shape (research + codebase — short version)
- **Retention loop** (Drammer): visible progress + "one more to unlock" nudges repeat engagement; fits
  VirtualBar's existing social surfaces (profile, public bar, bell) with no new page.
- **Catalog in code, not DB.** Badges change only with releases; a seeded table + translations would be
  dead weight. Mirrors the locked `FlavorTag` decision: enum + frontend i18n, **append-only**.
- **Evaluate on trigger, `count >= threshold`.** No background job: any relevant action re-evaluates the
  user's counts and awards **everything missed** — a user with 50 pre-existing bottles gets all six
  collection badges on their next add (lazy catch-up for free).
- **The one real constraint found in code:** `NotificationValidationDecorator.CreateAsync` **silently
  drops self-notifications** (`recipientId == currentUser.UserId`), and most badge events are
  self-triggered (your own 10th bottle). The fix is a deliberate, small extension:
  **`CreateSystemAsync`** on `INotificationService` — no self-skip, actor = the recipient themselves.

## 3. Locked decisions
1. **`BadgeType` enum (18 members) + static `BadgeCatalog`** (`BadgeDefinition(Type, Trigger, CountKind,
   Threshold)` records). No DB catalog, no admin CRUD. Frontend translates names/descriptions
   (`badges` i18n namespace, keys = enum names). Enum is **append-only**.
2. **`UserBadge` junction** — composite PK `(UserId, BadgeType)`, `AwardedAt`, **no `BaseEntity`**
   (mirror `BottleLike`/`UserFollow`). Awards are **permanent** — never revoked when counts later drop
   (unlike/delete): standard gamification semantics, no revocation logic.
3. **Five `BadgeTrigger`s, six hook sites** (the exact lines where notifications already fire):
   `BottleAdded` (`AddBottleAsync`), `LikeReceived` (`LikeAsync` → bottle owner), `FollowerGained`
   (`FollowAsync` → followed user), `BottleListed` (`ListForSaleAsync`), `OfferAccepted`
   (`AcceptAsync` → **two** calls: seller and buyer).
4. **Eight `BadgeCountKind`s**, one COUNT query each, computed at most once per evaluation:
   `Bottles`, `Categories` (distinct), `LimitedBottles`, `LikesReceived` (on own non-deleted bottles),
   `Followers`, `ActiveListings`, `SalesAccepted`, `PurchasesAccepted`. All rules are uniform
   `count >= threshold` — no special cases in the engine.
5. **One `IBadgeService`** (mirror `INotificationService`'s dual nature): trigger-facing
   `Task EvaluateAsync(Guid userId, BadgeTrigger trigger, CancellationToken)` (returns `Task`, not
   `Result<T>`, not controller-exposed) + query methods returning `Result<T>` for the API.
6. **`EvaluateAsync` never breaks the host operation.** Its body is wrapped in `try/catch` → `LogError`
   + return (the provider error-swallow philosophy): a badge bug must not fail adding a bottle.
7. **Idempotency & races via the composite PK.** Award badges **one at a time**: `Add` →
   `SaveChangesAsync` → on success fire the notification; on `DbUpdateException` (concurrent trigger
   already awarded it) → detach, skip its notification, continue. At-most-once award AND at-most-once
   notification. EF InMemory enforces PK uniqueness → **no SQLite needed** in tests.
8. **`INotificationService.CreateSystemAsync(recipientId, type, resourceId, resourceName, ct)`** — new
   method: **no self-skip** in the decorator; inner sets `ActorId = recipientId` and looks up the
   **recipient's own** display name (recipient missing → return). Used only by the badge engine for
   `NotificationType.BadgeEarned` (appended at the enum END), with `ResourceId = null`,
   `ResourceName = badgeType.ToString()` — the frontend translates the badge name from it.
9. **API:** `GET /api/badges/user/{userId}` (**`[AllowAnonymous]`** — public bars are public; earned
   badges only, newest first) and `GET /api/badges/progress` (`[Authorize]` — the full catalog with
   `current`/`threshold`/`earned`/`awardedAt` for the current user). Progress is **own-only** — never
   exposed for other users.
10. **No backfill job.** Lazy catch-up (§2) covers active users; a one-time backfill for dormant
    accounts is **parked** (would be a startup seeder-style pass — decide post-launch).

### The catalog (initial 18)
| BadgeType | Trigger | CountKind | Threshold |
|---|---|---|---|
| `FirstBottle` / `Collector5` / `Collector10` / `Collector25` / `Collector50` / `Collector100` | `BottleAdded` | `Bottles` | 1 / 5 / 10 / 25 / 50 / 100 |
| `Explorer3` / `Explorer5` | `BottleAdded` | `Categories` | 3 / 5 |
| `LimitedHunter` | `BottleAdded` | `LimitedBottles` | 5 |
| `Liked10` / `Liked50` / `Liked100` | `LikeReceived` | `LikesReceived` | 10 / 50 / 100 |
| `FirstFollower` / `Popular10` / `Influencer50` | `FollowerGained` | `Followers` | 1 / 10 / 50 |
| `FirstListing` | `BottleListed` | `ActiveListings` | 1 |
| `FirstSale` | `OfferAccepted` | `SalesAccepted` | 1 |
| `FirstPurchase` | `OfferAccepted` | `PurchasesAccepted` | 1 |

## 3a. Architecture at a glance
```
AddBottleAsync ─┐                                   ┌─▶ UserBadges (composite PK (UserId, BadgeType))
LikeAsync ──────┤  EvaluateAsync(userId, trigger)   │      one row per award, permanent
FollowAsync ────┼─▶ BadgeValidationDecorator ─▶ BadgeService
ListForSaleAsync┤   (guards, cancellation)      │   │
AcceptAsync ×2 ─┘                               │   └─▶ INotificationService.CreateSystemAsync
   (fire-and-forget, error-swallowed §3.6)      │         (BadgeEarned — NO self-skip)
                                                │
                catalog defs for trigger → group by CountKind → one COUNT each
                → award every def with count ≥ threshold not yet earned (§3.7)

BadgesController: GET /api/badges/user/{id} (public, earned)   GET /api/badges/progress (own, full)
ProfilePage (earned + progress bars)   PublicBarPage (earned strip)   NotificationBell (BadgeEarned)
```

## 4. What already exists (reuse, don't rebuild)
- **The six notification call sites** (grep `notificationService.` in `VirtualBar.Infrastructure/Services`)
  — badge hooks go on the adjacent lines; same DI style (constructor-injected interface).
- **`INotificationService` + `NotificationService` + decorator** — the template for a `Task`-returning,
  non-controller service with guards in a decorator; `CreateSystemAsync` extends this trio.
- **`BottleLike`/`UserFollow`** — composite-PK junction entity + `AppDbContext` config template for
  `UserBadge`.
- **`JsonStringEnumConverter`** global — `BadgeType` serializes as strings.
- **Frontend:** `ProfilePage`/`PublicBarPage`, `NotificationBell` type mapping, i18n namespaces,
  module-level `CSSProperties`, lucide icons (already used on the Dashboard).

## 5. Backend conventions (from CLAUDE.md — follow exactly)
- `Result<T>` + typed factories for the query methods; `Task` for `EvaluateAsync`/`CreateSystemAsync`
  (mirror `CreateAsync`).
- **Decorator pattern**: guards + `ThrowIfCancellationRequested()` in `BadgeValidationDecorator`; inner
  service pure; both in `DependencyInjection.cs`.
- Primary-constructor DI; `CancellationToken cancellationToken` everywhere; controllers `[Authorize]`
  default + explicit `[AllowAnonymous]`; full XML docs; `ToActionResult`.
- **Migrations ADD-only**; one blank line between properties; one top-level type per file.
- Mock only `ICurrentUser`, `INotificationService` — **extended for this feature**: trigger-service
  tests also mock `IBadgeService` (`Mock.Of<IBadgeService>()` optional ctor parameter, same convention
  as `INotificationService`).

## 6. Risks
- **Self-notification suppression** — the found constraint (§2): using plain `CreateAsync` would silently
  drop most badge notifications. `CreateSystemAsync` (§3.8) is the fix; do NOT "fix" it by removing the
  existing self-skip (it protects every other type).
- **Concurrent double-award** — closed by the composite PK + per-badge save/catch (§3.7); the existence
  pre-check alone is NOT sufficient.
- **Constructor ripple** — six services gain an `IBadgeService` parameter → their existing test classes
  need the new optional mock parameter (slice 04 lists them; slice 07 updates them). Build breaks fast
  and loud, so the ripple is mechanical.
- **Count-query cost on hot paths** — each trigger adds 1–3 indexed COUNTs. Negligible at current scale;
  if it ever shows, cache earned-badge sets per request. **Parked.**
- **Enum drift** — `BadgeType` renames break stored rows and i18n keys; append-only, never rename.
- **`ActiveListings` semantics** — `ForSaleAt` is cleared on unlist, so "ever listed" is not derivable;
  `FirstListing` uses **currently listed** count at trigger time (fires on the listing action itself —
  correct in practice; a user who unlisted everything pre-feature misses it — acceptable).

## 7. Open questions (decide during build)
- **Review badges** (`FirstReview`, `Reviewer10`) — natural additions **after** `docs/bottle-reviews/`
  ships; the engine needs zero changes (new trigger + count-kind + defs). Parked as the first catalog
  extension.
- **Backfill for dormant users** (§3.10) — ship without, measure, decide.
- **Badge showcase on the Dashboard** ("2 до следващата значка" nudge card) — default no for MVP;
  ProfilePage progress covers it.
- **Icon set** — one lucide icon per badge (static map) vs a single medal glyph with tier colors.
  Default: static lucide map (visual variety, zero assets).

## 8. Slice index, dependencies & order
| Slice | Doc | Depends on |
|---|---|---|
| 1 | `01-domain-migration.md` (enums, catalog, `UserBadge`, migration) | — |
| 2 | `02-contracts-dtos.md` (`IBadgeService`, DTOs, `CreateSystemAsync` contract) | 1 |
| 3 | `03-award-engine.md` **(the core — engine + system notification)** | 2 |
| 4 | `04-triggers.md` (six hook sites, ctor ripple) | 3 |
| 5 | `05-api.md` (`BadgesController`) | 3 |
| 6 | `06-frontend.md` (profile/public strips, progress, bell, i18n bg+en) | 4, 5 |
| 7 | `07-backend-tests.md` **(ALL backend unit tests — written last)** | 1–5 |

**Execution order (tests last):** backend slices **1→2→3→4→5 with build-only gates (no tests yet)**,
then **all** backend unit tests in **slice 7**. The frontend slice **6** runs after slices 4–5 (may swap
freely with 7).

## 9. Verification protocol (REQUIRED)
- **During each backend slice (1–5):** `dotnet build VirtualBar.Api/VirtualBar.Api.csproj --no-restore -v q`
  → **0 errors**. Each slice lists **Test targets** — record them, but **do NOT write tests yet**.
- **Slice 7:** write `BadgeServiceTests`, extend `NotificationServiceTests` + the six touched test
  classes — **100% branch** for every new/changed service method — then
  `dotnet test VirtualBar.Tests/VirtualBar.Tests.csproj` → `Failed: 0`.
- **Frontend slice (6):** `npm --prefix VirtualBar.Web run build` clean + exercise in **bg + en**.
- Do **not** commit unless the user asks.

## 10. Docs & CLAUDE.md (final step, after slice 7)
Update `CLAUDE.md`: `UserBadges` in the DbSet table, a `UserBadge`/`BadgeType` entity-details section,
the `BadgeEarned` row in the NotificationType table + the `CreateSystemAsync` method note under
`INotificationService`, the `/api/badges` endpoints, `badges` in the i18n namespace list, and the
extended mock convention (`IBadgeService`) under Testing Conventions.
