---
paths:
  - "VirtualBar.Api/Program.cs"
  - "**/*Seeder*"
  - "**/SeedData/**"
---

# Startup Behavior

On `dotnet run`:
1. Pending EF Core migrations are applied automatically (`MigrateAsync`).
2. If `AdminEmail` is configured, that user's `IsAdmin` flag is set to `true`.
3. `DistillerySeeder` tops up missing distilleries/category pairs (insert-only), then `ProductSeeder` tops up the
   product catalog from the embedded per-category `products.<category>.seed.json` files: insert-only by
   `CanonicalKey`, except that it gives an existing product with no year the year its seed row carries (#59,
   `.claude/rules/domain/catalog.md`).
4. Swagger/OpenAPI available at `/openapi/v1.json` (development only).
