# Slice 7 — BottleDetailPanel → Dialog + AlertDialog + nested MakeOffer Dialog

> Read `00-OVERVIEW.md` first. This slice assumes the §6 token cheat-sheet, the §7 component
> inventory, the §8a RHF+zod form pattern, and the §8b/§8c conversion rules. It does not repeat them.

## Context recap

`src/components/BottleDetailPanel.tsx` is the single densest file in the app: **~94 inline style
objects**, one module-level `inputStyle` + six more `CSSProperties` constants, a hand-rolled
full-screen modal, a hand-rolled **nested** offer modal, and an inline delete-confirm. It is shared
by **three** call sites and must keep its exact public prop contract so none of them need editing:

- `DashboardPage.tsx` (L810) — owner view; passes `onDelete` (invalidates `['bottles', user.id]` + `setSelectedBottle(null)`).
- `MarketplacePage.tsx` (L1189) — viewer view; `currentUserId={user?.id ?? ''}`, no `onDelete`.
- `PublicBarPage.tsx` (L197) — viewer view; `currentUserId={user?.id ?? ''}`, no `onDelete`.

The panel is mounted conditionally by each parent (`selectedBottle && <BottleDetailPanel … />`), so
the parent owns open/close — the Dialog must route every close path back to the `onClose` prop.

## Goal

Re-skin the panel to the amber/stone/Inter token theme and replace all three hand-rolled overlays
with Radix primitives (focus-trap, scroll-lock, ESC, portal for free), **without changing the
component's props, its data hooks, or any query-invalidation key**. Map:

- full-screen centered overlay → `Dialog` (`DialogContent`, scrollable, `max-w-[680px]`, `max-h-[90vh]`)
- owner `DeleteSection` inline confirm → `AlertDialog`
- `SaleSection` (list / unlist) → tokens + `Button`s (keep the two mutations)
- non-owner `MakeOfferSection` modal → a **nested** `Dialog` whose form uses the §8a RHF+zod pattern
- `LikesSection` → lucide `Heart` + pluralized count
- `CommentsSection` → list to tokens; add-comment box stays a plain `Textarea` + `Button` (§3/§8a trivial input)
- category/condition pills → `Badge` (per-category color from `CATEGORY_COLORS` **stays inline** per §8b)

## Files to touch

- `src/components/BottleDetailPanel.tsx` — the whole rewrite.
- `src/components/ui/badge.tsx` — add a `success` variant (reused by Slice 11 OffersPage too). Per §7.
- `src/i18n/bg.json` + `src/i18n/en.json` — add **one** new key `offers.priceInvalid` for the zod
  positive-price message (the only new copy; §8a sanctioned exception — none exists today because the
  old code just disabled the button). Everything else reuses existing keys.
- **No edits** to `DashboardPage.tsx`, `MarketplacePage.tsx`, `PublicBarPage.tsx` — the prop contract
  (`bottle`, `userId`, `currentUserId`, `onClose`, `onDelete?`) is preserved verbatim.

## Current state (what each part does now)

**Module-level constants / helpers (all to be deleted after migration):**
- `inputStyle` (L11) — dark `#0A0502` bg, gold-alpha border, Cormorant 16px; spread into every input/textarea/select.
- `focusOn` / `focusOff` (L23–29) — manual border-color swap on focus/blur (shadcn `Input`/`Textarea` give this free → delete).
- `formatRelativeTime(iso, t)` (L31) — relative-time formatter; **keep as-is**, it only emits `t('bottle.*Ago')` keys.
- `sectionLabelStyle` (L55), `offerLabelStyle` (L472), `offerOverlayStyle` (L482), `offerCardStyle` (L492), `offerSubmitStyle` (L504), `offerCancelStyle` (L517) — Cinzel labels / offer-modal chrome → all replaced by tokens.

**Sub-sections and their data hooks:**

