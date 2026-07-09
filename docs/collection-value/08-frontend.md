# 08 — Frontend

> Phase **A** · Depends on: **07** · Read `00-OVERVIEW.md` first.

## Goal
Honest, **citation-compliant** UX (overview §3, §4.3, §4.6): every estimate shows **range + confidence + sources
(clickable citations) + "as of" date**, labelled **indicative**; `None` → "—". Collection value (Sealed only) on
the Dashboard. Bulgarian **and** English.

## Recover from stash
- `VirtualBar.Web/src/api/pricesApi.ts`
- `VirtualBar.Web/src/types/index.ts` *(PriceEstimate / CollectionValue types)*
- `VirtualBar.Web/src/pages/DashboardPage.tsx` *(value card)*
- `VirtualBar.Web/src/components/BottleDetailPanel.tsx` *(estimate row)*
- `VirtualBar.Web/src/i18n/bg.json`, `en.json` *(collectionValue namespace)*

## Build new / extend
1. **Types + api:** add `confidence`, `source`, and **`sources: { url, title }[]`** (the min–max is already
   `lowEstimate`/`highEstimate`).
2. **Dashboard card:** total collection value (Sealed only) + "as of"; "—" when empty.
3. **Bottle panel:** estimate row — **min–max** range, confidence chip, an **indicative** label, a **source label**
   ("researched" for `ClaudeResearch` / "community" for `Internal`), `AsOf`, and a **"Sources" list of clickable
   citation links** (MANDATORY — Anthropic requires displaying citations when AI output is shown to end users).
   Never a fabricated number.
4. **i18n:** indicative / confidence / source / "sources" labels in `bg` and `en`.

## Gate
`npm --prefix VirtualBar.Web run build` clean; exercise the Dashboard + bottle panel in **bg + en**; sources render
as links; zero console errors.
