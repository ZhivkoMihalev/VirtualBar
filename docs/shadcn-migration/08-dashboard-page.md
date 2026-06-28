# Slice 8 — DashboardPage (the user's Virtual Bar)

> Read `00-OVERVIEW.md` first. This slice depends on **Slice 6** (`DistillerySelect` → `Command`
> in `Popover`) and **Slice 7** (`BottleDetailPanel` → `Dialog` + `AlertDialog`), both of which this
> page composes unchanged-by-contract. Follow the RHF+zod+`Form` reference established by the auth
> pages (§8a). End with a green `npm run build`.

## Context recap

`DashboardPage.tsx` is the owner's virtual bar: a header, an optional stats row, the bespoke
**`VirtualBarScene`** shelf (kept as-is — product art, §3/§8b), a category **filter** strip, an empty
state, the **`AddBottlePanel`** right-side drawer (a large form), and the **`BottleDetailPanel`**
overlay. It is ~89% inline-styled speakeasy hex. The drawer is the heaviest piece: a 10-field add-bottle
form plus image upload, drag-and-drop, and barcode auto-fill.

## Goal

Re-skin the page to amber/stone/Inter tokens and move its overlays/controls onto shadcn:
`AddBottlePanel` → **`Sheet side="right"`** wrapping an **RHF + zod `Form`**; the category/condition
button grids → **`ToggleGroup`** (single-select); the filter pills → **`ToggleGroup`** toggles; the
stats box → tokens (`Card` + `Separator`); the `isLimited` hand-toggle → **`Switch`**; the dropzone +
barcode row → tokens + lucide icons. **Keep `VirtualBarScene`/`BottleSvg`/`CATEGORY_COLORS` untouched.**
Preserve every `useQuery`/`useMutation` and the detail-panel open/close + `onDelete` wiring.

## Files to touch

- `src/pages/DashboardPage.tsx` — full rewrite of chrome + `AddBottlePanel`; `CategoryPill`, `StatItem`,
  `inputStyle`/`labelStyle`/`focusOn`/`focusOff` deleted.
- `src/i18n/bg.json` + `src/i18n/en.json` — add **one** new key `addBottle.nameRequired` (see i18n §).
- **Do NOT touch** `src/components/BarShelf.tsx` (kept), `DistillerySelect.tsx` (Slice 6),
  `BottleDetailPanel.tsx` (Slice 7), `src/api/bottlesApi.ts` (unchanged).

## Current state (what the file does now)

**Module scope:** `CATEGORIES = Object.keys(CATEGORY_COLORS)`; `CONDITIONS = ['Sealed','Opened','Empty']`.
Inline-styled helpers to be removed: `CategoryPill` (filter pill w/ hover state), `StatItem`
(`statValueStyle` Playfair-24-gold, `statLabelStyle` Cormorant uppercase), shared `inputStyle`,
`labelStyle`, `focusOn`/`focusOff` border-swap handlers.

**`AddBottlePanel({ onClose, onSuccess })`** — fixed `inset:0 z-50` backdrop (`onClick=onClose`) + a
480px right panel (gradient bg, `borderLeft`, `overflowY:auto`, `fadeInUp`). Local `useState`:
`category`('Whisky'), `condition`('Sealed'), `name`, `distilleryId`, `age`, `abv`, `volume`,
`isLimited`, `description`, `imageFile`, `imagePreview`, `barcodeImageUrl`, `dropHover`, `barcode`,
`barcodeLoading`, `barcodeStatus`('idle'|'found'|'error'). Logic:
- `handleImageChange(file)` — set `imageFile`, clear `barcodeImageUrl`, set object-URL `imagePreview`.
- `handleBarcodeSearch()` — `lookupBarcode(code)`; on hit set `name`/`volume`/`abv`/`imagePreview`
  (+ `barcodeImageUrl`, clear `imageFile`), status `found`; `catch` → status `error`.
- `mutation` — `addBottle(payload)`; then `if (imageFile) uploadBottleImage` else
  `if (barcodeImageUrl) linkBottleImage`; `onSuccess → onSuccess()`.
- `handleSubmit` — `preventDefault`, guard `!name.trim()` → return, build `AddBottlePayload`
  (numerics `x ? Number(x) : undefined`, `description.trim() || undefined`), `mutation.mutate`.
