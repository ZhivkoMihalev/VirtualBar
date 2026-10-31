# Slice 4 — Shared chrome (NavBar, Footer, LanguageSwitcher, NotificationBell, Avatar)

> Read `00-OVERVIEW.md` first. This slice assumes the §6 token cheat-sheet, the §7 component
> inventory, and the §8 patterns. It does not repeat them.

## Context recap

These five components render on **every** page (NavBar + Footer are mounted in the `App.tsx` shell;
`LanguageSwitcher`, `NotificationBell`, and `Avatar` are embedded in the NavBar). They are still
100% hand-rolled inline-style speakeasy code with two manual `useRef`+`mousedown` click-outside
dropdowns and one click-outside mobile menu. Because they are global chrome, this slice lands
**before** the page slices (8–13) so the new amber/stone/Inter look appears site-wide immediately.

## Goal

Re-skin and re-platform all shared chrome onto shadcn: NavBar links → `Button`, mobile menu →
`Sheet`, profile menu → `DropdownMenu`; `LanguageSwitcher` → `DropdownMenu` + `DropdownMenuRadioGroup`;
`NotificationBell` → `Popover` + `ScrollArea` with a lucide `Bell` + `Badge`; `Avatar` wraps shadcn
`Avatar` while keeping its exact `(displayName, avatarUrl?, size)` props API so every caller works
unchanged. All inline hex → §6 token utilities. **Every `t()` key and all data/context behavior is
preserved.** End on a green build.

## Files to touch

- `src/components/NavBar.tsx`
- `src/components/Footer.tsx`
- `src/components/LanguageSwitcher.tsx`
- `src/components/NotificationBell.tsx`
- `src/components/Avatar.tsx`

No API or context files change. `src/api/notificationsApi.ts` (`getNotifications`,
`markNotificationRead`, `markAllNotificationsRead`) and `ChatContext` (`toggleInbox`, `openChat`)
are consumed verbatim.

## Current state

**`NavBar.tsx`** — sticky wrapper `div` (`position:sticky; top:0; zIndex:40`) with a `wrapperRef` +
`useEffect` `mousedown` listener that closes the mobile menu. State: `menuOpen` (`useState`).
Contexts: `useAuth` (`user`, `isAuthenticated`, `logout`), `useChat` (`toggleInbox`). Two module
constants `navLinkStyle` / `mobileLinkStyle` (Cinzel 11px, `#B09868`). Layout: logo (VB circle +
"VIRTUALBAR"), desktop center `<Link>`s (Home/Browse/Marketplace, +Dashboard/Offers when auth, +a
`<button onClick={toggleInbox}>` for Messages), desktop right slot (auth: profile `<Link>` with
`<Avatar size={32}>` + name, `<NotificationBell/>`, `<LanguageSwitcher/>`, logout `<button>`;
anon: Login link, `<LanguageSwitcher/>`, gold-gradient Register link), a mobile hamburger `<button>`
(`☰`/`✕` unicode), and a conditionally-rendered mobile dropdown panel mirroring all links + a
hairline `<div>` divider.

**`Footer.tsx`** — `useTranslation` + `useAuth` (`isAuthenticated`). Builds `navLinks[]` and
`legalItems[]`. Opaque `#07030A` footer, gold top border, 3-col grid (`2fr 1fr 1fr`): brand block
(VB circle + VIRTUALBAR + italic tagline), Navigation column (`<Link>`s with manual
`onMouseEnter/onMouseLeave` color swaps), Legal column (plain `<span>`s). A decorative divider (two
`flex:1` hairlines + a `◆`), then a centered copyright line. All inline Cinzel/Cormorant hex.

**`LanguageSwitcher.tsx`** — `open` state + `ref` + `mousedown` `useEffect`. `languages[]` =
`[{bg, t('lang.bg')}, {en, t('lang.en')}]`. `currentCode = i18n.language?.startsWith('bg') ? 'bg' : 'en'`,
`currentLabel` = `BG`/`EN`. Trigger `<button>` (`BG ▼`); panel of two `<button>`s each calling
`i18n.changeLanguage(code)` and showing a `✓` on the active one. All inline gold/dark.

