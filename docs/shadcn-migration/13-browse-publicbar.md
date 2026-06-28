# Slice 13 — BrowsePage + PublicBarPage

> Read `00-OVERVIEW.md` first. This slice re-skins the **collector discovery** surface
> (`BrowsePage`) and the **public profile** surface (`PublicBarPage`) onto shadcn tokens +
> primitives. It is one of the last page slices: it **depends on the migrated `Avatar` and `NavBar`
> from Slice 4** and the **migrated `BottleDetailPanel` from Slice 7**, and it **reuses
> `VirtualBarScene` (the shelf art) completely unchanged** per §3.5 / §8b.

## Context recap

These two pages form the social "browse other collectors" path: `/browse` is a search-and-grid of
`CollectorCard`s; clicking one routes to `/bar/:userId`, the public virtual bar. Both already use the
shared `NavBar` and `Avatar` (migrated in Slice 4) and currently render ~100% inline speakeasy
styling. The bottle shelf on the public bar is the bespoke `VirtualBarScene` SVG art — **theme
chrome it is not, so it does not change** (§3.5, §8b "What STAYS inline"). The bottle detail overlay
is the Slice-7 `BottleDetailPanel`, consumed as-is.

## Goal

Convert all inline `CSSProperties`/`style={}` on both pages to §6 tokens and shadcn `Card` / `Input` /
`Button` / `Badge` / `Skeleton`, keep every `t()` key and the debounced-search + follow-mutation
data flow byte-for-byte, **fix the message button to open the `ChatWidget` via
`ChatContext.openChat` instead of navigating to the non-existent `/messages` route**, and leave
`VirtualBarScene` / `BarShelf` / `BottleDetailPanel` untouched. End on a green build.

## Files to touch

- `src/pages/BrowsePage.tsx` — full re-skin (page header, search, `CollectorCard`, states).
- `src/pages/PublicBarPage.tsx` — full re-skin (header card, `FollowButton`, message button, states).

**NOT touched (consumed as-is):** `src/components/BarShelf.tsx` (`VirtualBarScene`, `CATEGORY_COLORS`
— §8b), `src/components/BottleDetailPanel.tsx` (Slice 7), `src/components/Avatar.tsx` and
`src/components/NavBar.tsx` (Slice 4), `src/api/usersApi.ts` and `src/api/bottlesApi.ts` (data
contract unchanged), `src/types/index.ts` (`UserSearchResult`, `UserProfile` unchanged).

## Current state

### BrowsePage (`src/pages/BrowsePage.tsx`)
- **Debounce hook** `useDebounced<T>(value, 300)` — generic `setTimeout`/`clearTimeout` hook. **Keep.**
- **Data hook** `useQuery({ queryKey: ['users', query], queryFn: () => searchUsers(query || undefined) })`
  where `query = useDebounced(search.trim(), 300)`. `searchUsers(q?)` → `GET /users` (q param optional).
  `collectors` defaults to `[]`. **Keep wiring; only the `<input>` element changes.**
- **`inputStyle`** module-level `CSSProperties` const: `#0A0502` bg, gold-alpha border, `#F0DDB4` text,
  Cormorant serif, `padding 12px 16px 12px 44px` (the 44px reserves room for the search glyph),
  `borderRadius 4`. Plus inline `onFocus`/`onBlur` handlers that swap the border color.
- **Search box:** relative wrapper (`maxWidth 480`), an absolutely-positioned `⌕` unicode glyph span,
  and the styled `<input>`.
- **Page header:** `browse.discover` eyebrow (Cinzel, `0.4em` tracking, `#B09868`), `browse.title` `<h1>`
  (Playfair `#E8C870`), `browse.subtitle` (Cormorant italic `#C9A84C`).
- **`CollectorCard`** (`<Link to={/bar/:id}>`): local `useState(hover)` drives `translateY(-3px)`,
  border brighten, and a drop shadow. Body = Avatar(56) + name (Playfair `#E8C870`, `nowrap` ellipsis)
  + optional `collector.country` (`#B09868`); a 2-line `-webkit-line-clamp` bio (italic `#C9A84C`,
  `minHeight 46`, falls back to `browse.defaultBio`); a top-bordered footer row with
  `browse.bottles`/`browse.followers` pluralized counts (`#B09868`) and a `browse.viewBar` label whose
  color brightens on hover.