- Markup: header (`addBottle.title` + × close); dropzone (`onDragOver/Leave/Drop`, click → hidden
  `#bottle-image-input`; preview `<img>` w/ hover "change photo", else 📷 + `uploadPhoto`/`clickOrDrag`);
  `<BottleSvg category condition />` live preview (width 50); `<form>`: barcode row (input + FIND
  button, `···` while loading, found/error/idle status lines); category 4-col button grid (color dot
  `c.glass` + `c.label`, click sets category **and resets `distilleryId` to null**); name input
  (`required`); `<DistillerySelect value onChange category style={{marginBottom:18}} />`; age/abv/volume
  3-col `type="number"` grid; condition 3-col button grid (`addBottle.condition${cond}`); `isLimited`
  click-row with a hand-rolled sliding knob; description `<textarea>`; error line (`addBottle.error`);
  submit button (disabled `isPending || !name.trim()`, `submitting`/`submit`).

**`DashboardPage`** — `useAuth`, `useQueryClient`; state `addOpen`, `selectedBottle`, `activeCategory`.
Hooks: `useQuery(['bottles', user?.id] → getBottlesByUser, enabled: !!user?.id)`; `displayedBottles`
(`useMemo`, filter by `activeCategory`); `categoryCounts` (`useMemo`). Render: `NavBar`; header
(`yourCollection`, `virtualBar` h1, `collectionAwaits`/`bottlesInCollection` subtitle) + amber "add"
button (`dashboard.addBottle` → `setAddOpen(true)`); stats row when `bottles.length>0`
(`totalBottles`/`sealed`/`forSale`/`limited` `StatItem`s + `w:1` dividers); shimmer loading
(`dashboard.loading`); `<VirtualBarScene bottles={displayedBottles} onAdd onSelect />` when loaded;
filter strip of `CategoryPill`s (an "all" pill = `marketplace.allCategories`, then per-category where
`count>0`); empty state (`emptyTitle`/`emptySubtitle`/`emptyButton`). Conditionals: `addOpen &&
<AddBottlePanel onClose onSuccess={invalidate+close} />`; `selectedBottle && user && <BottleDetailPanel
bottle={bottles.find(b=>b.id===selectedBottle.id) ?? selectedBottle} userId currentUserId onClose
onDelete={invalidate+close} />`.

## Transformation plan

### Drawer shell → `Sheet side="right"`
- Lift open state to the page: `<Sheet open={addOpen} onOpenChange={setAddOpen}>` with
  `<SheetContent side="right" className="w-full sm:max-w-[480px] overflow-y-auto">`. Delete the manual
  backdrop, the 480px panel div, the `fadeInUp` style, and the × button (Sheet ships overlay + slide +
  focus-trap + ESC + scroll-lock + a built-in close). `<SheetHeader><SheetTitle>{t('addBottle.title')}`.
- The 3 triggers (header button, empty-state button, shelf `EmptySlot` via `VirtualBarScene onAdd`) all
  keep `onClick={() => setAddOpen(true)}` — controlled Sheet, not `SheetTrigger` (multiple entry points).
- `AddBottlePanel` becomes the SheetContent body; `onSuccess` still invalidates `['bottles', user?.id]`
  and closes via `setAddOpen(false)`.

### §8a form — RHF + zod factory
```ts
const emptyToUndef = (v: unknown) => (v === '' || v == null ? undefined : Number(v))
const makeSchema = (t: TFn) => z.object({
  name:        z.string().trim().min(1, t('addBottle.nameRequired')),
  category:    z.enum(['Whisky','Rum','Cognac','Vodka','Gin','Tequila','Brandy','Other']),
  condition:   z.enum(['Sealed','Opened','Empty']),
  distilleryId: z.string().nullable(),
  age:         z.preprocess(emptyToUndef, z.number().int().min(0).max(100).optional()),
  abv:         z.preprocess(emptyToUndef, z.number().min(0).max(100).optional()),
  volume:      z.preprocess(emptyToUndef, z.number().int().min(0).optional()),
  isLimited:   z.boolean(),
  description: z.string().trim().optional(),
})
type Values = z.infer<ReturnType<typeof makeSchema>>
const schema = useMemo(() => makeSchema(t), [t])
const form = useForm<Values>({ resolver: zodResolver(schema), defaultValues: {
  name:'', category:'Whisky', condition:'Sealed', distilleryId:null,
  age:'', abv:'', volume:'', isLimited:false, description:'' } as never })
```
- `onSubmit(values)` — values are already coerced (`age/abv/volume` → `number|undefined`,
  `distilleryId` → `string|null`, `description` → `string|''`). Build `AddBottlePayload` (use
  `description || undefined`), `mutation.mutate(payload)`. Server error → `form.setError('root', {
  message: t('addBottle.error') })` rendered in the standard **error box** (§6). On success → `onSuccess()`.
