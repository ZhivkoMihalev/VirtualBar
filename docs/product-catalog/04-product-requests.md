# Slice 04 — Product requests (the core): service, admin resolve, notifications

> Prereq: `00-OVERVIEW.md`, slice 01. Build gate at the end; **no tests yet**.

## Scope
`IProductRequestService` + inner service + validation decorator + `ProductRequestsController` +
two appended `NotificationType` members. Everything user-facing goes through `Result<T>`.

## Files — create

### DTOs (`VirtualBar.Application/DTOs/ProductRequests/`)
- **`CreateProductRequestRequest`** — `Name` (`[Required]`), `Brand?`, `DistilleryId?`, `Category`
  (`SpiritCategory`), `Age?`, `AbvPercent?`, `VolumeMl?`, `Barcode?`, `Country?`, `Region?`,
  `UserNote?`, `SourceBottleId?` (set only by `BottleService` — controller clients leave it null;
  ownership enforced in the decorator when present).
- **`ResolveProductRequestRequest`** (approve body) — `ExistingProductId?` + optional overrides for
  every product field (`Name?`, `Brand?`, `DistilleryId?`, `Category?`, `Age?`, `AbvPercent?`,
  `VolumeMl?`, `Barcode?`, `Country?`, `Region?`, `ImageUrl?`, `Description?`) + `AdminNote?` +
  `UseSourceBottleImage` (bool, default `false` server-side; the UI defaults it to checked when the
  request has a source bottle). Null override → take the value from the request row.
- **`RejectProductRequestRequest`** — `AdminNote?`.
- **`ProductRequestDto`** — all proposed fields + `Id`, `Status`, `UserNote`, `AdminNote`,
  `RequesterId`, `RequesterDisplayName`, `DistilleryName?`, `ResolvedProductId?`, `SourceBottleId?`,
  `CreatedAt`, `RespondedAt?`.

### `VirtualBar.Application/Interfaces/IProductRequestService.cs`
```csharp
Task<Result<ProductRequestDto>> CreateAsync(CreateProductRequestRequest request, CancellationToken cancellationToken);
Task<Result<List<ProductRequestDto>>> GetMineAsync(CancellationToken cancellationToken);
Task<Result<bool>> WithdrawAsync(Guid requestId, CancellationToken cancellationToken);
Task<Result<List<ProductRequestDto>>> GetAllAsync(ProductRequestStatus? status, CancellationToken cancellationToken);
Task<Result<ProductRequestDto>> ApproveAsync(Guid requestId, ResolveProductRequestRequest request, CancellationToken cancellationToken);
Task<Result<ProductRequestDto>> RejectAsync(Guid requestId, RejectProductRequestRequest request, CancellationToken cancellationToken);
```

### `VirtualBar.Infrastructure/Services/ProductRequestService.cs` (inner)
Primary ctor: `(AppDbContext db, ICurrentUser currentUser, INotificationService notificationService)`.

- **`CreateAsync`** — compute `CanonicalKey` (distillery name resolved from `DistilleryId` when set,
  else `Brand`; same `ProductKey.For(dist ?? brand, name, category, age, null, volumeMl)` shape as
  everywhere), trim string fields, save. **Map the filtered-unique-index race**: catch
  `DbUpdateException` → `Result<ProductRequestDto>.Conflict("This product has already been requested.")`
  (the `OfferService.CreateAsync` pattern — detach the failed entity first). No notification on
  create (the admin polls the queue; a bell type for the admin is parked).
- **`GetMineAsync`** — own non-deleted requests, newest first.
- **`WithdrawAsync`** — soft-delete (`IsDeleted = true`, `DeletedAt`) — ownership/status guards live
  in the decorator.
- **`GetAllAsync`** — all non-deleted, optional status filter, newest first, join requester
  display name.
- **`ApproveAsync`** — the one non-trivial method:
  1. Load the request (tracked).
  2. Resolve the product: `ExistingProductId` set → use it; else build a new `Product` from
     request values + overrides (`Origin = ProductOrigin.Approved`, `CanonicalKey` recomputed from
     the **effective** field values). Image: an explicit `ImageUrl` override wins; else when
     `UseSourceBottleImage` and the request has a non-deleted source bottle, reuse its primary
     image URL (`IsPrimary`, else lowest `SortOrder`; no images → stays null — **lenient
     best-effort enrichment, never an error**, so no decorator guard). The flag is ignored on the
     `ExistingProductId` path. Save; catch `DbUpdateException` on the product unique key →
     `Conflict("A product with the same canonical identity already exists.")` (detach first).
  3. Mark the request: `Status = Approved`, `ResolvedProductId`, `AdminNote` (if provided),
     `RespondedAt = DateTime.UtcNow`.
  4. Link the source bottle if `SourceBottleId != null` and that bottle is non-deleted and still
     `ProductId == null`.
  5. **Retro-link pass**: candidates = non-deleted bottles with `ProductId == null`,
     `Category == product.Category`, `Age == product.Age`, `VolumeMl == product.VolumeMl`
     (nullable `==` translates to `IS NULL` — intended), `Include(b => b.Distillery)`; compare
     `ProductKey.For(bottle.Distillery?.Name, bottle.Name, bottle.Category, bottle.Age, null,
     bottle.VolumeMl)` to `product.CanonicalKey` in memory (note: bottle keys are computed **without
     vintage** here on purpose — catalog identity ignores vintage); set `ProductId` on matches.
     No notifications to retro-linked owners (silent enrich).
  6. One `SaveChangesAsync` for steps 3–5.
  7. `notificationService.CreateAsync(request.UserId, NotificationType.ProductRequestApproved,
     product.Id, product.Name, cancellationToken)` — plain `CreateAsync`; the decorator self-skip is
     correct (admin approving own request → no notification, fine).
  8. *(added after this slice)* `badgeService.EvaluateAsync(request.UserId,
     BadgeTrigger.ProductRequestApproved, cancellationToken)` — awards `FirstCatalogProduct` to the
     **requester**, never the approving admin. Last line before the `Ok`, after the save and the
     notification, exactly like the `OfferAccepted` hooks; the `Conflict` return in step 2 means a
     lost race awards nothing. `ProductRequestService` gained an `IBadgeService` constructor
     parameter for it. See `docs/badges/00-OVERVIEW.md` §3 "Added after this slice".