- **States:** `isLoading` → centered Cinzel shimmer text (`browse.loading`); `isError` → `#C04040`
  (`browse.error`); empty → `browse.noResults`; results → CSS grid `repeat(auto-fill, minmax(300px,1fr))`.

### PublicBarPage (`src/pages/PublicBarPage.tsx`)
- **Two data hooks:** `['profile', userId]` → `getUserProfile(userId!)` (enabled `!!userId`) and
  `['bottles', userId]` → `getBottlesByUser(userId!)` (enabled `!!userId`). Combined
  `isLoading`/`isError`; `canFollow = !!user && !!profile && user.id !== profile.id`. **Keep all.**
- **`FollowButton`** sub-component: `useMutation` toggling `unfollowUser`/`followUser` on
  `profile.isFollowedByMe`, `onSuccess` invalidates `['profile', userId]`. Local `useState(hover)`
  + `mutation.isPending` drive a 3-state label: not-following → `publicBar.follow`; following+idle →
  `publicBar.following`; following+hover → `publicBar.unfollow`. Styling: gold gradient when not
  following, transparent + gold border when following; `wait` cursor + `opacity 0.6` while pending.
- **Header block:** `publicBar.backToBrowse` `<Link>` (`←` unicode), then a gold-tint card (`rgba(201,168,76,0.04)`)
  with Avatar(72), name `<h1>` (Playfair `#E8C870`), `[city, country].filter(Boolean).join(', ')`,
  optional bio, and a `publicBar.stats` line built from **nested** `t()` calls
  (`bottles`/`followers`/`following` each pluralized). When `canFollow`: `<FollowButton>` +
  a message `<button>` that currently calls **`navigate('/messages?with=${profile.id}')`** — a dead
  route (overview: "There is **no `/messages` route**"). Label = `messages.sendMessage`.
- **Shelf:** `bottles.length > 0 ? <VirtualBarScene bottles onSelect={setSelectedBottle}/> : publicBar.empty`.
  `onAdd` is **not** passed (read-only bar). `BottleDetailPanel` renders when `selectedBottle && userId`.
- **States:** combined `isLoading` → Cinzel shimmer (`publicBar.loading`); `isError` → `#C04040` (`publicBar.error`).

## Transformation plan

### Component / token mappings

