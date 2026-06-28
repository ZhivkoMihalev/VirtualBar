# VirtualBar.Web → shadcn/ui Migration — OVERVIEW & SHARED CONTEXT

> **Read this document first, in every session, before executing any slice doc.**
> It is the single source of truth for the decisions, the theme, the component APIs,
> the established patterns, and the gotchas. Each `NN-*.md` slice doc assumes you have
> read this file and will not repeat it.

---

## 1. Where we came from (the problem)

`VirtualBar.Web` is the React 19 + Vite 7 + TypeScript 6 + Tailwind v4 frontend of VirtualBar
(a social platform for spirits collectors — see the repo-root `CLAUDE.md` for the full product
description and backend conventions). Before this migration the frontend had grown an
inconsistent, hard-to-maintain UI layer:

- **~89% of styling was inline** (595 `style={}` vs 75 `className=`). The "speakeasy" visual
  theme (antique **gold** `#C9A84C`/`#E8C870`, near-black `#07030A`, **serif** fonts Playfair
  Display / Cormorant Garamond / Cinzel) lived as hardcoded hex inside hundreds of module-level
  `CSSProperties` constants scattered across ~18 files.
- **7 hand-rolled modals/overlays** with no focus-trap, scroll-lock, portal, or consistent ESC
  handling; a hand-rolled ARIA **combobox** (`DistillerySelect`); two click-outside dropdowns
  (`LanguageSwitcher`, `NotificationBell`); custom bottom-border **tabs**; a custom **toggle**;
  inline-SVG / emoji / unicode icons.
- The 4 auth pages already used Tailwind, but on a mismatched `stone/amber` palette.
- No `cn()`, no `clsx`/`tailwind-merge`/`cva`, no Radix, no icon library.

## 2. The goal

Put the **whole** site on **shadcn/ui** (Radix primitives + Tailwind design tokens + `cn()`),
**adopting the design system packaged in the opaque preset `bKZUFNo0`**. Outcome: a consistent,
token-driven theme; real accessibility (focus management, ESC, ARIA, portals); far less
duplicated styling; the app stays fully working in **Bulgarian (default) + English** throughout.

## 3. Locked decisions (do NOT relitigate — the user signed off on each)

1. **Full visual re-skin to the preset's theme** — adopt its colors AND fonts. This is a
   deliberate identity change, not a "keep the old look" refactor.
2. **Spike-first, then incremental slices.** The preset was decoded and approved before any
   code changed. Migration proceeds page-by-page; **every slice must end with a green
   `npm run build`.** Never big-bang.
3. **Forms**: `react-hook-form` + `zod` via shadcn `Form` for *validated* forms (auth, AddBottle,
   MakeOffer, WishList-add). Trivial single-field inputs (comment box, chat draft, search/sort)
   stay plain shadcn `Input`.
4. **Fonts**: the preset's **Inter** replaces the serif trio. (The Google-Fonts `<link>` for the
   serif fonts is intentionally **retained until the final cleanup slice** so un-migrated
   components keep their serif look during the transition instead of degrading to fallback serif.)
5. **Keep** the bespoke bottle/shelf SVG art (`BarShelf.tsx`, `CATEGORY_COLORS`) — it is product
   visualization, not theme chrome. **Dark mode + keep the room-photo background** (`bg-room.png`
   + overlay in `App.tsx`): the app runs in dark mode (`<html class="dark">`) and the atmospheric
   room photo stays.

## 4. What the preset (`bKZUFNo0`) actually is

Decoded via `npx shadcn@latest preset decode bKZUFNo0`:

| Field | Value |
|---|---|
| style | `radix-mira` (the newer **"mira"** style on Radix — note: **compact** sizing) |
| baseColor | `stone` (warm-gray neutrals) |
| theme / accent | **`amber`** → this is the `--primary` |
| font | `inter` (self-hosted via `@fontsource-variable/inter`; body + headings) |
| radius | `small` → `--radius: 0.45rem` |
| iconLibrary | `lucide` (`lucide-react`) |

Live reference of the exact look: <https://ui.shadcn.com/create?preset=bKZUFNo0>.

**Key consequence:** amber-on-stone is close to the old gold/dark identity, so the re-skin reads
as a refinement, not a jarring pivot. The biggest single change is **serif → Inter (sans)**.
**"mira" is a compact/dense style** (default Button/Input height `h-7` ≈ 28px, base text `text-xs`).
We embrace it; if a specific surface looks too cramped, bump heights locally with a className.

