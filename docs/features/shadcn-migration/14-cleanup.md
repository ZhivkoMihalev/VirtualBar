# Slice 14 — Cleanup & finalization

> Read `00-OVERVIEW.md` first. This slice runs **last**, after slices 3–13 are complete
> (see the dependency table in §11). It removes transition-only scaffolding, deletes dead
> code, code-splits the bundle, and does the final token/i18n sweep. No new UI is built here.

## Context recap

The serif Google-Fonts `<link>` was deliberately **retained through every prior slice** (§3.4,
§5 Slice 1) so un-migrated components kept their serif look instead of degrading to a browser
fallback serif. By now every page/component slice (4–13) has converted its inline speakeasy
styling to the §6 tokens and its serif fonts to Inter. This slice removes the now-pointless
serif link, drops the dead `next-themes` dep (§9 — `sonner.tsx` was already hardcoded to
`theme="dark"` in Slice 2), deletes two dead files, code-splits the routes to kill the
`>500 kB chunk` warning (§5), and runs the final residual-style + i18n parity sweep.

## Goal

A clean tree on the preset theme: **only** Inter loads (self-hosted via `@fontsource-variable/inter`),
no unused deps, no dead files, route-level code-splitting, zero residual hardcoded serif/hex
outside the legitimate-dynamic allowlist (§8b), and bg/en i18n at full parity. Build + lint green.

## Files to touch

- `VirtualBar.Web/index.html` — remove serif `<link>` + 2 `preconnect`s.
- `VirtualBar.Web/package.json` — remove `next-themes` (+ refresh `package-lock.json` via `npm install`).
- `VirtualBar.Web/src/App.tsx` — convert page imports to `React.lazy` + wrap routes in `Suspense`.
- `VirtualBar.Web/src/App.css` — **DELETE** (dead; ask first).
- `VirtualBar.Web/src/pages/MessagesPage.tsx` — **DELETE** (unrouted dead code; ask first).
- `VirtualBar.Web/src/i18n/bg.json` + `en.json` — remove orphan key(s), add any new-copy keys, verify parity.
- Any straggler page/component files surfaced by the final inline-style re-grep.

## Current state (verified by grep at planning time — RE-GREP at execution, counts shrink as 4–13 land)

- **`index.html`** lines 7–9: two `<link rel="preconnect">` (googleapis + gstatic) and the
  `<link href="…Playfair+Display…Cormorant+Garamond…Cinzel…">` stylesheet. `<html class="dark">`
  and `/favicon.svg` stay.
- **`next-themes`** — `package.json` line 27. **Zero source imports** (`grep next-themes src` →
  no matches): confirmed dead after the Slice-2 sonner fix.
- **`App.css`** — Vite starter leftovers (`.counter`, `.hero`, `#next-steps`, …). **Not imported
  anywhere** (`grep "App\.css" src` → no matches). Dead.
- **`MessagesPage.tsx`** — full serif/inline messages page. **Unrouted**: `App.tsx` maps
  `/messages` to `<Navigate to="/" replace />` (line 72) and never imports it; `grep MessagesPage src`
  hits only its own `export default` (line 141). Messaging lives in `ChatWidget` (§ frontend arch).
- **`App.tsx`** eagerly imports all 11 pages (lines 5–15) → one big entry chunk → the expected
  `>500 kB` Rollup warning. Baseline bundle ≈ **225 kB gzip JS / 14 kB gzip CSS** (§5).

### Residual inline-style inventory (planning-time baseline — most cleared by 4–13; re-grep to get the real remainder)

Three greps drive the final sweep. Run all three over `VirtualBar.Web/src`:

```
grep -rn "Cinzel|Playfair|Cormorant" src        # serif refs  → MUST be 0
grep -rn "style=\{"                       src     # inline style → only §8b allowlist may remain
grep -rn "#[0-9A-Fa-f]{6}|#[0-9A-Fa-f]{3}\b" src # hardcoded hex → only §8b allowlist may remain
```

Planning-time hotspots (heaviest first; these are the files to scrutinize last):

