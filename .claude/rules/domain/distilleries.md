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

Has `ICollection<DistilleryCategory> Categories` and `ICollection<Bottle> Bottles`. Seeded on startup by `DistillerySeeder` with **top-up semantics** — inserts distilleries missing by name (case-insensitive) and missing `(DistilleryId, Category)` pairs, strictly insert-only, never updates existing rows (~710 entries across Whisky/Rum/Vodka/Cognac/Brandy/Tequila/Gin). **No `POST /api/distilleries`** — seeder is the single source of truth. Public read via `GET /api/distilleries?category=Rum` (AllowAnonymous).

## DistilleryCategory
Composite PK: `(DistilleryId, Category)`. No `BaseEntity`. Cascade delete from `Distillery`.
| Property | Type |
|---|---|
| `DistilleryId` | `Guid` |
| `Category` | `SpiritCategory` |
