# 02 — Internal-First Layered Lookup

> Depends on: **01** · Read `00-OVERVIEW.md` first.

## Goal
Close **Gap 3**: `GET /api/products/barcode/{code}` answers from **our own crowdsourced bottle data
first** (rich spirits fields: category, distillery, age, vintage, country/region), using the external UPC
API only to fill what's still missing. Every bottle a user saves with a barcode (slice 01) makes this
lookup better for everyone — the compounding moat from the research.

## Build / extend
1. **`ProductDataSource` enum** — `VirtualBar.Domain/Enums/ProductDataSource.cs`: `Internal`, `External`.
   Serializes as string (global `JsonStringEnumConverter`); doubles as the UI provenance label
   ("от колекциите" / "от продуктовата база").
2. **`BarcodeProductDto`** — extend additively (all nullable, one blank line between properties):
   `SpiritCategory? Category`, `Guid? DistilleryId`, `string? DistilleryName`, `int? Age`,
   `int? VintageYear`, `string? Country`, `string? Region`, `ProductDataSource Source`.
3. **`ProductLookupService`** — inject `AppDbContext db` (keeps the typed `HttpClient`); restructure
   `LookupByBarcodeAsync` into two private steps:
   - **`LookupInternalAsync`** — non-deleted `Bottles` where `Barcode == code` (projection: the DTO
     fields + `DistilleryId` + primary image URL + `CreatedAt`; `Include(Distillery)` name via
     projection). Empty → `null`. Otherwise **majority vote per field** over the matches: most frequent
     non-null value wins, ties → the value from the most recent bottle; `ImageUrl` = primary image of the
     most recent match that has one (URL reused as-is — overview §7 image-reuse note);
     `Source = Internal`.
   - **`LookupExternalAsync`** — the existing UPC call + parsers, unchanged in behavior;
     `Source = External`.
   - **Compose:** internal hit → call external **best-effort** (swallow all errors — mirror the existing
     image-download `try/catch`) and fill **only still-null** fields (`Name` empty, `Brand`, `ImageUrl`,
     `VolumeMl`, `AbvPercent` — external has no spirits fields); internal miss → external alone (today's
     exact behavior, now with `Source = External`); both miss → `NotFound("Product not found.")`.
4. **`ProductValidationDecorator`** — align the guard with slice 01: barcode null/whitespace →
   `Fail("Barcode is required.")` (existing); add trimmed **digits-only 8–14** → else
   `Fail("Invalid barcode.")` (same rule, same message as the bottle decorator).
5. **`ProductsController`** — unchanged routes; refresh the XML `<response>` docs (still 200/400/404).

## Test targets (written in slice 05)
`ProductLookupServiceTests` (existing class — extend; `FakeHttpHandler` for the external leg):
internal single match → all spirits fields + `Source = Internal`; majority vote (2 vs 1 on category) →
mode wins; tie → most recent; soft-deleted bottle excluded; internal + external merge fills only null
fields (external name does NOT overwrite internal name); external fails + internal hit → still `Ok`;
internal miss → external path (existing tests keep passing, now assert `Source = External`); both miss →
`NotFound`; decorator: invalid format → `Fail`.

## Gate
`dotnet build` → **0 errors**; manual smoke: a barcode present on two seeded bottles returns the internal
merge in `/openapi/v1.json`-driven testing (development).