| File | serif | `style={` | hex | Disposition |
|---|---|---|---|---|
| `pages/MarketplacePage.tsx` | 38 | 88 | 57 | sweep → 0 (slice 10) |
| `components/BottleDetailPanel.tsx` | 43 | 94 | 55 | sweep → 0 (slice 7) |
| `pages/HomePage.tsx` | 33 | 85 | 51 | sweep → 0 (slice 9) |
| `pages/DashboardPage.tsx` | 25 | 73 | 37 | sweep → 0 (slice 8) |
| `pages/OffersPage.tsx` | 15 | 27 | 21 | sweep → 0 (slice 11) |
| `components/ChatWidget.tsx` | 15 | 33 | 21 | sweep hex/serif → 0; **keep** floating-shell positioning inline (§8c) |
| `pages/BrowsePage.tsx` | 12 | 22 | 15 | sweep → 0 (slice 13) |
| `pages/PublicBarPage.tsx` | 10 | 16 | 12 | sweep → 0 (slice 13) |
| `pages/ProfilePage.tsx` | 9 | 22 | 12 | sweep → 0 (slice 12) |
| `components/NavBar.tsx` | 9 | 36 | 13 | sweep → 0 (slice 4) |
| `components/Footer.tsx` | 8 | 18 | 14 | sweep → 0 (slice 4) |
| `components/NotificationBell.tsx` | 6 | 12 | 9 | sweep → 0 (slice 4) |
| `components/DistillerySelect.tsx` | 4 | 6 | 6 | sweep → 0 (slice 6) |
| `components/LanguageSwitcher.tsx` | 2 | 6 | 4 | sweep → 0 (slice 4) |
| `pages/MessagesPage.tsx` | 16 | 28 | 20 | **N/A — file deleted** (task 3) |
| **`components/BarShelf.tsx`** | 4 | 24 | 22 | **KEEP** — `CATEGORY_COLORS` + `BottleSvg` geometry/gradients (§3.5, §8b) |
| **`components/Avatar.tsx`** | 1 | 2 | 1 | **KEEP** — size-from-prop (§8b); shadcn `Avatar` may already replace in slice 4 |
| **`App.tsx`** | 0 | 3 | 0 | **KEEP** — `bg-room.png` img + darkening overlay (§3.5); `minHeight` wrapper optional |
| **`ui/sonner.tsx`, `ui/toggle-group.tsx`** | 0 | 1 each | 0 | **KEEP** — generated shadcn files; do not hand-edit |
| `index.css` | 0 | — | 3 | scrollbar pseudo-rules (`#0E0603/#3D2010/#C9A84C`) — **optional** tokenize to `var(--*)`; low priority |

`assets/*.svg` hex hits are unused starter assets — ignore (or delete if you also remove imports; out of scope).

## Transformation plan

### Task 1 — Strip serif `<link>` + preconnects (`index.html`)
Delete lines 7, 8, 9 (the two `preconnect`s + the Google-Fonts stylesheet). Nothing else changes.
**Sequence:** do this **after** task 5's sweep confirms 0 serif refs — removing the link before the
sweep makes any straggler fall back to ugly browser serif. Inter needs no `<link>` (CSS-imported in
`index.css` line 4 via `@import "@fontsource-variable/inter"`).

### Task 2 — Remove `next-themes` (`package.json`)
Delete the `"next-themes": "^0.4.6"` line (27). Run `npm install` (in `VirtualBar.Web`) to prune it
from `package-lock.json` + `node_modules`. Re-confirm `src/components/ui/sonner.tsx` still hardcodes
`theme="dark"` and does **not** re-import `next-themes` (§9 gotcha).

