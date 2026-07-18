# Bottle Reviews (Оценки + Tasting Notes) — OVERVIEW & SHARED CONTEXT

> **Read this first, before any slice.** Single source of truth for the decisions, the architecture,
> the conventions, and the risks. Each `NN-*.md` slice assumes you read this. Format mirrors
> `docs/collection-value/`.

> **Approach — structured ratings on the existing social surface.** Deep research (verified against
> Whiskybase, Distiller, Drammer primary docs) showed a structured **score + tasting-note system** is the
> single most consistent feature VirtualBar lacks versus every major competitor. It is also the foundation
> for the future flavor-discovery / recommendation phase. We add a first-class `BottleReview` object
> (score 0–100 + nose/palate/finish/summary + flavor tags) next to the existing likes/comments — reusing
> the exact `BottleComment` service/decorator/controller pattern, the `Offer` filtered-unique-index
> concurrency pattern, and the existing notification fan-in.

---

## 1. Goal
Every collector can leave **one review per bottle**: a mandatory **score (0–100)**, optional structured
tasting notes (**nose / palate / finish / summary**), and up to **5 flavor tags**. Every bottle shows a
**community aggregate** — average score, review count, top-3 flavor tags. Aggregates appear on the bottle
panel and as a small badge on bottle cards. The bottle owner gets a **notification** when reviewed.

## 2. Why this shape (research — short version)
- **Score 0–100, not stars.** Whiskybase deliberately chose the 100-point scale for finer granularity;
  it is the lingua franca of whisky reviewing. One integer field, no half-star UI complexity.
- **Quick for beginners, deep for enthusiasts** (Drammer insight): only the score is mandatory —
  notes and flavors are optional, so a rating takes 5 seconds and a full tasting note is possible.
- **Structured nose/palate/finish** (Whiskybase) rather than one blob: enables per-section display and
  future flavor analytics.
- **Flavor tags** (Distiller insight): the top-3 most-used tags become the bottle's crowdsourced flavor
  profile — the seed for the parked flavor-search/recommendation phase.
- **Reviews are a first-class object** — not comments. Comments stay as free discussion; a review is
  editable, deletable, one-per-user, and aggregated.

## 3. Locked decisions
1. **Score `int` 0–100, required.** Notes (`Nose`, `Palate`, `Finish`, `Summary`) each optional,
   trimmed, **≤ 2000 chars**. Flavors optional, **≤ 5**, distinct, defined enum values only.
2. **One review per `(BottleId, UserId)` — DB-enforced** via a **filtered unique index**
   (`WHERE [IsDeleted] = 0`), exactly like the `Offer` pending-index pattern: the decorator's
   already-reviewed pre-check is only a friendly fast path; `BottleReviewService.AddReviewAsync` maps the
   index violation (`DbUpdateException`) to `Conflict` when a concurrent create loses the race.
3. **Reviews attach to the bottle instance** (like likes/comments), not to a canonical product.
   ProductKey-level aggregation across identical bottles is a **parked** future option (discovery phase).
4. **The owner CAN review their own bottle** — a collector's tasting journal for their own bar is the
   primary use case. No self-notification (the existing `NotificationValidationDecorator` already skips
   `recipientId == currentUser.UserId`).
5. **Flavor tags are a Domain enum (`FlavorTag`, 28 members)** + junction entity `BottleReviewFlavor`
   with composite PK `(ReviewId, Flavor)` — mirrors the existing `DistilleryCategory` pattern. No seeded
   table, no admin CRUD; labels are translated on the **frontend** (i18n `flavors` namespace, keys = enum
   names). Enum is **append-only**.
6. **Aggregates are computed in queries, never denormalized.** `AverageScore` (1 decimal) + `ReviewsCount`
   are projected the same way `LikesCount`/`CommentsCount` already are in `BottleService`. A stored
   counter/average is a **parked** optimization.
7. **CRUD shape mirrors comments.** `POST` creates (duplicate → `409`), `PUT` updates own review,
   `DELETE` soft-deletes own review, `GET` is `[AllowAnonymous]` (public bars are public).
8. **One `GET` returns everything:** `BottleReviewsSummaryDto { AverageScore, ReviewsCount, TopFlavors,
   Reviews[], MyReview? }` — one round-trip; `MyReview` is null for anonymous users
   (`ICurrentUser.UserId == Guid.Empty` matches nothing — verified in `CurrentUserService`).
9. **New `NotificationType.BottleReviewed`**, appended at the END of the enum (stored values stay stable).
   Fired to the bottle owner from `AddReviewAsync` only (not on update/delete) — fire-and-forget like
   `BottleCommented`.
10. **Update replaces flavors wholesale** (clear junction rows + add new) — junction rows are hard rows
    by design, same as `BottleLike`/`DistilleryCategory`. The review row itself soft-deletes via
    `BaseEntity.IsDeleted`.

## 3a. Architecture at a glance
```
BottleDetailPanel (ReviewsSection)                bottle cards (★ badge)
        │  GET/POST/PUT/DELETE                            ▲ AverageScore/ReviewsCount on BottleDto
        ▼                                                 │ (projected in BottleService queries)
BottleReviewsController ──▶ IBottleReviewService
                                   │
                    BottleReviewValidationDecorator   (guards: score range, lengths, ≤5 distinct flavors,
                                   │                   bottle exists, ownership, friendly duplicate check)
                                   ▼
                          BottleReviewService         (create/update/delete + summary aggregation;
                                   │                   DbUpdateException → Conflict on the unique index;
                                   │                   fires NotificationType.BottleReviewed to owner)
                                   ▼
              BottleReviews + BottleReviewFlavors     (EF; filtered unique index (BottleId, UserId)
                                                       WHERE IsDeleted = 0; flavors cascade from review)
```

