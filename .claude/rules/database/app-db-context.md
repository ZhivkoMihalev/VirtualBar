# Database — AppDbContext

`AppDbContext` extends `IdentityDbContext<AppUser, IdentityRole<Guid>, Guid>`.

**Global rule:** all FK delete behavior defaults to `DeleteBehavior.Restrict` (set via a loop in `OnModelCreating`). Cascade deletes are added only explicitly per entity.

**Global rule — every `DateTime` round-trips as UTC.** `ConfigureConventions` maps every `DateTime` / `DateTime?` property through `UtcDateTimeConverter` / `NullableUtcDateTimeConverter` (`VirtualBar.Infrastructure/Persistence/Conversions/`). Reason: `datetime2` carries no offset, so EF materialises values as `DateTimeKind.Unspecified`, System.Text.Json then omits the `Z`, and clients parse UTC as local time — every timestamp in the API was shifted by the viewer's offset (a seconds-old notification rendered as "3h ago" in UTC+3). Reading always re-stamps `Utc`; writing converts only an explicitly `Local` value and leaves `Unspecified` untouched (assumed already UTC — shifting it would corrupt data). Keep writing `DateTime.UtcNow`; **never** compensate for the offset on the frontend. Pure conversion — no schema change, no migration. Pinned by `VirtualBar.Tests/Persistence/UtcDateTimeConverterTests.cs`, whose round-trip tests need **SQLite** (EF InMemory returns the same CLR instance and so preserves `Kind` on its own, hiding the bug).

Key DbSets:

| DbSet | Purpose |
|---|---|
| `Bottles` | A collector's bottle — the core entity |
| `BottleImages` | Images attached to a bottle (primary + gallery) |
| `BottleLikes` | Junction: user liked a bottle (composite PK) |
| `BottleComments` | Comments on a bottle |
| `UserFollows` | Junction: follower → followed (composite PK) |
| `Messages` | Direct messages between users |
| `NewsPosts` | Admin-authored news articles (multilingual via `NewsPostTranslations`) |
| `NewsPostTranslations` | Per-language title + content for a `NewsPost` |
| `Notifications` | In-app notifications for users |
| `Distilleries` | Master list of distilleries (seeded on startup via `DistillerySeeder`) |
| `DistilleryCategories` | Junction: distillery → spirit category (composite PK; one distillery can have multiple categories) |
| `WishListItems` | A collector's desired bottle criteria — matched against listings |
| `Offers` | Purchase offers from one user to another on a specific bottle |
| `PriceSnapshots` | Read-through cache of indicative per-product price estimates (5-day TTL) — powers Collection Value |
| `BottleReviews` | One structured review per user per bottle: score 0–100 + tasting notes + flavors |
| `BottleReviewFlavors` | Junction: review → flavor tag (composite PK; up to 5 per review) |
| `UserBadges` | Junction: user → earned achievement badge (composite PK; awards are permanent) |
| `Products` | Canonical spirits catalog (seeded via `ProductSeeder` + admin-approved requests) — powers add-form autocomplete, auto-linking, barcode L0 |
| `ProductRequests` | User-filed requests to add a missing catalog product — admin approves/rejects; one pending per canonical key |

All entities extend `BaseEntity` which adds `Id (Guid)`, `CreatedAt`, `UpdatedAt`, `IsDeleted`, `DeletedAt`.

`BottleLike`, `UserFollow`, `DistilleryCategory`, `BottleReviewFlavor`, and `UserBadge` are junction tables with composite PKs — no `BaseEntity`.