**`NotificationBell.tsx`** — `open` state + `ref` + `mousedown` `useEffect`. Data:
`useQuery(['notifications'], getNotifications, { refetchInterval: 30_000 })`;
`useMutation(markNotificationRead)` and `useMutation(markAllNotificationsRead)`, both
`invalidateQueries(['notifications'])` on success. Contexts: `useChat` (`openChat`), `useNavigate`.
Pure helpers `relativeTime(iso, t)`, `describe(item, t)` (switch over all 10 `NotificationType`),
`targetPath(item)` (switch → `/bar/{actorId}`, `null`, `/marketplace`, or `/offers`). Nine
module-level `CSSProperties` constants (`bellButtonStyle`, `badgeStyle`, `dropdownStyle`,
`dropdownHeaderStyle`, `dropdownTitleStyle`, `markAllButtonStyle`, `emptyStateStyle`, `itemTextStyle`,
`itemTimeStyle`). Bell is an **inline SVG**; badge is a gold-gradient pill (`99+` cap). Dropdown =
header (title + mark-all) + empty state or up to 30 item `<button>`s (per-item bg by `isRead`).
`handleItemClick`: marks read if unread, `setOpen(false)`, then `NewMessage → openChat(actorId)`
else `navigate(targetPath(item))`.

**`Avatar.tsx`** — props `{ displayName, avatarUrl?, size }`. `initial` = first char upper, `'?'`
fallback. If `avatarUrl` → `<img>` (inline `width/height = size`, `rounded-full`, gold border).
Else → gradient `<div>` with `font-size = size*0.42`, Playfair, gold text, the initial.

## Transformation plan

### NavBar.tsx
- **Drop** `navLinkStyle`, `mobileLinkStyle`, `wrapperRef`, the `mousedown` `useEffect`, the `close`
  helper, and `menuOpen` — the `Sheet` owns mobile-menu open/close, and `SheetClose` auto-closes on
  navigation. Keep `useAuth` + `useChat`.
- **Sticky wrapper**: `style={{position:'sticky',top:0,zIndex:40}}` → `className="sticky top-0 z-40"`.
- **Bar**: bg `rgba(7,3,10,.95)` + blur + gold border → `className="flex h-16 items-center justify-between gap-4 border-b border-border bg-background/95 px-6 backdrop-blur"` (per §6 nav bg → `bg-background/95 backdrop-blur`).
- **Logo**: keep `<Link to="/">`. VB circle (`border #C9A84C`, gold text) → `flex h-9 w-9 items-center justify-center rounded-full border border-primary text-xs text-primary`; brand word (Playfair gold) → `text-lg font-semibold tracking-[0.1em] text-primary` (drop serif).
- **Desktop center links** (§8c dropdown/links → `Button`): wrap each `<Link>` in
  `<Button asChild variant="ghost" size="sm"><Link to="…">{t('…')}</Link></Button>`. The Messages
  item stays a real button — `<Button variant="ghost" size="sm" onClick={toggleInbox}>{t('nav.messages')}</Button>` (no `asChild`; **keep `toggleInbox()`**). Wrapper `div` → `className="hidden flex-1 items-center justify-center gap-2 md:flex"`.
- **Desktop right slot** `className="hidden items-center gap-2 md:flex"`:
  - **Authenticated** → `<NotificationBell/>`, `<LanguageSwitcher/>`, then the **profile `DropdownMenu`**
    (per §8c): `DropdownMenuTrigger asChild` wrapping a `<Button variant="ghost" size="sm">` that
    renders `{user && <Avatar displayName={user.displayName} avatarUrl={user.avatarUrl} size={32} />}`
    + `<span className="text-sm text-primary">{user?.displayName}</span>`. `DropdownMenuContent align="end"`
    holds `DropdownMenuItem asChild → <Link to="/profile">` and a `DropdownMenuItem onSelect={logout}`
    for `t('nav.logout')`. **The standalone logout `<button>` is absorbed into this menu.**
  - **Anonymous** → `<Button asChild variant="ghost" size="sm"><Link to="/login"></Button>`,
    `<LanguageSwitcher/>`, and the gold-gradient Register → `<Button asChild><Link to="/register">`
    (default variant **is** amber, §7 — no custom gradient).
