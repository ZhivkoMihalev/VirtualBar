---
paths:
  - ".github/**"
  - "VirtualBar.Web.Tests/e2e/**"
  - ".claude/launch.json"
---

# E2E

`.github/workflows/e2e.yml` (the **E2E** workflow) runs the Playwright suite in `VirtualBar.Web.Tests/e2e` on Chromium
against the real stack:
- SQL Server 2022 in a service container;
- the API in `Development` on a fresh database (migrations + seeders run on startup);
- the Vite dev server of `VirtualBar.Web` that `VirtualBar.Web.Tests/playwright.config.ts` starts.

## Two runs (#47)
The job is a matrix over `confirmation: ['off', 'on']`, so a PR shows two checks, **E2E (confirmation off)** and
**E2E (confirmation on)**. Each has its own SQL Server, API and Vite, on the same ports; `fail-fast: false`, so one
failing does not cancel the other.
- **confirmation off:** the API with `Identity:RequireConfirmedEmail=false`, and every test except the ones tagged
  `@confirmation-on`. The suite registers collectors and logs them straight in, which needs confirmation off.
- **confirmation on:** the API with `Identity:RequireConfirmedEmail=true`, as production runs it, and only the tests
  tagged `@confirmation-on` (`email-confirmation-on.spec.ts`): register signs nobody in and login is refused with
  `EmailNotConfirmed`, the link in the registration email confirms the account, and "send a new link" after a refused
  login sends a link that works. Nothing is rewritten in the browser.
- `playwright.config.ts` picks the tests from `E2E_CONFIRMATION`: `on` sets `grep` to the tag, anything else sets
  `grepInvert`, so a plain `npx playwright test` never runs the tagged tests against an API with confirmation off.
- `email-confirmation.spec.ts` keeps the `/confirm-email` page's own branches (a wrong or incomplete link, a retried
  failure), which work with confirmation off. Its two tests that rewrote the register and login answers were dropped
  for the real ones.
- The confirmation specs read the emails through `e2e/mail.ts` (`findConfirmationLink`, `waitForConfirmationLink`).

## When it runs
- It is too slow for every PR, so it runs nightly on `master` (02:17 UTC), on demand (Actions → E2E → Run workflow),
  and on PRs labelled **`e2e`**.
- On a PR it starts when the `e2e` label is added, on a push and on a reopen, never on `opened`. A PR opened with the
  label gets a `labeled` event at the same moment, and an `opened` run would cancel it or be cancelled by it in the
  shared concurrency group, leaving a cancelled E2E check that greys out the merge button (#105).
- Adding any other label starts a run in a concurrency group of its own, whose job is skipped.
- It is therefore not a required check.

## CI-only config overrides
Set as environment variables in the workflow:
- `Identity:RequireConfirmedEmail`: `false` in the "off" run (the tests log new users straight in), `true` in the "on"
  run.
- `Email:PickupDirectory`: every email is written there as an `.eml` file instead of sent. The confirmation specs read
  the same directory through the `E2E_MAIL_DIR` environment variable and open the link in the email.
- `RateLimiting:AuthPermitLimit=10000`: `Program.cs` defaults to 10 auth requests a minute per IP, which the suite
  exceeds from one machine.
- `Anthropic:UseProviderStats=false` and `Pricing:RefreshEnabled=false`.

Each run uploads its Playwright HTML report (traces + screenshots of failures) and its API log as artifacts, named
after the run: `playwright-report-confirmation-off`/`-on` and `api-log-confirmation-off`/`-on`.

## Running it locally
- Start the API with the same overrides: the **`api-e2e`** configuration in `.claude/launch.json` passes them as
  command-line arguments and writes the emails to `VirtualBar.Web.Tests/.e2e-mail/` (gitignored).
- It uses the database of `appsettings.Development.json`, and the suite fills that database with test collectors,
  bottles, listings, offers, wishes and messages.
- Then run `cd VirtualBar.Web.Tests && E2E_MAIL_DIR=<absolute path of .e2e-mail> npx playwright test --project=chromium`.
- The `@confirmation-on` tests need the API restarted with the **`api-e2e-confirmation-on`** configuration (the same
  overrides with `Identity:RequireConfirmedEmail=true`), then the same command with `E2E_CONFIRMATION=on`.
- CI runs one test at a time (`workers: 1`). A local run at Playwright's default parallelism makes many browsers compete
  for the CPU against the unbundled dev server, which can push the older specs' 5 s assertions over their limit, so
  `--workers=1` reproduces CI.
- `Email:PickupDirectory` is for development only: the API refuses to start with it in any other environment, where it
  would silently stop every email.
