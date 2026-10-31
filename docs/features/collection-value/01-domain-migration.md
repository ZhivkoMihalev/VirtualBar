# 01 — Domain Migration (+ Barcode, + Sources)

> Phase **A** · Depends on: **—** · Read `00-OVERVIEW.md` first.

## Goal
The data model for the price cache: the `PriceSnapshot` entity (now also storing the **citation sources** that
must be displayed), the slim source/confidence enums, and the `Bottle.Barcode` column.

## Recover from stash (`git checkout stash@{0} -- <path>`)
- `VirtualBar.Domain/Entities/PriceSnapshot.cs`
- `VirtualBar.Domain/Enums/PriceSource.cs`, `PriceConfidence.cs`
- `VirtualBar.Domain/Entities/Bottle.cs` *(the `Barcode` property only)*
- `VirtualBar.Infrastructure/Persistence/AppDbContext.cs`, `Migrations/20260629120116_AddPriceSnapshotAndBarcode.*`,
  `Migrations/AppDbContextModelSnapshot.cs`

## Build new / extend
1. **`PriceSource` enum** — slim to `ClaudeResearch`, `Internal`. (Drop the forbidden auction/Wine-Searcher/
   WhiskyHunter/Systembolaget members from the stash version.) This is also the UI label.
2. **`PriceConfidence` enum** — clean `Low`, `Medium`, `High`. The provider sets it (Claude from the model's
   confidence; Internal from sample count).
3. **`PriceSnapshot`** — keep `ProductKey`, `EstimatedPrice`, `LowEstimate`, `HighEstimate`, `Currency`
   (**base currency**, e.g. EUR), `Confidence`, `Source`, `SampleSize`, `AsOf`; **add** `string SourcesJson`
   — a JSON array of `{ url, title }` citations (required for display per overview §3). One blank line between
   properties (CLAUDE.md). *(No `SignalType`, no native-currency/amount — dropped in the simplification.)*
4. **`PriceCitation`** record `(string Url, string Title)` — own file (Application/DTOs/Pricing — see slice 02).
5. **`AppDbContext`** — index `ProductKey`; `decimal(18,2)` for money; enums as strings; `SourcesJson` as `nvarchar(max)`.

## DB reconciliation (ADD-only)
The live DB already has the OLD `PriceSnapshots` + `Bottle.Barcode` applied. Recover the stashed migration +
model snapshot (so EF history matches), then add **one** migration **`AddSourcesJsonToPriceSnapshot`** that
**ADDs** the `SourcesJson` column. Never drop/recreate (CLAUDE.md).

## Test targets (written in slice 10)
Enums/entity carry no logic → none.

## Gate
`dotnet build` → **0 errors**; migration applies on `dotnet run`.
