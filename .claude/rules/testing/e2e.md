---
paths:
  - "VirtualBar.Web.Tests/e2e/**"
  - "VirtualBar.Web.Tests/playwright.config.ts"
  - ".github/workflows/e2e.yml"
  - ".claude/launch.json"
---

# E2E tests

**E2E tests (`e2e/`)** — Playwright against the real stack (see `.claude/rules/ci/e2e.md`): one spec per feature flow, role/text selectors first, no fixed waits.
- The newer specs (`marketplace`, `offers`, `messages`, `catalog-browse`, `product-page`) build on `e2e/support.ts`:
  - `registerCollector` registers fresh users through the API;
  - the `openAs(who)` fixture (use the `test` exported by `support.ts`) gives each collector a browser context of its own, `null` an anonymous visitor, and closes them all after the test, failed or not. A context left open keeps polling the API every 30 s for the rest of the worker's run;
  - `api` / `createBottle` set up data through the API;
  - `untouchedProduct` picks a catalog product no bottle is linked to yet, so a product page or card shows only what the test created;
  - `card`, `openBottlePanel`, `notification`, `chatToggle`, `chatMessage` and `searchMarketplace` locate the UI, and `switchToEnglish` uses the NavBar's language switcher.
- **Set up through the API, assert through the UI**: a spec drives only the flow it is about, and creates everything else directly.
- Expected text comes from the app's `bg.json` / `en.json` (exported by `support.ts` as `bg` and `en`), never copied into the spec. The older specs predate this and keep their own string constants.
- A multi-user flow uses `FLOW_TIMEOUT` and the `expect` exported by `support.ts`, which waits up to 15 s. Each step crosses browser contexts and page loads.
- Names come from `uniqueName`, so a run never collides with data from an earlier run, and a spec that leaves something other collectors would see removes it: a wish matches every later listing of its category.
