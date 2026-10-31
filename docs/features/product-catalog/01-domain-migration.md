# Slice 01 — Domain entities, enums, `Bottle.ProductId`, EF config, migration

> Prereq: read `00-OVERVIEW.md`. Depends on: nothing. Build gate at the end; **no tests yet**
> (targets recorded for slice 07).

## Scope
Two entities, two enums, one nullable FK on `Bottle`, `AppDbContext` wiring, one ADD-only migration.

## Files — create
| File | Content |
|---|---|
| `VirtualBar.Domain/Enums/ProductOrigin.cs` | `public enum ProductOrigin { Seeded, Approved }` |
| `VirtualBar.Domain/Enums/ProductRequestStatus.cs` | `public enum ProductRequestStatus { Pending, Approved, Rejected }` — **append-only**; `Pending` must stay `0` (the filtered index literal depends on it) |
| `VirtualBar.Domain/Entities/Product.cs` | see below |
| `VirtualBar.Domain/Entities/ProductRequest.cs` | see below |

### `Product` (extends `BaseEntity`, one blank line between properties)
| Property | Type | Notes |
|---|---|---|
| `Name` | `string` | required, `= string.Empty` |
| `Brand` | `string?` | free-text producer when no distillery FK |
| `DistilleryId` | `Guid?` | FK → Distillery |
| `Distillery` | `Distillery?` | nav |
| `Category` | `SpiritCategory` | |
| `Country` / `Region` | `string?` | |
| `Age` | `int?` | |
| `AbvPercent` | `double?` | |
| `VolumeMl` | `int?` | |
| `Barcode` | `string?` | EAN/UPC digits |
| `ImageUrl` | `string?` | |
| `Description` | `string?` | |
| `CanonicalKey` | `string` | required; always set via `ProductKey.For(distilleryName ?? brand, name, category, age, null, volumeMl)` |
| `Origin` | `ProductOrigin` | `Seeded` / `Approved` |
| `Bottles` | `ICollection<Bottle>` | `= [];` |

### `ProductRequest` (extends `BaseEntity`)
| Property | Type | Notes |
|---|---|---|
| `UserId` | `Guid` | requester, FK → AppUser |
| `User` | `AppUser` | nav, `= null!` |
| `Name` | `string` | required |
| `Brand` | `string?` | |
| `DistilleryId` | `Guid?` | FK → Distillery |
| `Distillery` | `Distillery?` | nav |
| `Category` | `SpiritCategory` | |
| `Age` | `int?` | |
| `AbvPercent` | `double?` | |
| `VolumeMl` | `int?` | |
| `Barcode` | `string?` | |
| `Country` / `Region` | `string?` | |
| `UserNote` | `string?` | requester's note to the admin |
| `CanonicalKey` | `string` | computed at create with the same `ProductKey.For` call shape as `Product` |
| `Status` | `ProductRequestStatus` | default `Pending` |
| `AdminNote` | `string?` | set on reject (optionally on approve) |
| `ResolvedProductId` | `Guid?` | FK → Product, set on approve |
| `ResolvedProduct` | `Product?` | nav |
| `SourceBottleId` | `Guid?` | FK → Bottle — the add that triggered it |
| `RespondedAt` | `DateTime?` | set on approve/reject |

## Files — modify
- `VirtualBar.Domain/Entities/Bottle.cs` — add after `DistilleryId`/`Distillery`:
  `public Guid? ProductId { get; set; }` + `public Product? Product { get; set; }`
  (keep the one-blank-line style).
- `VirtualBar.Infrastructure/Persistence/AppDbContext.cs`:
  - `public DbSet<Product> Products => Set<Product>();`
  - `public DbSet<ProductRequest> ProductRequests => Set<ProductRequest>();`
  - `OnModelCreating` (style-match the `Offer`/`BottleReview` blocks; the global Restrict loop
    already covers delete behavior — do NOT add cascades):
    ```csharp
    modelBuilder.Entity<Product>(e =>
    {
        e.HasIndex(p => p.CanonicalKey)
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");

        e.HasIndex(p => p.Barcode);

        e.HasIndex(p => new { p.Category, p.Name });
    });

    modelBuilder.Entity<ProductRequest>(e =>
    {
        e.HasIndex(r => r.CanonicalKey)
            .HasFilter("[Status] = 0 AND [IsDeleted] = 0")
            .IsUnique();

        e.HasIndex(r => new { r.UserId, r.IsDeleted });

        e.HasIndex(r => new { r.Status, r.IsDeleted, r.CreatedAt });
    });
    ```
    (`Product` has no decimal properties — nothing to configure beyond the indexes. Indexed strings
    get `nvarchar(450)` by EF convention; this codebase never calls `HasMaxLength` — keep it that
    way. No explicit column type for anything — SQLite tests depend on provider defaults, same
    reason `SourcesJson` has none.)

## Migration
```bash
dotnet ef migrations add AddProductCatalog --project VirtualBar.Infrastructure --startup-project VirtualBar.Api
```
Verify the generated migration is **ADD-only**: `Products` + `ProductRequests` tables,
`Bottles.ProductId` column + FK + index, the five indexes above. No drops, no data changes.

## Build gate
`dotnet build VirtualBar.Api/VirtualBar.Api.csproj --no-restore -v q` → 0 errors.

## Test targets (record only — written in slice 07)
- Filtered-unique pending-key race → `DbUpdateException` (SQLite in-memory — InMemory doesn't
  enforce secondary unique indexes).
- Product `CanonicalKey` unique race on approve (SQLite).
