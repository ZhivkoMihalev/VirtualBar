# Slice 03 — Catalog search API (`IProductCatalogService` + `GET /api/products`)

> Prereq: `00-OVERVIEW.md`, slice 01 (slice 02 only for demo data). Build gate at the end; **no
> tests yet**.

## Scope
Read-only autocomplete search over `Products`, exposed on the **existing** `ProductsController`.
Mirrors the `DistilleryService` trio in shape.

## Files — create
### `VirtualBar.Application/DTOs/Products/ProductDto.cs`
`Id`, `Name`, `Brand?`, `DistilleryId?`, `DistilleryName?` (projected from nav, soft-delete aware —
copy the `BottleService.MapToDto` distillery handling), `Category` (`SpiritCategory`), `Country?`,
`Region?`, `Age?`, `AbvPercent?`, `VolumeMl?`, `Barcode?`, `ImageUrl?`.

### `VirtualBar.Application/Interfaces/IProductCatalogService.cs`
```csharp
Task<Result<List<ProductDto>>> SearchAsync(string search, SpiritCategory? category, int limit, CancellationToken cancellationToken);
```

### `VirtualBar.Infrastructure/Services/ProductCatalogService.cs` (inner — pure logic)
Query non-deleted products where `Name`/`Brand`/`Distillery.Name` contains the term
(`EF.Functions.Like` or `Contains` — case-insensitivity comes from the default SQL Server collation;
do not `ToLower()` the column, it kills the index). Order: `Name.StartsWith(term)` matches first
(`OrderByDescending(startsWith).ThenBy(Name)`), take `limit`, project straight to `ProductDto`
(no tracking, single round-trip).

### `VirtualBar.Infrastructure/Decorators/ProductCatalogValidationDecorator.cs`
`ThrowIfCancellationRequested()` first (every method). Guards:
- `search` null/whitespace or trimmed length < 2 → `Fail("Search term must be at least 2 characters.")`
- clamp `limit` to 1–50, default 20 when the controller passes 0/absent (normalize here, then call
  inner with the clamped value — inner assumes valid).

## Files — modify
- `VirtualBar.Api/Controllers/ProductsController.cs` — add:
  ```csharp
  [HttpGet]
  [AllowAnonymous]
  public async Task<IActionResult> Search([FromQuery] string search, [FromQuery] SpiritCategory? category,
      [FromQuery] int limit, CancellationToken cancellationToken)
  ```
  → `productCatalogService.SearchAsync(...)`; full XML docs (`<summary>`, `<param
  name="cancellationToken">`, `<response code="200">`, `<response code="400">`). The controller gains
  the `IProductCatalogService` constructor dependency alongside the existing lookup service.
- `VirtualBar.Infrastructure/DependencyInjection.cs`:
  ```csharp
  services.AddScoped<ProductCatalogService>();
  services.AddScoped<IProductCatalogService>(sp => new ProductCatalogValidationDecorator(
      sp.GetRequiredService<ProductCatalogService>()));
  ```
  (No `AppDbContext`/`ICurrentUser` needed in this decorator — read-only, anonymous.)

## Route shape (locked)
`GET /api/products?search=macal&category=Whisky&limit=20` → `200 [ProductDto…]`, `400` on a short
term. `[AllowAnonymous]` — public catalog data, same policy as `GET /api/distilleries`.

## Build gate
`dotnet build` 0 errors. Manual smoke: `curl "http://localhost:5000/api/products?search=maca"`
returns seeded Macallans.

## Test targets (record only — written in slice 07)
`ProductCatalogServiceTests`: term matches name / brand / distillery name; category filter on/off;
starts-with ordered before contains; limit respected; deleted products excluded; decorator: short
term → Fail, whitespace → Fail, limit clamped (0→20, 999→50), cancellation throws.
