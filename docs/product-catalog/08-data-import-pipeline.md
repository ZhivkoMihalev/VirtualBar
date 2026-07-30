# Slice 08 — Offline data-import pipeline (Iowa / Open Food Facts → `products.seed.json`)

> ## ⛔ WITHDRAWN — built, never used, removed 2026-07-30
>
> `tools/ProductImport` was implemented in full to this spec and then deleted. Everything below is kept
> as the design record, not as a description of the codebase. **Do not re-add it without re-reading
> this box first.**
>
> **Why it went.** It never produced a single row of the shipped catalog. The tool writes an
> `ATTRIBUTION.md` next to any seed file it generates; `SeedData/` has never contained one. All ~3400
> rows came from hand curation instead, because Iowa's portfolio covers US distribution and the
> catalog's centre of gravity turned out to be limited European bottlings that it simply does not list.
> By then re-running the importer would have *destroyed* curated work rather than added to it — which
> is what the all-or-nothing `--force` guard in `SeedWriter` existed to prevent. ~2850 lines of code and
> tests, plus a CSV/encoding/HTTP/Claude surface that attracted a disproportionate share of review
> findings, for no shipped output.
>
> **What was kept.** The only genuinely load-bearing part was `SeedValidator`, and it only ever ran when
> someone remembered to pass `--validate`. Its checks now live in
> `VirtualBar.Tests/Services/ProductSeedDataTests.cs`, where they run on every build against the real
> embedded files: duplicate canonical keys across all eight files (the important one — `ProductSeeder`
> is insert-only by key, so a duplicated curated row is skipped in total silence), each row sitting in
> the file of its own category, and the same numeric/barcode bounds that
> `ProductRequestValidationDecorator` enforces on user-filed requests.
>
> **If you ever need it back:** bootstrapping a brand-new category from bulk public data is the one job
> it was actually good at. Rebuild from this spec, target a *new* empty seed file, and never point it at
> a curated one.

> Prereq: slice 02 (the seed schema + seeder). **Offline deliverable, optional for MVP** — the
> feature ships on the curated starter seed; run this when the user wants the 5–15k catalog.
> Never runs inside the API process; regenerates the embedded JSON, which seeds **only into an
> empty `Products` table** (growing an already-seeded DB is a separate, ask-first operation —
> CLAUDE.md forbids touching existing rows without asking).

## Sources (verified 2026-07; re-check availability when building)
| Source | What | License / terms | Strength | Weakness |
|---|---|---|---|---|
| Iowa Liquor Products — `data.iowa.gov/Sales-Distribution/Iowa-Liquor-Products/gckp-fe7r` (CSV export; Socrata API `resource/gckp-fe7r.csv`) | State distributor product portfolio: item description, category name, vendor, bottle volume, proof, age, **UPC** | US public data | structured, has UPC + volume + proof | US market only, ALL-CAPS names, needs normalization |
| Open Food Facts — `world.openfoodfacts.org/data` (JSONL dump ~gz, or per-barcode API) | Barcode-keyed products incl. spirits, images | **ODbL** — attribution + share-alike; add attribution to the repo/app footer if shipped | barcodes + images | spirits coverage patchy, noisy fields |
| TTB COLA registry (`ttb.gov` public registry) | Every US-approved label since 1999 | US public data | most complete | raw legal records, hard to normalize — **phase 2, skip in v1** |

Do NOT scrape Whiskybase (ToS). Systembolaget community mirrors are gray-zone — skip for the
committed dataset.

