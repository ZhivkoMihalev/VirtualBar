# Slice 02 — `ProductSeeder` + curated starter seed (embedded JSON)

> Prereq: `00-OVERVIEW.md`, slice 01. The offline mass-import tool that REGENERATES the seed file at
> 5–15k scale is slice **08** — this slice ships the plumbing + a curated starter set so the feature
> demos immediately.

## Scope
An idempotent startup seeder (mirror `DistillerySeeder`) reading an **embedded** JSON resource, plus
the starter data file (~200–400 iconic core-range bottles).

## Files — create
### `VirtualBar.Infrastructure/Persistence/SeedData/products.seed.json` (embedded resource)
Array of objects, schema (all fields except `name`/`category` nullable):
```json
{
  "name": "12 Year Old Sherry Oak",
  "brand": null,
  "distillery": "Macallan",
  "category": "Whisky",
  "country": "Scotland",
  "region": "Speyside",
  "age": 12,
  "abvPercent": 40.0,
  "volumeMl": 700,
  "barcode": null,
  "imageUrl": null,
  "description": null
}
```
- `distillery` is a **name** matched against the seeded `Distilleries` at seed time (exact →
  case-insensitive); no match → the string moves to `Brand` and `DistilleryId` stays null.
- `category` is the `SpiritCategory` enum name (parse with `Enum.TryParse(ignoreCase: true)`; a row
  that fails to parse is **skipped**, not defaulted).
- **Honesty rule (from 00 §6):** curated rows contain only well-known core-range facts; unknown →
  `null`. **No barcodes in the starter seed** (they arrive via the import tool or user requests).
  Starter content: the recognizable core ranges across all 8 categories (Whisky-heavy is fine:
  Macallan/Glenfiddich/Ardbeg/Lagavulin/… 10/12/15/18-year expressions; standard rums — Diplomático
  Reserva Exclusiva, Zacapa 23, Havana Club 7; cognacs — Hennessy VS/VSOP/XO, Rémy Martin VSOP/XO;
  vodkas, gins, tequilas similarly). Aim 200–400 rows.

### `VirtualBar.Infrastructure/Persistence/ProductSeeder.cs`
`public static class ProductSeeder` with
`public static async Task SeedProductsAsync(AppDbContext db, CancellationToken cancellationToken = default)`:
1. `if (await db.Products.AnyAsync(cancellationToken)) return;` (empty-table guard — same contract
   as `DistillerySeeder`; re-running the app never duplicates).
2. Read the embedded resource (`Assembly.GetManifestResourceStream`), deserialize with
   `JsonSerializerOptions { PropertyNameCaseInsensitive = true }` + `JsonStringEnumConverter`.
3. Load `Distilleries` name→id once (`ToDictionaryAsync`, case-insensitive comparer).
4. For each row: resolve `DistilleryId`/`Brand` (rule above), compute
   `CanonicalKey = ProductKey.For(distilleryName ?? brand, name, category, age, null, volumeMl)`,
   set `Origin = ProductOrigin.Seeded`, `Id = Guid.NewGuid()`, `CreatedAt`/`UpdatedAt = now`.
5. **Dedupe by `CanonicalKey`** (`GroupBy(...).Select(g => g.First())`) — the unique index must never
   trip on our own seed.
6. Single `AddRange` + one `SaveChangesAsync(cancellationToken)`.

No decorator, no `Result<T>` — static seeder like `DistillerySeeder`. A malformed resource should
throw at startup (fail fast, same as a bad migration) — do NOT swallow.

## Files — modify
- `VirtualBar.Infrastructure/VirtualBar.Infrastructure.csproj` — embed the resource:
  ```xml
  <ItemGroup>
    <EmbeddedResource Include="Persistence\SeedData\products.seed.json" />
  </ItemGroup>
  ```
- `VirtualBar.Api/Program.cs` — in the existing startup scope, directly after
  `await DistillerySeeder.SeedDistilleriesAsync(db);`:
  `await ProductSeeder.SeedProductsAsync(db);`
  (order matters — distillery resolution needs `Distilleries` populated).

## Build gate
`dotnet build` 0 errors; `dotnet run` once locally → log shows startup OK; `Products` row count
equals the deduped seed count; second run inserts nothing.

## Test targets (record only — written in slice 07)
`ProductSeederTests`: seeds when empty; skips when non-empty; distillery name resolved (exact +
case-insensitive) vs falls back to `Brand`; unparseable category row skipped; duplicate canonical
keys in the file collapse to one row; `Origin == Seeded` and `CanonicalKey` computed as specified.
(InMemory is fine — no unique-index enforcement needed for these.)
