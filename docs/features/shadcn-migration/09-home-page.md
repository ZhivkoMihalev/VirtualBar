# Slice 9 — HomePage (public news / social feed + admin post editor)

> Read `00-OVERVIEW.md` first. This doc assumes the §-numbered decisions, the token cheat-sheet
> (§6), the component inventory (§7), and the established patterns (§8) — it will not repeat them.
> Reference implementation for the admin form is the four auth pages (§8a). Follows the §13 template.

## Context recap

`/` is the public home page (`HomePage.tsx`, ~1100 lines, one file): a hero band, then a vertical
social/news **feed** mixing admin-authored news articles with "added a bottle" / "listed for sale"
activity from followed users. Anonymous visitors see a read-only feed; **admins** (`user.isAdmin`)
additionally get a "New Post" button, per-card Edit/Delete, a right-side **post editor drawer** with
**bg/en language tabs**, and a delete confirm. It is the most overlay-heavy page after the dashboard
and is ~100% inline-styled speakeasy hex. It depends on Slice 4 (it renders `<NavBar />`).

## Goal

Re-skin the whole page to the amber/stone/Inter token theme (§6) and rebuild every hand-rolled piece
on shadcn primitives: news/activity cards → `Card`; loaders → `Skeleton`; the admin editor drawer →
`Sheet` + `Tabs` + the §8a RHF+zod `Form`; the delete confirm → `AlertDialog`. Preserve every `t()`
key, the public/admin gating, the `lang` query param wiring, and the multilingual payload contract.
End on a green `npm run build` (§10).

## Files to touch

- `src/pages/HomePage.tsx` — full rewrite (primary work). Keep the local sub-components, but rebuild
  their internals. Consider extracting the editor into a `PostFormSheet` local component for clarity.
- **No API changes** — `src/api/newsApi.ts` (`createNewsPost`, `updateNewsPost`, `deleteNewsPost`,
  `getNewsPost`, `uploadNewsCover`) and `src/api/feedApi.ts` (`getFeed`) are reused verbatim.
- **No new i18n keys** — all copy already exists under `home`/`hero`/`lang` (see list below). If a
  `SheetDescription`/`AlertDialogDescription` is added purely for a11y, reuse existing copy or add ONE
  key to **both** `bg.json` + `en.json` (§8d) — do not invent gratuitous keys.
- `src/lib/validation.ts` — reuse `type TFn`; the news schema factory lives page-local in HomePage.

## Current state (what the file does now)

- **`Hero()`** — `hero.vol` kicker, `hero.title`, a gradient divider, `hero.subtitle`. Pure inline
  Playfair/Cinzel/Cormorant + gold hex.
- **`SkeletonCard()`** — a div with `animation: shimmer 1.6s …` and five gold-alpha placeholder bars;
  rendered ×3 while `isLoading`.
- **`NewsPostCard({ post, isAdmin, onEdit, onDelete })`** — `<article>` with `useState(hover)` driving
  `translateY`/`boxShadow`/image `scale`; optional cover `<img>` (h-220); a kicker line; an `<h2>`
  title; a content `<p>` with `whitespace-pre-wrap` truncated at `PREVIEW_LENGTH = 280` (on the last
  space) with a local `expanded` toggle button (`home.readMore`/`home.readLess` + a ▼ unicode that
  rotates); a footer with `home.authorBy` + `formatDate(createdAt)` and, when `isAdmin`, `home.editBtn`
  / `home.deleteBtn` buttons.
- **`BottleThumb({ url, category })`** — 80×80 image, or a bordered box with the category initial / 🥃.
- **`BottleActivityCard({ item })`** — horizontal `<article>` (hover lift), a username `Link`, header
  text (`home.addedToCollection` / `home.listedForSale`), an amber "ЗА ПРОДАЖБА" badge (`home.forSale`),
  the bottle name `Link` to `/marketplace` or `/bar/:userId`, a category chip, price, date, `BottleThumb`.
- **`PostFormPanel({ mode, initial, pending, error, onSubmit, onClose })`** — the admin editor. A hand-
  rolled `fixed inset-0 z-50` overlay: click-backdrop + a 480px right drawer with manual `×` close. It
  holds **per-language drafts** `drafts: Record<'bg'|'en', { title, content }>` plus a `coverImageUrl`
  string, an `activeLang` tab state, custom bottom-border **tab buttons** (`lang.bg`/`lang.en`), a title
  `Input`, a content `Textarea`, a **cover upload** (styled `<label>` over a hidden file input calling
  `uploadNewsCover`, a URL `Input`, preview + remove, `coverUploadError`), a `validationError`
  (`home.postValidationRequired`), a submit error (`home.errorSubmit`), and submit/cancel. On submit it
  **requires `drafts.bg.title`** (else flips to the bg tab), prunes languages whose title+content are
  both blank, and calls `onSubmit({ coverImageUrl: …||undefined, translations })`. A `useEffect([initial])`
  seeds the drafts from `initial.translations` on edit (or clears on create).
