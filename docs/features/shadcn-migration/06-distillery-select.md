# Slice 6 — `DistillerySelect` → `Command` (cmdk) in `Popover`

> Read `00-OVERVIEW.md` first. This doc assumes its decisions, the §6 token cheat-sheet,
> the §7 component inventory, and the §8c overlay-mapping table.

## Context recap

`DistillerySelect` is the only **hand-rolled ARIA combobox** in the app (called out in overview
§1 and §8c). It is a controlled autocomplete: a free-text `<input role="combobox">` over a
category-scoped, server-fetched distillery list, with a manual `role="listbox"`/`role="option"`
dropdown, manual `aria-activedescendant` highlight tracking, manual ArrowUp/Down/Enter/Escape
keyboard handling, manual `scrollIntoView`, and a manual `mousedown` click-outside listener.
Its entire styling is hardcoded speakeasy hex inline (overview §1). It is consumed by exactly
two callers — `DashboardPage` (AddBottle form) and `MarketplacePage` (WishList-add form).

## Goal

Replace the hand-rolled machine with shadcn **`Command` (cmdk) inside `Popover`** (overview §8c:
"hand-rolled combobox → `Command` inside `Popover`"). cmdk natively provides filtering, keyboard
navigation, and `role=listbox/option` + `aria-selected`/`aria-activedescendant` a11y; Radix
`Popover` natively provides focus management, ESC, and click-outside. Re-skin to §6 tokens.
**The public props/callback API must stay byte-identical so neither caller changes** (their page
slices are 8 and 10, executed later and independently).

## Files to touch

| File | Change |
|---|---|
| `src/components/DistillerySelect.tsx` | Full rewrite: `Popover` + `Command` body; drop all manual state machine + inline styles. |
| — callers — | **None.** `DashboardPage.tsx` / `MarketplacePage.tsx` are NOT edited in this slice. |
| `src/api/distilleriesApi.ts` | **None.** `getDistilleries(category?)` is reused as-is. |
| `src/i18n/{bg,en}.json` | **None** (reuse the two existing `distillerySelect.*` keys). |

Primitives consumed (all present from Slice 2, overview §7): `command.tsx` (`Command`/`CommandInput`
— internally renders `InputGroup`+`SearchIcon` — /`CommandList`/`CommandEmpty`/`CommandGroup`/
`CommandItem`), `popover.tsx` (`Popover`/`PopoverTrigger`/`PopoverContent`), `button.tsx`
(`Button variant="outline"`), `lucide-react` (`ChevronsUpDown`, `X`), `cn` from `@/lib/utils`.

## Current state (`DistillerySelect.tsx`, 212 lines)

**Props contract (L8–15) — preserve verbatim:**
```ts
interface Props {
  value: string | null                                          // selected distillery id (controlled)
  onChange: (id: string | null, name: string | null) => void    // (id,name) tuple
  placeholder?: string; category?: string
  style?: CSSProperties; inputStyle?: CSSProperties
}
```

**State machine (L81–161):** `query` (input text, doubling as the displayed selected label),
`open` (dropdown visibility), `highlight` (active option index), plus `wrapperRef`/`highlightedRef`.

**Async load (L88–92):** `useQuery({ queryKey: ['distilleries', category ?? 'all'],
queryFn: () => getDistilleries(category), staleTime: 5 * 60_000 })`, defaulted to `[]`. The server
returns the list already filtered by `category`.

**Effects / derived (the machinery cmdk+Popover replaces):**
- L94–101: sync `query` from `value` — null clears it, else find the match in the loaded list and
  show its `name` (async: only resolves once data loads).
- L103–105: `scrollIntoView` on highlight change. L107–111: `suggestions` = client-side
  `name.toLowerCase().includes(query)` substring filter. L113–124: `mousedown` click-outside listener.

**(id,name) contract + clear (L126–137):** `select(d)` → `onChange(d.id, d.name)` + close;
`handleInput('')` (empty text) → **`onChange(null, null)`** (the clear path).

