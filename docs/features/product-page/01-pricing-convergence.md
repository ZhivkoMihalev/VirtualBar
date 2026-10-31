# Slice 01 — Pricing convergence (PR 1, ships alone)

> Prereq: `00-OVERVIEW.md` §3.7–3.8. This slice **changes live behaviour** (which snapshot a bottle
> reads), so it carries its own tests and goes out as its own PR before anything else.

## Scope
A linked bottle (non-deleted product) is priced under `Product.CanonicalKey`; an unlinked bottle keeps
its current key. One helper, five call sites, one new cache-only endpoint for a product.

## Step 1 — the helper
### Create `VirtualBar.Application/Common/PriceKey.cs`
```csharp
public static class PriceKey
{
    /// Linked to a live product → the product's stored canonical key (shared by every copy).
    /// Otherwise → the bottle's own ProductKey, exactly as before.
    public static string ForBottle(string? productCanonicalKey, string? distilleryName, string name,
        SpiritCategory category, int? age, int? vintageYear, int? volumeMl)
        => productCanonicalKey ?? ProductKey.For(distilleryName, name, category, age, vintageYear, volumeMl);
}
```
Do **not** recompute the product key from its fields — use the stored `CanonicalKey`, which is
already `ProductKey.For(distillery ?? brand, name, category, age, null, volume)`.

### Projection fragment (used in every query below)
```csharp
ProductCanonicalKey = b.Product != null && !b.Product.IsDeleted ? b.Product.CanonicalKey : null
```

## Step 2 — `PriceEstimationService` (3 sites)
- Extend `BottleKeyInfo` with `ProductCanonicalKey` and the product's research fields:
  `ProductName`, `ProductDistilleryOrBrand` (`Product.Distillery` non-deleted name ?? `Product.Brand`),
  `ProductCategory`, `ProductAge`, `ProductVolumeMl`, `ProductBarcode` — all null when not linked /
  product deleted. Add a private `KeyOf(BottleKeyInfo b)` that calls `PriceKey.ForBottle`.
- `GetBottleEstimateAsync`: key via `KeyOf`. Build `PriceProviderInput` from the **product** fields
  when `ProductCanonicalKey != null` (vintage `null`, barcode `ProductBarcode ?? bottle.Barcode`),
  else from the bottle (unchanged). Pass `input.Barcode` and `input.Category` to `UpsertSnapshotAsync`.
  Keep `BottleId` on the input.
- `GetCachedBottleEstimateAsync`: key via `KeyOf`.
- `GetCollectionValueAsync`: `keyByBottle` via `KeyOf`. Nothing else changes (still Sealed-only,
  still × `Quantity`).

## Step 3 — `PreWarmWorker.SelectStaleTopBottlesAsync`
Add `ProductCanonicalKey` to `BottleKeyFields`; `GroupBy(b => PriceKey.ForBottle(...))`. Ownership
count now counts **all copies of a product together**, so popular products rise to the top — intended.
The representative bottle goes through `GetBottleEstimateAsync`, which resolves the same key.

## Step 4 — `InternalMarketPriceProvider`
Add `ProductCanonicalKey` to `PriceCandidate` in **both** projections (listings; accepted offers via
`o.Bottle.Product`). `AddMatching` compares `PriceKey.ForBottle(...)` to `input.ProductKey`. Effect:
listings/offers on any copy of a product (any vintage) feed the product's internal estimate.

## Step 5 — product estimate endpoint
- `IPriceEstimationService.GetCachedProductEstimateAsync(Guid productId, CancellationToken)`.
- Inner: load `CanonicalKey` of the non-deleted product (`NotFound("Product not found.")` when
  missing), read the snapshot `AsNoTracking`, return `Ok(null!)` when none (same as the bottle
  method), else `MapToDto`.
- `PriceEstimationValidationDecorator`: `ThrowIfCancellationRequested()` + pass-through (match how
  the decorator handles `GetCachedBottleEstimateAsync`; if it guards `Guid.Empty` there, do the same).
- `PricesController`: `[HttpGet("product/{productId:guid}")]`, `[Authorize]` (class default),
  `200` / `204` (null data) / `404`, full XML docs — copy the bottle action.

## Step 6 — guard against drift
After the change:
```bash
grep -rn "ProductKey.For(" --include=*.cs VirtualBar.Infrastructure VirtualBar.Application
```
Allowed hits only: `PriceKey.cs`, `ProductKey.cs` itself, `ProductSeeder`, `ProductRequestService`,
`BottleService` (catalog keys, not price keys). Any hit under `Services/Pricing/` is a bug.

## Tests (in this slice)
- `PriceEstimationServiceTests`:
  - two bottles linked to one product, different vintage + different spelling → `GetBottleEstimateAsync`
    on the first writes one snapshot under `CanonicalKey`; the second gets a cache hit (provider
    mock/stub called **once**), and `PriceSnapshots.Count() == 1`;
  - linked bottle: provider input carries product name/brand, `VintageYear == null`, product barcode;
    product without barcode → bottle barcode;
  - product soft-deleted → falls back to the bottle key (old behaviour);
  - unlinked bottle → key unchanged (regression);
  - `GetCachedBottleEstimateAsync` + `GetCollectionValueAsync` read the product-key snapshot;
  - `GetCachedProductEstimateAsync`: found → DTO; no snapshot → null data; unknown id → NotFound;
    deleted product → NotFound.
- `PreWarmWorkerTests`: two linked bottles + one unlinked identical → two groups; fresh product
  snapshot → skipped; ownership count of the product group = 2.
- `InternalMarketPriceProviderTests`: listing on a linked bottle with a vintage matches the product
  key; accepted offer on a linked bottle matches.
- `PriceEstimationValidationDecoratorTests` + `PricesControllerTests`: new method/action (200, 204, 404).
- `PriceKeyTests` (new, `Services/Pricing/`): both arms of `??`.

## Gate
`dotnet build` → 0 errors; `dotnet test` → `Failed: 0`. PR description must mention the cold-cache
effect after deploy (overview §6) and that old snapshots are left untouched.
