---
paths:
  - "**/*Distiller*"
  - "**/distiller*"
---

# Distilleries

## Distillery
| Property | Type | Notes |
|---|---|---|
| `Name` | `string` | Unique index |
| `Country` | `string?` | |
| `Region` | `string?` | |

- Has `ICollection<DistilleryCategory> Categories` and `ICollection<Bottle> Bottles`.
- Seeded on startup by `DistillerySeeder` with **top-up semantics**: inserts distilleries missing by name
  (case-insensitive) and missing `(DistilleryId, Category)` pairs, strictly insert-only, never updates existing rows.
  ~950 distilleries in 963 name/category pairs, across Whisky/Rum/Vodka/Cognac/Brandy/Tequila/Gin.
- **No `POST /api/distilleries`** — seeder is the single source of truth.
- Public read via `GET /api/distilleries?category=Rum` (AllowAnonymous).

## DistilleryCategory
Composite PK: `(DistilleryId, Category)`. No `BaseEntity`. Cascade delete from `Distillery`.
| Property | Type |
|---|---|
| `DistilleryId` | `Guid` |
| `Category` | `SpiritCategory` |

## Related rules
- `Bottle.DistilleryId`, `WishListItem.DistilleryId` (an exact-match criterion) and `Product.DistilleryId` point here:
  `.claude/rules/domain/bottles.md`, `.claude/rules/domain/wish-list.md`, `.claude/rules/domain/catalog.md`.
- A product seed row whose distillery name is unknown loses its FK (`.claude/rules/domain/catalog.md`, Seeding).
