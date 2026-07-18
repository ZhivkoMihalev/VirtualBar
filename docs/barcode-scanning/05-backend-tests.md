# 05 — Backend tests (written LAST)

> Depends on: **01–03** · Read `00-OVERVIEW.md` first.

## Goal
**All** backend unit tests for Barcode/Label Scanning, written **after the whole backend is implemented**
(testing-last protocol from `docs/collection-value/`). **100% branch** for every new/changed service
method.

## `BottleServiceTests` — additions (slice 01)
Add/update persist the trimmed barcode; whitespace-only input → stored `null`; `MapToDto` carries
`Barcode`. Decorator branches: valid 8 / 12 / 13 / 14-digit codes → `Ok`; 7 digits → `Fail`; 15 digits →
`Fail`; letters/mixed → `Fail`; null and empty → `Ok` (optional field).

## `ProductLookupServiceTests` — extend the existing class (slice 02)
Uses the isolated InMemory DB **plus** `FakeHttpHandler` for the external leg:
- **Internal:** single match → all spirits fields mapped + `Source = Internal`; majority vote — 2-vs-1 on
  category/name → mode wins; tie → most recent bottle's value; soft-deleted bottles excluded; primary
  image of the most recent match reused.
- **Merge:** internal hit + external success → external fills ONLY null fields (internal name/category
  never overwritten); internal hit + external non-2xx/throw → still `Ok` (best-effort swallow).
- **External-only:** internal miss → existing external behavior + `Source = External` (keep the current
  test suite green — image download, `ParseVolumeMl`/`ParseAbv` branches already covered there).
- **Both miss** → `NotFound`.
- **Decorator:** empty → `Fail("Barcode is required.")`; letters / 7 / 15 digits →
  `Fail("Invalid barcode.")`.

## `LabelScanServiceTests` — new class (slice 03)
Test **through the decorator** (mirror the comment/review test style); `FakeHttpHandler` with canned
Anthropic Messages responses; **real** `AnthropicDailyCallBudget` + `MutableTimeProvider`; InMemory DB
seeded with a few `Distillery` rows:
- **Happy path** → all fields mapped, `Confidence` parsed, distillery matched exact-insensitive →
  `DistilleryId` + canonical name.
- **Fenced response** (```json ... ```) → parsed.
- **Nulls preserved** — response with only `name` → everything else `null`, still `Ok`.
- **Sanity bounds, one test per field:** ABV 3 and 97 → `null`; age 0 and 101 → `null`; vintage 1850 and
  next year → `null`; volume 10 and 7000 → `null`; unknown category string → `null` (all still `Ok` while
  a name survives).
- **Unidentifiable:** name AND distillery null → `NotFound`.
- **Distillery `Contains`:** single hit → matched; multiple hits → text only, `DistilleryId` null; no
  hit → text only.
- **Provider failures:** non-2xx → `NotFound`; malformed JSON → `NotFound`; empty content → `NotFound`.
- **Budget:** exhausted → `Fail` and the handler records **zero** HTTP calls (TryConsume precedes the
  request).
- **Decorator:** `Enabled=false` → `Fail`; empty bytes → `Fail`; `MaxImageBytes+1` → `Fail`;
  `image/gif` → `Fail`; boundary `MaxImageBytes` exactly → passes to inner.
- **Cancellation** → `OperationCanceledException` propagates from the decorator.

## Conventions (CLAUDE.md)
Naming `<Method>_When<Condition>_<Outcome>`; isolated InMemory DB per test (`Guid.NewGuid()` name);
EF InMemory everywhere (no `ExecuteUpdate/Delete` in these slices — **no SQLite needed**); mock only
`ICurrentUser`, `INotificationService`, `HttpMessageHandler`; `private static` seed helpers.

## Gate
`dotnet test VirtualBar.Tests/VirtualBar.Tests.csproj` → **`Failed: 0`**; **100% branch** on
`LabelScanService`, `LabelScanValidationDecorator`, the reworked `ProductLookupService`, and the touched
`BottleService`/decorator paths (coverage run: `--collect:"XPlat Code Coverage"`).