- **`ConfirmDeleteDialog({ pending, onConfirm, onCancel })`** — hand-rolled centered `fixed inset-0
  z-60` modal: `home.confirmDelete`, `home.cancelBtn`, `home.confirmDeleteBtn`.
- **`HomePage()` (default export)** — `useAuth()` → `isAdmin = user?.isAdmin === true`;
  `lang = i18n.language?.startsWith('bg') ? 'bg' : 'en'`; `useQuery(['feed', lang], () => getFeed(0, 50,
  lang))`; `createMutation`/`updateMutation`/`deleteMutation` (each `invalidateQueries(['feed'])` on
  success); `handleEditPost(id)` does an async `getNewsPost(id, lang)` (to load BOTH translations) then
  opens the editor. Three booleans drive the overlays: `showCreatePanel`, `editingPost`, `confirmDelete`.
  Renders loading (3× skeleton) / error / empty / feed; maps each `FeedItem` → a synthetic `NewsPost`
  for `type==='News'` (→ `NewsPostCard`) else `BottleActivityCard`.

## Transformation plan

### Overlays → Radix (§7, §8c) — the headline change

| Now | Target |
|---|---|
| `PostFormPanel` `fixed inset-0` + backdrop + 480px drawer + manual `×`/click-outside | **`Sheet` / `SheetContent side="right"`** — portal, overlay, ESC, focus-trap, scroll-lock free |
| custom bottom-border lang tab buttons | **`Tabs` / `TabsList` / `TabsTrigger value="bg"|"en"` / `TabsContent`** |
| `ConfirmDeleteDialog` centered modal | **`AlertDialog`** (destructive confirm) |
| 3× `SkeletonCard` shimmer div | **`Skeleton`** blocks inside a `Card` |
| `NewsPostCard` / `BottleActivityCard` `<article>` | **`Card`** (+ `CardContent` / `CardFooter`) |

Render the Sheet **once**, controlled: `open={showCreatePanel || !!editingPost}`, `onOpenChange` →
close both. Derive `mode` from `editingPost ? 'edit' : 'create'`. The original mounted two separate
`PostFormPanel`s — collapse to one Sheet and reset the form via `useEffect` (below). Drop all manual
`useRef`+`mousedown` / backdrop-onClick / ESC / `×` code — Radix supplies them (§9 a11y win).

### Admin editor: RHF + zod with bg/en tabs feeding ONE payload (§8a)

This **is** a validated admin form → use the auth-page pattern. The bg/en `Tabs` are a *visual*
container only; **all four fields live in one flat RHF form**:

```ts
const makeSchema = (t: TFn) => z.object({
  coverImageUrl: z.string().optional(),
  bgTitle:   z.string().trim().min(1, t('home.postValidationRequired')), // required-Bulgarian rule
  bgContent: z.string().optional(),
  enTitle:   z.string().optional(),
  enContent: z.string().optional(),
})
type Values = z.infer<ReturnType<typeof makeSchema>>
const schema = useMemo(() => makeSchema(t), [t])
const form = useForm<Values>({ resolver: zodResolver(schema), defaultValues: { coverImageUrl: '', bgTitle: '', bgContent: '', enTitle: '', enContent: '' } })
```

- **Reset on open/edit** (replaces the `useEffect([initial])`): when `editingPost` is set, `form.reset`
  from its `translations` (find `bg`/`en`, default to `''`) + `coverImageUrl`; for create, `form.reset`
  to empty. Reset on the Sheet's `onOpenChange(false)` too.
- **Submit** `form.handleSubmit(onValid, onInvalid)`. `onValid(values)` rebuilds the payload exactly
  like the original: map `['bg','en']` → `{ languageCode, title: values[`${lc}Title`].trim(), content:
  values[`${lc}Content`].trim() }`, **filter out languages where title AND content are both empty**,
  then `mutate({ coverImageUrl: values.coverImageUrl?.trim() || undefined, translations })`. `onInvalid`
  → if `errors.bgTitle`, `setActiveTab('bg')` (mirrors the old `setActiveLang('bg')`) so the
  `FormMessage` is visible.