**ARIA + keyboard (L139–209):** input is `role="combobox"` (`aria-expanded`/`-controls`/
`-activedescendant`/`-autocomplete`); dropdown `role="listbox"`, rows `role="option"` + `aria-selected`.
`handleKeyDown`: Escape closes; ArrowDown opens + `min(h+1,len-1)`; ArrowUp `max(h-1,0)`; Enter selects
`suggestions[highlight]`. Each row = `d.name` + a `region, country` meta line; empty → `noOptions`.

**Inline styles (L17–78):** `wrapper/defaultInput/dropdown/optionBase/optionActive`
(`rgba(201,168,76,0.12)`)/`optionMeta` (Cinzel 10px gold)/`noOptions` (italic) — all hardcoded hex,
to be deleted (overview §8b).

**Callers (exact props — the contract both depend on):**
```tsx
// DashboardPage.tsx L414 (AddBottle)
<DistillerySelect value={distilleryId} onChange={(id) => setDistilleryId(id)}
  category={category || undefined} placeholder={t('addBottle.distilleryPlaceholder')}
  style={{ marginBottom: 18 }} />

// MarketplacePage.tsx L886 (WishList add)
<DistillerySelect value={distilleryId} onChange={(id) => setDistilleryId(id)}
  category={category || undefined} placeholder={t('wishList.distilleryPlaceholder')} />
```
Both consume **only `id`** from `onChange` (the `name` arg is ignored), both pass `value`,
`category`, `placeholder`. Dashboard also passes `style`. **Neither passes `inputStyle`.**

## Transformation plan

Rewrite the component body around `Popover` + `Command`. The new state is a single
`const [open, setOpen] = useState(false)`. The selected label is **derived** from `value`
(not stored): `const selected = distilleries.find(d => d.id === value)`.

```tsx
export default function DistillerySelect({ value, onChange, placeholder, category, style }: Props) {
  const { t } = useTranslation()
  const [open, setOpen] = useState(false)

  const { data: distilleries = [] } = useQuery({
    queryKey: ['distilleries', category ?? 'all'],   // UNCHANGED — keep category scoping
    queryFn: () => getDistilleries(category),
    staleTime: 5 * 60_000,
  })

  const selected = distilleries.find(d => d.id === value) ?? null

  return (
    <div className="relative w-full" style={style}>
      <Popover open={open} onOpenChange={setOpen}>
        <PopoverTrigger asChild>
          <Button variant="outline" role="combobox" aria-expanded={open}
            className={cn('w-full justify-between font-normal',
              value && 'pr-9', !selected && 'text-muted-foreground')}>
            <span className="truncate">
              {selected ? selected.name : (placeholder ?? t('distillerySelect.placeholder'))}
            </span>
            <ChevronsUpDown className="size-3.5 shrink-0 opacity-50" />
          </Button>
        </PopoverTrigger>
        <PopoverContent align="start"
          className="w-(--radix-popover-trigger-width) p-0">
          <Command>
            <CommandInput placeholder={t('distillerySelect.placeholder')} />
            <CommandList>
              <CommandEmpty>{t('distillerySelect.noOptions')}</CommandEmpty>
              <CommandGroup>
                {distilleries.map(d => (
                  <CommandItem key={d.id} value={d.name}
                    onSelect={() => { onChange(d.id, d.name); setOpen(false) }}>
                    <div className="flex flex-col gap-0.5">
                      <span>{d.name}</span>
                      {(d.region || d.country) && (
                        <span className="text-[10px] uppercase tracking-wide text-muted-foreground">
                          {[d.region, d.country].filter(Boolean).join(', ')}
                        </span>
                      )}
                    </div>
                  </CommandItem>
                ))}
              </CommandGroup>
            </CommandList>
          </Command>
        </PopoverContent>
      </Popover>
      {value && (
        <button type="button" aria-label={t('distillerySelect.placeholder')}
          onClick={(e) => { e.stopPropagation(); onChange(null, null) }}
          className="absolute right-7 top-1/2 -translate-y-1/2 text-muted-foreground hover:text-foreground">
          <X className="size-3.5" />
        </button>
      )}
    </div>
  )
}
```

**Behavior-by-behavior mapping (old → new):**