## 5. Current state — what is already DONE (Slices 0–3)

All work is on branch **`feat/shadcn-migration`** (off `master`). **Nothing is committed yet** —
all changes live in the working tree. The user has NOT asked for commits; do not commit unless asked.

- **Slice 0 — init scaffold (DONE).** Ran in-place `npx shadcn@latest init --preset bKZUFNo0 --yes
  --cwd VirtualBar.Web` (NOT `--template`, which scaffolds a fresh app). It created
  `components.json` (style `radix-mira`, aliases under `@/`), `src/lib/utils.ts` (`cn`), installed
  deps (`clsx`, `tailwind-merge`, `class-variance-authority`, `lucide-react`, `radix-ui` unified
  pkg, `shadcn`, `sonner`, `cmdk`, `tw-animate-css`, `@fontsource-variable/inter`), and rewrote
  `src/index.css`. The `@/*` import alias was added to `tsconfig.json`, `tsconfig.app.json`, and
  `vite.config.ts` (the latter uses `fileURLToPath(new URL('./src', import.meta.url))` because the
  config is ESM). **`baseUrl` was removed** (TS 6 deprecates it; `paths` alone resolves under
  `moduleResolution: bundler`).
- **Slice 1 — CSS reconcile + dark mode (DONE).** `init` *merged* (did not clobber) our custom CSS:
  the `@keyframes shimmer/fadeInUp/bottleIn`, `::-webkit-scrollbar*`, and `.vb-shelf-row` rules are
  preserved at the top of `src/index.css`. We stripped the old unlayered `body { background-color;
  font-family: Cormorant; color }` (it was winning the cascade over shadcn's `@layer base` and
  would have forced serif onto shadcn components). Added `class="dark"` to `<html>` in `index.html`.
  The serif Google-Fonts `<link>` is still present (removed in Slice 14).
