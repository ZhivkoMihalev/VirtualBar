# CLAUDE.md

This file provides guidance to Claude Code when working with code in this repository.

---

## What is VirtualBar

VirtualBar is a platform for collectors of premium spirits (whisky, rum, cognac, vodka and more).
- Every user gets a **virtual bar** — a public profile showcasing their collection.
- Others can browse collections, follow collectors, like and comment on bottles, leave **structured reviews** (0–100 score + tasting notes + flavor tags), send direct messages, and buy/sell limited editions through a built-in marketplace.
- The home page (`/`) is a social news feed showing admin-authored articles and activity from followed users.
- Users receive **in-app notifications** when someone likes/comments their bottle, follows them, sends a message, or when someone they follow adds a bottle or lists one for sale.
- Users can add bottles to a **wish list** and get notified when a matching bottle appears for sale.
- Any user can **make an offer** on any bottle in another collector's bar (regardless of whether it is listed for sale); the seller receives a notification and can accept, decline, or ignore.
- Collectors earn **permanent achievement badges** for milestones (first bottle, 10 bottles, 5 categories, 50 likes received, first sale…) — shown with progress on the own profile and as an earned-only strip on the public bar.
- The platform maintains an admin-curated **product catalog** of canonical spirits: adding a bottle links it to a catalog product (explicit pick or silent exact-match auto-link).
- An unknown bottle silently files a **product request** the admin approves (creating the product and retro-linking identical bottles) or rejects — the add flow is never blocked.
- Anyone, logged in or not, can browse that catalog at **`/catalog`** and open a **product page** (`/product/:id`) that gathers every collector's bottle of it: the community score and top flavors, the owners, the current listings and all the reviews, computed on read.
- Direct messaging is handled via a **floating chat widget** (Facebook Messenger style) accessible from every page.
- A forgotten password is reset through a link sent by email.

**User roles:** Collector (all registered users). Platform administrators have `IsAdmin = true` on their `AppUser` record — seeded via `AdminEmail` in `appsettings.Development.json` on startup.

---

## Solution Structure

```
VirtualBar/
├── VirtualBar.Domain          # Entities, enums — zero outward dependencies
├── VirtualBar.Application     # Interfaces, DTOs, Result<T>
├── VirtualBar.Infrastructure  # EF Core, service implementations
├── VirtualBar.Api             # ASP.NET Core controllers, Program.cs
├── VirtualBar.Tests/          # service, integration and persistence tests (folders: .claude/rules/testing/backend.md)
├── VirtualBar.HttpTests/      # the whole API over HTTP on SQL Server (folders: .claude/rules/testing/http.md)
├── VirtualBar.Web/            # React + Vite 8 + Tailwind CSS 4 frontend
├── VirtualBar.Web.Tests/      # frontend tests — its own npm workspace, depends on VirtualBar.Web
│   ├── unit/                  # Vitest + React Testing Library (jsdom); mirrors VirtualBar.Web/src
│   └── e2e/                   # Playwright specs, run against the real stack
├── docs/
│   └── features/              # feature specs, one folder per feature (00-OVERVIEW.md first, then numbered slices)
├── .claude/
│   ├── rules/                 # project rules, one topic per file (see CRITICAL RULES)
│   ├── agents/                # custom agents: implementers, reviewers, testers
│   └── skills/                # project skills
└── package.json               # npm workspaces root: VirtualBar.Web + VirtualBar.Web.Tests, one lockfile

**Target framework:** .NET 9 (`net9.0`)
```

The frontend is two **npm workspaces** with one root lockfile; how the test workspace uses the app's packages:
`.claude/rules/frontend/conventions.md`.

**Dependency rule:** `Api → Application ← Infrastructure`. Domain has no outward dependencies.

**Feature docs:** `docs/features/<feature>/` holds the specs of each feature we built or planned, one folder per
feature: `00-OVERVIEW.md` first, then the numbered slices (`01-…`, `02-…`). A new feature's specs go into a new folder
there. Documents about the whole project (`PRODUCTION.md`) stay in the repo root; project rules go to `.claude/rules/`.

---

## Build & Run Commands

### Backend

```bash
# Build
dotnet build VirtualBar.Api/VirtualBar.Api.csproj --no-restore -v q
# Expected: 0 Error(s). Pre-existing warnings OK; new warnings are not.

# Run the API (auto-applies migrations on startup)
cd VirtualBar.Api && dotnet run
# API: http://localhost:5000  |  OpenAPI: http://localhost:5000/openapi/v1.json (development only)

# Add EF Core migration
dotnet ef migrations add <Name> --project VirtualBar.Infrastructure --startup-project VirtualBar.Api

# Apply migrations manually
dotnet ef database update --project VirtualBar.Infrastructure --startup-project VirtualBar.Api
```

### Frontend

Run from the **repo root** (each script forwards to its workspace):

```bash
npm install        # first time only — installs both workspaces into the root node_modules
npm run dev        # VirtualBar.Web dev server → http://localhost:5173
npm run lint       # ESLint in both workspaces — must report 0 problems (CI blocks on it)
npm test           # Vitest unit + component tests (VirtualBar.Web.Tests/unit) — must report 0 failed
npm run typecheck  # tsc over VirtualBar.Web.Tests (unit + e2e + configs)
npm run build      # VirtualBar.Web production build — must output "✓ built in ..."
npm run test:e2e   # Playwright (VirtualBar.Web.Tests/e2e) — needs the API running, see `.claude/rules/ci/e2e.md`
```

