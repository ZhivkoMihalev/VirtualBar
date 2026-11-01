---
paths:
  - "VirtualBar.Web.Tests/**"
---

# Frontend tests (`VirtualBar.Web.Tests`)

**Unit and component tests (`unit/`)** — Vitest + React Testing Library in jsdom, run by `npm test` on every PR.
- One file per module under test, in the folder that mirrors `VirtualBar.Web/src`: `src/lib/authErrors.ts` → `unit/lib/authErrors.test.ts`, `src/pages/ConfirmEmailPage.tsx` → `unit/pages/ConfirmEmailPage.test.tsx`.
- Import the code under test through the app's `@/` alias (`vitest.config.ts` and `tsconfig.json` point it at `../VirtualBar.Web/src`), exactly as the app imports it.
- Mock only the **API layer** (`vi.mock('@/api/authApi')`), never React Query, the router or i18n: components render with the real providers through `renderWithProviders` in `unit/testUtils.tsx`:
  - the same providers as `App.tsx` (`AuthProvider`, `MemoryRouter` at `route`, `ChatProvider`, `TooltipProvider`) and real i18n in Bulgarian;
  - a fresh `QueryClient` that does not retry, and retries at once when a query asks to (so `ProductPage`'s "retry anything but a 404" rule is checked by counting calls);
  - `path` (e.g. `/product/:id`) for a page that reads route parameters, and `location()` for the path and query string the page wrote;
  - `signIn()` before rendering saves a session, so the page renders logged in. A logged-in NavBar polls `@/api/notificationsApi`, so mock that too.
  - `apiError(status, body)` / `networkError()` build real `AxiosError`s, so `isAxiosError` and the `reason` handling run as in production; `deferred()` holds a request pending to check loading states.
  - Test data comes from builders in `unit/fixtures/` (`productCard`, `productDetail`, `review`, …): a valid default, and the test overrides only what it is about.
- Assert translated text from the translation files (`bg.confirmEmail.invalidLink`, or `i18n.t(...)` for interpolated text), never a hard-coded string.
- Query by role and accessible name first (`getByRole('button', { name: … })`), then text.
- `unit/setup.ts` unmounts after every test and clears `localStorage`; mocks are reset between tests (`mockReset`, `restoreMocks`).
- `unit/i18n/translations.test.ts` pins that `bg.json` and `en.json` have the same keys, the same `{{placeholders}}` per key, and no empty value. A new i18n key therefore goes into **both** files in the same change.
- Code under test is never changed to make a test pass. A test exists to fail on the bug it names, so check that it does once: break the code, watch it go red, restore.
