# Barcode / Label Scanning (Баркод и етикет сканиране) — OVERVIEW & SHARED CONTEXT

> **Read this first, before any slice.** Single source of truth for the decisions, the architecture,
> the conventions, and the risks. Each `NN-*.md` slice assumes you read this. Format mirrors
> `docs/collection-value/` and `docs/bottle-reviews/`.

> **Approach — camera scan + layered lookup + AI label reading.** Deep research (BoozApp/BAXUS 75k-bottle
> scanner, Whiskybase barcode+label scanner, Drammer's ~20k crowdsourced barcode DB) confirmed scan-to-add
> is the biggest entry-friction reducer VirtualBar lacks. Research caveat: competitor scanning is
> paywalled/flaky and **no licensable spirits barcode catalog exists** — so our answer is layered:
> **(L1) our own crowdsourced bottle data first** (every user save with a barcode enriches lookup for
> everyone), **(L2) the existing external UPC API** as gap-filler, and — for bottles with no barcode hit —
> **(L3) label-photo reading via Claude Vision**, reusing the entire Anthropic plumbing (API key, typed
> HttpClient, daily budget) already shipped for Collection Value.

---

## 1. Goal
Adding a bottle becomes: **point the camera at the barcode** → form pre-fills (name, category, distillery,
age, ABV, volume, image when known) → save; the barcode is **persisted on the bottle** (which also
activates the already-shipped pricing sharpening). No barcode match → **photograph the label** → Claude
Vision extracts the fields → pre-fill. Manual entry stays as the always-available fallback; auto-fill
**never overwrites what the user already typed**.

## 2. What exists today (the gaps this feature closes)
- `GET /api/products/barcode/{barcode}` → `ProductLookupService` (typed `HttpClient`, `ProductLookupOptions
  .LookupUrl?upc=`) → external UPC DB → `BarcodeProductDto { Name, Brand, ImageUrl (downloaded to
  `/uploads/bottles/`), VolumeMl, AbvPercent }`, wrapped by `ProductValidationDecorator`. **Manual text
  input** on the Dashboard add-form calls it and pre-fills name/volume/ABV + links the image after create.
- **Gap 1 — no camera.** The barcode must be typed by hand.
- **Gap 2 — `Bottle.Barcode` is a dead column.** `AddBottleRequest`/`UpdateBottleRequest`/`BottleDto` and
  the frontend payload don't carry it, so it is **never written**. The pricing layer's barcode sharpening
  (`PriceProviderInput`/`PriceSnapshot.Barcode`, UPC in the Claude research prompt) never activates.
- **Gap 3 — no spirits fields.** The UPC DB returns generic retail data: no category, distillery, age,
  vintage, country/region — yet our own `Bottles` table already holds exactly those fields for every
  bottle other collectors saved with the same barcode.
- **Gap 4 — no label path.** A bottle without a (known) barcode still requires full manual entry.

## 3. Verified platform facts
- **Claude Vision (Messages API):** images go as base64 content blocks next to text; **max 5 MB per image**
  (API hard limit); images over **1568 px** on the long edge are downscaled server-side, so the client
  should downscale first (faster upload, fewer tokens); token cost ≈ `(w×h)/750` → ~1.6k tokens at the
  1568 px cap. **No `web_search` tool needed** → cost is token-only (≈ $0.002–0.01/scan depending on
  model) — cheap, but still gated by `Enabled` + the shared daily budget. Single-shot call — no
  `pause_turn` loop (that exists only for server-tool turns).
- **Browser scanning:** the native `BarcodeDetector` API is Chromium-only (no Safari/Firefox) → use
  **`@zxing/browser`** (pure-JS decode over a `getUserMedia` stream; EAN-13/EAN-8/UPC-A/UPC-E). Camera
  requires a **secure context** — `localhost` dev works; production needs HTTPS.
- **Spirits barcodes** are GS1 EAN/UPC: **8–14 digits** (EAN-8, UPC-A/12, EAN-13, ITF-14) — the
  validation rule for both persistence and lookup.

## 4. Locked decisions
1. **Persist the barcode on the bottle** (`AddBottleRequest`/`UpdateBottleRequest`/`BottleDto` + service
   mapping + frontend payload). Format guard (decorator): optional; when provided → trimmed, digits-only,
   length 8–14, else `Fail`. **No migration** — the column already exists.
2. **Internal-first layered lookup, one endpoint.** `GET /api/products/barcode/{code}` stays; the service
   becomes: **L1** aggregate over non-deleted `Bottles` with the same barcode (majority vote per field,
   ties → most recent; primary-image URL reused as-is) → **L2** external UPC API fills only still-empty
   fields (best-effort, errors swallowed); L1 empty → L2 alone (today's behavior); both empty → `NotFound`.
3. **`BarcodeProductDto` is extended additively** (all nullable): `Category`, `DistilleryId`,
   `DistilleryName`, `Age`, `VintageYear`, `Country`, `Region` + `ProductDataSource Source`
   (`Internal`/`External` — new Domain enum, serializes as string, doubles as the UI provenance label).
4. **Label scan = Claude Vision, single-shot, strict JSON.** New `POST /api/products/label-scan`
   (`[Authorize]`, `IFormFile`) → `ILabelScanService.ScanLabelAsync(byte[] imageBytes, string contentType,
   ct)` (Application stays free of ASP.NET types) → base64 image + extraction prompt → JSON
   `{ name, distillery, category, ageYears, vintageYear, abvPercent, volumeMl, country, region,
   confidence }`; **unknown → `null`, never guessed** (the Collection-Value honesty rule).
5. **Sanity-validate every extracted field** server-side (mirror `ProductLookupService.ParseAbv` bounds):
   ABV 5–96, age 1–100, vintage 1900–current year, volume 20–6000 ml, category ∈ `SpiritCategory` else
   dropped to `null`. A result with no `name` **and** no `distillery` → `NotFound` ("could not identify").
6. **Distillery string → `DistilleryId`.** The service matches Claude's distillery name against the ~710
   seeded `Distilleries` (exact → case-insensitive → contains) so the frontend `DistillerySelect`
   pre-fills; no match → name returned as text only.
7. **Reuse the Anthropic plumbing wholesale:** same `Anthropic` options (key/version/base URL), same typed
   `HttpClient` registration pattern as `ClaudeMarketResearchProvider`, and the **shared
   `AnthropicDailyCallBudget.TryConsume()`** (one wallet for all billed Claude calls). New slim
   `LabelScan` config section: `Enabled`, `Model` (empty → falls back to `Anthropic:Model`),
   `MaxImageBytes` (5 MB), `MaxTokens`.
8. **Fail-soft, honest errors.** Disabled feature / budget exhausted → `Fail` ("Label scanning is
   unavailable right now.") → 400 + the form stays manual; unidentifiable label / no barcode match →
   `NotFound` → 404 (mirrors today's barcode 404). Provider/HTTP/JSON errors → log + `NotFound`, never a
   fabricated field.
9. **Auto-fill fills only empty form fields** and shows what came from where (source chip); the user
   reviews before saving. Nothing is written to the DB by scanning itself.
10. **Client downscales the label photo** to ≤ 1568 px long edge, JPEG ~0.85 (canvas) before upload;
    the backend still enforces `MaxImageBytes` + content-type whitelist (`jpeg`/`png`/`webp`). The photo
    is **not stored** server-side — it exists only for the duration of the call.

## 4a. Architecture at a glance
```
DashboardPage add-form
  ├─ BarcodeScannerModal (@zxing/browser, getUserMedia)     ├─ label photo (canvas downscale ≤1568px)
  │        │ decoded EAN/UPC                                 │ multipart IFormFile
  ▼        ▼                                                 ▼
GET /api/products/barcode/{code}                POST /api/products/label-scan
        │ ProductValidationDecorator (format guard)         │ LabelScanValidationDecorator
        ▼                                                    │ (file/size/type, Enabled, budget)
ProductLookupService                                         ▼
  L1: own Bottles by barcode ──────────┐          LabelScanService ── AnthropicDailyCallBudget
      (majority vote + image reuse)    │            │ Claude Vision (base64 + strict-JSON prompt,
  L2: external UPC API (gap-fill)      │            │  sanity bounds, distillery→DistilleryId)
        ▼                              │            ▼
BarcodeProductDto (+ spirits fields, Source)      LabelScanResultDto (+ ScanConfidence)
        └──────────────┬───────────────────────────┘
                       ▼
        pre-fill EMPTY form fields only → user reviews → save
        (barcode persisted on Bottle → activates pricing sharpening)
```

## 5. What already exists (reuse, don't rebuild)
- **`ProductLookupService` + `ProductValidationDecorator` + `ProductsController`** — the lookup slice to
  extend (parsers `ParseVolumeMl`/`ParseAbv`, image-download-to-uploads, `NotFound` shape).
- **`ClaudeMarketResearchProvider` registration** (`DependencyInjection.cs`) — the `AddHttpClient<T>` +
  `x-api-key`/`anthropic-version` header pattern to copy for the vision client.
- **`AnthropicDailyCallBudget`** — singleton `TryConsume()`/`Remaining()`; just consume it.
- **`AnthropicOptions`** (`ApiKey` in `appsettings.Development.json`/user-secrets — never committed).
- **`Distilleries`** (~710 seeded) — the match target for decision §4.6.
- **Frontend:** the Dashboard add-form barcode block (`addBottle.barcode*` i18n keys, `lookupBarcode`
  api fn, prefill + `linkBottleImage` flow), `DistillerySelect`, TanStack Query conventions.
- **Tests:** `FakeHttpHandler` (canned HTTP responses), `ProductLookupServiceTests`,
  `MutableTimeProvider`.

## 6. Backend conventions (from CLAUDE.md — follow exactly)
- `Result<T>` + typed factories; decorator owns ALL guards + `ThrowIfCancellationRequested()`; inner
  service pure; both registered in `DependencyInjection.cs`.
- Primary-constructor DI; `CancellationToken cancellationToken` everywhere; controllers `[Authorize]`,
  full XML docs, `result.Success ? Ok(...) : result.ToActionResult(this)`.
- Migrations ADD-only (none needed here); one blank line between properties; one top-level type per file.
- Mock only `ICurrentUser`, `INotificationService`, `HttpMessageHandler`.

## 7. Risks
- **Vision accuracy / hallucination** — stylized labels, Cyrillic, engraved glass. Mitigate: strict JSON +
  `null`-when-unsure prompt, server-side sanity bounds (§4.5), confidence surfaced in the UI, user reviews
  before save. Never auto-save.
- **Prompt injection via the photographed label/background** — the image is untrusted input. The system
  prompt allows ONLY field extraction into the fixed schema; the service validates shape + bounds and
  ignores everything else; no tools are given to the model.
- **Cost** — bounded: `Enabled` flag, shared daily budget (`TryConsume` BEFORE the HTTP call), client
  downscale, `MaxTokens`. Zero cost when the barcode path answers.
- **Privacy** — the label photo is sent to the Anthropic API (processing only, not stored by us; standard
  API no-training terms). Keep the endpoint `[Authorize]` and don't log image bytes.
- **Barcode DB coverage** — external UPC DBs are patchy for spirits (research caveat: even Whiskybase's
  scanner is paywalled and flaky). L1 grows with every user save — the moat compounds.
- **Camera UX** — permission denial / no camera / insecure context → the modal falls back to the existing
  manual input with a clear message. `getUserMedia` needs HTTPS in production.
- **Internal image reuse** — L1 returns another user's uploaded image URL; upload files are never
  physically deleted today, so links stay valid. Copy-on-link hardening is **parked**.

## 8. Open questions (decide during build)
- **Store the label photo as the bottle's image** when the user accepts the scan? (Default **no** for MVP —
  photo is transient; revisit as a checkbox later.)
- **Model for label scan** — `claude-haiku-4-5` (cheapest, likely sufficient for print extraction) vs
  reusing `Anthropic:Model` (Sonnet). Default: empty → `Anthropic:Model`; measure and downgrade.
- **Separate daily budget for scans** vs the shared wallet (default: shared — one knob).
- **Crowdsourced catalog phase 2** — a dedicated `ProductCatalog` read-model (barcode → canonical fields,
  built from saves) instead of live aggregation, once volume justifies it. **Parked.**

## 9. Slice index, dependencies & order
| Slice | Doc | Depends on |
|---|---|---|
| 1 | `01-persist-barcode.md` (bottle DTOs + mapping + format guard) | — |
| 2 | `02-internal-first-lookup.md` (L1 internal + L2 merge, richer DTO) | 1 |
| 3 | `03-label-scan.md` **(the core — Claude Vision + guardrails)** | — (parallel to 1–2) |
| 4 | `04-frontend.md` (scanner modal, label capture, prefill, i18n bg+en) | 1, 2, 3 |
| 5 | `05-backend-tests.md` **(ALL backend unit tests — written last)** | 1–3 |

**Execution order (tests last):** backend slices **1→2→3 with build-only gates (no tests yet)**, then the
frontend slice **4**, then **all** backend unit tests in **slice 5** (4 and 5 may swap freely).

## 10. Verification protocol (REQUIRED)
- **During each backend slice (1–3):** `dotnet build VirtualBar.Api/VirtualBar.Api.csproj --no-restore -v q`
  → **0 errors**. Each slice lists **Test targets** — record them, but **do NOT write tests yet**.
- **Slice 5:** write/extend `ProductLookupServiceTests`, `LabelScanServiceTests`, `BottleServiceTests` —
  **100% branch** for every new/changed service method — then
  `dotnet test VirtualBar.Tests/VirtualBar.Tests.csproj` → `Failed: 0`.
- **Frontend slice (4):** `npm --prefix VirtualBar.Web run build` clean + exercise in **bg + en** (camera
  path needs a device/`localhost`; keep the manual-input path demonstrably intact).
- Do **not** commit unless the user asks.

## 11. Docs & CLAUDE.md (final step, after slice 5)
Update `CLAUDE.md`: the new `POST /api/products/label-scan` route + enriched barcode lookup under a
"Products / scanning" note, the `LabelScan` config section, the `Bottle.Barcode` persistence note
(no longer pricing-only), and the new i18n keys.