| Current | Target |
|---|---|
| `CollectorCard` `<Link>` + `hover` state + manual lift/shadow | `<Link to={…} className="group block">` wrapping a shadcn **`Card`**; replace the `useState(hover)` with CSS `group-hover` (`transition-all group-hover:-translate-y-1 group-hover:border-primary/40 group-hover:shadow-lg`). `Card` is a plain div — **do not use `asChild`** (it is not Slot-based); the `<Link>` stays the outer nav target. |
| `inputStyle` const + `<input>` + `onFocus`/`onBlur` border swap | shadcn **`Input`** with `className="pl-9"`; delete `inputStyle` and both handlers (Input's `aria-invalid`/focus-ring is built in). The `⌕` glyph → lucide **`Search`** icon, absolutely positioned (`absolute left-2.5 top-1/2 -translate-y-1/2 size-4 text-muted-foreground pointer-events-none`). |
| `browse.bottles` / `browse.followers` counts | **plain tokens** (`text-xs text-muted-foreground`) — they are pluralized running text, so a `Badge` reads worse; **`Badge variant="secondary"`** is an acceptable alternative if a chip look is wanted. |
| `FollowButton` gold-gradient / outline `<button>` | shadcn **`Button`**: not-following → `variant="default"` (amber); following → `variant="outline"`. Keep `disabled={mutation.isPending}`; optionally add a lucide `Loader2 className="animate-spin"` while pending. |
| 3-state follow label | Keep the logic. Prefer CSS swap to drop `hover` state: `<span className="group-hover:hidden">{t('publicBar.following')}</span><span className="hidden group-hover:inline">{t('publicBar.unfollow')}</span>` inside the following branch; non-following branch renders `t('publicBar.follow')`. (Keeping the `useState(hover)` is also fine.) |
| Message `<button onClick={navigate('/messages?…')}>` | **`Button variant="outline"`** with `onClick={() => openChat(profile.id)}` from `useChat()`. **Functional fix — see gotchas.** |
| PublicBar header surface + `backToBrowse` link | Wrap the header in a **`Card`** (`bg-card border rounded-lg`, `CardContent`) or token div; `backToBrowse` → `text-sm text-muted-foreground hover:text-foreground`, `←` → lucide **`ArrowLeft`** (`size-4`). |
| `isLoading` Cinzel shimmer (both pages) | shadcn **`Skeleton`**: Browse → a grid of ~6 `Skeleton` cards (`h-40 rounded-lg`); PublicBar → a header `Skeleton` (avatar circle + text lines) + one `Skeleton className="h-[560px] rounded"` for the shelf. |
| `isError` `#C04040` text | standard **error box** (§6): `rounded-md border border-destructive/40 bg-destructive/10 px-3 py-2 text-sm text-destructive`, centered. |
| empty `noResults` / `empty` | centered `text-muted-foreground` (drop Cormorant italic serif). |

### Inline → token map (both pages)

| Old speakeasy value | Token / utility |
|---|---|
| `color: #F0DDB4` page text | `text-foreground` (page bg comes from the App shell) |
| `#E8C870` / `#C9A84C` gold (names, eyebrow, accents) | `text-primary` |
| `#B09868` meta text | `text-muted-foreground` |
| `#C04040` error | `text-destructive` + error box |
| `rgba(201,168,76,0.04)` card fill | `bg-card` |
| `rgba(201,168,76,0.1 / 0.12 / 0.4)` borders | `border-border`, hover `group-hover:border-primary/40` |
| `linear-gradient(135deg,#C9A84C,#E8C870)` follow btn | `Button variant="default"` |
| transparent + gold-border buttons | `Button variant="outline"` |
| Playfair Display headings | `font-heading` (Inter) |
| Cormorant Garamond / Cinzel labels | default `font-sans`; tiny labels → `text-xs font-medium uppercase tracking-wide text-muted-foreground` (lean to mira's clean look, §8b) |
| hover `translateY` + boxShadow lift | `transition-all group-hover:-translate-y-1 group-hover:shadow-lg` |
| `⌕` / `←` unicode glyphs | lucide `Search` / `ArrowLeft` |
| shimmer loading text | `Skeleton` |

**Stays inline / untouched:** everything inside `VirtualBarScene` and `BarShelf` (shelf gradients,
`CATEGORY_COLORS[category]` per-category bottle fills, `BottleSvg` geometry, bottle hover
`translateY/scale`, `animationDelay`) — §8b. `Avatar` size-from-prop is handled inside the already-migrated `Avatar`.

## i18n keys to preserve (verbatim — verify bg + en)

- **browse:** `browse.discover`, `browse.title`, `browse.subtitle`, `browse.searchPlaceholder`,
  `browse.loading`, `browse.error`, `browse.noResults`, `browse.defaultBio`,
  `browse.bottles` (plural `_one`/`_other`), `browse.followers` (plural), `browse.viewBar`.
  *(Note: `browse.follow`/`following`/`unfollow` exist in the JSON but are **not** referenced by this
  page — leave them; PublicBar uses its own.)*
- **publicBar:** `publicBar.loading`, `publicBar.error`, `publicBar.backToBrowse`, `publicBar.stats`,
  `publicBar.bottles` (plural), `publicBar.followers` (plural), `publicBar.following` (plural),
  `publicBar.follow`, `publicBar.following`, `publicBar.unfollow`, `publicBar.empty`.
- **messages:** `messages.sendMessage` (the message button).
- **barShelf:** `barShelf.virtualBar` renders **inside** the untouched `VirtualBarScene` — do not
  modify, just be aware it appears (`barShelf.addBottle` does not fire here — no `onAdd`).

Keep the nested-interpolation `publicBar.stats` call shape exactly (`{{bottles}} · {{followers}} · {{following}}`).

## Slice-specific gotchas

- **Debounced search is load-bearing — preserve it.** `useDebounced`, `search.trim()`, the `300`ms
  delay, and `queryKey: ['users', query]` must stay identical. Only the rendered `<input>` becomes
  `Input`; the state/query wiring is off-limits.
- **Message button is a functional fix, not cosmetic.** Replace `navigate('/messages?with=…')` with
  `openChat(profile.id)` from `useChat()` (`ChatContext`). The `ChatWidget` is mounted globally in
  `App.tsx`, so calling `openChat` opens the inbox + thread on this page. Add
  `import { useChat } from '../contexts/ChatContext'`; **remove the now-unused `useNavigate`** import
  (it was only used here) or lint/`noUnusedLocals` will fail.
- **Follow 3-state label.** Don't collapse it to two states. Map only the *styling* to Button variants
  (`default` vs `outline`); keep the follow → following → (hover) unfollow text behavior and
  `disabled={mutation.isPending}`. The mutation + `invalidateQueries(['profile', userId])` are unchanged.
- **`Card` is not a link.** It renders a `div` with no `asChild`; keep `<Link className="group block">`
  outside it and drive the lift via `group-hover` so the whole card stays one tab stop / nav target.
- **`VirtualBarScene` is untouched (§3.5/§8b).** Do not re-skin the shelf, do not tokenize
  `CATEGORY_COLORS`, do not pass new props. `BottleDetailPanel` is the Slice-7 component used as-is.
- **Loading/empty → `Skeleton`**, not the Cinzel shimmer string; keep card heights consistent (give the
  bio `line-clamp-2` a `min-h-[2.75rem]` so cards in a row align like the old `minHeight: 46`).
- **mira is compact (§4).** These are focal discovery cards; if Avatar(56/72)+`text-xs` feels cramped,
  bump locally (`text-sm`/`text-base`) — token-faithful only, no hex.
- **Field differences:** Browse `CollectorCard` uses `collector.country` only (UserSearchResult has no
  city); PublicBar joins `city, country`. Preserve both as written.

## Verification (§10)

1. **Gate:** `npm --prefix VirtualBar.Web run build` (green) + `npm --prefix VirtualBar.Web run lint` (clean).
2. **Backend up** (`dotnet run` in `VirtualBar.Api`) — both pages are data-bearing. Run `npm run dev`.
3. **`/browse`, in bg + en:** type a query → results debounce-filter (~300ms); clear → full list;
   loading shows `Skeleton` cards; search gibberish → `browse.noResults`; hover a card → it lifts;
   click → routes to `/bar/:id`. Counts + `viewBar` render in both languages.
4. **`/bar/:userId`, in bg + en:** header (avatar/name/location/bio/`stats`) renders on tokens; the
   shelf renders bottles via the unchanged `VirtualBarScene`; click a bottle → `BottleDetailPanel` opens.
5. **Follow/unfollow:** click follow → label → `following`; hover → `unfollow`; counts refresh after
   invalidate; click again → unfollows. Disabled state while pending.
6. **Message → chat:** click the message button → the floating `ChatWidget` inbox/thread opens for that
   user (`openChat`); confirm **no** route change and **no** 404.
7. **a11y:** `Input` focus ring; `Button` focus; card is a single keyboard tab stop. No console errors.

## Acceptance criteria

- `npm run build` green, `npm run lint` clean.
- Dark **amber/stone/Inter** look on both pages; **no serif**; **no hardcoded speakeasy hex remains in
  either page file** (the excepted `BarShelf`/`CATEGORY_COLORS` are untouched, not in scope).
- **BrowsePage:** search still debounced + functional; `CollectorCard` uses `Card` + the migrated
  `Avatar` + `Search` icon `Input`; loading → `Skeleton`; error → standard box; empty tokenized.
- **PublicBarPage:** header on tokens/`Card`; `FollowButton` → `Button` with correct 3-state label and
  unchanged mutation; **message button opens the `ChatWidget` via `openChat` (no `/messages`
  navigation)**; `VirtualBarScene` rendered unchanged; `BottleDetailPanel` opens on bottle click.
- All listed `t()` keys preserved and verified in **bg + en**; no console errors.

## Dependencies

- **Slice 4** (`04-shared-chrome.md`) — provides the migrated `Avatar`, `NavBar`, and the globally
  mounted `ChatWidget` that `openChat` drives.
- **Slice 7** (`07-bottle-detail-panel.md`) — provides the migrated `BottleDetailPanel` used by the bar.

Otherwise self-contained. This is among the last page slices; `14-cleanup.md` follows.
