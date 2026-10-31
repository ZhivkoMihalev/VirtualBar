# VirtualBar.Web → shadcn/ui Migration — Plan Docs

These documents let you execute the frontend migration **one slice per session**. Each slice doc
is self-contained when paired with the overview.

## How to use

1. `git checkout feat/shadcn-migration` (all migration work lives on this branch).
2. Read **`00-OVERVIEW.md` in full** (decisions, theme tokens, component APIs, established patterns,
   gotchas, and the §14 shared-additions ledger).
3. Read the one slice doc you're executing.
4. Implement only that slice, reuse the overview's patterns, keep all `t()` i18n keys.
5. Verify per overview §10 (`npm run build` green is the gate; `e2e-tester` for visual; test bg + en).
6. Do **not** commit unless the user asks.

## Status snapshot (at time of writing)

Slices **0–2 done** (init + theme + 25 primitives + RHF/zod infra; build green). **Slice 3 code done**
(auth pages = the RHF+zod reference), visual verification pending. **Slices 4–14 pending** — to be
implemented in separate sessions using these docs. Nothing is committed yet.

## Index (execute in this order — see overview §11 for dependencies)

| # | Doc | Scope |
|---|---|---|
| — | [`00-OVERVIEW.md`](./00-OVERVIEW.md) | **Read first.** Shared context: decisions, theme/token cheat-sheet, component APIs, patterns, gotchas, shared-additions ledger |
| 3 | [`03-auth-pages-remaining.md`](./03-auth-pages-remaining.md) | Visual verification of the already-built auth pages |
| 4 | [`04-shared-chrome.md`](./04-shared-chrome.md) | NavBar, Footer, LanguageSwitcher→DropdownMenu, NotificationBell→Popover, Avatar (on every page) |
| 5 | [`05-chat-widget.md`](./05-chat-widget.md) | ChatWidget — keep floating shell, rebuild internals with non-modal primitives |
| 6 | [`06-distillery-select.md`](./06-distillery-select.md) | Hand-rolled combobox → `Command` in `Popover` (preserve `(id,name)` contract) |
| 7 | [`07-bottle-detail-panel.md`](./07-bottle-detail-panel.md) | Dialog + AlertDialog + nested Make-Offer Dialog (RHF+zod); shared by 2 pages |
| 8 | [`08-dashboard-page.md`](./08-dashboard-page.md) | AddBottle drawer → Sheet (RHF+zod), Switch, ToggleGroup, filters |
| 9 | [`09-home-page.md`](./09-home-page.md) | News/feed Cards, admin PostForm Sheet + bg/en Tabs, AlertDialog, Skeleton |
| 10 | [`10-marketplace-page.md`](./10-marketplace-page.md) | Tabs, publish + contact Dialogs (RHF+zod publish), Cards (largest file) |
| 11 | [`11-offers-page.md`](./11-offers-page.md) | Tabs, OfferCard→Card, status Badges (adds `success`/`warning` variants + `--warning` token) |
| 12 | [`12-profile-page.md`](./12-profile-page.md) | Profile editor → shadcn inputs; inline messages → Sonner toasts |
| 13 | [`13-browse-publicbar.md`](./13-browse-publicbar.md) | Collector Cards + search; PublicBar header/follow/message (dead-route fix) |
| 14 | [`14-cleanup.md`](./14-cleanup.md) | Remove serif `<link>` + `next-themes`, delete dead code (ask first), route code-split, final sweep |

The original high-level plan (background only) is at
`.claude/plans/i-need-to-refactor-iterative-torvalds.md`.
