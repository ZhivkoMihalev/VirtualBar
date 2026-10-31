# Slice 10 — MarketplacePage (Tabs + publish/contact Dialogs + Cards)

> Read `00-OVERVIEW.md` first. This slice has the **largest inline-style count in the app**
> (~88 module-level `CSSProperties` constants + several inline `style={{…}}` blobs). It is also
> the second consumer of two already-migrated building blocks: **`DistillerySelect`** (Slice 6) and
> **`BottleDetailPanel`** (Slice 7). Do not re-migrate those here — import and reuse them.

## Context recap

`src/pages/MarketplacePage.tsx` is a single ~1200-line file containing **one page + four nested
components + one presentational pill + two helper functions**. It renders two independent tabs:

- **FOR SALE** (`marketplace.tabSale`) — a filterable/sortable grid of bottles listed for sale.
- **LOOKING FOR** (`marketplace.tabSearch`) — the public wish-list ("searches") feed, where any
  authenticated user can publish a search and contact other searchers.

It pulls in `BottleDetailPanel` (opened from a sale card) and reaches into `ChatContext.openChat`
(from the contact modal) — so this page wires together two cross-cutting overlays. It is the
canonical `Tabs` + double-`Dialog` page of the migration.

## Goal

Re-skin the whole page to the amber/stone/Inter token system (§6) on shadcn primitives, with **zero
behavioural change**: same two data sets, same filters, same wish-list CRUD, same "open detail
panel" and "open chat" wiring, same i18n. Convert the custom tab bar → `Tabs`, both card types →
`Card`, the two hand-rolled modals → `Dialog`, the publish form → a §8a RHF+zod form, the
URL/upload toggle → `Switch`, and the filters → `Input`/`Select`/`Badge`. End on a green build.

## Files to touch

- `src/pages/MarketplacePage.tsx` — the whole rewrite (strip ~88 style constants).
- `src/lib/validation.ts` — optional: export the publish schema factory, or keep it co-located. No
  new shared regex needed.
- `src/i18n/bg.json` / `en.json` — **no new keys required** (every label already exists, see below).
- Do **not** touch `DistillerySelect.tsx`, `BottleDetailPanel.tsx`, `BarShelf.tsx`, `ChatContext.tsx`,
  `wishListApi.ts`, `bottlesApi.ts`, `messagesApi.ts`.

## Current state (what the file does today)

**Module-level state at the page root (`MarketplacePage`):** `tab: 'sale' | 'search'` and
`selectedBottle: Bottle | null`. Renders `NavBar`, a Cinzel label + Playfair `<h1>` (title switches
on `tab`), the custom tab bar (two `<button>`s toggling `tab`), then either `<SaleTab>` or
`<SearchTab>`, and a conditional `<BottleDetailPanel>` when `selectedBottle` is set
(`onClose={() => setSelectedBottle(null)}`, `currentUserId={user?.id ?? ''}`).