| Sub-section (line) | Data hooks | Invalidation keys (PRESERVE) | UI today |
|---|---|---|---|
| `LikesSection` (L64) | `useMutation(toggleBottleLike)` | `['bottles', userId]` | unicode `♥` with `WebkitTextStroke`/`textShadow`, count via `t('bottle.likes',{count})` |
| `CommentsSection` (L124) | `useQuery(['comments', bottle.id])`→`getBottleComments`; `addMutation`→`addBottleComment`; `deleteMutation`→`deleteBottleComment` | add+delete invalidate `['comments', bottle.id]` **and** `['bottles', bottle.userId]` | `draft` state, scrollable list (`maxHeight:320`), per-comment `×` delete when `comment.userId===currentUserId`, textarea + gold submit button |
| `SaleSection` (L259) — owner only | `listMutation`→`listBottleForSale(id,Number(price),currency)`; `unlistMutation`→`unlistBottleFromSale` | both invalidate `['bottles', userId]` **and** `['marketplace']` | `price`/`currency` state; branches on `bottle.isForSale` (green price + unlist) vs (number input + native `<select>` + list button) |
| `DeleteSection` (L374) — owner only | `deleteMutation`→`removeBottle` | `['bottles', bottle.userId]`, then `onDelete?.()` | `confirming` state toggles inline red confirm row (confirm/cancel buttons) |
| `MakeOfferSection` (L530) — non-owner | `mutation`→`createOffer({bottleId, offeredPrice:Number(price), currency, message})` | `['offers']`; on success `setOpen(false)` + clear message | own `open` state + hand-rolled `fixed inset-0` overlay/card; `price`/`currency`/`message` state; `canSubmit = !!price && Number(price)>0 && !pending` |
| `DetailRow` (L663) | — | — | label/value spec cell (Cinzel label + Cormorant value) |

**Shell — `BottleDetailPanel` (L676, default export):**
- `fixed inset-0 z-50` flex-center + backdrop `div onClick={onClose}` (`rgba(4,2,1,0.88)`).
- Card: `max-width:680, max-height:90vh`, gradient `#0F0604→#130805`, gold border, `overflow-y:auto`, `animation: fadeInUp`, heavy box-shadow.
- Image area (L712): 280px tall; primary image (`objectFit:cover`) **or** `BottleSvg` fallback on a `CATEGORY_COLORS` radial glow; bottom scrim gradient; custom round `×` close (L729); overlay row of pills (category via `col.glass/col.label`, condition via `t('addBottle.condition'+condition)`, `bottle.isLimited`, `bottle.isForSale`) + `<h2>` title + `distilleryName`.
- Body (L796): specs grid of `DetailRow` (age/abv/volume/vintage); origin block; **owner-vs-viewer branch** (`bottle.userId === currentUserId` → `SaleSection` + `DeleteSection bottle onDelete={onDelete ?? onClose}`; else green asking-price box when `isForSale && askingPrice!=null`, then `{currentUserId && <MakeOfferSection/>}`); description; gallery (horizontal thumbnails); `LikesSection`; `CommentsSection`.

## Transformation plan

### 1. Shell → `Dialog`
```tsx
<Dialog open onOpenChange={(o) => { if (!o) onClose() }}>
  <DialogContent className="max-w-[680px] max-h-[90vh] overflow-y-auto p-0 gap-0">
    {/* image area + body */}
  </DialogContent>
</Dialog>
```
- Delete the manual backdrop `div`, the `fixed inset-0` wrapper, and the custom round `×` button — `DialogContent` provides overlay + scroll-lock + ESC + a built-in close. `open` is always `true` (parent controls mount); ESC / backdrop / built-in-X all flow through `onOpenChange → onClose`.
- a11y: wrap the visible title as the dialog title — `<DialogTitle asChild><h2 …>{bottle.name}</h2></DialogTitle>` inside the image overlay; add `<DialogDescription className="sr-only">{bottle.distilleryName ?? bottle.name}</DialogDescription>` (Radix warns if either is missing).
- `p-0 gap-0` because the image is full-bleed at the top. Re-add body padding with a wrapper `<div className="px-7 pb-10 pt-6">` (was `24px 28px 40px`).
- Image area: `relative h-[280px] w-full overflow-hidden bg-background`; scrim → `absolute inset-0 bg-gradient-to-b from-transparent to-card`; empty-state radial glow with `col.glow` **stays inline** (computed). `BottleSvg` stays.

