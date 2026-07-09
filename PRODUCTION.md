# Production Checklist

Things to do before going to production.

---

## 1. JWT Secret Key

`Jwt:Key` is not set in `appsettings.json` — add it in production config or as an environment variable.

```json
"Jwt": {
  "Key": "<at least 32 random characters>",
  "Issuer": "VirtualBar",
  "Audience": "VirtualBarClient"
}
```

**Generate one:**
```powershell
[Convert]::ToBase64String((1..32 | ForEach-Object { Get-Random -Maximum 256 }) -as [byte[]])
```

> Never commit the real key. Use an environment variable or secrets manager.

---

## 2. Email (SMTP)

Fill in `appsettings.Production.json` (or environment variables):

```json
"Email": {
  "SmtpHost": "smtp.gmail.com",
  "SmtpPort": 587,
  "Username": "your-gmail@gmail.com",
  "Password": "<Google App Password>",
  "FromAddress": "your-gmail@gmail.com",
  "FromName": "VirtualBar",
  "FrontendBaseUrl": "https://your-domain.com"
}
```

**Google App Password:** Google Account → Security → 2-Step Verification → App passwords.

> For higher volume (>500 emails/day) — migrate to Resend, SendGrid, or Mailgun; only `SmtpHost`, `SmtpPort`, `Username`, `Password` change.

---

## 3. Database Connection String

```json
"ConnectionStrings": {
  "Default": "Server=<host>;Database=VirtualBar;User Id=<user>;Password=<pass>;TrustServerCertificate=True"
}
```

> Never commit a connection string with real credentials.

---

## 4. Admin User

In `appsettings.Production.json`:
```json
"AdminEmail": "admin@your-domain.com"
```

Register the user normally — on next startup they automatically receive `IsAdmin = true`.

---

## 5. CORS

In `Program.cs` CORS is hardcoded to `http://localhost:5173`. Change to the real domain:

```csharp
policy.WithOrigins("https://your-domain.com")
```

Or move it to `appsettings.json`:
```json
"AllowedOrigins": [ "https://your-domain.com" ]
```

---

## 6. HTTPS

Add to `Program.cs` after `app.Build()`:

```csharp
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
    app.UseHsts();
}
```

---

## 7. AllowedHosts

Currently `"*"` in `appsettings.json`. Change to the specific domain:

```json
"AllowedHosts": "your-domain.com"
```

---

## 8. Uploads Directory

Uploaded images are stored in `wwwroot/uploads/`. In production this directory must be:
- Outside the deploy directory (so it is not wiped on every deploy), **or**
- A mounted volume / blob storage (Azure Blob, AWS S3)

> When deploying with Docker: mount `/app/wwwroot/uploads` as a persistent volume.

---

## 9. Frontend Build

```bash
cd VirtualBar.Web
npm run build
```

Output is in `VirtualBar.Web/dist/`. Serve via:
- Static hosting (Netlify, Vercel, Azure Static Web Apps), **or**
- Nginx in front of the .NET API

Make sure the Vite proxy config (`vite.config.ts`) points to the production API URL.

---

## 10. Serilog — Log Files

In production, log files land in `logs/` next to the binary. Make sure:
- The directory has write permissions
- Log aggregation or monitoring is in place (e.g. Seq, ELK, Azure Monitor)

---

## 11. Password Reset Token Lifetime

ASP.NET Core Identity generates reset tokens valid for **1 day** by default. To change:

```csharp
builder.Services.Configure<DataProtectionTokenProviderOptions>(options =>
    options.TokenLifespan = TimeSpan.FromHours(2));
```

---

## 12. ASPNETCORE_ENVIRONMENT

```
ASPNETCORE_ENVIRONMENT=Production
```

In Production: OpenAPI/Scalar UI are disabled automatically (see `Program.cs`).

---

## 13. Anthropic — Collection Value price research

The Collection Value feature calls the **Anthropic Messages API** (`web_search` tool) to research indicative bottle prices. Before enabling it in production:

- **API key:** set `Anthropic:ApiKey` via environment variable / user-secrets (it is empty in `appsettings.json` — never commit it).
  ```json
  "Anthropic": { "ApiKey": "<your key>", "Model": "claude-sonnet-4-6", "UseProviderStats": true }
  ```
- **Enable web search** for the organisation in the Anthropic Console — otherwise the tool call fails soft (no estimate, logged).
- **Cost guardrails** (all in `appsettings.json`): `Anthropic:DailyCallBudget` caps billed calls per UTC day; `Pricing:SnapshotTtlDays` (5-day cache) + `Pricing:PreWarmTopNBottles` bound total spend. Tune to your budget.
- **Kill switches:** `Anthropic:UseProviderStats` toggles the Claude provider entirely; `Pricing:RefreshEnabled` toggles the pre-warm background job.
- **Legal:** estimates are indicative and always display their citations (Anthropic requirement). Get legal sign-off before a public/commercial launch.
