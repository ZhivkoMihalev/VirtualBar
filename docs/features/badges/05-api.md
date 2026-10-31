# 05 — API

> Depends on: **03** · Read `00-OVERVIEW.md` first.
>
> **Since this slice:** the catalog is **19 badges** — see `00-OVERVIEW.md` §3 "Added after this slice".

## Goal
`BadgesController` — the public earned-badges read and the private progress read.

## Build new — `VirtualBar.Api/Controllers/BadgesController.cs`
`[ApiController]`, `[Route("api/badges")]`, `[Authorize]`, primary constructor
`(IBadgeService badgeService)`.

| Verb | Route | Action | Codes |
|---|---|---|---|
| GET | `user/{userId:guid}` | earned badges of any user, newest first — **`[AllowAnonymous]`** (public bars are public) | 200, 404 |
| GET | `progress` | the **full** catalog (one entry per `BadgeType`) with `current`/`threshold`/`earned`/`awardedAt` for the **current** user (overview §3.9 — own-only, never for others) | 200 |

- Full XML docs on both actions (`<summary>`, `<param name="cancellationToken">Cancellation token.</param>`,
  every `<response code="...">`).
- Bodies: `result.Success ? Ok(result.Data) : result.ToActionResult(this)`.
- `EvaluateAsync` is **not** exposed — trigger-services-only (mirror `CreateAsync`).

## Test targets (written in slice 07)
Controller mapping is thin — covered by the slice-03 service/decorator tests; nothing controller-specific.

## Gate
`dotnet build` → **0 errors**; both routes visible in `/openapi/v1.json` (development).