- **Markup**: `<Form {...form}><form onSubmit={…}>` → `<Tabs value={activeTab} onValueChange>` with
  `TabsTrigger value="bg">{t('lang.bg')}` / `value="en">{t('lang.en')}`; inside each `TabsContent` the
  two `FormField`s (`{lc}Title` → `Input`, `{lc}Content` → `Textarea rows={10}` with `FormLabel`
  `home.postTitleLabel`/`home.postContentLabel` and placeholders). Cover block outside the tabs.
- **Server error** → `form.setError('root', { message: t('home.errorSubmit') })` on mutation error;
  render the standard error box (§6). Submit `<Button type="submit" size="lg" className="w-full"
  disabled={mutation.isPending}>` (amber default — §7 has no gradient variant); cancel via
  `<SheetClose asChild><Button variant="outline" size="lg" className="w-full">{t('home.cancelPost')}`.

### Cover upload (stays imperative, outside `handleSubmit`)

Keep `uploadNewsCover(file)` as a side action (own `useMutation` or async handler with local
`uploading`/`error` state). Trigger via a `Button variant="outline" size="sm"` (lucide `Upload`, spinner
= lucide `Loader2 animate-spin`) over a hidden `<input type="file" ref>`; on success `form.setValue
('coverImageUrl', url, { shouldDirty: true })`. Keep the URL `Input` (bound to the `coverImageUrl`
field) + the preview `<img>` with a remove `Button size="icon-xs" variant="secondary"` (lucide `X`).
Cover error → standard error box (§6) **or** `toast.error(t('home.coverUploadError'))` (sonner, §7).

### Cards → `Card` (§7)

- **`NewsPostCard`** → `Card className="group overflow-hidden border-l-[3px] border-l-primary transition
  hover:-translate-y-1 hover:shadow-xl"`. **Delete `useState(hover)`** — use `group`/CSS hover. Cover
  `<img>` (product content — keep `<img>`) in a fixed-height wrapper with `group-hover:scale-[1.04]
  transition-transform`. `CardContent`: kicker (`text-xs font-medium uppercase tracking-wide
  text-primary` + a `h-px w-6 bg-primary`), title `<h2 className="font-heading text-2xl font-semibold
  text-foreground">` (override `CardTitle`'s small default — §7), preview `<p className="text-sm
  text-muted-foreground whitespace-pre-wrap">` (keep `whitespace-pre-wrap`; keep the 280/last-space
  truncation + `expanded` state), read-more `<Button variant="link" size="xs">` with lucide `ChevronDown`
  (`className={expanded ? 'rotate-180' : ''} transition-transform`). `CardFooter className="border-t
  border-border justify-between"`: author/date `text-sm italic text-muted-foreground`; **admin** Edit
  `<Button variant="outline" size="xs">` (lucide `Pencil`) + Delete `<Button variant="outline" size="xs"
  className="text-destructive border-destructive/40 hover:bg-destructive/10">` (lucide `Trash2`).
- **`BottleActivityCard`** → `Card className="flex items-center gap-4 p-5 border-l-[3px] border-l-primary
  transition hover:-translate-y-1 hover:shadow-xl"`. Username/bottle `Link`s → `text-primary
  hover:underline` / title `font-heading text-lg font-semibold text-foreground`. "FOR SALE" →
  `<Badge>` (amber default, `text-[10px] uppercase tracking-wide`). Category chip → `<Badge
  variant="outline">`. Price → `text-base font-semibold text-primary`; date → `text-xs italic
  text-muted-foreground`. `BottleThumb` keeps its `<img>`; border → `border-border`, fallback bg →
  `bg-primary/5`, initial → `text-primary font-heading`.
- **`Hero`** → `hero.vol`: `text-xs tracking-[0.35em] text-muted-foreground`; `hero.title`:
  `font-heading text-5xl font-bold text-primary` (Inter, **no Playfair**); divider: `h-0.5 w-44
  bg-gradient-to-r from-transparent via-primary to-transparent`; `hero.subtitle`: `text-lg italic
  text-primary/90 max-w-xl mx-auto`.
- **`SkeletonCard`** → a `Card` with composed `Skeleton`s: `h-3 w-28`, `h-7 w-3/4`, then three lines
  (`h-3.5 w-full`, `w-5/6`, `w-3/5`). The `shimmer` keyframe is no longer referenced here (Skeleton uses
  `animate-pulse`); **leave the keyframe in `index.css`** — other slices still use it (§9).