### 2. `DeleteSection` → `AlertDialog` (owner only)
Drop the `confirming` state entirely. Keep `deleteMutation`.
```tsx
<AlertDialog>
  <AlertDialogTrigger asChild>
    <Button variant="outline" className="w-full border-destructive/45 text-destructive hover:bg-destructive/10">
      {t('bottle.remove')}
    </Button>
  </AlertDialogTrigger>
  <AlertDialogContent>
    <AlertDialogHeader>
      <AlertDialogTitle>{t('bottle.remove')}</AlertDialogTitle>
      <AlertDialogDescription>{t('bottle.removeConfirmText')}</AlertDialogDescription>
    </AlertDialogHeader>
    <AlertDialogFooter>
      <AlertDialogCancel disabled={deleteMutation.isPending}>{t('bottle.removeCancel')}</AlertDialogCancel>
      <AlertDialogAction
        disabled={deleteMutation.isPending}
        onClick={(e) => { e.preventDefault(); deleteMutation.mutate() }}
        className="bg-destructive text-white hover:bg-destructive/90">
        {deleteMutation.isPending ? t('bottle.removing') : t('bottle.removeConfirm')}
      </AlertDialogAction>
    </AlertDialogFooter>
  </AlertDialogContent>
</AlertDialog>
```
- `e.preventDefault()` on the action stops Radix's auto-close so an async failure keeps the dialog open; close happens via the existing `onSuccess` (which fires `onDelete?.()` → on Dashboard closes the whole panel, elsewhere `onDelete ?? onClose` closes the panel). **Keep `deleteMutation.onSuccess` exactly:** invalidate `['bottles', bottle.userId]` then call the callback.
- Keep the `bottle.removeError` box (render below the trigger when `deleteMutation.isError`).

### 3. `SaleSection` → tokens + `Button`s (owner only)
- Keep `price`/`currency` state and both mutations + their `invalidate()` (`['bottles', userId]` + `['marketplace']`).
- Wrapper card: `rounded-md border p-4` with state colors — for-sale `border-success/30 bg-success/10`, otherwise `border-primary/15 bg-primary/[0.04]`.
- For-sale branch: price `font-heading text-xl font-semibold text-success`; unlist → `<Button variant="outline" size="sm" className="border-destructive/40 text-destructive">`.
- List branch: number `<Input type="number" className="w-36">`; currency `<Select>` (see §5 pattern, `w-24`); `<Button size="sm" disabled={listMutation.isPending || !price || Number(price) <= 0}>` (default = amber). Drop `inputStyle`/`focusOn`/`focusOff`/native `<select>`.
- Error → standard destructive box (§6) with `t('bottle.errorSale')`.