- **Mobile menu → `Sheet`** (§8c): replace hamburger + conditional panel. `SheetTrigger asChild`
  wrapping `<Button variant="ghost" size="icon" className="md:hidden"><Menu className="size-5"/></Button>`
  (lucide `Menu`; Sheet's built-in close `X` replaces the `✕`). `SheetContent side="right"` includes
  a `SheetHeader`/`SheetTitle` (e.g. brand — **required for a11y**, may be `sr-only`) then a vertical
  stack. Each nav item is `<SheetClose asChild><Button variant="ghost" className="justify-start" asChild><Link …></Button></SheetClose>`
  so tapping navigates **and** closes; the Messages button is `<SheetClose asChild><Button variant="ghost" className="justify-start" onClick={toggleInbox}>…</Button></SheetClose>`.
  Replace the hairline `<div>` with `<Separator/>`. Auth footer of the sheet: the profile `<Link>`
  (Avatar + name), a row with `<NotificationBell/>` + `<LanguageSwitcher/>`, and a logout `Button`;
  anon footer: Login link, Register `Button`, `<LanguageSwitcher/>`.

### Footer.tsx
- Root: `background:'#07030A'` → opaque dark token **`bg-background`** (covers the fixed room photo,
  honoring "fully opaque" from CLAUDE.md); gold top border → `border-t border-border`; keep
  `relative z-[1]`; padding → `className="relative z-[1] border-t border-border bg-background px-10 pt-13 pb-7 text-muted-foreground"`.
- Grid → `className="mx-auto grid max-w-[1100px] grid-cols-1 gap-12 md:grid-cols-[2fr_1fr_1fr]"`.
- Brand circle/word: same token mapping as NavBar (`border-primary text-primary`, drop serif).
  Tagline (`#7A6A52` italic) → `text-sm italic text-muted-foreground`.
- Column headers (`#C9A84C` Cinzel) → small section labels (§8b): `text-xs font-medium uppercase tracking-wide text-primary`.
- Nav links: **delete the `onMouseEnter/onMouseLeave` handlers** → `<Link className="text-xs text-muted-foreground transition-colors hover:text-primary">`. Legal `<span>`s → `text-xs text-muted-foreground`.
- Decorative divider (two hairlines + `◆`) → **`<Separator className="my-6" />`** (per task; optionally keep a centered lucide `Diamond` accent in `text-primary`).
- Copyright (`#4A3A22` Cinzel) → `text-center text-xs tracking-wide text-muted-foreground`.

### LanguageSwitcher.tsx
- **Drop** `open`, `ref`, and the `mousedown` `useEffect` (Radix handles dismissal). Keep
  `currentCode`/`currentLabel` derivation.
- `DropdownMenu` → `DropdownMenuTrigger asChild` wrapping `<Button variant="outline" size="sm">{currentLabel}<ChevronDown className="size-3 opacity-70"/></Button>` (lucide `ChevronDown` replaces `▼`).
- `DropdownMenuContent align="end"` → `DropdownMenuRadioGroup value={currentCode} onValueChange={(v) => i18n.changeLanguage(v)}` with `DropdownMenuRadioItem value="bg">{t('lang.bg')}` and `value="en">{t('lang.en')}`. The radio item's built-in indicator replaces the manual `✓`.
- Persistence is automatic: `i18n.changeLanguage` writes `vbar_lang` via the languagedetector cache — **do not add manual `localStorage`**.

### NotificationBell.tsx (per §8c: rich list → `Popover` + `ScrollArea`)
- Keep all data hooks (`useQuery` `refetchInterval: 30_000`, both mutations + invalidation),
  `useChat`/`useNavigate`, and the `relativeTime` / `describe` / `targetPath` / `handleItemClick`
  logic **verbatim** (incl. `NewMessage → openChat(actorId)`). **Drop** `ref` + the `mousedown`
  `useEffect`; keep `open` state but bind it: `<Popover open={open} onOpenChange={setOpen}>` (item
  clicks still call `setOpen(false)`; mark-all must NOT close).
- Trigger: `PopoverTrigger asChild` → `<Button variant="ghost" size="icon" className="relative" aria-label={t('notifications.title')}>` with `<Bell className="size-5 text-primary"/>` (lucide, replaces inline SVG). The unread pill → shadcn `<Badge className="absolute -top-1 -right-1 h-4 min-w-4 justify-center rounded-full px-1 text-[10px]">{unreadCount > 99 ? '99+' : unreadCount}</Badge>` (default amber variant; render only when `unreadCount > 0`).
- `PopoverContent align="end" className="w-[340px] p-0"`. Header: `flex items-center justify-between px-4 py-3` + `border-b border-border`, title `text-sm font-medium text-primary`, mark-all → `<Button variant="link" size="sm" onClick={() => readAllMutation.mutate()} disabled={readAllMutation.isPending}>`.
- Wrap the list in `<ScrollArea className="max-h-[420px]">`. Empty state → `px-4 py-7 text-center text-sm text-muted-foreground`.
- Each item `<button>`: keep full-width/left-aligned; per-item bg stays dynamic via
  `cn('block w-full border-b border-border px-4 py-3 text-left', item.isRead ? 'bg-transparent' : 'bg-accent')`.
  Actor name (`#E8C870`/600) → `text-primary font-medium`; body → `text-sm text-foreground leading-snug`; time → `mt-1 text-xs text-muted-foreground`.
- Delete all nine `CSSProperties` constants.

### Avatar.tsx (wrap shadcn `Avatar`, keep the exact props API)
- Signature unchanged: `{ displayName, avatarUrl?, size }`. Keep `initial` derivation.
- Collapse both branches: `<Avatar className="shrink-0 border border-primary/40" style={{ width: size, height: size }}>` (prop-driven inline size **stays** per §8b) with
  `<AvatarImage src={avatarUrl ?? undefined} alt={displayName} />` (Radix auto-falls-back when src is
  absent/fails) and `<AvatarFallback className="bg-muted text-primary" style={{ fontSize: Math.round(size * 0.42) }}>{initial}</AvatarFallback>`.
  Gold border `rgba(201,168,76,.4)` → `border-primary/40`; drop the radial gradient + Playfair (lean
  clean, §8b). **No prop or call-site change** — NavBar, ChatWidget (Slice 5), Marketplace, Browse,
  PublicBar, Profile all keep passing `size={n}`.

## i18n keys to preserve (verbatim)

- **NavBar**: `nav.home`, `nav.browse`, `nav.marketplace`, `nav.myBar`, `nav.offers`,
  `nav.messages`, `nav.login`, `nav.register`, `nav.logout`.
- **Footer**: `nav.home`, `nav.browse`, `nav.marketplace`, `nav.myBar`; `footer.tagline`,
  `footer.explore`, `footer.legal`, `footer.about`, `footer.privacy`, `footer.terms`, `footer.rights`.
- **LanguageSwitcher**: `lang.bg`, `lang.en`.
- **NotificationBell**: `notifications.title`, `notifications.empty`, `notifications.markAllRead`,
  `notifications.justNow`, `notifications.minutesAgo`, `notifications.hoursAgo`,
  `notifications.daysAgo`, and every `describe` key + its `…NoName` pair: `bottleLiked`,
  `bottleCommented`, `newFollower`, `newMessage`, `newBottleFromFollowing`, `bottleListedForSale`,
  `wishListMatch`, `offerReceived`, `offerAccepted`, `offerDeclined`. (Plurals via `{{count}}`,
  bottle via `{{bottle}}` — unchanged.) **No new keys are needed for this slice.**

## Slice-specific gotchas

- **`Sheet`/`Dialog` need a title.** Radix logs an a11y warning without `SheetTitle`; include one
  (wrap in `sr-only` if you don't want it visible).
- **Sheet auto-close**: prefer uncontrolled `Sheet` + `SheetClose asChild` on every interactive
  child so navigation/`toggleInbox` closes the drawer — no manual `menuOpen` plumbing.
- **Controlled Popover, partial close**: clicking a notification closes (`setOpen(false)` in
  `handleItemClick`); the mark-all button must leave it open — don't wrap it in anything that closes.
- **Icon-button badge clipping**: keep `className="relative"` on the bell `Button` and **no**
  `overflow-hidden`, or the `-top-1 -right-1` `Badge` gets cut.
- **Avatar size is prop-driven** — keep `style={{ width:size, height:size }}` and the
  `Math.round(size*0.42)` fallback font inline; do **not** convert to a Tailwind size class (callers
  pass arbitrary px like 28/32/40).
- **`AvatarImage` with no src** renders nothing and Radix shows `AvatarFallback` — pass
  `src={avatarUrl ?? undefined}`, never `null`.
- **Portals escape the sticky bar**: `PopoverContent`/`DropdownMenuContent`/`SheetContent` portal to
  `body` with high z — they won't be clipped by `z-40`; no extra z-index needed.
- **mira is compact** (§4): the bar is a fixed `h-16`; `size="sm"` buttons center fine. Bump only if
  a control reads too small.

## Verification (per §10, both bg + en)

1. `npm --prefix VirtualBar.Web run build` (green gate) and `npm --prefix VirtualBar.Web run lint`.
2. `npm run dev` → http://localhost:5173 (backend `dotnet run` needed so NotificationBell + auth
   chrome have data). Exercise with the `e2e-tester` agent at 1280×800 **and** a ~390px mobile width.
3. **NavBar desktop**: each center link navigates; **Messages opens the chat inbox** (`toggleInbox`);
   profile `DropdownMenu` opens → Profile routes to `/profile`, Logout signs out; `NotificationBell`
   + `LanguageSwitcher` show only when authenticated; anon shows Login/Register/Lang only.
4. **NavBar mobile** (<768px): `Menu` icon opens the right `Sheet`; tapping a link navigates **and**
   closes; Messages opens inbox + closes; logout works; **ESC and click-outside close** (Radix);
   focus is trapped; no `SheetTitle` console warning.
5. **LanguageSwitcher**: open dropdown, switch БГ↔EN — the radio indicator marks the active language;
   NavBar/Footer/NotificationBell copy re-localizes; reload keeps the choice (`vbar_lang`).
6. **NotificationBell**: badge shows the unread count (`99+` cap); open popover, `ScrollArea`
   scrolls; clicking an item marks it read + navigates (or **opens chat for `NewMessage`**); mark-all
   clears unread; empty state renders; relative-time strings localize; list refetches ~30 s.
7. **Avatar**: image when `avatarUrl` set, initial fallback otherwise, correct size in NavBar (32)
   and unchanged in other call sites.
8. **Footer**: opaque dark bg (room photo does **not** bleed through), links hover to amber,
   `Separator` divider renders, copyright centered.

## Acceptance criteria

- All five files on Inter + §6 tokens — **zero hardcoded hex / serif font-families**; no
  `useRef`+`mousedown` click-outside anywhere in this slice.
- NavBar uses `Button`/`Sheet`/`DropdownMenu`; `toggleInbox()` preserved; `NotificationBell` +
  `LanguageSwitcher` in the right slot (authenticated only).
- LanguageSwitcher uses `DropdownMenuRadioGroup`/`RadioItem` bound to `i18n.changeLanguage`.
- NotificationBell uses `Popover` + `ScrollArea`, lucide `Bell` + `Badge`, 30 s `refetchInterval`,
  mark-read/mark-all, `NewMessage → openChat(actorId)`, relative-time formatting.
- Avatar renders shadcn `Avatar`/`AvatarImage`/`AvatarFallback` internally with the **identical**
  `(displayName, avatarUrl?, size)` API and prop-driven inline sizing — all callers compile unchanged.
- Footer background is an opaque dark token; divider is a `Separator`.
- `build` + `lint` green; verified in **bg + en**; no console errors and no a11y warnings.

## Dependencies

- **Depends on** Slice 2 (primitives: `button`, `sheet`, `dropdown-menu`, `popover`, `scroll-area`,
  `badge`, `avatar`, `separator` are all present in `src/components/ui/`; lucide installed).
- **Feeds** Slice 5 (`ChatWidget` reuses the rebuilt `Avatar` — keeping the props API stable is what
  makes that slice a no-touch consumer) and lands the global look ahead of page slices 8–13.
- `NavBar` + `Footer` are mounted in the `App.tsx` shell; no routing/shell changes here.