- **Slice 2 — primitives (DONE).** Added 25 components under `src/components/ui/` (see §7). Fixed
  `sonner.tsx` (it imported `next-themes`, which we don't have → hardcoded `theme="dark"`). The
  `radix-mira` registry does **not** ship the RHF `Form` component, so we hand-added the canonical
  `src/components/ui/form.tsx` and installed `react-hook-form @hookform/resolvers zod`. Wired
  `<Toaster />` and `<TooltipProvider>` into `App.tsx`; converted App's loading states to tokens.
- **Slice 3 — auth pages (CODE DONE; visual verification still pending — see `03-auth-pages-remaining.md`).**
  `LoginPage`, `RegisterPage`, `ForgotPasswordPage`, `ResetPasswordPage` rewritten with shadcn
  `Form` + RHF + zod, a shared `src/components/AuthLayout.tsx`, and `src/lib/validation.ts`
  (`EMAIL_REGEX`, `PASSWORD_REGEX`, `type TFn`). A custom **`--success`** color token was added to
  `index.css`. **These auth pages are the reference implementation** for every other validated form.

**Build is green** at this point: `npm run build` = `tsc -b && vite build`. Current bundle baseline
≈ **225 kB gzip JS / 14 kB gzip CSS** (RHF/zod/radix now bundled). The `>500 kB chunk` warning is
expected; route code-splitting is a Slice 14 concern.

## 6. The theme — token cheat-sheet (dark mode values)

`src/index.css` defines tokens as OKLCH CSS variables under `:root` (light) and `.dark` (active).
Tailwind v4 exposes them as utilities via `@theme inline`. **Always use the utility classes below;
never hardcode hex.** Active (`.dark`) values, for reference:

| Token / utility | `.dark` OKLCH | Replaces (old speakeasy) |
|---|---|---|
| `bg-background` | `0.147 0.004 49.25` (near-black warm) | `#07030A` page bg |
| `text-foreground` | `0.985 0.001 106.423` (near-white) | `#F0DDB4` body text |
| `bg-card` / `bg-popover` | `0.216 0.006 56.043` (dark stone) | panel gradients `#0F0604…` |
| `text-card-foreground`/`-popover-foreground` | `0.985 …` | — |
| `bg-primary` / `text-primary` | `0.473 0.137 46.201` (**amber**) | gold `#C9A84C`/`#E8C870` |
| `text-primary-foreground` | `0.987 0.022 95.277` | — |
| `bg-secondary` / `text-secondary-foreground` | `0.274 …` / `0.985` | neutral fills |
| `bg-muted` / `text-muted-foreground` | `0.268 0.007 34.298` / `0.709 0.01 56.259` | `#B09868`/`#7A6040` meta text |
| `bg-accent` / `text-accent-foreground` | `0.268 …` / `0.985` | subtle hover fills |
| `text-destructive` / `bg-destructive` | `0.704 0.191 22.216` (red) | `#C04040`/`#D42020` errors |
| `border-border` / `border-input` / `bg-input` | `oklch(1 0 0 / 10%)` / `/15%` | gold-alpha borders |
| `ring-ring` (focus) | `0.553 0.013 58.071` | gold focus ring |
| `text-success` / `bg-success` (**custom**) | `0.696 0.17 162` (green) | `#4A9A6A` for-sale/accepted |
| radius | `--radius: 0.45rem` → `rounded-sm/md/lg/xl` | various |

Font: everything inherits **Inter** (`html { @apply font-sans }`). For headings use
`font-heading` (also Inter). For emphasis use `font-medium`/`font-semibold`.

Standard inline-message boxes (established in auth pages — reuse verbatim):
- **error**: `rounded-md border border-destructive/40 bg-destructive/10 px-3 py-2 text-sm text-destructive`
- **success**: `rounded-md border border-success/40 bg-success/10 px-3 py-2 text-sm text-success`

## 7. Component inventory (`src/components/ui/`) + APIs & quirks

All are imported via the `@/` alias, e.g. `import { Button } from '@/components/ui/button'`.
They internally use the **unified `radix-ui`** package (`import { Slot } from "radix-ui"`,
`import { Dialog as DialogPrimitive } from "radix-ui"`, etc.) — you don't need to touch that;
just compose the exported components.

| File | Exports / notes |
|---|---|
| `button.tsx` | `Button`, `buttonVariants`. variants: `default`(amber) `outline` `secondary` `ghost` `destructive` `link`; sizes: `default`(h-7) `xs` `sm` `lg`(h-8) `icon` `icon-xs` `icon-sm` `icon-lg`; `asChild`. **No gold/gradient variant** — `default` IS amber; that's the adopted look. |
| `badge.tsx` | `Badge`, `badgeVariants`. variants: `default`(amber) `secondary` `destructive` `outline` `ghost` `link`. **Add `success`/`warning` variants here when a slice needs them** (use `bg-success/10 text-success` etc.). |
| `card.tsx` | `Card` `CardHeader` `CardTitle` `CardDescription` `CardContent` `CardFooter` `CardAction`. Uses `--card-spacing` (default `spacing(4)`); `CardTitle` is small (`font-heading text-sm`) — override className for large titles. |
| `input.tsx` | `Input` (h-7, `bg-input/20`, built-in `aria-invalid` styling). |
| `textarea.tsx` | `Textarea`. |
| `label.tsx` | `Label`. |
| `form.tsx` | `Form` `FormField` `FormItem` `FormLabel` `FormControl` `FormMessage` `FormDescription` `useFormField`. **Hand-added** (RHF-based). This is the abstraction for validated forms. |
| `dialog.tsx` | `Dialog` `DialogTrigger` `DialogContent` `DialogHeader` `DialogFooter` `DialogTitle` `DialogDescription` `DialogClose`. Portals to body, focus-trap + scroll-lock + ESC for free. |
| `alert-dialog.tsx` | `AlertDialog…` family — for **destructive confirms** (delete). |
| `sheet.tsx` | `Sheet` `SheetTrigger` `SheetContent` (`side="right|left|top|bottom"`) `SheetHeader` `SheetTitle` `SheetDescription` `SheetFooter` `SheetClose` — for **side drawers**. |
| `popover.tsx` | `Popover` `PopoverTrigger` `PopoverContent`. |
| `dropdown-menu.tsx` | `DropdownMenu…` family (incl. `DropdownMenuRadioGroup`/`RadioItem`). |
| `select.tsx` | `Select` `SelectTrigger` `SelectValue` `SelectContent` `SelectItem` … (native-select replacement). |
| `command.tsx` | `Command` `CommandInput` `CommandList` `CommandEmpty` `CommandGroup` `CommandItem` (cmdk) — combobox engine for `DistillerySelect`. |
| `tabs.tsx` | `Tabs` `TabsList` `TabsTrigger` `TabsContent`. |
| `switch.tsx` | `Switch`. |
| `avatar.tsx` | `Avatar` `AvatarImage` `AvatarFallback`. |
| `sonner.tsx` | `Toaster` (already mounted in `App.tsx`; `theme="dark"` hardcoded). Use `import { toast } from 'sonner'` to fire toasts. |
| `skeleton.tsx` | `Skeleton`. |
| `tooltip.tsx` | `Tooltip` `TooltipTrigger` `TooltipContent` `TooltipProvider` (provider already in `App.tsx`). |
| `separator.tsx` | `Separator`. |
| `scroll-area.tsx` | `ScrollArea` `ScrollBar`. |
| `toggle.tsx`, `toggle-group.tsx` | `Toggle`, `ToggleGroup`/`ToggleGroupItem` (category/condition pickers). |
| `input-group.tsx` | auto-added dependency; available if useful. |

Icons: `lucide-react` (installed). Replace UI emoji/unicode/inline-SVG with named lucide imports,
e.g. `import { Bell, Check, X, Heart, MessageCircle } from 'lucide-react'`. **Keep the bespoke
bottle/shelf SVGs** in `BarShelf.tsx`.

## 8. Established patterns (follow these exactly)

### 8a. Validated forms — RHF + zod + i18n (reference: the 4 auth pages)
- Put shared regex/types in `src/lib/validation.ts` (`EMAIL_REGEX`, `PASSWORD_REGEX`, `type TFn = (key: string) => string`).
- Build the schema with a **factory closing over `t`**, memoized so messages re-localize on language switch:
  ```ts
  const makeSchema = (t: TFn) => z.object({ /* fields, messages via t('namespace.key') */ })
  type Values = z.infer<ReturnType<typeof makeSchema>>
  const schema = useMemo(() => makeSchema(t), [t])
  const form = useForm<Values>({ resolver: zodResolver(schema), defaultValues: { … } })
  ```
- **Reuse the EXISTING i18n keys** the page already used for validation (e.g. `register.errorPasswordMatch`).
  Never invent new keys unless the page truly needs new copy (then add to BOTH `src/i18n/bg.json` and `en.json`).
- Submit via the existing TanStack `useMutation`: `mutationFn: (values) => …`. On server error map to
  `form.setError('root', { message })` and render the standard error box (§6). On success keep existing `navigate`.
- Markup: `<Form {...form}><form onSubmit={form.handleSubmit(onSubmit)}>` then per field
  `<FormField control={form.control} name="…" render={({ field }) => (<FormItem><FormLabel/><FormControl><Input {...field}/></FormControl><FormMessage/></FormItem>)} />`.
- Primary submit: `<Button type="submit" size="lg" className="w-full" disabled={mutation.isPending}>`.
- zod v4 is installed: `.min(n, "msg")` (string message) and `.refine(fn, { path:['x'], message })` both work.
  Use `import type` for type-only imports (`verbatimModuleSyntax` is on).
- **Use `useWatch({ control: form.control, name })`, NOT `form.watch(name)`**, to read a live field value
  (e.g. to drive a dependent control). The React Compiler eslint plugin warns on `form.watch()`
  ("cannot be memoized safely") — `useWatch` is the compiler-safe hook. _(Learned in Slice 8.)_
- For numeric optional fields, keep them as **string** schema fields (`z.string()`) seeded `''` and convert
  `x ? Number(x) : undefined` at the mutation boundary — avoids `z.coerce.number('')→0` and RHF/`as never`
  generic friction. _(Used in Slices 7 + 8.)_

### 8b. Inline-style → token conversion
Translate hardcoded speakeasy hex to the §6 utilities. Convert `style={{…}}` constants into
`className` strings + `cn(...)`. **What STAYS inline (do NOT tokenize — genuinely dynamic/computed):**
`CATEGORY_COLORS[category]` fills, `BottleSvg` gradient stops / drop-shadow / liquid geometry,
`Avatar` size-from-prop, bottle hover `translateY/scale`, animation delays. Heavy uppercase +
letter-spacing "Cinzel" labels: prefer mira's cleaner look (`text-xs font-medium text-muted-foreground`,
optionally `uppercase tracking-wide` only where it clearly belongs, e.g. small section labels). When
in doubt, lean into the preset's clean style rather than recreating the old ornate styling.