- Image/barcode state stays plain `useState` (not schema fields). The mutation reads `imageFile` /
  `barcodeImageUrl` from closure exactly as today (two-step add-then-image flow unchanged).
- Per field: `<FormField control={form.control} name="…" render={({ field }) => (<FormItem><FormLabel/>
  <FormControl>…</FormControl><FormMessage/></FormItem>)} />`. Name → `<Input {...field}/>`;
  description → `<Textarea {...field} rows={3}/>`; numerics → `<Input type="number" min={0} max={100}
  step={…} {...field}/>` (keep HTML bounds as a browser guard alongside zod). Submit:
  `<Button type="submit" size="lg" className="w-full" disabled={mutation.isPending}>` →
  `submitting`/`submit`.

### `ToggleGroup` mappings
| Old | New |
|---|---|
| category 4-col `<button>` grid | `<ToggleGroup type="single" className="grid grid-cols-4 gap-2" value={field.value} onValueChange={(v)=>{ if(v){ field.onChange(v); form.setValue('distilleryId', null) } }}>` → `<ToggleGroupItem value={cat} className="flex-col gap-1">` with the **inline** color dot `<span style={{background: CATEGORY_COLORS[cat].glass}} className="size-2.5 rounded-full"/>` + `{CATEGORY_COLORS[cat].label}` |
| condition 3-col `<button>` grid | `<ToggleGroup type="single" className="grid grid-cols-3 gap-2" value={field.value} onValueChange={(v)=>{ if(v) field.onChange(v) }}>` → `<ToggleGroupItem value={cond}>{t('addBottle.condition'+cond)}` |
| filter pills (`CategoryPill`×N) | `<ToggleGroup type="single" value={activeCategory ?? 'all'} onValueChange={(v)=> setActiveCategory(!v || v==='all' ? null : v as SpiritCategory)} className="flex flex-wrap gap-2">` → an "all" item (`marketplace.allCategories`, `bottles.length`) + per-category items where `categoryCounts[cat]>0`, each `{label}<span className="ml-1.5 opacity-70">{count}</span>` |

Delete the `CategoryPill` component. The active **amber `data-[state=on]`** styling replaces the old
per-category `glass` coloring on the filter (§8b: lean into the preset; `CATEGORY_COLORS` is kept only
for the bottle art and the in-form category dot). Optionally retain a tiny color dot per filter item.

### Inline → token map
| Element | Tokens |
|---|---|
| page header `yourCollection` label | `text-xs uppercase tracking-widest text-muted-foreground` |
| `virtualBar` h1 | `font-heading text-3xl sm:text-4xl font-bold text-primary` |
| subtitle (`collectionAwaits`/`bottlesInCollection`) | `text-lg italic text-primary/90` |
| "add bottle" button | `<Button>` (default amber); empty-state button → `<Button variant="outline">` |
| stats row | `<Card><CardContent className="flex gap-6 px-6 py-4">` ; value `text-2xl font-semibold text-primary`, label `text-xs uppercase tracking-wider text-muted-foreground`; dividers → `<Separator orientation="vertical" className="h-auto" />` |
| loading | tokenized muted line `text-primary animate-[shimmer_1.6s_ease-in-out_infinite]` (keyframe kept in `index.css`), or a couple `<Skeleton>` blocks |
| dropzone | `rounded-md border-2 border-dashed border-primary/25`; `dropHover` → `cn('border-primary/70 bg-primary/5')`; camera 📷 → lucide `<Camera/>` / `<ImagePlus/>`; hover overlay → `bg-black/45 text-primary`; keep `dropHover` state + drag handlers + hidden input |
| barcode row | `<Input>` + `<Button variant="secondary">` (`barcodeFind`); `···` → lucide `<Loader2 className="animate-spin"/>`; status → **success box** (§6) for `found` (lucide `Check`), **error box** for `error`; drop the idle spacer (Form spacing handles gaps) |
| `isLimited` toggle | `<FormItem className="flex items-center justify-between rounded-md border p-3"><FormLabel>{t('addBottle.limited')}</FormLabel><FormControl><Switch checked={field.value} onCheckedChange={field.onChange}/></FormControl></FormItem>` |
| error line | standard error box (§6) bound to `form.formState.errors.root` |