### 4. `MakeOfferSection` → **nested** `Dialog` + RHF+zod (§8a)
Controlled so success can close + reset:
```tsx
const makeOfferSchema = (t: TFn) => z.object({
  offeredPrice: z.coerce.number({ message: t('offers.priceInvalid') }).positive(t('offers.priceInvalid')),
  currency: z.string().min(1),
  message: z.string().optional(),
})
type OfferValues = z.infer<ReturnType<typeof makeOfferSchema>>
const schema = useMemo(() => makeOfferSchema(t), [t])
const form = useForm<OfferValues>({
  resolver: zodResolver(schema),
  defaultValues: { offeredPrice: bottle.askingPrice ?? undefined, currency: bottle.currency ?? 'USD', message: '' },
})
const mutation = useMutation({
  mutationFn: (v: OfferValues) => createOffer({ bottleId: bottle.id, offeredPrice: v.offeredPrice, currency: v.currency, message: v.message?.trim() || undefined }),
  onSuccess: () => { queryClient.invalidateQueries({ queryKey: ['offers'] }); setOpen(false); form.reset() },
  onError: () => form.setError('root', { message: t('offers.errorCreate') }),
})
```
Markup:
```tsx
<Dialog open={open} onOpenChange={setOpen}>
  <DialogTrigger asChild><Button size="lg" className="w-full">{t('offers.makeOffer')}</Button></DialogTrigger>
  <DialogContent className="max-w-[440px]">
    <DialogHeader>
      <DialogTitle>{t('offers.offerModalTitle')}</DialogTitle>
      <DialogDescription>{bottle.name}</DialogDescription>
    </DialogHeader>
    <Form {...form}>
      <form onSubmit={form.handleSubmit((v) => mutation.mutate(v))} className="space-y-4">
        <div className="grid grid-cols-[1fr_110px] gap-3">
          <FormField control={form.control} name="offeredPrice" render={({ field }) => (
            <FormItem><FormLabel>{t('offers.price')}</FormLabel>
              <FormControl><Input type="number" min={0} step="0.01" {...field} /></FormControl>
              <FormMessage /></FormItem>)} />
          <FormField control={form.control} name="currency" render={({ field }) => (
            <FormItem><FormLabel>{t('offers.currency')}</FormLabel>
              <Select value={field.value} onValueChange={field.onChange}>
                <FormControl><SelectTrigger><SelectValue /></SelectTrigger></FormControl>
                <SelectContent>{CURRENCIES.map(c => <SelectItem key={c} value={c}>{c}</SelectItem>)}</SelectContent>
              </Select><FormMessage /></FormItem>)} />
        </div>
        <FormField control={form.control} name="message" render={({ field }) => (
          <FormItem><FormLabel>{t('offers.message')}</FormLabel>
            <FormControl><Textarea rows={3} placeholder={t('offers.messagePlaceholder')} {...field} /></FormControl>
            <FormMessage /></FormItem>)} />
        {form.formState.errors.root && (<div className="rounded-md border border-destructive/40 bg-destructive/10 px-3 py-2 text-sm text-destructive">{form.formState.errors.root.message}</div>)}
        <DialogFooter>
          <DialogClose asChild><Button type="button" variant="outline">{t('offers.cancel')}</Button></DialogClose>
          <Button type="submit" disabled={mutation.isPending}>{mutation.isPending ? t('offers.submitting') : t('offers.submit')}</Button>
        </DialogFooter>
      </form>
    </Form>
  </DialogContent>
</Dialog>
```
`CURRENCIES` array stays. The `{currentUserId && <MakeOfferSection/>}` guard in the shell stays so anonymous viewers (empty `currentUserId`) get nothing.

### 5. `LikesSection` → lucide `Heart`
`<Button variant="ghost" size="sm" onClick={() => mutation.mutate()} disabled={mutation.isPending}>` containing `<Heart className={cn('size-5', liked ? 'fill-primary text-primary' : 'text-primary')} />` + `<span className="text-sm">{t('bottle.likes', { count: bottle.likesCount })}</span>`. Drop the `WebkitTextStroke`/`textShadow` hack. Keep the `aria-label`. Keep invalidation `['bottles', userId]`.

### 6. `CommentsSection` → tokens (add-box stays plain, §3/§8a)
- Section label → `text-xs font-medium uppercase tracking-wide text-muted-foreground`.
- List wrapper → `max-h-80 overflow-y-auto` (optionally `ScrollArea`); name `text-sm font-medium text-primary`, time `text-xs text-muted-foreground`, body `text-sm text-foreground`.
- Per-comment delete → `<Button variant="ghost" size="icon-xs"><X className="size-3.5" /></Button>` shown only when `comment.userId === currentUserId` (keep `deleteMutation`).
- Add box: keep `draft` state + `handleSubmit` + `addMutation` (NOT RHF). Swap `<textarea>`→`<Textarea>` and the gold `<button>`→`<Button type="submit" disabled={addMutation.isPending || !draft.trim()}>`. Error → destructive text with `t('bottle.errorComment')`.

### 7. Pills / specs → `Badge` + tokens
- Category pill: **stays inline** — background `${col.glass}22`, border `${col.glass}66`, color `col.glass`, label `col.label` (computed from `CATEGORY_COLORS`, §8b). Optionally wrap in `Badge` with inline `style` for the color only.
- Condition pill → `<Badge variant="outline">{t('addBottle.condition'+bottle.condition)}</Badge>`.
- `isLimited` → `<Badge variant="secondary">{t('bottle.limited')}</Badge>`; `isForSale` → `<Badge variant="success">{t('bottle.forSale')}</Badge>` (needs the new variant — see Files to touch).
- `DetailRow`: label → `text-xs uppercase tracking-wide text-muted-foreground`; value → `text-base text-foreground`. Specs grid wrapper → `rounded-md border border-primary/10 bg-primary/[0.04] p-5`.
- Viewer asking-price box → `rounded-md border border-success/30 bg-success/10`; price `font-heading text-xl text-success`.
- Description → label tokens + `<p className="text-base italic text-primary leading-relaxed">`.
- Gallery thumbnails → `size-20 rounded border border-primary/15 object-cover`.

