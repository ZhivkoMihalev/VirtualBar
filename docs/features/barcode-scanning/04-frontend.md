# 04 — Frontend (scanner modal, label capture, prefill)

> Depends on: **01, 02, 03** · Read `00-OVERVIEW.md` first.

## Goal
The Dashboard add-bottle form gains two camera flows — **scan barcode** and **photograph label** — both
pre-filling **only empty fields**, with source/confidence chips and the manual path fully intact.
Speakeasy styling; module-level `CSSProperties` constants.

## Build / extend
1. **Dependency:** `npm install @zxing/browser` (+ its `@zxing/library` peer). No other new deps —
   downscaling is plain `<canvas>`.
2. **Types (`src/types/index.ts`):** `BarcodeProduct` += `category?`, `distilleryId?`, `distilleryName?`,
   `age?`, `vintageYear?`, `country?`, `region?`, `source: 'Internal' | 'External'`;
   new `LabelScanResult` (mirror the DTO, camelCase, `confidence: 'Low' | 'Medium' | 'High'`);
   (`barcode` on `Bottle`/`AddBottlePayload` landed in slice 01).
3. **API (`src/api/bottlesApi.ts` or new `productsApi.ts`):** keep `lookupBarcode`; add
   `scanLabel(file: Blob): Promise<LabelScanResult>` — `FormData` with `image`, POST
   `/products/label-scan`.
4. **`BarcodeScannerModal.tsx`** (new component):
   - `BrowserMultiFormatReader.decodeFromVideoDevice` over a `<video>` element (prefer the environment/
     rear camera), restricted to EAN-13/EAN-8/UPC-A/UPC-E; first successful decode → stop the stream,
     close, `onDecoded(code)`.
   - Always `reader.reset()` + stop all tracks on close/unmount (camera LED must go off).
   - Permission denied / no camera / insecure context → inline message + the modal closes back to the
     manual input (overview §7 camera UX). Feature-gate the trigger button on
     `navigator.mediaDevices?.getUserMedia`.
5. **Label capture:** `<input type="file" accept="image/*" capture="environment">` (mobile opens the
   camera, desktop a file picker) → canvas downscale to ≤ **1568 px** long edge, JPEG quality ~0.85
   (overview §4.10) → `scanLabel(blob)` with a spinner.
6. **Dashboard add-form wiring:**
   - "Сканирай баркод" button next to the existing barcode input → modal → decoded code lands in the
     input → the existing `handleBarcodeSearch` runs automatically.
   - Prefill (both flows): write **only into empty/untouched fields** (overview §4.9) — name, category,
     distillery (`DistillerySelect` via `distilleryId`; fallback: leave text hint), age, vintage, ABV,
     volume, country, region; keep the scanned `barcode` in state and **include it in the create
     payload**; image behavior unchanged (existing `linkBottleImage` after create).
   - Status chips: source (`Internal` → "от колекциите" / `External` → "от базата") for barcode hits;
     confidence (`Low`/`Medium`/`High`) for label scans; the existing found/error chips stay.
   - Label-scan errors: 400 (disabled/budget/file) → show the API message + stay manual; 404 → "не
     разпознахме етикета" hint.
7. **i18n (`bg.json` + `en.json`, extend `addBottle`):** `scanBarcode`, `scanning`, `cameraDenied`,
   `cameraUnavailable`, `scanLabel`, `labelScanning`, `labelScanFailed`, `labelScanUnavailable`,
   `prefilledFrom`, `sourceInternal`, `sourceExternal`, `confidenceLow/Medium/High` (reuse the existing
   `barcode*` keys for the input/find/success/notFound texts).

## Gate
`npm --prefix VirtualBar.Web run build` → clean "✓ built in ..."; exercise in **bg + en** on `localhost`
(secure context): scan a real barcode with the device camera → lookup fires → empty-field prefill +
source chip; photograph a bottle label → fields + confidence chip → save → bottle persists **with the
barcode**; deny camera permission → graceful fallback to manual input; zero console errors.