| Current behavior | New mechanism |
|---|---|
| `open` state + `onFocus`/Escape toggling | `Popover open/onOpenChange`; trigger toggles, ESC + click-outside free (overview §8c, §10.4) |
| `query` text + `suggestions` `includes()` filter | cmdk's built-in filter over each `CommandItem value={d.name}` |
| `highlight` index + ArrowUp/Down/Enter | cmdk native keyboard nav + active-descendant |
| `highlightedRef.scrollIntoView` effect | cmdk native scroll-into-view (`CommandList scroll-py-1`) |
| `mousedown` click-outside `useEffect` | Radix `Popover` (delete the listener) |
| `value → query` sync `useEffect` | `selected = distilleries.find(d => d.id === value)` derived label |
| `select(d)` → `onChange(d.id, d.name)` | `CommandItem onSelect` closes over `d` → `onChange(d.id, d.name)` |
| empty-text path → `onChange(null, null)` | the `X` clear button → `onChange(null, null)` |
| `role=combobox/listbox/option`, `aria-*` (manual) | `Button role="combobox"` + cmdk's native listbox/option roles |
| `noOptions` div | `CommandEmpty` |

**Props contract — explicitly preserved (callers need ZERO changes):**
- Keep the `Props` interface **exactly as-is**, including `inputStyle?`, so the type callers compile
  against is byte-identical. Destructure `value, onChange, placeholder, category, style`; **do NOT
  destructure `inputStyle`** (no caller passes it — an unused destructured binding trips
  `noUnusedLocals`, §9; declared-but-unextracted keeps the API identical and stays strict-TS clean).