### Stays inline / untouched (§3/§8b)
`VirtualBarScene` and all of `BarShelf.tsx` (shelf, `BottleCard`, `EmptySlot`, `BottleSvg`); the
`CATEGORY_COLORS[cat].glass` dot in the category ToggleGroup; the `<BottleSvg category condition>` live
preview; the dropzone's `dropHover`/drag handlers (genuinely dynamic — only its colors get tokenized).

### Composing the slice-6/7 dependencies
- **`DistillerySelect`** (Slice 6 = `Command` in `Popover`): wrap in `<FormField name="distilleryId">`;
  drop the old `style={{marginBottom:18}}`/`inputStyle` props (Slice 6 replaces the style API). Drive via
  `field.value` + `onChange={(id) => field.onChange(id)}` (the `(id, name)` contract is preserved). Pass
  `category={form.watch('category')}` so it filters; the category ToggleGroup's `form.setValue
  ('distilleryId', null)` preserves the "category change clears distillery" behavior.
- **`BottleDetailPanel`** (Slice 7 = controlled `Dialog`): keep `selectedBottle` state + the fresh-data
  lookup `bottles.find(b=>b.id===selectedBottle.id) ?? selectedBottle`; map `onClose →
  setSelectedBottle(null)` (Slice 7's `open`/`onOpenChange`), keep `onDelete` (invalidate `['bottles',
  user.id]` + close). Do not wrap it in another Dialog.

## i18n keys to preserve (all current `t()` calls)

`dashboard.*`: `yourCollection`, `virtualBar`, `bottlesInCollection` (`_one`/`_other`, `{count}`),
`addBottle`, `totalBottles`, `sealed`, `forSale`, `limited`, `loading`, `emptyTitle`, `emptySubtitle`,
`emptyButton`, `collectionAwaits`.
`addBottle.*`: `title`, `uploadPhoto`, `clickOrDrag`, `changePhoto`, `barcodeLookup`,
`barcodePlaceholder`, `barcodeFind`, `barcodeSuccess`, `barcodeNotFound`, `category`, `name`,
`namePlaceholder`, `distillery`, `distilleryPlaceholder`, `age`, `abv`, `volume`, `condition`,
`conditionSealed`, `conditionOpened`, `conditionEmpty`, `limited`, `description`,
`descriptionPlaceholder`, `error`, `submit`, `submitting`.
`marketplace.allCategories` (the "All" filter chip).
`barShelf.addBottle` / `barShelf.virtualBar` — used **inside** the unchanged `VirtualBarScene`;
preserved implicitly (do not touch).

**New key (the one §8a exception — add to BOTH `bg.json` + `en.json`):** `addBottle.nameRequired`
— the zod required message (old UI only disabled the button, so there is genuinely no message key today).
Suggested copy: BG `"Въведете наименование."`, EN `"Name is required."`.

## Slice-specific gotchas

- **Sheet, not Dialog.** A right-side drawer for a wide+tall form → `Sheet side="right"` (§8c). mira's
  default `SheetContent` is narrow (`sm:max-w-sm`) — **widen** with `className="w-full sm:max-w-[480px]"`
  and add `overflow-y-auto` (the content scrolls, the page doesn't). Drop the manual `fadeInUp`; Sheet
  slides itself.
- **Numeric coercion.** `z.coerce.number('')` yields `0`, not `undefined` — wrong for optional fields.
  Use `z.preprocess(emptyToUndef, z.number()…optional())` so blank → `undefined`. Keep the fields as
  **strings** in `defaultValues` (`age:''`) and on the `<Input>` to match the preprocess pipeline.
- **`ToggleGroup type="single"` can emit `''`** on deselect. Guard `if (v)` in `onValueChange` for
  category and condition (both are required — always exactly one selected; defaults seed them).
- **Barcode auto-fill now targets RHF:** replace `setName/setVolume/setAbv` with `form.setValue('name',
  product.name)`, `form.setValue('volume', String(product.volumeMl))`, `form.setValue('abv',
  String(product.abvPercent))` (numerics as **strings**, matching preprocess). Image
  preview/`imageFile`/`barcodeImageUrl` stay local state.
- **`Switch`** uses `checked`/`onCheckedChange`, not `value`/`onChange`; inside RHF map
  `checked={field.value} onCheckedChange={field.onChange}`. Keep the `◆` in `addBottle.limited`.
- **DistillerySelect contract**: only `value`/`onChange(id,name)`/`category`/`placeholder` survive Slice 6;
  remove the `style` prop here.
- `import type` for type-only imports (`SpiritCategory`, `Values`, `TFn`) — `verbatimModuleSyntax` is on.
  Delete now-unused `CSSProperties`/`AddBottlePayload`-helper imports if they go unreferenced.

## Verification (§10 — run in **both bg + en**)

1. `npm --prefix VirtualBar.Web run build` and `… run lint` → green.
2. `npm run dev` (5173) + backend `dotnet run` (5000, data-bearing page).
3. **Sheet**: open from the header button, the empty-state button, and a shelf **`EmptySlot` "+"** — all
   open the one drawer; ESC, overlay click, and the built-in close all dismiss it; focus is trapped and
   body scroll is locked.
4. **Form**: submit with empty name → localized `addBottle.nameRequired` under the field; category +
   condition ToggleGroups single-select with amber on-state; toggle `isLimited` `Switch`; enter
   age/abv/volume (blank stays blank, no spurious `0`); barcode FIND with a real code auto-fills
   name/volume/abv + preview (success box), a bad code → error box; drag-drop an image → preview + hover
   "change photo"; the `BottleSvg` preview reflects the chosen category/condition.
5. Submit valid → bottle added, `['bottles']` invalidated, Sheet closes, the bottle appears on the shelf.
6. **Filter** ToggleGroup: pick a category → shelf filters, counts correct, "All" resets.
7. Click a bottle → `BottleDetailPanel` (Dialog) opens; as owner shows Sale + Delete; **delete**
   invalidates and closes.
8. Toggle БГ↔EN: header, labels, placeholders, ToggleGroup labels, and validation messages re-localize.
9. No console errors/warnings.

## Acceptance criteria

- Build + lint green; dark amber/stone/**Inter** theme; `VirtualBarScene`/`BottleSvg`/`CATEGORY_COLORS`
  untouched.
- `AddBottlePanel` is a `Sheet side="right"` (overlay, ESC, focus-trap, scroll-lock free); no hand-rolled
  backdrop or × button; content widened + scrollable.
- The form is RHF+zod via shadcn `Form`; name-required validates and re-localizes; numerics coerce
  blank→`undefined`; **all** existing `dashboard.*`/`addBottle.*` keys preserved; exactly one new key
  (`addBottle.nameRequired`) added to both `bg.json` + `en.json`.
- Category + condition pickers are `ToggleGroup` single-select (guarded against empty); category change
  still resets `distilleryId`; filter pills are `ToggleGroup` toggles wired to `activeCategory`.
- `isLimited` is a shadcn `Switch`; dropzone + barcode row tokenized with lucide icons and §6
  success/error boxes; image-upload + barcode + add mutations behave exactly as before.
- Stats row uses tokens (`Card` + `Separator`).
- `DistillerySelect` (Slice 6) and `BottleDetailPanel` (Slice 7) compose with their contracts intact
  (`(id,name)`; `onClose`/`onDelete`; fresh-data lookup).
- All `useQuery`/`useMutation` (list, add, image upload, image link, barcode) preserved; works in bg+en;
  no console errors.

## Dependencies

- **Slice 6** (`DistillerySelect` → `Command`/`Popover`) and **Slice 7** (`BottleDetailPanel` → `Dialog`)
  — must land first; this page composes both.
- **Slice 2** primitives: `Sheet`, `ToggleGroup`, `Switch`, `Form`/`FormField`, `Input`, `Textarea`,
  `Button`, `Card`, `Separator`, `Badge`, plus `lucide-react` icons.
- **Slice 4** `NavBar` (already migrated) is rendered here.
- Mirrors the §8a auth-page reference for the validated form.