Inside a workspace folder the same scripts work too (`cd VirtualBar.Web && npm run dev`; in `VirtualBar.Web.Tests`
also `npm run test:watch` and `npm run test:e2e:ui` / `test:e2e:debug` / `test:e2e:report`).

The Vite dev server proxies `/api` and `/uploads` to `http://localhost:5000`.

### Tests

```bash
# Full suite
dotnet test VirtualBar.Tests/VirtualBar.Tests.csproj --verbosity minimal

# One service class only
dotnet test VirtualBar.Tests/VirtualBar.Tests.csproj \
  --filter "FullyQualifiedName~BottleServiceTests" --verbosity minimal

# Branch coverage report
dotnet test VirtualBar.Tests/VirtualBar.Tests.csproj \
  --collect:"XPlat Code Coverage" --results-directory ./coverage-results
```

Expected output: `Failed: 0`. The `coverage-results/` directory is gitignored.

```bash
# HTTP tests: the whole API on SQL Server (about 30 s; the first host migrates and seeds a fresh database)
dotnet test VirtualBar.HttpTests/VirtualBar.HttpTests.csproj --verbosity minimal
```

They need no setup: the server comes from `VIRTUALBAR_TEST_SQL` if set, else from the API's own
`appsettings.Development.json`. On a machine where no server answers, the SQL tests are **skipped** with the reason
(the startup-guard tests still run); in CI (`CI=true`) they **fail** instead, so a missing server never passes as green.
See `.claude/rules/testing/http.md`.

---

## CRITICAL RULES — Never Violate

- `CancellationToken` parameter is always named `cancellationToken`. Never `ct`, never `token`.
- Never delete or modify database records without asking the user first.
  - EF Core migrations that ADD schema are fine.
  - For any `DELETE`, `UPDATE`, `ExecuteDeleteAsync`, `ExecuteUpdateAsync`, or drop-column migration — stop and ask.
  - For migrations the **Migration guard** CI check enforces this: it blocks the PR until it carries the
    `db-destructive-approved` label.
- After every backend change: run `dotnet build` (0 errors required) and the relevant test class.
- After every frontend change: run, from the repo root, `npm run lint` (0 problems — CI blocks on it), `npm test` (0 failed) and `npm run build` (clean output required).
- Every new service method needs 100% branch coverage in tests.

More project rules live in **`.claude/rules/`**, one topic per file (kebab-case name), in folders by area. **Every new
project rule goes there as a file of its own, not into this file**, and is added to the list below.
- A file without `paths` frontmatter loads in every session, with the same weight as this file.
- A file with `paths` loads only when Claude reads, writes or edits a matching file (also `cat` or `head` on one file),
  not when it only searches with Grep or Glob. The patterns are relative to the repo root, and each one is also tried
  against every folder on the path: `**/badges*` loads `domain/badges.md` for `docs/features/badges/00-OVERVIEW.md`.
- A new rule gets `paths`, unless it is needed when no code file is open: planning, writing an issue, opening a PR.
- A reference to a rule with `paths` names its file (`.claude/rules/testing/http.md`), never just its section: the
  section is not in context until the file loads.
- Custom agents in `.claude/agents/` receive them too, the rules with `paths` included (checked in #70). A subagent gets
  the always-loaded files as its session loaded them at start: a rule changed during a session reaches its subagents
  only from the next session.
- After a change, the **`document-repository`** skill (`/document-repository`) updates the documentation it touches and
  checks it against the code; `documentation.md` holds the conventions it follows.

Current rules. **Always loaded:** the first three, `backend/result-pattern.md` and `backend/validation-decorators.md`.
Every other file has `paths` for the code it describes.
- `bug-issues.md`: every bug found gets its own bug issue right away.
- `regression-tests.md`: every bug fix comes with a test that fails without the fix, so the bug cannot come back.
- `issue-status.md`: the `status: …` labels, what each status means, and `Part of #N` (never `Closes #N`) in a PR.
- `documentation.md`: where documentation goes, `paths`, how a rule file is written; the `document-repository` skill applies it.
- `backend/`: `result-pattern.md`, `validation-decorators.md`, `conventions.md` (ownership check, DI, registration, `CancellationToken`, naming, code style, XML docs, admin), `middleware.md`, `startup.md`.
- `database/`: `app-db-context.md` (global delete behaviour and UTC rules, the DbSets).
- `domain/`: one file per feature, with its entities, endpoints and design: `users.md`, `bottles.md`, `reviews.md`, `follows.md`, `messages.md`, `news.md`, `notifications.md`, `distilleries.md`, `wish-list.md`, `offers.md`, `pricing.md` (with Collection Value), `badges.md`, `catalog.md`.
- `frontend/`: `conventions.md` (auth, API layer, data, types, contexts, i18n, app shell and theme), `routing.md`, `shared-components.md`.
- `testing/`: `backend.md` (service and integration tests), `http.md`, `frontend-unit.md`, `e2e.md`.
- `ci/`: `workflows.md`, `migration-guard.md`, `e2e.md`, `codeql-and-dependabot.md`.

---

## Security Rules

- All controllers require `[Authorize]`. Public endpoints (browse public bars, view bottle details) use `[AllowAnonymous]` explicitly, and are listed in `AnonymousEndpoints` in `VirtualBar.HttpTests/Access/EndpointAccessTests.cs` (see `.claude/rules/testing/http.md`).
- Every mutation on a user-owned resource checks `resource.UserId == currentUser.UserId`.
- `appsettings.Development.json` is gitignored. Connection strings and JWT keys never go in committed files.
- Only EF Core LINQ queries — no raw SQL. If raw SQL is ever added, parameterize it.
- `DeleteBehavior.Restrict` globally. Cascade only where explicitly intentional.