## Tool
`tools/ProductImport/ProductImport.csproj` — .NET 9 console app, added to the solution but referenced
by **nothing** (never ships in the API). Plain `System.Text.Json` + `CsvHelper` (or hand-rolled CSV —
Iowa's file is clean). No EF, no network beyond the optional dataset download.

```
dotnet run --project tools/ProductImport -- \
  --iowa path/to/iowa-liquor-products.csv \
  [--off path/to/openfoodfacts-products.jsonl.gz] \
  [--max 15000] \
  --out VirtualBar.Infrastructure/Persistence/SeedData/products.seed.json
```

## Pipeline (Iowa primary)
1. **Parse** the CSV; keep rows with a category mappable to `SpiritCategory` (mapping table in code:
   `"CANADIAN WHISKIES"→Whisky`, `"STRAIGHT BOURBON"→Whisky`, `"PUERTO RICO & VIRGIN ISLANDS RUM"→Rum`,
   `"IMPORTED VODKA"→Vodka`, gin/tequila/brandy/cognac groups likewise; unmapped → skip + count).
2. **Normalize the name** (ALL CAPS → title case with small-word exceptions; strip trailing pack/size
   tokens like `750ML`, `6PK`, `MINI`; collapse whitespace). Extract an **age statement** (`12YR`,
   `12 YO`, `AGED 12 YEARS`) into `age` and remove it from the name.
3. **Fields**: `proof / 2 → abvPercent` (Iowa lists proof); `Bottle Volume (ml) → volumeMl`;
   `UPC → barcode` (digits only, length 8–14 else null); vendor name → candidate distillery/brand.
4. **Distillery resolution** against the seeded ~710 names (exported to a JSON snapshot or reuse
   `DistillerySeeder`'s array via a project reference to Infrastructure — acceptable for a tool):
   exact → case-insensitive → token-contains; no match → `brand` keeps the vendor/label text.
5. **Merge OFF** (optional): stream the JSONL, keep entries whose `categories_tags` contain spirits
   categories AND that share a barcode with the Iowa set OR pass a name sanity filter; contribute
   `imageUrl` (only `https` static OFF URLs) and fill missing `abvPercent`/`volumeMl`. Record that
   OFF contributed → emit `ATTRIBUTION.md` alongside the seed (ODbL notice) and add the notice to
   the app footer before shipping OFF-derived data.
6. **Dedupe**: by barcode first, then by computed `CanonicalKey` (same `ProductKey.For` shape — the
   tool may reference `VirtualBar.Application` for the helper; never copy the algorithm).
7. **Rank & cap**: prefer rows with more filled fields; `--max` cap (default 15000 — startup-insert
   budget from 00 §6); deterministic order (category, name) so the JSON diffs cleanly in git.
8. **Emit**: seed-schema JSON + a stats report to stdout (rows in/out, skipped categories,
   distillery-match rate, barcode coverage) — paste the report into the PR description.

## Optional Claude-assisted normalization (`--claude`)
For rows whose normalized name still looks raw (heuristic: > 40% uppercase or contains vendor
codes), batch 50 rows per Messages-API call (reuse `Anthropic` config keys from user-secrets;
respect a hard call cap for the run) asking only: cleaned display name + distillery guess from the
seeded list + null-when-unsure. Deterministic re-runs matter more than perfection — cache responses
to a local `.cache.json` so re-running the tool is free. This is an offline cost decision — ask the
user before running with `--claude`.

## Acceptance
- `products.seed.json` validates against the slice-02 schema (a `--validate` flag re-reads the
  output through the same deserializer + key-dedupe the seeder uses, and fails non-zero on issues).
- Fresh DB (`dotnet run`) seeds the full set in acceptable time (measure; target < ~10 s locally).
- Spot-check 30 random rows against reality (names readable, no fabricated ABVs — null is correct).
- `ATTRIBUTION.md` present when OFF data included; Iowa noted as source in the same file.

---

## As built

`tools/ProductImport` (net9.0 console, in the solution, referenced by nothing that ships).
References `VirtualBar.Application` for `ProductKey` and `VirtualBar.Infrastructure` for the seeded
distillery list. No NuGet packages — the CSV reader is a hand-rolled RFC 4180 streamer.

```bash
dotnet run --project tools/ProductImport -- \
  --iowa path/to/iowa-liquor-products.csv \
  --out-dir VirtualBar.Infrastructure/Persistence/SeedData \
  [--off path/to/openfoodfacts-products.jsonl.gz] [--max 15000] [--force] [--validate]
```

### Deviations from the spec above, and why
- **Output is per category.** The single `products.seed.json` no longer exists — the seed was split
  into `products.<category>.seed.json` and `ProductSeeder` now globs them. `--out-dir` is the
  primary mode and writes that layout; `--out <file>` still emits one combined file.
- **Overwriting is opt-in (`--force`).** The committed seed files are hand-curated. Regenerating
  from Iowa would discard that work, so the writer refuses to clobber an existing file without the
  flag. Not in the spec, but the spec's own warning about not touching existing rows implies it.
- **Distillery list is reused, not copied.** `DistillerySeeder.KnownDistilleries` is a new
  `internal` accessor over the existing array, with `InternalsVisibleTo("ProductImport")` on
  Infrastructure. Resolution can never drift from what actually gets seeded.
- **Claude key comes from `ANTHROPIC_API_KEY` only**, not user-secrets — a console tool should not need
  the configuration stack for one value. There is deliberately no `--claude-key` flag: a secret passed
  as an argument is readable by anything that can list the machine's process table, and lands in the
  shell history on top of that. Default model `claude-sonnet-5`, overridable with `--claude-model`.
  The suggestion cache lives under `%LOCALAPPDATA%/VirtualBar/ProductImport` (not `bin/`, which
  `dotnet clean` wipes — that would mean paying for the same suggestions twice).
- **Column lookup is fuzzy.** Socrata renames headers between revisions, so columns are matched on
  their alphanumeric skeleton and the resolved indexes are printed at the start of every run.

### Traps worth remembering
- `"PUERTO RICO & VIRGIN ISLANDS RUM"` contains the letters `GIN`. Category mapping matches whole
  words and checks rum before gin; substring matching misfiles the entire Puerto Rican portfolio.
- Cognac is checked before brandy — every cognac is a brandy, not the reverse.
- Bottle sizes appear as `750ML`, `1.75L` and `1 LTR`. A pattern that only knows `ML`/`CL` leaves
  `1.75L` glued to the display name.
- Barcode leading zeros are significant (an EAN-13 starting `0` is a UPC-A) — never trim them.
- Categories containing `LIQUEUR`/`CREAM`/`SCHNAPP`/`COCKTAIL` are excluded before the spirit match,
  so `WHISKEY LIQUEUR` does not become a whisky.

Covered by `VirtualBar.Tests/Tools/ProductImportNormalizationTests.cs` (category map, name/age
normalization, distillery resolution).

### Not done
The pipeline has **not been run against the real datasets** and the committed seed files are
untouched. Running it is a deliberate, ask-first step: it replaces the curated catalog with
distributor data. `--claude` likewise costs money and is off by default.
