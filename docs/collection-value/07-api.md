# 07 — API

> Phase **A** · Depends on: **05** · Read `00-OVERVIEW.md` first.

## Goal
Expose the per-bottle indicative estimate (with **sources**) and the collection-value total to the frontend.

## Recover from stash
- `VirtualBar.Api/Controllers/PricesController.cs`

## Build new / extend
1. `GET /api/prices/bottle/{bottleId}` → `PriceEstimateDto` — min–max (`LowEstimate`/`HighEstimate`) + confidence
   + **sources** + `Source` + `AsOf`; `None` → `204` (UI renders "—"). Reads through the cache (does **not**
   trigger a synchronous Claude call on the request path — returns cached/None; pre-warm or an async refresh
   populates it).
2. `GET /api/prices/collection` → `CollectionValueDto` — Sealed-only total + per-bottle lines (`BottlePriceLineDto`).
3. `[Authorize]`; verify ownership where user-scoped; full XML docs (`<summary>`, `<param name="cancellationToken">`,
   `<response>`); `result.Success ? Ok(...) : result.ToActionResult(this)`.

## Test targets (written in slice 10)
Controller mapping is thin (covered by service tests in slice 05); add a smoke test per action if logic warrants.
Mock `ICurrentUser`.

## Gate
`dotnet build` → 0 errors; endpoints visible in `/openapi/v1.json` (development).