### Task 3 — Delete dead code (PROPOSE → confirm with user → then delete)
Both files are tracked and were **not** authored by this migration, so per CLAUDE.md ("never delete…
without asking"):
1. **PROPOSE** to the user: "`src/App.css` (unimported Vite-starter CSS) and
   `src/pages/MessagesPage.tsx` (unrouted — `/messages` redirects to `/`, never imported) are dead.
   May I delete them?" Cite the grep evidence above.
2. **WAIT** for explicit confirmation.
3. On yes: `git rm VirtualBar.Web/src/App.css VirtualBar.Web/src/pages/MessagesPage.tsx`. Then grep
   again to confirm no dangling import/route references remain. If the user declines, leave them and
   note it.

### Task 4 — Route-level code-splitting (`App.tsx`)
Convert the 11 eager page imports (lines 5–15) to `React.lazy` and wrap the route tree in `Suspense`.
Keep `Footer`, `ChatWidget`, `Toaster`, `TooltipProvider`, contexts eager (they render on every route —
splitting them buys nothing). All pages already use `export default`, which `lazy` requires.

Exact shape:

```tsx
import { lazy, Suspense } from 'react'
import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { AuthProvider, useAuth } from './contexts/AuthContext'
import { ChatProvider } from './contexts/ChatContext'
import Footer from './components/Footer'
import ChatWidget from './components/ChatWidget'
import { Toaster } from '@/components/ui/sonner'
import { TooltipProvider } from '@/components/ui/tooltip'

const HomePage = lazy(() => import('./pages/HomePage'))
const LoginPage = lazy(() => import('./pages/LoginPage'))
const RegisterPage = lazy(() => import('./pages/RegisterPage'))
const ForgotPasswordPage = lazy(() => import('./pages/ForgotPasswordPage'))
const ResetPasswordPage = lazy(() => import('./pages/ResetPasswordPage'))
const DashboardPage = lazy(() => import('./pages/DashboardPage'))
const BrowsePage = lazy(() => import('./pages/BrowsePage'))
const PublicBarPage = lazy(() => import('./pages/PublicBarPage'))
const MarketplacePage = lazy(() => import('./pages/MarketplacePage'))
const OffersPage = lazy(() => import('./pages/OffersPage'))
const ProfilePage = lazy(() => import('./pages/ProfilePage'))

// reuse the existing loading markup for both Suspense + auth-loading (DRY)
function RouteFallback() {
  return (
    <div className="min-h-screen flex items-center justify-center">
      <div className="text-muted-foreground">Loading...</div>
    </div>
  )
}
```

Then wrap the router output (inside the existing `minHeight` wrapper) in `Suspense`:

```tsx
<div style={{ minHeight: '100vh' }}>
  <Suspense fallback={<RouteFallback />}>
    <AppRoutes />
  </Suspense>
</div>
```

Replace the two inlined `ProtectedRoute`/`AppRoutes` loading blocks with `<RouteFallback />` too.
Leave the `<Routes>` table (incl. `/messages` → `Navigate`) unchanged.

### Task 5 — Final inline-style sweep
Re-run the 3 greps above. For every hit **not** on the §8b KEEP allowlist (BarShelf
`CATEGORY_COLORS`/`BottleSvg`, Avatar size, App.tsx bg+overlay, bottle hover `translateY/scale`,
animation delays, generated `ui/*`), convert to §6 tokens: serif `font-family` → drop (inherits
Inter) or `font-heading`; hex → the matching utility (`#C9A84C`→`text-primary`, `#07030A`→`bg-background`,
`#F0DDB4`→`text-foreground`, `#B09868`→`text-muted-foreground`, `#C04040`→`text-destructive`, etc. per
§6 table); merge with `cn(...)`. **Serif count MUST reach 0** (the `<link>` is gone). Optionally
tokenize the `index.css` scrollbar hex to `var(--primary)`/`var(--card)` (cosmetic, low priority).

### Task 6 — i18n parity + orphan/missing-key audit
1. **Orphan:** `messages.selectConversation` (`bg.json:231`, `en.json:231`) is used **only** by the
   deleted `MessagesPage` — `ChatWidget` uses `messages.{title,loading,error,noConversations,you,inputPlaceholder,sending,send}`
   and `PublicBarPage` uses `messages.sendMessage`, none use `selectConversation`. **Remove it from
   both JSONs.** Do **NOT** delete the rest of the `messages.*` namespace — ChatWidget still needs it.
2. **Parity:** the key sets of `bg.json` and `en.json` must be identical. Diff them (e.g.
   `node -e "const a=require('./src/i18n/bg.json'),b=require('./src/i18n/en.json');…compare flattened keys"`
   or jq) — flag any key in one file but not the other.
3. **No missing:** every `t('ns.key')` in `src` must resolve in **both** files. For each
   `t('…')` literal, confirm the key exists.
4. **No orphan:** for each leaf key, grep `src` for its usage; flag unused (expect only
   `messages.selectConversation` to fall out — investigate any others before removing).
5. **New copy:** any keys introduced by slices 4–13 (toast strings, dialog titles/descriptions,
   tab labels) must be present in **both** `bg.json` and `en.json` (§8a/§8d). Add Bulgarian + English.

### Task 7 — Full e2e regression (both languages)
Use the `e2e-tester` agent (§10.3): walk every route — `/`, `/browse`, `/marketplace`, `/bar/:id`,
`/login`, `/register`, `/forgot-password`, `/reset-password`, `/dashboard`, `/offers`, `/profile`,
and `/messages`→`/` redirect — in **bg and en**. Confirm: lazy chunks load per route (DevTools
Network shows a separate JS chunk on first visit), no FOUC/serif-fallback flash, **Inter renders
everywhere** (incl. Cyrillic), all overlays/dialogs/dropdowns still work, ChatWidget intact.
Backend (`dotnet run` in `VirtualBar.Api`) up for data-bearing pages.

### Task 8 — Final gates
`npm run build` (green; record `dist` sizes) and `npm run lint` (clean). See §10.1–2.

## i18n keys to preserve

Preserve **all** existing `t('…')` calls verbatim (§8d). The migration touches styling, not copy.
The **only** intended key removal is `messages.selectConversation` (both files). Every other key in
all namespaces (`nav, lang, login, register, dashboard, addBottle, browse, marketplace, publicBar,
bottle, messages, profile, footer, hero, home, barShelf, notifications, wishList, distillerySelect,
offers`) stays. Net key-count delta after this slice = (−1 orphan) + (new-copy keys added in 4–13).

## Slice-specific gotchas

- **Order matters:** sweep serif refs to 0 (task 5) **before** pulling the `<link>` (task 1), else
  stragglers fall back to browser serif. Build between the two to confirm.
- **Don't nuke the `messages` namespace** — only `selectConversation` is orphaned; ChatWidget +
  PublicBarPage keep the rest.
- **`lazy` needs default exports** — all pages have them; if a future page uses a named export,
  wrap: `lazy(() => import('./X').then(m => ({ default: m.X })))`.
- **`noUnusedLocals` / lint:** after the lazy rewrite, ensure no leftover static page imports linger
  (they'd be unused → lint error).
- **Deletions need a human yes** (CLAUDE.md) — never delete `App.css`/`MessagesPage.tsx` unprompted.
- **`next-themes` removal** must be paired with `npm install` so the lockfile matches; a stale lock
  can break CI installs.
- **Gzip vs raw:** the `>500 kB` Rollup warning is **raw** bytes; the ~225 kB baseline (§5) is
  **gzip**. Report both so the before/after is apples-to-apples.
- **Suspense fallback flash:** cached chunks shouldn't re-trigger the fallback; verify on back/forward nav.

## Verification (per §10)

1. **Greps:** `Cinzel|Playfair|Cormorant` → **0** in `src`; `style={` and `#hex` → only the §8b
   allowlist (BarShelf, Avatar, App.tsx bg/overlay, generated `ui/*`, optional `index.css` scrollbar).
2. **`npm run build`** green (`tsc -b && vite build`). Record `dist/assets/*.js` gzip sizes before vs
   after; confirm the entry chunk dropped and the `>500 kB` warning is gone (or materially reduced),
   with per-route chunks emitted.
3. **`npm run lint`** clean (no unused imports after the lazy rewrite).
4. **i18n:** `bg.json` and `en.json` key sets identical; `messages.selectConversation` removed from
   both; every `t('…')` resolves in both; new-copy keys present in both.
5. **`next-themes`** absent from `package.json`, `package-lock.json`, `node_modules`; `sonner.tsx`
   still `theme="dark"`.
6. **e2e (bg + en):** all routes load, lazy chunks fetch per route, Inter everywhere, no serif
   fallback, overlays/ChatWidget functional, `/messages`→`/` redirect intact.

## Acceptance criteria

- `index.html` has no Google-Fonts `<link>` and no `preconnect`s; only self-hosted Inter loads.
- `next-themes` fully removed; build still green; `sonner.tsx` unchanged (`theme="dark"`).
- `App.css` + `MessagesPage.tsx` deleted **after user confirmation** (or explicitly retained if
  declined); no dangling imports/routes.
- `App.tsx` uses `React.lazy` + `Suspense`; entry-chunk gzip is below the baseline and no chunk trips
  the 500 kB raw warning; before/after `dist` sizes recorded in the slice report.
- Zero residual serif font refs; zero non-allowlisted hardcoded hex/inline style in `src`.
- `bg.json`/`en.json` at full key parity; orphan removed; any new copy added in both languages.
- `npm run build` + `npm run lint` green; e2e regression passes in **both** bg and en.

## Dependencies

**All prior slices (3–13)** must be complete — this slice assumes every page/component has already
been migrated to tokens + Inter (§11). It is the terminal slice; nothing depends on it.