- `style` spreads onto the wrapper `<div>` so Dashboard's `style={{ marginBottom: 18 }}` still
  applies (caller layout, not theme — passthrough, don't tokenize).
- `onChange` stays `(id, name)` — select fires `(d.id, d.name)`, clear fires `(null, null)`; both
  callers ignoring `name` is unaffected.

**Token mapping (inline hex → §6 utilities, overview §8b):**

| Old inline | New |
|---|---|
| `defaultInputStyle` (`#0A0502` bg, gold border, `#F0DDB4`, Cormorant, radius 4) | `Button variant="outline"` (bg-input/border-input/text-foreground/rounded, Inter) |
| `dropdownStyle` (`#0D0804`, gold border, `maxHeight:220`, shadow) | `PopoverContent` (`bg-popover`, `ring-foreground/10`, `shadow-md`, `rounded-lg`) + `CommandList max-h-72` |
| `optionActiveStyle` (`rgba(201,168,76,0.12)`) | `CommandItem` `data-selected:bg-muted` (built-in) |
| `optionMetaStyle` (Cinzel 10px `#7A6040` uppercase) | `text-[10px] uppercase tracking-wide text-muted-foreground` |
| `noOptionsStyle` (italic `#B09868`) | `CommandEmpty` (`py-6 text-center text-xs`) |

## i18n keys to preserve

Reuse the **two existing** keys (verified in `bg.json`/`en.json` L370–372) — invent none:
- `distillerySelect.placeholder` — bg `"Търси дестилерия..."` / en `"Search distillery..."`.
  Used for (a) the `CommandInput` search hint, and (b) the trigger fallback label when the caller's
  `placeholder` prop is absent, and (c) the `X` button `aria-label`.
- `distillerySelect.noOptions` — bg `"Няма намерени дестилерии"` / en `"No distilleries found"` →
  `CommandEmpty` text.

Caller-supplied placeholder props (`addBottle.distilleryPlaceholder`, `wishList.distilleryPlaceholder`)
flow through the `placeholder` prop unchanged → shown as the trigger's empty label.

## Slice-specific gotchas

- **Keep the selected value controlled & derived.** The label comes from
  `distilleries.find(d => d.id === value)`, never from search text — store no "selected name" in
  state. If `value` is set before the query resolves (or isn't in the loaded category list) the
  label briefly falls back to the placeholder — same async limit as the old `value→query` effect,
  harmless since both callers init `value = null`.
- **Don't trust cmdk's `onSelect` string** (it lowercases the item `value`). Close over the mapped
  `d` instead (official shadcn pattern): `onSelect={() => { onChange(d.id, d.name); setOpen(false) }}`.
- **`CommandItem value={d.name}`** — must be unique for cmdk identity; safe because `Distillery.Name`
  has a **unique index** (CLAUDE.md). No id suffix needed.
- **cmdk filtering vs server data:** two layers preserved — **server** filters by `category`
  (`queryKey`/`queryFn`), **cmdk** filters the returned list by typed text client-side. cmdk's
  default filter is fuzzy/subsequence (slightly more lenient than the old `includes()` — acceptable
  UX win); scope stays name-only (optionally add `keywords={[d.region, d.country]}` — not required).
- **Keep the category filter intact** (`queryKey: ['distilleries', category ?? 'all']`, `queryFn`
  unchanged). The caller already resets `value` to `null` on category change (Marketplace's
  `<select>` calls `setDistilleryId(null)`), so the trigger reverts while the new list refetches.
- **Clear `X` is a sibling, not a child, of the trigger Button** — nesting `<button>` in `<button>`
  is invalid HTML and would toggle the popover. Render `X` absolutely positioned (`right-7`, shown
  only when `value` set, `e.stopPropagation()`); give the trigger `pr-9` for room.
- **`PopoverContent`**: override default `w-72` with `w-(--radix-popover-trigger-width) p-0` +
  `align="start"` so it matches the trigger and `Command` owns its padding.
- **Delete dead imports** (`useEffect`/`useMemo`/`useRef`/`KeyboardEvent`/`CSSProperties` style
  consts); keep `useState`/`useQuery`/`useTranslation`/`getDistilleries`/`type Distillery`. Must pass
  `verbatimModuleSyntax` (`import type` for `Distillery`) and `noUnusedLocals` (overview §9).

## Verification (overview §10)

1. `npm --prefix VirtualBar.Web run build` → green (`tsc -b && vite build`); strict TS passes,
   including the `inputStyle`-not-destructured point.
2. `npm --prefix VirtualBar.Web run lint` → clean.
3. Dev server, exercise in **both bg and en** (backend `dotnet run` needed — the list is server-fed):
   - **Open** (click trigger → search + full list) · **type to filter** (cmdk narrows; clearing
     restores) · **keyboard select** (ArrowUp/Down + Enter → trigger shows name, `onChange(id,name)`
     fires, form `distilleryId` updates) · **ESC / click-outside** both close (Radix) · **clear**
     (the `X` shows once selected → `onChange(null,null)`, trigger reverts to placeholder).
   - **Category scoping**: change category in Marketplace WishList → list refetches and prior
     selection resets; Dashboard AddBottle scopes via the `category` prop. Confirm `queryKey` refetch.
   - **`CommandEmpty`**: type gibberish → `distillerySelect.noOptions` renders (localized).
4. a11y wins the old code lacked (overview §10.4): Radix focus management + ESC + click-outside;
   cmdk's native `role=listbox/option`, `aria-selected`, active-descendant.

## Acceptance criteria (overview §10)

- [ ] `npm run build` green; `npm run lint` clean.
- [ ] `DistillerySelect.tsx` contains **no** hardcoded hex / `CSSProperties` style consts; trigger,
      popover, list, items, empty, and meta all use §6 tokens (amber/stone/Inter re-skin — not pixel-parity).
- [ ] No manual `role`/`aria-activedescendant`/`mousedown` listener/`scrollIntoView`/keyboard
      handler remains; behavior is provided by `Popover` + `Command`.
- [ ] Props interface (incl. `inputStyle?`) is byte-identical; **`DashboardPage.tsx` and
      `MarketplacePage.tsx` are unmodified** and compile/run unchanged.
- [ ] `onChange(id, name)` on select and `onChange(null, null)` on clear both fire correctly.
- [ ] `queryKey: ['distilleries', category ?? 'all']`, `queryFn`, and `staleTime: 5*60_000` unchanged.
- [ ] `CommandEmpty` shows `distillerySelect.noOptions`; search hint shows `distillerySelect.placeholder`;
      both correct in bg + en.

## Dependencies

- **Requires Slice 2** (overview §11): `command.tsx`, `popover.tsx`, `button.tsx`, and
  `input-group.tsx` (used internally by `CommandInput`) — all already present.
- **Independent** of Slices 4, 5, 7. Self-contained; can run in any order after Slice 2.
- **Blocks nothing structurally**, but Slices **8 (Dashboard)** and **10 (Marketplace)** consume
  this component — preserving the public API here means those slices migrate their pages without
  having to touch the distillery picker.