### 8c. Overlays → Radix
| Old pattern | Target |
|---|---|
| centered modal (`fixed inset-0` + backdrop) | `Dialog` |
| right-side drawer panel | `Sheet side="right"` |
| destructive confirm | `AlertDialog` |
| click-outside dropdown of menu items | `DropdownMenu` |
| click-outside rich panel/list | `Popover` (+ `ScrollArea`) |
| hand-rolled combobox | `Command` inside `Popover` |
| custom bottom-border tabs | `Tabs` |
| custom toggle switch | `Switch` |
Replace manual `useRef`+`mousedown` click-outside and ESC handlers — Radix does it.
**`ChatWidget` keeps its custom floating shell** (see its slice doc) — do NOT convert it to a Dialog.

### 8d. i18n
Every component uses `const { t } = useTranslation()`. Preserve all `t('…')` calls verbatim.
Test every text-bearing change in **both** `bg` and `en`. Bulgarian is default; Inter includes Cyrillic.

## 9. Gotchas / lessons already learned

- **Never run `init`/add with `--template`** — that scaffolds a NEW app and overwrites config.
- **`baseUrl` is removed** from tsconfigs (TS 6 deprecation). Keep `paths` only.
- **`sonner.tsx` had a `next-themes` import** → already fixed to `theme="dark"`. If you re-add sonner, re-apply.
- **`form.tsx` is hand-added** (mira lacks it). Don't `shadcn add form`; it won't appear.
- **mira is compact** (h-7). Bump heights locally only where a focal surface needs it.
- **`init` merged our custom CSS** rather than clobbering — but unlayered rules beat `@layer base`;
  if you add global element styles, scope them or they'll override tokens.