**`SaleTab`** (data hook #1 — sale listings):
- Local state `search`, `category`, `sort: 'newest'|'price_asc'|'price_desc'`; `useDebounced(search, 300)`.
- `useQuery(['marketplace', debouncedSearch, category, sort], () => getMarketplace({ search, category, sort }))`
  → `bottlesApi.getMarketplace` → `GET /bottles/marketplace`.
- Filters row: a search `<input>` with a 🔍 emoji prefix; a row of `CategoryPill`s ("All" + every
  `CATEGORY_COLORS` key); a sort `<select>` (3 options).
- Renders a responsive `grid` (`repeat(auto-fill, minmax(280px,1fr))`) of `MarketplaceCard`, plus a
  shimmer loading line (`marketplace.loading`) and an italic empty state (`marketplace.empty`).

**`MarketplaceCard`** (presentational): hover state via `useState`; image header (`primaryImage` or
`BottleSvg` fallback over a `radial-gradient` tinted with `CATEGORY_COLORS[cat].glow`); a category
pill (`cat.label`/`cat.glass`), a condition pill (`conditionColor[condition]`, text via
`addBottle.condition{Condition}`), a `◆` for `isLimited`; Playfair name; italic distillery; a specs
row (`age yr` / `abv% ABV` / `volumeMl ml`); price (`askingPrice.toLocaleString()` + `currency`, else
`marketplace.priceOnRequest`); a seller `<Link to={`/bar/${userId}`}>` (`marketplace.by`); and a
full-width "View Bottle" `<button onClick={onView}>` that lifts the bottle to `selectedBottle`.

**`SearchTab`** (data hooks #2–#4 — wish-list read + add + image upload):
- `useQuery(['wishlist','all'], getAllWishListItems)` → `GET /wishlist/all` → `PublicWishListItem[]`.
- `uploadMutation = useMutation(uploadWishListImage)` → `POST /wishlist/image` (multipart),
  `onSuccess` sets `imageUrl`.
- `addMutation = useMutation(addWishListItem)` → `POST /wishlist`; `onSuccess` invalidates
  `['wishlist','all']`, clears all fields, resets the toggle and `uploadMutation`, closes the modal.
- Local form state: `bottleName`, `distilleryId`, `category: SpiritCategory | ''`, `imageUrl`,
  `imageTab: 'url'|'upload'`, `validationError`, `modalOpen`.
- A gold "POST A SEARCH" button (auth only) opens a hand-rolled modal (backdrop + panel + `×`).
  The form: a label-only **Description** input (`bottleName` — NOT used in matching), a
  `DistillerySelect` (filtered by `category`), a Category `<select>` (changing it **resets
  `distilleryId`**), an image **URL vs Upload** toggle (two buttons) → either a URL `<input>` or a
  hidden-file-input "CHOOSE FILE" label, an 80×80 preview with a `×` remove, plus inline
  `validationError` / `addMutation.isError` messages and a submit button.
- **Validation rule (mirrors the backend `WishListValidationDecorator`):** at least one of
  `distilleryId` or `category` must be set, else `setValidationError(t('wishList.atLeastOne'))`.
- Below the form: a vertical list of `WishCard` (loading `···`, empty `marketplace.searchEmpty`).

**`WishCard`** (data hook #5 — wish-list remove): shows the searcher's `userDisplayName`, optional
`bottleName` (Playfair), a chip row (distillery chip + category chip from `CATEGORY_COLORS[cat].label`),
the date, optional image. If `isOwn` → a **two-step inline** Remove button (`removeMutation =
useMutation(removeWishListItem)`, `onSuccess` invalidates `['wishlist','all']`; first click →
`wishList.removeConfirm`, second → delete). If `!isOwn && canContact` → a gold "MESSAGE" button
opening `ContactModal`.

**`ContactModal`** (data hook #6 — send message → open chat): a hand-rolled modal with a `<textarea>`
pre-filled with `t('wishList.contactDefaultMessage')`. `sendMutation = useMutation(() =>
sendMessage(item.userId, content.trim()))`; **`onSuccess` calls `openChat(item.userId)` then
`onClose()`** — i.e. submitting the contact form fires the message and pops open the floating
`ChatWidget` on that conversation. Cancel/close + a Send button (disabled while pending or empty).

**Helpers:** `useDebounced<T>`, `formatDate`, `focusOn`/`focusOff` (manual focus-border hacks),
`CategoryPill` (hover state + per-category color), `conditionColor` map.

## Transformation plan

### Tab bar → `Tabs` (§8c)
Replace `tabBarStyle`/`tabBaseStyle`/`tabActiveStyle`/`tabInactiveStyle` and the two `<button>`s with
`Tabs value={tab} onValueChange={(v) => setTab(v as MarketTab)}` → `TabsList` with two
`TabsTrigger value="sale" | "search"`, and `TabsContent value="sale"` / `value="search"` wrapping
`<SaleTab>` / `<SearchTab>`. Keep the `tab` state so the `<h1>` title ternary
(`tab === 'sale' ? marketplace.title : marketplace.titleSearch`) still works. The page header label
(`marketplace.label`, Cinzel) → `text-xs uppercase tracking-wide text-muted-foreground`; the `<h1>`
→ `font-heading text-3xl font-semibold text-foreground` (drop Playfair/`#E8C870`). Main wrapper
`maxWidth:1200; padding:40px` → `mx-auto max-w-6xl px-6 py-10` (or similar).

### Sale-tab filters → `Input` / `Badge`(or `Toggle`) / `Select`
- Search box: shadcn `Input` (`className="pl-8"`) in a `relative` wrapper with a lucide
  `<Search className="absolute left-2.5 top-1/2 -translate-y-1/2 size-4 text-muted-foreground" />`
  (replaces 🔍). Delete `focusOn`/`focusOff` + `inputStyle` — the token ring (`ring-ring`, §6) replaces
  the manual focus border.
- `CategoryPill` → rebuild on `Toggle` (or `Button variant="outline" size="sm"`), `rounded-full
  whitespace-nowrap`, **drop the hover `useState`**. **Per-category color stays inline (§8b):** when
  `active`, `style={{ background: `${color}26`, borderColor: color, color }}` from
  `CATEGORY_COLORS[cat].glass`; inactive uses `border-border text-muted-foreground`. The "All" pill
  uses amber primary, not a category color.
- Sort `<select>` → shadcn `Select` with three `SelectItem`s
  (`marketplace.sortNewest|sortPriceAsc|sortPriceDesc`). Drop `selectStyle`.
- Loading shimmer → keep the `marketplace.loading` line (`shimmer` keyframe survives in `index.css`)
  **or** a `Skeleton` grid. Empty → centered `italic text-muted-foreground`.

### MarketplaceCard → `Card`
- Outer wrapper → `Card` with `overflow-hidden flex flex-col`; replace the hover `useState` +
  inline transform with `className="transition hover:-translate-y-0.5 hover:border-primary/30"`.
- Image header: keep the `radial-gradient(... ${cat.glow}1A ...)` **inline (§8b — dynamic)**; the
  `BottleSvg` fallback stays (bespoke art, §3). Wrap in `CardHeader`/a plain `div` with a fixed `h-40`.
- Category badge → `Badge` shell with **inline per-category color (§8b)** from `cat.glass`
  (`style={{ background: `${cat.glass}22`, color: cat.glass }}`), text `cat.label`.
- Condition badge → prefer the new `success`/`warning` `Badge` variants (add them to `badge.tsx`
  per §7): `Sealed→success`, `Opened→warning`, `Empty→secondary`/`muted`. If that feels off, the
  existing `conditionColor` map applied inline is acceptable (§8b). Text via `addBottle.condition{X}`.
- `isLimited` `◆` → keep the glyph or swap to lucide `Gem`/`Diamond` `size-3 text-primary`.
- Name → `CardTitle` overridden to `text-base text-foreground` (CardTitle defaults small, §7);
  distillery → `text-sm italic text-muted-foreground`; specs row → `text-xs text-muted-foreground`.
- Price → `text-lg font-heading text-primary` (amber); seller `<Link>` → keep `react-router` `Link`,
  `text-sm italic text-muted-foreground hover:text-foreground`.
- View button → `<Button variant="outline" size="sm" className="w-full" onClick={onView}>`
  (`marketplace.viewBottle`). `onView` still lifts to `selectedBottle` → `BottleDetailPanel` (unchanged).

### Publish modal → `Dialog` + §8a RHF+zod form
- Shell: `Dialog open={open} onOpenChange={setOpen}`; `DialogTrigger asChild` wraps the
  `<Button>` (amber default, replaces the gold-gradient `publishSearchButtonStyle`,
  `wishList.publishSearch`) — render the trigger only when `isAuthenticated`. `DialogContent`
  (replaces `modalBackdropStyle`/`modalPanelStyle`/`modalCloseStyle` — Radix gives the backdrop,
  portal, focus-trap, scroll-lock, ESC, and the built-in close button for free, §8c).
  `DialogHeader`/`DialogTitle` = `wishList.publishSearchTitle`.
- Form (reference: the auth pages, §8a):
  ```ts
  const makePublishSchema = (t: TFn) =>
    z.object({
      bottleName: z.string().trim().max(120).optional(),
      distilleryId: z.string().nullable().optional(),
      category: z.enum([...CATEGORIES]).or(z.literal('')).optional(),
      imageUrl: z.string().optional(),
    }).refine((v) => Boolean(v.distilleryId) || Boolean(v.category),
              { path: ['category'], message: t('wishList.atLeastOne') })
  type PublishValues = z.infer<ReturnType<typeof makePublishSchema>>
  const schema = useMemo(() => makePublishSchema(t), [t])
  const form = useForm<PublishValues>({ resolver: zodResolver(schema),
    defaultValues: { bottleName: '', distilleryId: null, category: '', imageUrl: '' } })
  ```
  The `.refine` (path `['category']`) replaces the manual `setValidationError(t('wishList.atLeastOne'))`
  and surfaces under the Category field via `FormMessage`. (Alternative: attach to a `root` error and
  render the standard error box, §6 — pick one; the per-field message is cleaner.)
- Fields (each wrapped in `FormField`/`FormItem`/`FormLabel`/`FormControl`/`FormMessage`):
  - **Description** (label-only, not matched) → `Input` (`wishList.bottleName`,
    placeholder `wishList.bottleNamePlaceholder`).
  - **Distillery** → `DistillerySelect` (Slice 6) bound to `field.value`/`field.onChange`, pass
    `category={form.watch('category') || undefined}` (preserve the `(id,name)` contract),
    placeholder `wishList.distilleryPlaceholder`.
  - **Category** → shadcn `Select`; `SelectItem value=""` = `wishList.categoryAny`, then one per
    `CATEGORIES` (label `CATEGORY_COLORS[cat].label`). **Preserve the side-effect:** on change call
    `field.onChange(v)` **and** `form.setValue('distilleryId', null)` (mirrors today's reset).
  - **Image** URL-vs-upload toggle → **`Switch`** (replaces the two toggle buttons
    `wishImageToggleRow…`). Keep a local `uploadMode` boolean (`Switch checked` = upload). Label the
    two sides with `wishList.imageTabUrl` / `wishList.imageTabUpload`. When off → a URL `Input`
    (`wishList.imageUrl`, placeholder `wishList.imageUrlPlaceholder`) bound to the `imageUrl` field.
    When on → a "CHOOSE FILE" button: `<Button variant="outline" asChild><label>{t('wishList.chooseFile')}
    <input type="file" accept="image/jpeg,image/png,image/webp" className="hidden" onChange={handleFileChange}/></label></Button>`.
    `handleFileChange` still calls `uploadMutation.mutate(file)`; `onSuccess` →
    `form.setValue('imageUrl', result.url)`. Show `wishList.uploading` while pending and the standard
    error box (§6) for `uploadMutation.isError` (`wishList.uploadError`).
  - **Preview**: when `imageUrl` truthy, an 80×80 `rounded-md border` `<img>` with a remove button
    `<Button variant="outline" size="icon-xs">` (lucide `X`) → `form.setValue('imageUrl', '')`.
- Submit: `<Button type="submit" disabled={addMutation.isPending}>` (amber, `wishList.addBtn` /
  `wishList.adding`). `onSubmit` maps to the existing payload shape:
  `addMutation.mutate({ bottleName: v.bottleName?.trim() || undefined, distilleryId: v.distilleryId,
  category: v.category || undefined, imageUrl: v.imageUrl || undefined })`. `addMutation.onSuccess`:
  invalidate `['wishlist','all']`, `form.reset()`, `uploadMutation.reset()`, `setUploadMode(false)`,
  `setOpen(false)`. `addMutation.isError` → standard error box (§6) `wishList.addFailed`.

### WishCard → `Card`
- `wishCardStyle` → `Card` (`flex items-center justify-between gap-4 p-4`). User line → `text-xs
  uppercase tracking-wide text-muted-foreground`; `bottleName` → `CardTitle` (`text-lg text-foreground`).
- Chip row: distillery chip → `Badge` (amber/`outline`); category chip → `Badge variant="secondary"`
  (label `CATEGORY_COLORS[cat].label`). Date → `text-xs text-muted-foreground`. Image → `rounded-md border`.
- **Own item remove:** convert the two-step inline confirm to an `AlertDialog` (§8c — destructive
  confirm), matching the BottleDetailPanel delete from Slice 7: trigger `<Button variant="outline"
  size="sm">` (`wishList.remove`), `AlertDialogAction` = `wishList.removeConfirm` →
  `removeMutation.mutate()` (still invalidates `['wishlist','all']`). (If you prefer minimal churn,
  the existing inline two-step swap to `variant="destructive"` is acceptable — but AlertDialog is the
  §8c target and gives a11y for free.)
- **Contact (non-owner):** `<Button size="sm">` (amber, `wishList.contactBtn`) as the `ContactModal`
  `DialogTrigger`.

### ContactModal → `Dialog` + `Textarea` → `openChat` (the key wiring)
- `Dialog` (replaces backdrop/panel/close). `DialogTitle` = `wishList.contactTitle`. Body: a plain
  shadcn **`Textarea`** (single field — stays uncontrolled-by-RHF per §8a "trivial single-field input
  stays plain"), seeded with `useState(() => t('wishList.contactDefaultMessage'))`. Footer
  (`DialogFooter`): `DialogClose`→`<Button variant="outline">` (`wishList.contactCancel`) + a
  `<Button type="submit" disabled={sendMutation.isPending || !content.trim()}>`
  (`wishList.contactSend` / `wishList.contactSending`).
- **Preserve the wiring exactly:** `sendMutation = useMutation(() => sendMessage(item.userId,
  content.trim()))`, `onSuccess: () => { openChat(item.userId); onClose() }`. Use `openChat` from
  `useChat()` (ChatContext) — submitting sends the DM **and** opens the floating `ChatWidget` on that
  thread. `sendMutation.isError` → standard error box (§6) `wishList.contactError`. Close the Dialog
  via `onOpenChange` so `onClose` runs whether the user cancels or submits.

### Inline → token map (representative; ~88 constants collapse to classes)
| Old constant(s) | Replacement |
|---|---|
| `inputStyle`, `contactTextareaStyle`, `selectStyle`, `focusOn/Off` | `Input` / `Textarea` / `Select` (token ring) |
| `tabBar/Base/Active/Inactive` | `Tabs`/`TabsList`/`TabsTrigger` |
| `publishSearchButton`, `wishAddButton`, `wishContactButton` (gold gradient) | `Button` (amber `default`) |
| `modalBackdrop/Panel/Close/Title` | `Dialog`/`DialogContent`/`DialogTitle` (close built-in) |
| `wishLabel` | `FormLabel`/`Label` (`text-xs font-medium text-muted-foreground`) |
| `wishFieldGrid` | `grid grid-cols-1 sm:grid-cols-3 gap-3.5` |
| `wishImageToggleRow/Base/Active/Inactive` | `Switch` + two `Label`s |
| `wishChooseFileButton` | `Button variant="outline" asChild` + hidden `<input type=file>` |
| `wishImagePreviewWrap/Preview/Remove` | `div` + `img rounded-md border` + `Button size="icon-xs"` (`X`) |
| `wishCard`, `MarketplaceCard` outer | `Card`; `wishUser/CardTitle/Date` → token text |
| `wishDistilleryChip` / `wishCategoryChip` | `Badge` (amber) / `Badge variant="secondary"` |
| `wishRemoveButton`/`wishConfirmButton` | `AlertDialog` + `Button variant="outline"/"destructive"` |
| `wishError`, `addMutation.isError`, upload/contact errors | standard error box (§6) |
| `wishEmpty`/`wishLoading`, sale loading/empty | centered muted text / `Skeleton` |
| `CategoryPill` chrome | `Toggle`/`Button` + **inline per-category color (§8b)** |
| image `radial-gradient`, `cat.glow`/`cat.glass`, `conditionColor` | **stay inline (§8b)** |
| 🔍 emoji | lucide `Search`; `◆` → keep or lucide `Gem` |

## i18n keys to preserve (verbatim — every one already exists in bg.json + en.json)

**`marketplace.*`:** `label`, `title`, `titleSearch`, `tabSale`, `tabSearch`, `searchEmpty`,
`searchPlaceholder`, `allCategories`, `sortNewest`, `sortPriceAsc`, `sortPriceDesc`, `loading`,
`empty`, `priceOnRequest`, `by` (`{{name}}`), `viewBottle`.

**`wishList.*`:** `publishSearch`, `publishSearchTitle`, `close`, `bottleName`,
`bottleNamePlaceholder`, `distillery`, `distilleryPlaceholder`, `category`, `categoryAny`,
`imageTabUrl`, `imageTabUpload`, `imageUrl`, `imageUrlPlaceholder`, `uploadImage`, `chooseFile`,
`uploading`, `uploadError`, `atLeastOne`, `addFailed`, `adding`, `addBtn`, `remove`, `removeConfirm`,
`contactBtn`, `contactTitle`, `contactDefaultMessage`, `contactSend`, `contactSending`,
`contactCancel`, `contactError`.

**`addBottle.*`:** `conditionSealed`, `conditionOpened`, `conditionEmpty` (via the
`addBottle.condition${bottle.condition}` template — keep the dynamic key intact).

> Note: category/condition **labels** in the cards come from `CATEGORY_COLORS[cat].label` in
> `BarShelf.tsx`, **not** from i18n — leave them as-is. No new keys are needed for this slice.

## Slice-specific gotchas

- **Two fully independent data sets behind one `Tabs`.** `SaleTab` (`['marketplace', …]`) and
  `SearchTab` (`['wishlist','all']`) own separate `useQuery`s — keep them in the child components so
  switching tabs doesn't refetch the other. Do not hoist either query into the page.
- **The publish form has cross-field validation** ("≥1 of distillery/category"). Implement it as the
  zod `.refine` above, **not** a manual `if`. Don't drop the **category→distillery reset**
  side-effect (changing category clears the selected distillery) and keep passing the live category
  into `DistillerySelect` so its options stay filtered.
- **Image URL vs upload is one value, two inputs.** `imageUrl` is set *either* by typing (URL mode)
  *or* by `uploadMutation` success (upload mode). Keep a single `imageUrl` form field; the `Switch`
  only chooses which input is visible. The remove `×` must clear the field regardless of mode.
- **Wish-list matching criteria are `distilleryId` (FK) + `category` (enum) only** — `bottleName` is
  a label, never matched (CLAUDE.md). Keep `bottleName` optional and unvalidated beyond a length cap.
- **This page opens two overlays that live *outside* this file:** `BottleDetailPanel` (Slice 7, a
  `Dialog`) from a sale card, and the floating `ChatWidget` (Slice 5, a custom non-modal shell) via
  `openChat`. The ChatWidget is non-modal, so there's no focus-trap conflict with the closing
  ContactModal — but verify the chat actually pops. Keep the `onSuccess` sequence `openChat(item.userId)`
  then `onClose()`.
- **mira is compact (§4).** The publish `DialogContent` packs a 3-col field grid + image section +
  submit; if it feels cramped, bump field heights locally (`h-9`) or widen the dialog
  (`className="sm:max-w-2xl"`) — token-faithful, no hardcoded hex.
- **Keep `react-router` `Link`** for the seller link (`/bar/${userId}`) — don't replace it with a
  `Button`; just restyle with token classes.
- Strip `focusOn`/`focusOff` and **every** style constant; the token ring + `Card`/`Badge` shells
  replace them. Leave only the genuinely-dynamic inline styles enumerated in §8b.

## Verification (§10 — run in BOTH bg + en)

1. `npm --prefix VirtualBar.Web run build` (green; `tsc -b && vite build`) and `… run lint` (clean).
2. `dotnet run` in `VirtualBar.Api` (data-bearing page) + `npm run dev`; sign in.
3. **FOR SALE:** type in search (debounced), toggle category pills (active pill shows per-category
   color), change sort `Select` → grid updates. **View Bottle** → `BottleDetailPanel` opens (ESC
   closes, focus trapped, scroll locked).
4. **LOOKING FOR:** **POST A SEARCH** → Dialog. Submit with no distillery/category → `wishList.atLeastOne`
   under Category. Pick a category → distillery options filter and any prior distillery clears. Flip the
   image `Switch`; upload a file → preview, `×` removes. Submit valid → card appears, dialog closes,
   list invalidates.
5. **Own wish:** Remove → AlertDialog confirm → card disappears.
6. **Contact a searcher** (as another user): **MESSAGE** → Dialog (default message) → Send → the
   floating **ChatWidget opens** on that thread and the DM is delivered.
7. Toggle БГ↔EN: tab labels, filters, both dialogs, validation, card copy all re-localize. Use the
   `e2e-tester` agent for screenshots at 1280×800. Console clean (no React/Radix errors).

## Acceptance criteria (§10)

- Build + lint green; bundle delta reasonable (Tabs/Dialog/Switch/Select already bundled from Slice 2).
- Dark amber/stone/Inter theme throughout; **no serif, no hardcoded speakeasy hex** except the §8b
  dynamic cases (`CATEGORY_COLORS` fills/glow, `conditionColor`, `BottleSvg`, the per-pill active color).
- Custom tab bar → `Tabs`; both card types → `Card`; both modals → `Dialog`; publish form is RHF+zod
  via shadcn `Form` with the `.refine` cross-field rule; URL/upload toggle is a `Switch`; filters use
  `Input`/`Select`/`Badge`(or `Toggle`); destructive remove uses `AlertDialog`.
- All real a11y wins present: ESC + focus-trap + scroll-lock on both dialogs; keyboard nav on the
  `DistillerySelect` combobox and the sort `Select`.
- **Behaviour unchanged:** same two queries, same filters/sort/debounce, same wish-list add/remove +
  image upload, same "≥1 criterion" rule, same `BottleDetailPanel` open, same `openChat` contact flow.
- Every `t('…')` key above preserved; both languages verified.

## Dependencies

- **Slice 6 — `DistillerySelect`** (combobox in the publish form): must be migrated first.
- **Slice 7 — `BottleDetailPanel`** (opened from sale cards): must be migrated first.
- **Slice 4/5** indirectly (NavBar chrome + the `ChatWidget` that `openChat` pops). Slice 2 primitives
  (`Tabs`, `Dialog`, `AlertDialog`, `Select`, `Switch`, `Card`, `Badge`, `Input`, `Textarea`, `Form`,
  `Button`, `Skeleton`) are all already installed. Next: `11-offers-page.md`.
