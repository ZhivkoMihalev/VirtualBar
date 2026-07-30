# Slice 05 — Bottle integration: DTOs, auto-link, auto-request, barcode L0

> Prereq: `00-OVERVIEW.md`, slices 03–04. Build gate at the end; **no tests yet**.

## Scope
`ProductId` flows through the bottle DTOs; `AddBottleAsync` resolves catalog linkage (explicit →
auto-link → auto-request); the barcode lookup answers from the catalog first.

## Files — modify

### `VirtualBar.Application/DTOs/Bottles/*`
- `AddBottleRequest` + `UpdateBottleRequest`: add `public Guid? ProductId { get; set; }`.
- `BottleDto`: add `ProductId` (Guid?). Nothing else — the bottle's own fields remain the display
  truth; the frontend only needs the id for the "linked" chip.

### `VirtualBar.Infrastructure/Decorators/BottleValidationDecorator.cs`
For `AddBottleAsync` and `UpdateBottleAsync`: when `request.ProductId` is set → the product must
exist non-deleted, else `Fail("Selected product does not exist.")`. (Update with `ProductId = null`
simply unlinks — no guard.)

### `VirtualBar.Infrastructure/Services/BottleService.cs`
Primary ctor gains **`IProductRequestService productRequestService`** and
**`ILogger<BottleService> logger`** (if not already present — check; add only what's missing).

**`AddBottleAsync`** — after mapping, before save:
1. `request.ProductId` set → assign it (decorator already validated). Skip 2–3.
2. Else compute `key = ProductKey.For(distilleryName, request.Name, request.Category, request.Age,
   null, request.VolumeMl)` — distillery name fetched when `DistilleryId` set (reuse whatever lookup
   the method already does for mapping; do not add a second query if one exists). Exact match in
   `Products` (non-deleted) → `bottle.ProductId = match.Id` (**auto-link**, silent).
3. Else, **after** the bottle save + existing follower fan-out + badge hook, file the auto-request:
   ```csharp
   try
   {
       await productRequestService.CreateAsync(new CreateProductRequestRequest
       {
           Name = request.Name,
           DistilleryId = request.DistilleryId,
           Category = request.Category,
           Age = request.Age,
           AbvPercent = request.AbvPercent,
           VolumeMl = request.VolumeMl,
           Country = request.Country,
           Region = request.Region,
           SourceBottleId = bottle.Id,
       }, cancellationToken);
   }
   catch (Exception ex)
   {
       logger.LogError(ex, "Auto product request failed for bottle {BottleId}", bottle.Id);
   }
   ```
   A non-success `Result` (duplicate pending / cap / product raced into existence) is **expected and
   ignored** — the decorator/service already handled it gracefully; the `try/catch` only guards
   genuine bugs (the badge-engine philosophy: never break the host operation).
   Note the call goes through the **decorated** `IProductRequestService`, so `SourceBottleId`
   ownership passes trivially (same current user) and all sanity bounds apply. Out-of-bounds values
   the bottle itself allows (e.g. ABV 0.5) simply produce a failed `Result` → no request — acceptable.

**`UpdateBottleAsync`** — assign `ProductId` from the request (set or null). **No** auto-link and
**no** auto-request on update (v1 keeps update dumb; revisit with usage).

**`MapToDto`** (and any query projections that build `BottleDto`) — carry `ProductId` through.

### `VirtualBar.Infrastructure/Services/ProductLookupService.cs` — barcode L0
Primary ctor gains **`AppDbContext db`**. At the top of `LookupByBarcodeAsync`:
```csharp
var product = await db.Products
    .Where(p => !p.IsDeleted && p.Barcode == barcode)
    .OrderBy(p => p.CreatedAt)
    .FirstOrDefaultAsync(cancellationToken);

if (product is not null)
    return Result<BarcodeProductDto>.Ok(new BarcodeProductDto
    {
        ProductId = product.Id,
        Name = product.Name,
        Brand = product.Brand ?? /* distillery name when loaded */ null,
        ImageUrl = product.ImageUrl,
        VolumeMl = product.VolumeMl,
        AbvPercent = product.AbvPercent,
    });
```
Miss → the existing external path, unchanged. (When `docs/barcode-scanning/` slice 02 lands later,
this block simply becomes L0 above its L1/L2 — no conflict.)

### `VirtualBar.Application/DTOs/Products/BarcodeProductDto.cs`
Additive: `public Guid? ProductId { get; set; }` — the frontend uses it to link on barcode hit.

### `VirtualBar.Infrastructure/DependencyInjection.cs`
No change needed for `BottleService` (registered as `AddScoped<BottleService>()` — the container
resolves the new ctor parameter). Verify the `ProductLookupService` typed-`HttpClient` registration
still resolves with the added `AppDbContext` (it does — scoped).

## Circular-dependency check (done at design time)
`BottleService → IProductRequestService → (AppDbContext, ICurrentUser, INotificationService)` —
no path back to `IBottleService`. Safe.

## Build gate
`dotnet build` 0 errors. Manual smoke: add a bottle matching a seeded product (name/category/age/
volume) → `BottleDto.ProductId` set; add an unknown bottle → row appears in `ProductRequests`.

## Test targets (record only — written in slice 07)
`BottleServiceTests` additions: explicit `ProductId` valid → linked, no request (Moq `Verify`
`CreateAsync` never called); explicit invalid → decorator `Fail`; no `ProductId` + exact key match →
auto-linked; no match → `CreateAsync` called once with `SourceBottleId = bottle.Id`; request service
returns failed `Result` → add still succeeds; request service **throws** → add still succeeds +
logged; update set/clear/invalid `ProductId`. Ctor ripple: `CreateBottleService` helper gains
optional `IProductRequestService` (default `Mock.Of<>`). `ProductLookupServiceTests`: catalog hit →
DTO with `ProductId`, **no external HTTP call** (FakeHttpHandler call counter = 0); catalog miss →
external path unchanged; ctor now needs the InMemory `AppDbContext`.