- Generated `ui/*` components must pass strict TS (`verbatimModuleSyntax`, `noUnusedLocals`,
  `erasableSyntaxOnly`). They currently do; keep any hand-edits compliant.
- **`shadcn ScrollArea` in a flex column → use `min-h-0 flex-1`** (without `min-h-0` the flex item
  won't shrink, so a long list overflows and pushes sibling rows out of a clipped fixed-height panel),
  and **wrap its content in a single `w-full` div** (Radix's viewport is `display:table`, so unwrapped
  flex/`justify-end` content collapses to content width). Use the sentinel pattern for auto-scroll-to-
  bottom: a trailing `<div ref={endRef}/>` + `endRef.current?.scrollIntoView({block:'end'})`. _(Learned
  in Slice 5; reuse for `NotificationBell` in Slice 4 and any scrollable list.)_
- **Radix `Select` forbids `<SelectItem value="">`** (empty string is reserved for clear/placeholder).
  For an "All"/"Any" option, use a sentinel value (e.g. `const ANY = 'any'`), drive the field/state with
  it, and map `value === ANY ? undefined : value` at the query/payload boundary. _(Learned in Slice 10.)_
- **`npm run lint` is NOT clean at baseline.** Pre-existing errors live in shadcn `ui/*` (they
  co-export `*Variants` → `react-refresh/only-export-components`) and in original app files
  (`react-hooks/set-state-in-effect` in `AuthContext`, `HomePage`, `DistillerySelect`, the dead
  `MessagesPage`, etc.). A slice passes the lint gate if it adds **no new** errors — do not chase
  pre-existing ones inside a feature slice. _(Optional infra task: a `components/ui/**` eslint override
  silencing `react-refresh/only-export-components` clears the shadcn ones.)_

## 10. Verification protocol (per slice — REQUIRED)

1. `npm --prefix VirtualBar.Web run build` → must be green (`tsc -b && vite build`). This is the gate.
2. `npm --prefix VirtualBar.Web run lint` → clean.
3. Visual/functional: run the dev server (`npm run dev`, http://localhost:5173) and exercise the
   touched flow in **both bg + en**. The `e2e-tester` agent (Playwright, already installed) is the
   tool for screenshots + UI review. The backend (`dotnet run` in `VirtualBar.Api`, http://localhost:5000)
   is needed for data-bearing pages; auth pages and pure UI render without it.
4. Confirm the a11y wins the old code lacked: ESC closes modals, focus trap, body-scroll-lock,
   combobox keyboard nav.
5. Because this is a *re-skin*, the goal is NOT pixel-parity with the old look — it's the new
   amber/stone/Inter look rendering correctly with all functionality intact.

## 11. Slice index, dependencies & recommended order

Execute in ID order. Each has its own `docs/shadcn-migration/NN-*.md`.

| Slice | Doc | Depends on | Notes |
|---|---|---|---|
| 3 (remainder) | `03-auth-pages-remaining.md` | — | visual verification only; code already written |
| 4 | `04-shared-chrome.md` | 2 | NavBar, Footer, LanguageSwitcher→DropdownMenu, NotificationBell→Popover, Avatar. On every page → do early. |
| 5 | `05-chat-widget.md` | 4 (Avatar) | keep floating shell; rebuild internals with non-modal primitives |
| 6 | `06-distillery-select.md` | 2 | hand-rolled combobox → `Command` in `Popover`; preserve `(id,name)` contract |
| 7 | `07-bottle-detail-panel.md` | 2 | Dialog + AlertDialog + nested MakeOffer Dialog (RHF+zod). Shared by Dashboard + Marketplace |
| 8 | `08-dashboard-page.md` | 6, 7 | AddBottlePanel→Sheet (RHF+zod), Switch, ToggleGroup, filters |
| 9 | `09-home-page.md` | 4 | PostForm→Sheet, ConfirmDelete→AlertDialog, lang Tabs, Skeleton, news Cards |
| 10 | `10-marketplace-page.md` | 6, 7 | Tabs, publish + contact Dialogs (RHF+zod for publish), Cards. Largest inline count |
| 11 | `11-offers-page.md` | 2 | Tabs, OfferCard→Card, StatusBadge→Badge (+success/warning variants) |
| 12 | `12-profile-page.md` | 4 | inputs→shadcn, avatar upload, success/error→toast |
| 13 | `13-browse-publicbar.md` | 4, 7 | collector Cards, PublicBar header; reuses VirtualBarScene |
| 14 | `14-cleanup.md` | all | remove serif `<link>` + unused `next-themes`; delete dead `App.css`/`MessagesPage.tsx` (ASK first); route code-split; final sweep |

## 12. How to run a slice in a fresh session

1. `git checkout feat/shadcn-migration` (all migration work lives here).
2. Read `CLAUDE.md` (repo conventions), then THIS overview, then the slice's `NN-*.md`.
3. Implement only that slice. Reuse the patterns in §8. Keep `t()` keys.
4. Run the §10 verification. Do not commit unless the user asks.
5. The original high-level plan is at `.claude/plans/i-need-to-refactor-iterative-torvalds.md` (background only).

## 13. Slice-doc template (each `NN-*.md` follows this)

`Context recap` → `Goal` → `Files to touch` → `Current state` (what each file does now) →
`Transformation plan` (specific component/token mappings) → `i18n keys to preserve` →
`Slice-specific gotchas` → `Verification` → `Acceptance criteria` → `Dependencies`.

---

## 14. Shared additions ledger (do these idempotently — CHECK before adding)

Several slices need the *same* small shared additions. Because slices run in separate sessions,
this ledger is authoritative — **always check whether the thing already exists before adding it**,
so two sessions don't create duplicate definitions.

**CSS tokens (`src/index.css`):**
- `--success` / `--success-foreground` (+ `--color-success*` in `@theme inline`) — **ALREADY ADDED
  (Slice 3).** Use `text-success` / `bg-success` / `border-success`. Do **not** re-add.
- `--warning` / `--warning-foreground` (+ `--color-warning*`) — **add the first time a slice needs
  it (Slice 11 — Offers).** Mirror the existing appended `--success` blocks: add
  `--color-warning: var(--warning); --color-warning-foreground: var(--warning-foreground);` to the
  appended `@theme inline`, and `--warning`/`--warning-foreground` to the appended `:root` and
  `.dark` blocks. Suggested dark values: `--warning: oklch(0.79 0.16 86)` (amber-gold),
  `--warning-foreground: oklch(0.2 0.05 86)`.

**Badge variants (`src/components/ui/badge.tsx` → `badgeVariants` CVA):**
- `success` — **introduced by Slice 7 (BottleDetailPanel).** Suggested:
  `success: "bg-success/10 text-success [a]:hover:bg-success/20"`.
- `warning` — **introduced by Slice 11 (Offers).** Suggested:
  `warning: "bg-warning/10 text-warning [a]:hover:bg-warning/20"`.
- If your slice runs *after* the one that introduced a variant, it already exists — just use it.

**New i18n keys (add to BOTH `src/i18n/bg.json` and `src/i18n/en.json` only if missing):**
- `offers.priceInvalid` — Slice 7 (zod "price must be positive" message in Make-Offer).
- `addBottle.nameRequired` — Slice 8 (zod "name required" message in Add-Bottle).
- These are the **only** net-new keys the migration introduces; everything else reuses existing keys.

**Dead-route fix (Slice 13):** `PublicBarPage`'s message button currently calls
`navigate('/messages?with=…')` — a route that just redirects to `/`. Switch it to
`openChat(userId)` via `ChatContext` (messaging is the floating `ChatWidget`; there is no
`/messages` page). Drop the now-unused `useNavigate` import.