- **`AlertDialog`** (delete): `open={!!confirmDelete}`, `AlertDialogTitle` = `home.confirmDelete`,
  `AlertDialogCancel` = `home.cancelBtn`, `AlertDialogAction className={buttonVariants({ variant:
  'destructive' })}` = `home.confirmDeleteBtn` (`disabled={deleteMutation.isPending}`) → `mutate
  (confirmDelete)`. (Optionally add an `sr-only` `AlertDialogDescription` to silence the Radix a11y
  warning — reuse copy, no new key.)
- **Page chrome**: outer wrapper drops `color:'#F0DDB4'` (inherits `text-foreground`). "New Post" →
  `<Button>` (amber, lucide `Plus`), **gated `isAdmin`**. Loading → 3× SkeletonCard. Error block →
  `text-destructive` (reuse `home.errorLoading`). Empty → lucide `Newspaper`/`PenLine` (`text-muted-
  foreground`) + `home.noNews` in `text-muted-foreground italic`.

### Inline → token map (§6, §8b)

| Inline constant / hex (now) | Token / utility |
|---|---|
| `inputStyle` (`#0A0502`, `rgba(201,168,76,.2)`, `#F0DDB4`, Cormorant) + `focusOn/Off` | shadcn `Input`/`Textarea` (`bg-input`, `border-input`, `text-foreground`, built-in `ring-ring`) — delete focus handlers |
| `labelStyle` (Cinzel 11, `#B09868`, uppercase) | `FormLabel`/`Label` → `text-xs font-medium text-muted-foreground` (opt. `uppercase tracking-wide`) |
| hero gold `#E8C870` / `#C9A84C` / `#B09868`, Playfair/Cormorant | `text-primary` / `text-muted-foreground`, `font-heading`, Inter |
| card bg `rgba(15,8,5,.7)`; border `rgba(201,168,76,.12)`; left `#C9A84C` | `bg-card`; `border-border`; `border-l-[3px] border-l-primary` |
| hover `translateY`/`boxShadow`/img `scale` (via `useState`) | Tailwind `transition hover:-translate-y-1 hover:shadow-xl`, `group-hover:scale-[1.04]` |
| title `#E8C870` | `text-foreground` (big titles; amber reserved for kickers/links per §8b) |
| preview `#D8C9A8`; author `#B09868` italic; date `#8A7650` | `text-muted-foreground` (+ `italic`) |
| delete/error `#C04040` / `#D42020` / `#7A2020` | `text-destructive` / `bg-destructive` / `buttonVariants destructive` |
| FOR-SALE gold gradient badge | `<Badge>` (amber default) |
| submit/new-post gold gradient `linear-gradient(#C9A84C,#E8C870)` | `<Button>` default (amber) |
| drawer gradient `#0F0604→#130805`, `border-left` | `SheetContent` (`bg-background`/`border-border`) |
| `×` close, ▼ unicode, 🥃, ✎ | lucide `X`, `ChevronDown`, fallback initial, `Newspaper`/`PenLine` |

## i18n keys to preserve (verbatim — page uses `home`/`hero`/`lang`, **no `news` namespace**)

`hero.vol`, `hero.title`, `hero.subtitle` ·
`lang.bg`, `lang.en` (via `t('lang.' + lc)`) ·
`home.newPost`, `home.noNews`, `home.errorLoading`, `home.addedToCollection`, `home.listedForSale`,
`home.forSale`, `home.authorBy` (interpolates `{{name}}`), `home.editBtn`, `home.deleteBtn`,
`home.readMore`, `home.readLess` ·
editor: `home.createPostTitle`, `home.editPostTitle`, `home.postTitleLabel`, `home.postTitlePlaceholder`,
`home.postContentLabel`, `home.postContentPlaceholder`, `home.postCoverLabel`, `home.uploadCover`,
`home.postCoverUrlPlaceholder`, `home.coverUploadError`, `home.postValidationRequired`,
`home.errorSubmit`, `home.submitPost`, `home.updatePost`, `home.cancelPost` ·
delete: `home.confirmDelete`, `home.confirmDeleteBtn`, `home.cancelBtn`.

## Slice-specific gotchas

- **One payload from two tabs.** The bg/en `Tabs` are visual only — keep all four fields in a single
  `useForm`. **Do NOT set `shouldUnregister: true`**: Radix unmounts the inactive `TabsContent`, but the
  RHF `Controller` store is independent of DOM mount, so the EN draft survives while the BG tab is active
  (and vice-versa). If you ever fall back to native `register`, add `forceMount` to both `TabsContent`.
- **Required-Bulgarian (product rule, `CLAUDE.md`).** zod requires `bgTitle`. If it errors while the EN
  tab is active, the `FormMessage` lives in the unmounted BG tab — so in `onInvalid` `setActiveTab('bg')`
  (preserves old behavior). Nice-to-have: a destructive dot on the BG `TabsTrigger` when it has an error.
