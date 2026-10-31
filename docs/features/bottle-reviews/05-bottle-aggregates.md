# 05 — Bottle Aggregates (`AverageScore` / `ReviewsCount` on `BottleDto`)

> Depends on: **01** (entity) · Read `00-OVERVIEW.md` first.

## Goal
Every surface that renders a bottle card gets the community score without extra requests: extend
`BottleDto` + the existing `BottleService` projections. Computed, never denormalized (overview §3.6).

## Build / extend
1. **`BottleDto`** — add (one blank line between properties):
   - `double? AverageScore` — rounded 1 decimal, `null` when no reviews
   - `int ReviewsCount`
2. **`BottleService`** — extend the SAME places that already compute `Likes.Count` / `Comments.Count`:
   - the `Include`-based list/detail queries (my bottles, by-id, public bar, browse, marketplace):
     include/filter non-deleted reviews and pass
     `reviews.Any() ? Math.Round(reviews.Average(r => (double)r.Score), 1) : (double?)null` +
     `reviews.Count` into `MapToDto` — extend `MapToDto`'s parameter list exactly like
     `likesCount`/`commentsCount` are passed today. **Match the existing filtered-include style used for
     `Comments` (`!IsDeleted`)** so soft-deleted reviews never count.
   - mutation paths that return a `BottleDto` (add/update/list-for-sale/…): count/average via direct
     `db.BottleReviews` aggregate queries (mirror the `CountAsync` pattern used after `AddCommentAsync`).
     A brand-new bottle → `AverageScore = null`, `ReviewsCount = 0`.
3. **No new endpoints, no service interface changes** — pure projection widening.

## Test targets (written in slice 07)
`BottleServiceTests` additions: bottle with reviews → correct rounded average + count; without reviews →
`null` / `0`; soft-deleted review excluded from both.

## Gate
`dotnet build` → **0 errors**. No tests yet.