- **`RejectAsync`** — `Status = Rejected`, `AdminNote`, `RespondedAt`, save, then
  `CreateAsync(request.UserId, NotificationType.ProductRequestRejected, request.Id, request.Name, ...)`.

### `VirtualBar.Infrastructure/Decorators/ProductRequestValidationDecorator.cs`
`ThrowIfCancellationRequested()` first in every method. Guards:
- **CreateAsync**: `Name` required/trimmed non-empty; `Category` defined enum value; `Barcode` (when
  present) digits-only length 8–14 (the barcode-scanning §4.1 rule) else `Fail`; `Age` 1–100,
  `AbvPercent` 1–96, `VolumeMl` 20–6000 when present (the label-scan sanity bounds) else `Fail`;
  `UserNote`/`Brand` length caps (500/200); `DistilleryId` set → must exist non-deleted else `Fail`;
  `SourceBottleId` set → bottle must exist non-deleted else `Fail`, and be **owned by
  `currentUser.UserId`** else `Forbidden`; **open-request cap**: `Pending` non-deleted count for the user ≥ 25 →
  `Fail("You have too many open catalog requests.")`; **friendly dedupe pre-checks** (fast path
  only — the DB index is the real guard): a non-deleted `Product` with the same `CanonicalKey` →
  `Conflict("This product already exists in the catalog.")`; a `Pending` non-deleted request with
  the same key → `Conflict("This product has already been requested.")`.
- **GetMineAsync**: none (identity comes from `ICurrentUser`).
- **WithdrawAsync**: request exists non-deleted else `NotFound`; `UserId == currentUser.UserId` else
  `Forbidden` (CLAUDE.md ownership rule); `Status == Pending` else
  `Conflict("Only pending requests can be withdrawn.")`.
- **GetAllAsync / ApproveAsync / RejectAsync**: `!currentUser.IsAdmin` →
  `Forbidden("Only administrators can manage product requests.")` (admin checks live in decorators —
  never in controllers). Approve/Reject additionally: request exists non-deleted else `NotFound`;
  `Status == Pending` else `Conflict("Request already resolved.")`. Approve: `ExistingProductId`
  set → product exists non-deleted else `Fail`; else effective `Name` (override ?? request) trimmed
  non-empty + the same numeric/barcode sanity bounds on effective values.

## Files — modify
- `VirtualBar.Domain/Entities/NotificationType.cs` — append at the END (never reorder):
  `ProductRequestApproved`, `ProductRequestRejected`.
- `VirtualBar.Infrastructure/DependencyInjection.cs`:
  ```csharp
  services.AddScoped<ProductRequestService>();
  services.AddScoped<IProductRequestService>(sp => new ProductRequestValidationDecorator(
      sp.GetRequiredService<ProductRequestService>(),
      sp.GetRequiredService<AppDbContext>(),
      sp.GetRequiredService<ICurrentUser>()));
  ```

### `VirtualBar.Api/Controllers/ProductRequestsController.cs` (create)
`[ApiController] [Route("api/product-requests")] [Authorize]`, full XML docs on every action:
| Route | Method | Notes |
|---|---|---|
| `POST /api/product-requests` | `CreateAsync` | manual request (the UI's "not in the catalog" path); `201`-style `Ok(dto)` per existing controller convention; `409` duplicate |
| `GET /api/product-requests/mine` | `GetMineAsync` | own requests |
| `DELETE /api/product-requests/{id}` | `WithdrawAsync` | own pending only |
| `GET /api/product-requests?status=Pending` | `GetAllAsync` | admin queue (`403` non-admin) |
| `PATCH /api/product-requests/{id}/approve` | `ApproveAsync` | admin; body `ResolveProductRequestRequest` |
| `PATCH /api/product-requests/{id}/reject` | `RejectAsync` | admin; body `RejectProductRequestRequest` |

## Build gate
`dotnet build` 0 errors.

## Test targets (record only — written in slice 07)
`ProductRequestServiceTests` — every decorator guard branch above; create happy path + key
computation (DistilleryId vs Brand fallback); duplicate-pending race → `Conflict`
(**SQLite in-memory** — filtered unique index); approve: new product / existing product /
`ExistingProductId` missing → `Fail` / source bottle linked / source bottle already linked (skip) /
source bottle deleted (skip) / retro-link match + non-match (category same but name differs) /
duplicate product key → `Conflict` (SQLite) / notification fired with `ProductRequestApproved`
(Moq `Verify`); reject: fields set + `ProductRequestRejected` fired; withdraw branches; `GetAllAsync`
status filter on/off; non-admin `Forbidden` on all three admin methods.

Added with step 8: `IBadgeService` is `Verify`-ed on both approve paths (new product and
`ExistingProductId`), asserted **never** called on the duplicate-key `Conflict` path or on reject,
and the whole chain — approve → notification → badge → what the requester's bell and progress
endpoint actually return — is covered end-to-end in
`VirtualBar.Tests\Integration\ProductRequestApprovalFlowTests.cs` with the real notification and
badge services rather than mocks.