### Inline → token map (quick reference)
| Old hardcoded | Token / utility |
|---|---|
| `#0F0604→#130805` panel gradient, `rgba(4,2,1,0.88)` backdrop, `fadeInUp`, box-shadow | `DialogContent` defaults (drop all) |
| `#0A0502` / `#0A0402` input/img bg | `bg-input/20` / `bg-background` |
| `rgba(201,168,76,0.2)` borders | `border-input` / `border-primary/20` |
| `#F0DDB4` / `#E8D4A0` body text | `text-foreground` |
| `#E8C870` / `#C9A84C` accents | `text-primary` |
| `#7A6040` / `#B09868` meta | `text-muted-foreground` |
| `#4A9A6A` / `#6ABF8A` for-sale green | `text-success`, `bg-success/10`, `border-success/30` |
| `#C04040` + `rgba(192,64,64,*)` | `text-destructive`, `border-destructive/40`, `bg-destructive/10` |
| `linear-gradient(135deg,#C9A84C,#E8C870)` gold buttons | `Button` default (amber) — **no gradient** |
| `Playfair Display` headings | `font-heading` (Inter) |
| `Cormorant`/`Cinzel` body+labels | drop `fontFamily` (inherits Inter); labels → `text-xs … text-muted-foreground` |

### Stays inline (do NOT tokenize — §8b)
- `CATEGORY_COLORS[bottle.category]` → `col.glow` (empty-image radial glow), `col.glass` (category pill bg/border/text), `col.label`.
- `BottleSvg` art (kept per §3/§5).
- Any `objectPosition: 'center top'` image framing.

## i18n keys to preserve (all already exist unless noted)

- **`bottle.*`:** `justNow`, `minutesAgo`, `hoursAgo`, `daysAgo`, `weeksAgo`, `monthsAgo`, `yearsAgo`
  (all pluralized `_one`/`_other`, via `formatRelativeTime`), `likes` (pluralized), `comments`,
  `loadingComments`, `errorComments`, `noComments`, `commentPlaceholder`, `errorComment`, `post`,
  `posting`, `saleLabel`, `askingPrice`, `listForSale`, `removeFromSale`, `errorSale`, `remove`,
  `removeConfirmText`, `removeConfirm`, `removeCancel`, `removing`, `removeError`, `limited`,
  `forSale`, `age`, `abv`, `volume`, `vintage`, `origin`, `notes`, `gallery`.
- **`addBottle.*`:** `conditionSealed`, `conditionOpened`, `conditionEmpty` (via `t('addBottle.condition'+bottle.condition)`).
- **`offers.*`:** `makeOffer`, `offerModalTitle`, `price`, `currency`, `message`, `messagePlaceholder`,
  `submit`, `submitting`, `cancel`, `errorCreate`.
- **NEW — `offers.priceInvalid`** (zod positive-price message): add to **both** `bg.json` and `en.json`.
  Suggested BG `"Въведете валидна цена."` / EN `"Enter a valid price."` This is the only new copy.
- Note: the `aria-label`s `"Like"`/`"Unlike"`/`"Delete comment"` are currently hardcoded (not i18n);
  leave them or optionally localize — out of scope, do not block on it.

## Slice-specific gotchas

1. **Nested overlays.** Both the MakeOffer `Dialog` and the Delete `AlertDialog` are rendered **inside**
   the outer `DialogContent`. Radix stacks them (each portals to `body`); ESC closes only the topmost,
   focus returns to the inner trigger on close. This is fine — but verify ESC inside the offer modal
   does **not** also close the whole panel.
2. **Outer Dialog is parent-controlled.** Mount stays `selectedBottle && <Panel/>`; set `open` always
   `true` and route `onOpenChange(false) → onClose()`. Do not keep the old backdrop div / custom `×`.
3. **Built-in close over the image.** `DialogContent`'s default top-right close sits over the full-bleed
   280px image and may be low-contrast. Check `ui/dialog.tsx`: if it supports `showCloseButton={false}`,
   use it and add a custom `<DialogClose asChild>` styled `bg-background/70 text-primary`; otherwise
   override via `className="[&>button]:bg-background/70 [&>button]:text-primary"`.
