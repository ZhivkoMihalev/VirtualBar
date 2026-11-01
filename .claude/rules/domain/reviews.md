---
paths:
  - "**/*Review*"
  - "**/*review*"
  - "**/*Flavor*"
---

# Reviews

## BottleReview
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

### Design
- One review per `(BottleId, UserId)` — DB-enforced via a **filtered unique index** (`WHERE [IsDeleted] = 0`), same
  pattern as the `Offer` pending index.
  - The decorator's already-reviewed pre-check is only a friendly fast path.
  - `BottleReviewService.AddReviewAsync` maps the index violation (`DbUpdateException`) to `Conflict` when a concurrent
    create loses the race.
- The owner CAN review their own bottle (tasting journal use case) — no self-notification (decorator skips it).
- Update replaces flavors wholesale (clear junction rows + add new); the review row soft-deletes via
  `BaseEntity.IsDeleted`.
- Reviews attach to the bottle instance (like likes/comments), not to a canonical product. The product page aggregates
  the reviews of every linked bottle **on read only** (`ProductCatalogService`), never stored.
- Additional index on `(BottleId, IsDeleted)` for the summary query.

### FlavorTag enum (Domain, append-only, 28 members)
`Smoky, Peaty, Medicinal, Maritime, Vanilla, Caramel, Toffee, Honey, Chocolate, Coffee, Nutty, Malty, Creamy, Fruity, Citrus, TropicalFruit, DriedFruit, Berry, Floral, Herbal, Grassy, Spicy, Pepper, Cinnamon, Oak, Sherry, Leather, Tobacco`

Labels are translated on the frontend only (i18n `flavors` namespace, keys = enum names) — no seeded table, no admin
CRUD.

### API endpoints (`/api/bottles/{bottleId}/reviews`)
- `GET /api/bottles/{bottleId}/reviews` — `[AllowAnonymous]`; one round-trip
  `BottleReviewsSummaryDto { AverageScore, ReviewsCount, TopFlavors (top-3), Reviews[], MyReview? }`; `MyReview` is
  null for anonymous users
- `POST /api/bottles/{bottleId}/reviews` — create own review (duplicate → `409`)
- `PUT /api/bottles/{bottleId}/reviews/{reviewId}` — update own review
- `DELETE /api/bottles/{bottleId}/reviews/{reviewId}` — soft-delete own review

## BottleReviewFlavor
Composite PK: `(ReviewId, Flavor)`. No `BaseEntity`. Cascade delete from `BottleReview` (hard junction rows, same as
`DistilleryCategory`).
| Property | Type |
|---|---|
| `ReviewId` | `Guid` |
| `Flavor` | `FlavorTag` |

## Related rules
- `BottleReviewService.AddReviewAsync` sends `BottleReviewed` to the bottle owner (`.claude/rules/domain/notifications.md`).
- The product page aggregates the reviews of every linked bottle (`.claude/rules/domain/catalog.md`, `GET /api/products/{id}`).
- The home feed shows the reviews of followed collectors as `Reviewed` items (`.claude/rules/domain/news.md`).

Design record (the spec, written before the code): `docs/features/bottle-reviews/`.
