# 01 — Persist the Barcode on the Bottle

> Depends on: **—** · Read `00-OVERVIEW.md` first.

## Goal
Close **Gap 2**: the scanned/entered barcode is saved on the bottle, exposed in `BottleDto`, and thereby
activates the already-shipped pricing sharpening (`PriceProviderInput`/`PriceSnapshot.Barcode`, UPC in the
Claude research prompt). **No migration** — `Bottle.Barcode` already exists.

## Build / extend
1. **`AddBottleRequest`** + **`UpdateBottleRequest`** — add `string? Barcode` (one blank line between
   properties).
2. **`BottleDto`** — add `string? Barcode` (the frontend needs it for edit prefill and to skip re-lookup).
3. **`BottleService`** — map `Barcode` in the add path, the update path, and `MapToDto`
   (normalize: `Trim()`, empty → `null`).
4. **`BottleValidationDecorator`** — one new guard on add + update, matching overview §3 (GS1 formats):
   when `Barcode` is provided (non-null/whitespace) → trimmed value must be **digits-only, length 8–14**,
   else `Fail("Invalid barcode.")`. Null/empty stays valid (barcode is optional).
5. **Frontend types only as far as the contract** (`AddBottlePayload`/`Bottle` += `barcode?: string`) —
   the form wiring itself lands in slice 04, but adding the fields here keeps `npm run build` green later.

## Test targets (written in slice 05)
`BottleServiceTests` additions: add/update persist the trimmed barcode; whitespace-only → stored as
`null`; decorator branches — valid 8/12/13/14-digit codes → `Ok`; 7 digits / 15 digits / letters /
mixed → `Fail`; null/empty → `Ok`; `MapToDto` carries it.

## Gate
`dotnet build` → **0 errors**.