4. **Owner-vs-viewer branch must stay exact.** `bottle.userId === currentUserId` → `SaleSection` +
   `DeleteSection` (pass `onDelete={onDelete ?? onClose}` unchanged). Else asking-price box (only when
   `isForSale && askingPrice != null`) + `{currentUserId && <MakeOfferSection/>}`. Anonymous viewers
   (`currentUserId === ''`) must still see neither owner sections nor MakeOffer.
5. **Do not touch invalidation keys.** Likes `['bottles', userId]`; comments `['comments', bottle.id]`
   + `['bottles', bottle.userId]`; sale `['bottles', userId]` + `['marketplace']`; delete
   `['bottles', bottle.userId]` + `onDelete?.()`; offer `['offers']`. Copy them verbatim.
6. **mira is compact (§4).** The offer modal is focal — if `h-7` inputs/select feel cramped, bump with
   `className="h-9"` locally. Token-faithful only (no hex).
7. **`z.coerce.number` for the price** so the `type="number"` string value validates/serializes as a
   number; keep `Number(price)` semantics out of the mutation now that RHF owns the value.
8. **Badge `success` variant** must be added to `ui/badge.tsx` before referencing `variant="success"`
   (use `bg-success/10 text-success border-success/20` per §7 guidance); keep it generic/reusable.

## Verification (§10)

1. `npm --prefix VirtualBar.Web run build` (gate) and `npm --prefix VirtualBar.Web run lint` — green.
2. Backend up (`dotnet run` in `VirtualBar.Api`) for data. Run dev server; use the `e2e-tester` agent
   (Playwright) at 1280×800. Exercise in **both bg + en**:
   - **As owner** (open own bottle from `/dashboard`): SaleSection list → unlist round-trip; `DeleteSection`
     opens `AlertDialog`, cancel keeps it, confirm deletes → panel closes and the bottle disappears
     (Dashboard `onDelete` path); like toggles (Heart fills); add a comment, then delete it.
   - **As non-owner** (open someone else's bottle from `/marketplace` and a public bar): no owner
     sections; MakeOffer opens the nested `Dialog`; submit empty/zero price → localized
     `offers.priceInvalid` under the field; valid submit → toast/closes, `['offers']` refetched.
   - **Anonymous** (logged out, Marketplace): no MakeOffer button, no owner sections; like/comment guarded.
3. a11y wins the old code lacked: ESC closes the topmost overlay only; focus trap in each; body-scroll
   locked while open; Tab cycles within the modal.
4. Language toggle БГ↔EN re-localizes pills, labels, validation, buttons. Console clean.

## Acceptance criteria

- Build + lint green; no console errors.
- All three overlays are Radix (`Dialog` / `AlertDialog` / nested `Dialog`) — no `fixed inset-0`
  hand-rolled modal, no manual backdrop, no `useState('confirming')`, no `focusOn`/`focusOff`/`inputStyle`
  left in the file.
- Dark amber/stone/Inter theme renders; gold gradients gone; **`CATEGORY_COLORS` pill color + empty-image
  glow remain inline**; `BottleSvg` untouched.
- Offer form uses RHF+zod+`Form` with `Select` currency + `Textarea` message; reuses `offers.*` keys plus
  the single new `offers.priceInvalid` (present in both `bg.json` and `en.json`).
- Public prop contract unchanged → `DashboardPage`/`MarketplacePage`/`PublicBarPage` compile untouched;
  `onDelete` (Dashboard) still invalidates `['bottles', user.id]` + closes; `onDelete ?? onClose` fallback
  preserved for the other two.
- Every preserved hook fires its original `invalidateQueries` key(s); like/comment/offer/list/unlist/delete
  all functional in bg and en.

## Dependencies

Slice 2 (primitives: `Dialog`, `AlertDialog`, `Select`, `Form`, `Badge`, `Input`, `Textarea`, `Button`,
lucide). Independent of Slice 6. Blocks **Slice 8** (DashboardPage) and **Slice 10** (MarketplacePage),
which both render this panel; land Slice 7 before them. The `badge.tsx` `success` variant added here is
also consumed by **Slice 11** (OffersPage).
