# 04 — API

> Depends on: **03** · Read `00-OVERVIEW.md` first.

## Goal
`BottleReviewsController` — same routing/doc/`ToActionResult` shape as `BottleCommentsController`.

## Build new — `VirtualBar.Api/Controllers/BottleReviewsController.cs`
`[ApiController]`, `[Route("api/bottles")]`, `[Authorize]`, primary constructor
`(IBottleReviewService bottleReviewService)`.

| Verb | Route | Action | Codes |
|---|---|---|---|
| GET | `{bottleId:guid}/reviews` | summary (aggregate + list + `myReview`) — **`[AllowAnonymous]`** | 200, 404 |
| POST | `{bottleId:guid}/reviews` | create the current collector's review | 201, 400, 404, **409** |
| PUT | `{bottleId:guid}/reviews/{reviewId:guid}` | update own review | 200, 400, 403, 404 |
| DELETE | `{bottleId:guid}/reviews/{reviewId:guid}` | soft-delete own review | 200, 403, 404 |

- `POST` returns `CreatedAtAction(nameof(GetReviews), new { bottleId }, result.Data)`; `409` when already
  reviewed (decorator pre-check or index race).
- Full XML docs on every action: `<summary>`, every `<param>` incl.
  `<param name="cancellationToken">Cancellation token.</param>`, every `<response code="...">`.
- Body: `result.Success ? Ok(result.Data) : result.ToActionResult(this)` (Created for POST).

## Test targets (written in slice 07)
Controller mapping is thin — covered by the service/decorator tests in slice 03; no controller tests
unless logic creeps in.

## Gate
`dotnet build` → **0 errors**; the four routes visible in `/openapi/v1.json` (development).
