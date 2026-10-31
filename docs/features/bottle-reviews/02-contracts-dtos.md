# 02 — Contracts & DTOs

> Depends on: **01** · Read `00-OVERVIEW.md` first.

## Goal
The Application-layer surface: `IBottleReviewService` + the review DTOs. New folder
`VirtualBar.Application/DTOs/Reviews/` — one top-level type per file.

## Build new
1. **`BottleReviewDto`** — `Id`, `BottleId`, `UserId`, `UserDisplayName`, `UserAvatarUrl`,
   `Score`, `Nose`, `Palate`, `Finish`, `Summary`, `List<FlavorTag> Flavors`, `CreatedAt`, `UpdatedAt`
   (`UpdatedAt` lets the UI show "edited"). Mirror `CommentDto` style (sealed class, init-less setters).
2. **`BottleReviewsSummaryDto`** — the single GET payload (overview §3.8):
   - `double? AverageScore` — rounded to 1 decimal; `null` when no reviews
   - `int ReviewsCount`
   - `List<FlavorTag> TopFlavors` — top 3 by frequency across non-deleted reviews (ties → enum order)
   - `List<BottleReviewDto> Reviews` — newest first
   - `BottleReviewDto? MyReview` — the current user's review; `null` for anonymous/none
3. **`AddReviewRequest`** — `int Score`, `string? Nose`, `string? Palate`, `string? Finish`,
   `string? Summary`, `List<FlavorTag>? Flavors`.
4. **`UpdateReviewRequest`** — same shape as `AddReviewRequest` (full replace, including flavors).
5. **`IBottleReviewService`** — `VirtualBar.Application/Interfaces/IBottleReviewService.cs`:
   ```csharp
   Task<Result<BottleReviewsSummaryDto>> GetReviewsAsync(Guid bottleId, CancellationToken cancellationToken);
   Task<Result<BottleReviewDto>> AddReviewAsync(Guid bottleId, AddReviewRequest request, CancellationToken cancellationToken);
   Task<Result<BottleReviewDto>> UpdateReviewAsync(Guid reviewId, UpdateReviewRequest request, CancellationToken cancellationToken);
   Task<Result<bool>> DeleteReviewAsync(Guid reviewId, CancellationToken cancellationToken);
   ```
   (Mirror `IBottleCommentService` naming/shape; `FlavorTag` serializes as strings via the global
   `JsonStringEnumConverter` — nothing to configure.)

## Test targets (written in slice 07)
DTOs carry no logic → none.

## Gate
`dotnet build` → **0 errors**.
