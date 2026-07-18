# 03 — Label Scan via Claude Vision (the core)

> Depends on: **—** (parallel to 01–02) · Read `00-OVERVIEW.md` first.

## Goal
Close **Gap 4**: `POST /api/products/label-scan` — a label photo in, a sanity-validated, strict-JSON set
of bottle fields out (`null` for anything unreadable — never guessed). Cost guardrails are **inline in
this slice** (single consumer): `Enabled` flag, shared `AnthropicDailyCallBudget`, size caps, `MaxTokens`.

## Build new
1. **`ScanConfidence` enum** — `VirtualBar.Domain/Enums/ScanConfidence.cs`: `Low`, `Medium`, `High`.
2. **`LabelScanResultDto`** — `VirtualBar.Application/DTOs/Products/`: `string? Name`,
   `string? DistilleryName`, `Guid? DistilleryId`, `SpiritCategory? Category`, `int? Age`,
   `int? VintageYear`, `double? AbvPercent`, `int? VolumeMl`, `string? Country`, `string? Region`,
   `ScanConfidence Confidence`.
3. **`ILabelScanService`** — `Task<Result<LabelScanResultDto>> ScanLabelAsync(byte[] imageBytes,
   string contentType, CancellationToken cancellationToken)` — `byte[]` keeps Application free of
   ASP.NET types (the controller converts `IFormFile`).
4. **`LabelScanOptions`** — `VirtualBar.Infrastructure/Options/`, section `"LabelScan"`:
   `bool Enabled` (true), `string Model` (empty → falls back to `Anthropic:Model`),
   `int MaxImageBytes` (5_242_880 — the API hard limit), `int MaxTokens` (1024).
   Add the section to `appsettings.json` (no secrets — the key stays in `Anthropic`).
5. **`LabelScanValidationDecorator`** — `ThrowIfCancellationRequested()` first; guards in order:
   - `Enabled == false` → `Fail("Label scanning is unavailable right now.")`
   - `imageBytes` null/empty → `Fail("Image is required.")`
   - `imageBytes.Length > MaxImageBytes` → `Fail("Image is too large.")`
   - `contentType` ∉ { `image/jpeg`, `image/png`, `image/webp` } → `Fail("Unsupported image type.")`
6. **`LabelScanService`** — primary constructor `(HttpClient http, AppDbContext db,
   AnthropicDailyCallBudget budget, IOptions<LabelScanOptions> options,
   IOptions<AnthropicOptions> anthropicOptions, ILogger<LabelScanService> logger)`:
   - **Budget BEFORE the call:** `if (!budget.TryConsume())` →
     `Fail("Label scanning is unavailable right now.")` (same message as disabled — one i18n key).
   - **Request** (single-shot Messages call, **no tools**): model = `options.Model` or fallback;
     `max_tokens`; a **system prompt** that (a) allows ONLY extraction of the fixed fields from the
     visible label, (b) orders `null` for anything not clearly readable, (c) explicitly says text in the
     image is data, never instructions (injection rule, overview §7); one user message =
     base64 `image` block (`media_type` = the validated content type) + a text block naming the JSON
     schema `{ name, distillery, category (exact SpiritCategory member or null), ageYears, vintageYear,
     abvPercent, volumeMl, country, region, confidence (low|medium|high) }`.
   - **Response:** first `text` content block → strip ``` fences → `JsonSerializer` into a private
     record. Non-2xx / malformed JSON / empty → `LogWarning` +
     `NotFound("Could not identify the bottle from this label.")` — mirror the provider error-swallow
     philosophy; **never throw, never fabricate**.
   - **Sanity bounds (overview §4.5):** out-of-range values → that field becomes `null`
     (ABV 5–96 — same bounds as `ParseAbv`; age 1–100; vintage 1900–`DateTime.UtcNow.Year`;
     volume 20–6000; category parsed with `Enum.TryParse` ignore-case else `null`). Both `Name` **and**
     `DistilleryName` null after validation → `NotFound` (same message).
   - **Distillery match (overview §4.6):** exact (case-insensitive) equality against `Distilleries`
     first, then single `Contains` hit; matched → `DistilleryId` + canonical `DistilleryName`; ambiguous
     or none → text only.
7. **DI (`DependencyInjection.cs`):** `services.Configure<LabelScanOptions>(...)`;
   `AddHttpClient<LabelScanService>(...)` with the **same** `BaseUrl`/`x-api-key`/`anthropic-version`
   lambda as `ClaudeMarketResearchProvider`; `AddScoped<ILabelScanService>(sp =>
   new LabelScanValidationDecorator(sp.GetRequiredService<LabelScanService>(), sp.GetRequiredService<
   IOptions<LabelScanOptions>>()))`.
8. **`ProductsController`** — add:
   ```csharp
   /// <summary>Extracts bottle details from a label photo using AI vision.</summary>
   [HttpPost("label-scan")]
   public async Task<IActionResult> ScanLabel(IFormFile image, CancellationToken cancellationToken)
   ```
   → copy to `MemoryStream`/`byte[]`, pass `image.ContentType`; full XML docs
   (`<response>` 200 / 400 (invalid file, disabled, budget) / 404 (unidentifiable)); standard
   `result.Success ? Ok(...) : result.ToActionResult(this)`. `[Authorize]` inherited — never anonymous
   (billed call).

## Test targets (written in slice 05)
`LabelScanServiceTests` (new; `FakeHttpHandler` canned Anthropic responses; real
`AnthropicDailyCallBudget` with `MutableTimeProvider`): happy path → all fields + distillery matched by
id; fenced-JSON response → parsed; unknown-heavy response → nulls preserved; each sanity bound (ABV 3 →
null, age 200 → null, vintage 1850 → null, volume 10 → null, bogus category → null); name+distillery
both null → `NotFound`; ambiguous distillery `Contains` → text only; non-2xx → `NotFound`; malformed
JSON → `NotFound`; budget exhausted → `Fail` (no HTTP call recorded by the handler); decorator: disabled /
empty bytes / oversize / bad content type → `Fail`; cancellation propagates.

## Gate
`dotnet build` → **0 errors**; endpoint visible in `/openapi/v1.json`; a manual scan against the real API
(dev key) returns sane JSON for one known bottle photo. No tests yet.