- **Prune empty languages.** Only push a translation whose title OR content is non-empty (keep the
  original `.filter`). EN left blank must be omitted from the payload, not sent empty.
- **Admin-only + nullable user.** The feed is public, so `user` may be `null`. Gate the New-Post button
  AND per-card Edit/Delete on `isAdmin = user?.isAdmin === true`; never crash when `user` is null. The
  Sheet/AlertDialog/mutations are only reachable from admin UI — keep it that way.
- **`lang` param & feed-vs-news shape.** `getFeed(0,50,lang)` returns News items with flat
  `postTitle`/`postContent` in the requested language and **`translations: []`**. Full bg+en text is only
  fetched on Edit via `getNewsPost(id, lang)` — so `form.reset` for edit must read that result's
  `translations`, NOT the feed card. Keep `handleEditPost` async. `lang` derives from `i18n.language` and
  re-keys `['feed', lang]`, so switching language refetches — preserve it.
- **Synthetic `NewsPost` from `FeedItem`** (the `type==='News'` mapping) stays; the Card only needs
  `title/content/coverImageUrl/authorDisplayName/createdAt`.
- **Sheet width.** shadcn `SheetContent side="right"` defaults to ~`sm:max-w-sm` (~384px); the drawer is
  480px → override `className="w-full sm:max-w-[480px] overflow-y-auto"`.
- **mira is compact (§4).** `Input`/`Button` are `h-7`/`text-xs`. Bump the editor's title/content fields
  and feed body text to `text-sm`/`h-9` locally where the focal surface looks cramped — token-faithful.

## Verification (§10 — run in **both bg + en**)

1. `npm --prefix VirtualBar.Web run build` (green, the gate) + `run lint` (clean).
2. Dev server + backend (`dotnet run`, feed needs data). Anonymous `/`: hero in Inter/amber; feed loads
   via `GET /api/feed?lang=bg`; News + activity cards render; 3 skeletons while loading; empty + error
   states render. **No** New-Post/Edit/Delete anywhere.
3. Expand/collapse a >280-char post → read-more toggles, `ChevronDown` rotates.
4. Log in as the seeded admin (`AdminEmail`). New-Post → Sheet opens; verify **ESC**, overlay click, and
   the built-in Close all dismiss; **focus trapped**; **body scroll locked** (a11y wins the old code lacked).
5. Create: fill BG title+content, switch to EN tab (BG draft persists), fill EN, upload a cover
   (multipart), submit → feed invalidates and shows the post; switch app language to EN → EN text shows.
6. Required-BG: clear BG title, submit from the EN tab → tab auto-flips to BG, `FormMessage` =
   `home.postValidationRequired` (localized).
7. Edit: pencil → Sheet pre-fills **both** tabs from `getNewsPost`; edit + save updates.
8. Delete: trash → `AlertDialog`; Cancel keeps it; Confirm (destructive) removes it; pending disables Action.
9. Toggle БГ↔EN: hero, labels, buttons, validation, feed all re-localize; feed refetches. No console errors.

## Acceptance criteria

- Build + lint green; bundle within Slice-3 baseline (~225 kB gz JS) ± small.
- Dark amber/stone/**Inter** theme; **no serif**, no hardcoded speakeasy hex left in `HomePage.tsx`
  (nothing here qualifies as genuinely-dynamic per §8b).
- News/activity cards are shadcn `Card`; loaders are `Skeleton`; the admin editor is a `Sheet` + `Tabs`
  (bg/en) + RHF+zod `Form`; delete is an `AlertDialog`. All manual backdrop/ref/`×`/ESC code removed.
- Multilingual contract intact: per-language drafts persist across tabs, feed **one** `CreateNewsPostPayload`,
  empty languages pruned, **Bulgarian title required** (client-side, localized).
- Admin UI strictly gated on `user.isAdmin`; anonymous/non-admin get a read-only feed; no null-`user` crash.
- All listed `t()` keys preserved; verified in **both** bg + en; no console errors.

## Dependencies

- **Slice 4** (shared chrome) — `HomePage` renders `<NavBar />`; land 4 first.
- **Slice 2** primitives — `Card`, `Sheet`, `Tabs`, `AlertDialog`, `Skeleton`, `Form`, `Button`, `Badge`,
  `Input`, `Textarea`, `Label` (all present) + `lucide-react`, RHF/zod (§8a reference = Slice 3).
- No backend changes; independent of Slices 6/7/8/10. Next: `10-marketplace-page.md`.