## 4. What already exists (reuse, don't rebuild)
- **`BottleComment` slice** — entity/service/decorator/controller/tests: the 1:1 structural template.
- **`Offer` filtered unique index + `Conflict` mapping** (`AppDbContext` §Offer, `OfferService.CreateAsync`)
  — the concurrency pattern for decision §3.2.
- **`DistilleryCategory`** — composite-PK junction with an enum member: the template for `BottleReviewFlavor`.
- **`INotificationService`** — `CreateAsync` + self-skip decorator; just add the enum member.
- **`JsonStringEnumConverter`** is already global (`Program.cs`) — `FlavorTag` serializes as strings.
- **Frontend:** `BottleDetailPanel` section components (`CommentsSection` as template), TanStack Query
  conventions, i18n namespaces, module-level `CSSProperties` style constants.

## 5. Backend conventions (from CLAUDE.md — follow exactly)
- `Result<T>` everywhere; typed factories (`NotFound`/`Forbidden`/`Conflict`/`Fail`); never throw for
  expected failures.
- **Decorator pattern**: ALL guards in `BottleReviewValidationDecorator` (+
  `cancellationToken.ThrowIfCancellationRequested()` first line of every method); inner service is pure
  logic, no precondition `if`s. Register both in `DependencyInjection.cs`.
- Primary-constructor DI; `CancellationToken cancellationToken` on every async + EF call.
- Controllers `[Authorize]` by default, `[AllowAnonymous]` explicit; full XML docs;
  `result.Success ? Ok(...) : result.ToActionResult(this)`.
- **Migrations are ADD-only.** One blank line between entity properties.
- Every mutation checks `review.UserId == currentUser.UserId` (in the decorator).

## 6. Risks
- **Duplicate-create race** — closed by the filtered unique index + `Conflict` mapping (decision §3.2);
  the decorator pre-check alone is NOT sufficient.
- **EF InMemory does not enforce unique indexes** — the race test MUST use **SQLite in-memory**
  (mirror `OfferServiceTests`); everything else stays on InMemory.
- **Review spam / score bombing** — mitigated by one-review-per-user + edit/delete own only. Moderation
  tools (owner hiding reviews, admin removal) are **parked**; revisit before public launch.
- **Enum drift** — `FlavorTag` and `NotificationType` are append-only; renaming/removing members breaks
  stored data and i18n keys.
- **Projection weight** — summary GET includes `User` + `Flavors` per review; fine at expected volumes
  (page-sized lists). Pagination is **parked** until a bottle exceeds ~100 reviews.

## 7. Open questions (decide during build)
- **Feed integration** — should "X rated Y 92/100" appear in the home feed (`FeedItemDto` already carries
  bottle fields)? Cheap and on-strategy, but scope creep → default **no** for this slice set.
- **Card badge scope** — ★ badge on all card surfaces (BarShelf, Browse, Marketplace, PublicBar) or
  BarShelf-only for MVP? Default: **all** (the DTO carries the fields anyway).
- **Score UI** — number input vs slider for 0–100 (default: number input with quick-pick chips 70/80/85/90/95).
- **Minimum review gate for the aggregate** — show average from 1 review (default) or only from ≥ 3?

## 8. Slice index, dependencies & order
| Slice | Doc | Depends on |
|---|---|---|
| 1 | `01-domain-migration.md` (entities, enums, index, migration) | — |
| 2 | `02-contracts-dtos.md` (interface + DTOs) | 1 |
| 3 | `03-service-decorator.md` **(the core)** | 2 |
| 4 | `04-api.md` (controller) | 3 |
| 5 | `05-bottle-aggregates.md` (`BottleDto.AverageScore`/`ReviewsCount`) | 1 |
| 6 | `06-frontend.md` (ReviewsSection, badges, i18n bg+en) | 4, 5 |
| 7 | `07-backend-tests.md` **(ALL backend unit tests — written last)** | 1–5 |

**Execution order (tests last):** implement backend slices **1→2→3→4→5 with build-only gates (no tests
yet)**, then write **all** backend unit tests in **slice 7**. The frontend slice **6** runs after slices 4–5
(can run before or after 7).

## 9. Verification protocol (REQUIRED)
- **During each backend slice (1–5):** `dotnet build VirtualBar.Api/VirtualBar.Api.csproj --no-restore -v q`
  → **0 errors**. Each slice lists **Test targets** — record them, but **do NOT write tests yet**.
- **Slice 7 (after the whole backend is implemented):** write `BottleReviewServiceTests` + the
  `BottleServiceTests` additions — **100% branch** for every new service method — then
  `dotnet test VirtualBar.Tests/VirtualBar.Tests.csproj` → `Failed: 0`.
- **Frontend slice (6):** `npm --prefix VirtualBar.Web run build` clean + exercise in **bg + en**.
- Do **not** commit unless the user asks.

## 10. Docs & CLAUDE.md (final step, after slice 7)
Update `CLAUDE.md`: add `BottleReviews` + `BottleReviewFlavors` to the DbSet table, a `BottleReview`
entity-details section, the `BottleReviewed` row in the NotificationType table, `reviews` + `flavors` in
the i18n namespace list, and the new API routes.
