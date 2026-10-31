# Slice 11 — OffersPage

> Read `00-OVERVIEW.md` first. This slice migrates the **Offers** page (received / sent purchase
> offers) onto shadcn `Tabs`, `Card`, `Badge`, and `Button`. It is the slice that **extends the
> shared `Badge` component** with two new semantic variants (`success`, `warning`) — so it touches
> one shared primitive that later slices (and the Marketplace status pills) may reuse.

## Context recap

`OffersPage.tsx` is a protected route (`/offers`, see §11) reachable from the NavBar `nav.offers`
item. It shows two tabs — **Received** (offers others made on my bottles) and **Sent** (offers I made
on others' bottles) — each a list of offer cards with a coloured status pill and contextual action
buttons. Today it is 100% inline-styled "speakeasy" (Cinzel/Playfair, gold/green/red hex, hand-rolled
bottom-border tabs). The data layer (`react-query` queries + mutations) is already clean and must be
preserved verbatim. Per §8c custom tabs → `Tabs`, and per §8b every hardcoded hex maps to a §6 token.

## Goal

Re-skin OffersPage to the amber/stone/Inter dark theme: real Radix `Tabs`, `Card`-based offer rows,
status `Badge`s driven by **new `success`/`warning` Badge variants**, and `Button` action variants —
while keeping the received/sent `useQuery`s, the accept/decline/withdraw `useMutation`s, and their
query invalidations byte-for-byte. No new i18n keys (every string already exists).

## Files to touch

- `src/components/ui/badge.tsx` — **add `success` + `warning` variants** to the `badgeVariants` CVA.
- `src/index.css` — **add a custom `--warning` token** (mirroring the existing `--success` block at
  the bottom of the file) so `text-warning`/`bg-warning` utilities resolve.
- `src/pages/OffersPage.tsx` — full re-skin (Tabs / Card / Badge / Button; tokenize all inline styles).
- No changes to `src/api/offersApi.ts`, `src/types/index.ts`, or the i18n JSON.

## Current state (what `OffersPage.tsx` does now)

- **Default export `OffersPage`** — `useState<Tab>('received')`; renders `NavBar`, a header (small
  `nav.offers` label + `offers.title` `<h1>`), a hand-rolled `role="tablist"` row of two `TabButton`s,
  then `tab === 'received' ? <ReceivedTab /> : <SentTab />`.
- **`STATUS_COLORS: Record<OfferStatus, {fg,bg,border}>`** — data map of hex per status
  (Pending gold `#E8C870`, Accepted green `#6ABF8A`, Declined red `#D46A6A`, Withdrawn tan `#9A8E78`).
  **This map is deleted** — its job moves to Badge variants.
- **`STATUS_KEYS: Record<OfferStatus, string>`** — status → i18n key. **Kept as-is.**
- **Module-level `CSSProperties` constants** — `tabRowStyle`, `cardStyle`, `acceptBtnStyle`,
  `declineBtnStyle`, `withdrawBtnStyle`. All deleted (replaced by `className`/component variants).
- **`formatDate(iso)`** — pure `toLocaleDateString` helper. **Kept.**
- **`TabButton`** — custom underline tab `<button>`. **Deleted** (→ `TabsTrigger`).
- **`StatusBadge`** — `<span>` styled from `STATUS_COLORS`. **Deleted** (→ inline `<Badge>`).
- **`OfferCard`** — `<div style={cardStyle}>` with bottle name + `StatusBadge`, `offers.by`, optional
  quoted `message`, `formatDate`, a green price (`currency + offeredPrice.toLocaleString()`), and an
  `actions` slot. **Rebuilt on `Card`.**
- **`ReceivedTab`** — `useQuery(['offers','received'], getReceivedOffers)`; `acceptMutation` +
  `declineMutation` (both `invalidateQueries(['offers','received'])`); `pending =
  acceptMutation.isPending || declineMutation.isPending`. Renders accept/decline only when
  `offer.status === 'Pending'`; counterparty = `offer.buyerDisplayName`. **Hooks preserved.**
- **`SentTab`** — `useQuery(['offers','sent'], getSentOffers)`; `withdrawMutation`
  (`invalidateQueries(['offers','sent'])`); withdraw only when `Pending`; counterparty =
  `offer.sellerDisplayName`. **Hooks preserved.**
- **`StateMessage`** — centered italic loading/empty/error line. **Kept, tokenized.**

## Transformation plan

### 1. Extend `Badge` — new `success` + `warning` variants (§7)

`badgeVariants` currently has `default | secondary | destructive | outline | ghost | link`. Add two
entries, modeled exactly on the existing `destructive` recipe (`bg-X/10 text-X …`):

```ts
// inside badgeVariants → variants.variant, after `destructive`:
success:
  "bg-success/10 text-success focus-visible:ring-success/20 dark:bg-success/20 dark:focus-visible:ring-success/40 [a]:hover:bg-success/20",
warning:
  "bg-warning/10 text-warning focus-visible:ring-warning/20 dark:bg-warning/20 dark:focus-visible:ring-warning/40 [a]:hover:bg-warning/20",
```

`text-success`/`bg-success` already resolve (the `--success` token was added in Slice 3 — see §5/§6
and the block at the bottom of `index.css`). `text-warning`/`bg-warning` do **not** yet exist, so add
a `--warning` token **mirroring the `--success` block** at the end of `index.css`:

```css
@theme inline {
  --color-warning: var(--warning);
  --color-warning-foreground: var(--warning-foreground);
}
:root {
  --warning: oklch(0.75 0.13 75);            /* amber */
  --warning-foreground: oklch(0.985 0 0);
}
.dark {
  --warning: oklch(0.83 0.12 88);            /* gold ≈ old Pending #E8C870 */
  --warning-foreground: oklch(0.27 0.07 70);
}
```

(OKLCH values are a faithful starting point for the old gold Pending pill; fine-tune visually in §
Verification. `--warning-foreground` is added for symmetry with `--success`; the badge variant uses
`text-warning`, not the foreground.)

### 2. Status → Badge variant map (replaces `STATUS_COLORS`)

| `OfferStatus` | Old `STATUS_COLORS.fg` | New Badge `variant` | Renders as |
|---|---|---|---|
| `Pending` | gold `#E8C870` | **`warning`** (new) | amber/gold tint |
| `Accepted` | green `#6ABF8A` | **`success`** (new) | green tint |
| `Declined` | red `#D46A6A` | `destructive` (existing) | red tint |
| `Withdrawn` | muted tan `#9A8E78` | `secondary` (existing) | neutral grey fill |

```ts
import { Badge, badgeVariants } from '@/components/ui/badge'
import type { VariantProps } from 'class-variance-authority'
type BadgeVariant = VariantProps<typeof badgeVariants>['variant']

const STATUS_VARIANTS: Record<OfferStatus, BadgeVariant> = {
  Pending: 'warning',
  Accepted: 'success',
  Declined: 'destructive',
  Withdrawn: 'secondary',
}
```

Badge usage: `<Badge variant={STATUS_VARIANTS[offer.status]}>{t(STATUS_KEYS[offer.status])}</Badge>`.
(Withdrawn alternative if `secondary` reads too solid: `variant="outline"` +
`className="text-muted-foreground"` for a more "muted tan" look — pick `secondary` unless verification
says otherwise.)

### 3. Tabs (§8c) — drop the `useState`/`TabButton`, go uncontrolled

```tsx
<Tabs defaultValue="received">
  <TabsList variant="line" className="mb-6">
    <TabsTrigger value="received">{t('offers.tabReceived')}</TabsTrigger>
    <TabsTrigger value="sent">{t('offers.tabSent')}</TabsTrigger>
  </TabsList>
  <TabsContent value="received"><ReceivedTab /></TabsContent>
  <TabsContent value="sent"><SentTab /></TabsContent>
</Tabs>
```

`variant="line"` reproduces the old underline-on-active look (vs the default segmented pill). Radix
mounts **only the active `TabsContent`**, so each tab's `useQuery` still fires only when its tab is
shown — identical to today's ternary. This removes `type Tab`, `useState`, `tabRowStyle`, `TabButton`.

### 4. `OfferCard` → `Card` + `CardContent` (token map per §6/§8b)

| Element | Old inline | New |
|---|---|---|
| card shell | `cardStyle` (gold-alpha bg/border) | `<Card><CardContent className="flex flex-wrap items-start justify-between gap-4">` |
| bottle name | Playfair 20 gold `#E8C870` | `font-heading text-lg font-semibold text-foreground` |
| status pill | `StatusBadge` | `<Badge variant={STATUS_VARIANTS[status]}>` |
| `offers.by` line | Cormorant italic gold `#C9A84C` | `text-sm text-muted-foreground` |
| message quote | Cormorant `#E8D4A0` | `text-sm italic leading-relaxed text-foreground/90` |
| date | Cormorant `#7A6040` | `text-xs text-muted-foreground` |
| price | Playfair 24 green `#6ABF8A` | `font-heading text-xl font-semibold whitespace-nowrap text-success` |

`Card` supplies vertical padding via `--card-spacing` and `CardContent` the horizontal padding, so the
old `padding:'20px 24px'` is dropped.

### 5. Action buttons → `Button` variants (§7)

- **Accept** → `<Button onClick={…} disabled={pending}>` (default = amber). Optional `<Check />`
  (lucide) before the label.
- **Decline** → `<Button variant="destructive" disabled={pending}>` + optional `<X />`.
- **Withdraw** → `<Button variant="outline" disabled={withdrawMutation.isPending}>` + optional `<Undo2 />`.

mira's Button gives `disabled:opacity-50 disabled:pointer-events-none` for free → delete every manual
`cursor`/`opacity` ternary. Keep the exact `onClick={() => xMutation.mutate(offer.id)}` calls and the
`offer.status === 'Pending'` render guards.

### 6. Error + state messages (§6)

- The per-tab error line → standard error box:
  `className="rounded-md border border-destructive/40 bg-destructive/10 px-3 py-2 text-sm text-destructive"`
  (keep the `acceptMutation.isError || declineMutation.isError` / `withdrawMutation.isError` conditions).
- `StateMessage` → `cn('py-16 text-center text-sm', error ? 'text-destructive' : 'text-muted-foreground')`.
  Optionally swap the loading state for `Skeleton` cards (§7) — nice-to-have, not required.

### 7. Header / page shell

Keep `NavBar`; tokenize the wrapper (`text-foreground`, `mx-auto max-w-[880px] px-10 py-10`). Header:
small label `text-xs font-medium uppercase tracking-widest text-muted-foreground` (`nav.offers`) over
`<h1 className="font-heading text-3xl font-semibold text-foreground">` (`offers.title`).

## i18n keys to preserve (offers + nav)

Every key already exists in `bg.json`/`en.json` — **do not add or rename any.** Used by this page:

- `nav.offers` (header eyebrow)
- `offers.title`
- `offers.tabReceived`, `offers.tabSent`
- `offers.statusPending`, `offers.statusAccepted`, `offers.statusDeclined`, `offers.statusWithdrawn`
  (via `STATUS_KEYS`)
- `offers.by` (interpolates `{{name}}`)
- `offers.loading`
- `offers.errorRespond`
- `offers.emptyReceived`, `offers.emptySent`
- `offers.accept`, `offers.decline`, `offers.withdraw`

**Not used here (they belong to MakeOfferSection / Slice 7 — leave untouched):** `offers.makeOffer`,
`offers.offerModalTitle`, `offers.price`, `offers.currency`, `offers.message`,
`offers.messagePlaceholder`, `offers.submit`, `offers.submitting`, `offers.cancel`, `offers.on`,
`offers.errorCreate`.

## Slice-specific gotchas

- **`badge.tsx` is shared.** Adding `success`/`warning` is additive (new union members) and safe, but
  the `warning` variant is **dead until `--warning` is added to `index.css`** — add the token in the
  same slice or `text-warning` silently renders as `currentColor`.
- **`--warning` must be registered in `@theme inline`** (`--color-warning: var(--warning)`), not just
  declared under `:root`/`.dark`, or the Tailwind v4 `text-warning`/`bg-warning` utilities won't generate.
- **Don't make `Tabs` controlled unless needed.** Nothing else reads the active tab, so `defaultValue`
  is correct and lets us delete `useState`/`type Tab`.
- **Preserve the exact query keys** `['offers','received']` / `['offers','sent']` — Slice 7's
  `MakeOfferSection` invalidates `['offers','sent']` after creating an offer; renaming breaks that.
- **Keep `STATUS_KEYS`** (status → i18n key). Only `STATUS_COLORS` is removed.
- mira is compact (§4); the offer cards are a focal list — if the action `Button`s feel cramped, bump
  to `size="sm"`/`size="lg"` locally rather than re-adding hardcoded padding.
- Keep the bespoke nothing-here: no SVG/product art on this page, so no §5/§8b "keep inline" exceptions apply.

## Verification (§10)

1. `npm --prefix VirtualBar.Web run build` (green) and `npm --prefix VirtualBar.Web run lint` (clean).
2. `npm run dev` + backend (`dotnet run` in `VirtualBar.Api`) — this page is data-bearing.
3. **bg (default):** open `/offers`. **Получени** tab: a `Pending` offer shows a gold **Изчаква**
   badge with **Приеми**/**Откажи** buttons → click **Приеми**, badge flips to green **Приета**, list
   refetches; another → **Откажи** → red **Отказана**. **Изпратени** tab: a `Pending` offer shows
   **Оттегли** → click → **Оттеглена** neutral badge. Confirm empty/loading lines render
   (`emptyReceived`/`emptySent`/`loading`).
4. **en:** switch БГ→EN via `LanguageSwitcher`; tabs, badges (Pending/Accepted/Declined/Withdrawn),
   buttons, and empty/error copy all re-localize.
5. **Badge colours:** Pending = amber/gold, Accepted = green, Declined = red, Withdrawn = neutral —
   legible on dark `Card`. Tune the `--warning` OKLCH if the gold is muddy.
6. **a11y win (§8c):** `Tabs` are keyboard-navigable (←/→ switch, Home/End), focus ring visible — the
   old `TabButton` had none of this. No console errors.

## Acceptance criteria (§10)

- Build + lint green; dark amber/stone theme; **Inter** (no serif) on the page.
- `Tabs` (Radix) replace `TabButton`; keyboard nav works; only the active tab's query runs.
- `OfferCard` is a shadcn `Card`; status pill is a `Badge` with the §2 variant per status.
- `badge.tsx` gains `success` + `warning`; `index.css` gains the `--warning` token (mirroring `--success`).
- Accept/decline/withdraw still mutate and invalidate `['offers','received']`/`['offers','sent']`;
  action buttons use `default`/`destructive`/`outline`.
- All listed `offers.*` + `nav.offers` keys preserved; **no new keys**; verified in **bg + en**.
- All hardcoded hex gone from `OffersPage.tsx` (only §6 tokens / component variants remain).

## Dependencies

Depends on **Slice 2** (primitives: `Tabs`, `Card`, `Badge`, `Button`). Independent of Slices 4–10, so
it can land any time after Slice 2. It **modifies the shared `badge.tsx` + `index.css`** — if a later
slice (e.g. Marketplace status pills) wants `success`/`warning`, those variants now exist. Next page
slice by ID order is `12-profile-page.md`.
