---
paths:
  - "VirtualBar.HttpTests/**"
  - "VirtualBar.Api/**"
---

# HTTP tests (`VirtualBar.HttpTests`)

A project of its own, because it is the only backend test layer that needs SQL Server: `VirtualBar.Tests` stays
self-contained and fast. These tests start the real `Program.cs` in memory (`WebApplicationFactory<Program>`; hence
`public partial class Program { }` at the end of `Program.cs`) and send it real HTTP requests, so they cover what the
service tests cannot: DI wiring, middleware, `[Authorize]`/`[AllowAnonymous]`, model validation, JSON shape, status
codes, JWT validation and the startup guards. They are not a place for branch coverage: that stays in `VirtualBar.Tests`.
- **SQL Server, not InMemory/SQLite:** startup applies the SQL Server migrations, and the app relies on filtered unique
  indexes and the case-insensitive collation.
- **One throwaway database per run** (`VirtualBar_HttpTests_<guid>`, `Support/ApiHosts.cs`): the first host's startup
  migrates and seeds it exactly as production does (about 16 s), and it is dropped at the end. It is never the
  development database: only the server is taken from the connection string. Tests keep apart by registering
  collectors with unique emails (`AuthApi.NewEmail`), as the E2E specs do.
- **Every class joins `[Collection(ApiCollection.Name)]`** and the assembly runs one test at a time: the classes share
  `ApiHosts`, and hosts built in parallel can catch each other's startup.
- **Hosts per configuration**, each started once on first use: `ConfirmationOn` (production behaviour),
  `ConfirmationOff` (Development / E2E behaviour), `EmailUnreachable` (confirmation on, SMTP refuses every connection).
  A new configuration is a new lazy host in `ApiHosts`, not a new database.
- **Settings go in through `UseSetting`** (`Support/ApiFactory.cs`), never `ConfigureAppConfiguration`: Program.cs reads
  `Jwt:Key`, `Email:PickupDirectory` and `Identity:RequireConfirmedEmail` before `Build()`, and only `UseSetting` reaches
  it that early. The defaults match the E2E workflow: emails to a temporary pickup folder (`Mailbox` reads them back
  with MimeKit), no Claude calls, no price pre-warm, auth rate limit raised, a random `Jwt:Key`.
- **A temporary web root:** `ApiFactory` also sets `webroot` to a folder of its own (`ApiFactory.WebRoot`), created
  before the host starts (static files are served only from a web root that exists at startup) and deleted with the
  factory. Every upload lands there, never in `VirtualBar.Api/wwwroot`; a test finds an uploaded file under it.
- **Collectors through the API:** `Collector.RegisterAsync(api, purpose)` registers a collector on `ConfirmationOff`
  and returns their id, email and an `HttpClient` that sends their token. `CollectorApi` sets data up the way the
  frontend does (`AddBottleAsync`, `MakeOfferAsync`, `RequestProductAsync`, …): each call asserts it succeeded and
  returns the new id, so a test states only what it is about.
- **Admins:** `Collector.RegisterAdminAsync` sets `IsAdmin` through the app's own `AppDbContext` (`factory.Services`)
  and logs in again, because the `isAdmin` claim is written into the token at login. `AdminEmail` cannot be used: it is
  read only at startup, before the test user exists.
- **`[SqlServerFact]`** (or **`[SqlServerTheory]`** for a data-driven test) for anything that touches the database;
  plain `[Fact]` only for tests that never get past the startup guards. Both skip for the same reason
  (`TestSqlServer.SkipReason`), never in CI.
- **Assert the wire format:** read bodies as `JsonElement` (`AuthApi.JsonAsync`), whose `GetProperty` is
  case-sensitive, so a test also pins the camelCase names and the `{ error, reason }` shape the frontend reads.
- **The access matrix** (`Access/EndpointAccessTests.cs`) reads every controller endpoint from the running app's
  `EndpointDataSource`. The endpoints a visitor may call without a token are written out in its `AnonymousEndpoints`
  list, and the test fails until the attributes and the list agree. So a new `[AllowAnonymous]` endpoint, or a removed
  one, goes into that list in the same change, as a deliberate decision. Every other endpoint must answer 401 without a
  token, and every listed one must answer without 401 or a 5xx. Requests carry placeholder route values and a body in
  the content type the endpoint accepts: routing checks that before authentication, so an upload endpoint answers a
  JSON body with 415, not 401. The OpenAPI and Scalar endpoints (Development only) are not part of the matrix.
- **Other collectors' resources** (`Access/OwnershipTests.cs`): one test per action that changes a user-owned resource,
  called by another collector. Each pins the refusal the API gives today (403; `ReorderBottles` 404, since it only sees
  the caller's own bottles) with `CollectorApi.AssertRefusedAsync`, which also checks the `{ error }` body, so a bare
  framework 404/403 cannot pass for it. A new mutating action on a user-owned resource gets a test here. A success or a
  500 is a bug: file it (`bug-issues.md`), never adjust the test to it.
- **Admin-only actions** (`Access/AdminTests.cs`): each one twice, 403 for a collector and success for an admin.
- **Uploads** (`Uploads/UploadTests.cs`): one theory per case over the four upload endpoints. A PNG sent under an
  `.html` file name is saved as `.png` in the temporary web root and served under `/uploads`, since the extension comes
  from the checked content type, never the file name. An SVG, or a file one byte over the endpoint's limit (10 MB for
  bottle images and news covers, 5 MB for avatars and wishes), answers 400 and leaves no file behind. A new upload
  endpoint joins the `Endpoint` enum.
- **Flows** (`Flows/`): a chain across users and services end to end over HTTP, one token per collector, like
  `VirtualBar.Tests/Integration` at service level. `MarketplaceFlowTests` lists a bottle, makes an offer and accepts it,
  then checks both collectors' notifications and their `FirstSale` / `FirstPurchase` badges.
- Method naming as in the service tests: `<Endpoint>_When<Condition>_<ExpectedOutcome>`.
