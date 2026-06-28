# Slice 5 — ChatWidget internals (keep the floating shell)

> Read `00-OVERVIEW.md` first (decisions §3, tokens §6, component APIs §7, patterns §8, gotchas §9,
> verification §10). This slice re-skins the **internals** of the floating chat widget onto shadcn
> primitives **without** turning it into a `Dialog`. Depends on Slice 4 (the migrated shared `Avatar`).

## Context recap

The widget is a Facebook-Messenger-style **floating, non-modal, persistent** chat pinned to the
bottom-right of every authenticated page (mounted once in `App.tsx`, inside `ChatProvider`). Its
defining UX property: **it never blocks the page** — you can keep scrolling and clicking the site
behind it while it is open. That is achieved by a `pointer-events` trick, NOT by a modal overlay.

**CRITICAL (locked, see overview §8c):** do **NOT** convert the widget to `Dialog`/`Sheet`. A Radix
modal would add a focus-trap, an ESC-to-close that fights the FAB toggle, and — fatally — it sets
`overflow:hidden` + `data-scroll-locked` on `<body>` and an inert backdrop, which would **break the
persistent, non-blocking UX**. Keep the bespoke fixed shell and the `pointerEvents:none/auto` trick.
Per the overview's note, Radix popovers/alert-dialogs (e.g. `NotificationBell`'s Popover, the
`BottleDetailPanel`/`MakeOffer` Dialogs) **portal to `document.body`** — *outside* this widget's
`pointer-events:none` container — so they are completely unaffected and stay clickable when opened
while the chat is open. Only the widget's own internal painting changes here.

## Goal

Rebuild every internal surface (toggle FAB, inbox list, conversation rows, message bubbles, draft
input, close buttons, loading/empty/error states) with shadcn `Button`/`Badge`/`ScrollArea`/`Textarea`
/`Avatar` and §6 tokens, replacing all hardcoded speakeasy hex and the inline SVG/`×` glyph with
lucide icons — while preserving the shell, the polling, auto-mark-read, Enter-to-send, and every
`ChatContext`/`messagesApi` hook byte-for-byte.

## Files to touch

- `src/components/ChatWidget.tsx` — the only file rewritten (shell kept, internals re-skinned).
- (read-only) `src/components/Avatar.tsx` — already migrated in Slice 4; reuse its `size`-prop wrapper.
- No changes to `src/contexts/ChatContext.tsx`, `src/api/messagesApi.ts`, `src/types`, or i18n files.

## Current state — what `ChatWidget.tsx` does now

- **Shell (`containerStyle`, lines 11-20 + default export 499-538):** a single `position:fixed;
  bottom:0; right:20; zIndex:1000` flex row, `align-items:flex-end; gap:12`, **`pointerEvents:'none'`**.
  Children are laid out right-to-left because flex order is `ChatWindow` (leftmost, only when
  `inboxOpen && activeUserId`) → `InboxPanel` (middle, only when `inboxOpen`) → toggle FAB (rightmost,
  always). **The pointer-events trick:** the container ignores the pointer so clicks/scroll fall
  *through* its empty area to the page beneath; each of the three children re-enables
  `pointerEvents:'auto'` (lines 23, 59, 73) so only the actual widgets are interactive. `if
  (!isAuthenticated) return null` (512) — renders for authed users only.
- **Toggle FAB (`toggleButtonStyle` 22-37, `toggleBadgeStyle` 39-56):** 52px round button,
  `linear-gradient(135deg,#C9A84C,#E8C870)` gold gradient on `#07030A`, `boxShadow 0 6px 24px`, an
  inline 24px **SVG** message-circle icon (529-531), and an absolutely-positioned red `#D42020`
  unread badge showing `totalUnread > 99 ? '99+' : totalUnread`. `onClick={toggleInbox}`.
- **`InboxPanel` (327-376):** 300×480 panel, `bg rgba(7,3,10,0.97)`, `border 1px rgba(201,168,76,0.25)`,
  `borderRadius 8px 8px 0 0`, `borderBottom:none`. Header (`panelHeaderStyle`) with Cinzel gold title
  `t('messages.title')` + a `×` close button. Body is a `flex:1; overflow-y:auto` list rendering
  loading/error/empty states and a `ConversationCard` per conversation. Owns its own
  `useQuery(['inbox'])` with **`staleTime:30_000, refetchInterval:30_000`**.
- **`ConversationCard` (212-302):** a `<button>` row — `Avatar size={40}` + name (Cormorant `#E8C870`,
  truncated) + `formatTime` timestamp + last-message preview (slice-to-40, with `you:` prefix when
  `lastMessageIsFromMe`) + a gold-gradient round unread `Badge`. Selected state = `rgba(201,168,76,0.08)`
  bg + 3px `#C9A84C` left border.
- **`ChatWindow` (378-497):** 320×480 panel (same chrome as inbox). Header = Playfair gold
  `headerName` (resolved from cached inbox, else from thread) + `×` close. Message area is `flex:1;
  overflow-y:auto` with a `scrollRef` that force-scrolls to bottom on every `thread` change. Footer
  is a `<textarea rows={1}>` + a gradient send `<button>`. `useQuery(['conversation', userId])`;
  `sendMutation` invalidates `['inbox']` + `['conversation',userId]`; **auto mark-as-read**: a
  `useMemo` collects `unreadIds` (`!isRead && senderId !== currentUserId`) and a `useEffect` fires
  `markRead` for each (mutation invalidates `['inbox']`). **Enter sends, Shift+Enter = newline**
  (onKeyDown 473-478). `handleSend` trims, no-ops while pending, clears draft.
- **`MessageBubble` (304-325):** left/right aligned; mine = `rgba(201,168,76,0.15)` bubble, theirs =
  `rgba(255,255,255,0.05)`; Cormorant `#F0DDB4` body + Cinzel `#806840` time, `whitespace:pre-wrap`.
- **Top-level inbox query (504-510):** a second `useQuery(['inbox'])` (30 s poll, `enabled:
  isAuthenticated`) feeding `totalUnread` for the FAB badge; dedupes with the panel's query by key.
- **`ChatContext` used (502):** `inboxOpen, toggleInbox, closeInbox, activeUserId, openChat, closeChat`.
  **`messagesApi` used:** `getInbox, getConversation, sendMessage, markRead`. **`formatTime` (197-210)**
  is a pure helper — keep verbatim.

## Transformation plan

**Keep exactly as-is:** the fixed shell + pointer-events trick, child render order, `if
(!isAuthenticated) return null`, both `['inbox']` polling queries (30 s), `['conversation',userId]`
query, `sendMutation`/`markReadMutation` + their invalidations, the `unreadIds` memo + auto-mark-read
effect, Enter/Shift+Enter handling, `handleSend`, `headerName` resolution, `formatTime`, and all
`useChat()`/`useAuth()` calls.

**Shell:** keep `containerStyle` as-is, **or** express it as utilities — both are acceptable provided
behavior is identical: `className="pointer-events-none fixed bottom-0 right-5 z-[1000] flex items-end
gap-3"` (`right-5`=20px, `gap-3`=12px, `z-[1000]`). The three children **must** keep
`pointer-events-auto`. The `pointer-events-none`-on-container / `-auto`-on-children pairing is
load-bearing — never drop it.

**Inline-constant → token map (§6):**

| Inline constant (line) | shadcn / token replacement |
|---|---|
| `containerStyle` (11) | keep, or `pointer-events-none fixed bottom-0 right-5 z-[1000] flex items-end gap-3` |
| `toggleButtonStyle` gold gradient + `#07030A` (22) | `<Button size="icon" className="pointer-events-auto relative mb-5 size-[52px] rounded-full bg-primary text-primary-foreground shadow-2xl hover:bg-primary/90">` (gradient → **`bg-primary`** per the brief) |
| toggle SVG icon (529-531) | lucide `inboxOpen ? <X className="size-6"/> : <MessageCircle className="size-6"/>` |
| `toggleBadgeStyle` `#D42020`/`#FFF` (39) | `<Badge variant="destructive" className="pointer-events-none absolute -top-1 -right-1 min-w-5 justify-center rounded-full px-1 text-[10px]">` |
| `panelStyle` / `chatWindowStyle` `rgba(7,3,10,0.97)` (58/72) | `pointer-events-auto flex h-[480px] flex-col overflow-hidden rounded-t-lg border border-b-0 border-border bg-popover shadow-2xl` + width `w-[300px]` (inbox) / `w-80` (window, 320px) |
| `panelHeaderStyle` (86) | `flex shrink-0 items-center justify-between border-b border-border px-4 py-3.5` |
| `panelTitleStyle` Cinzel `#E8C870` (95) | `text-sm font-medium tracking-wide text-primary` |
| `chatNameStyle` Playfair `#E8C870` (102) | `truncate font-heading text-base font-medium text-foreground` |
| `closeButtonStyle` `×` glyph `#C9A84C` (111) | `<Button variant="ghost" size="icon-xs">` + lucide `<X className="size-4"/>`; keep `aria-label={t('messages.title')}` |
| `listStyle` flex/scroll (123) | `<ScrollArea className="flex-1">` |
| `stateStyle` `#B09868` (128) | `px-4 py-10 text-center text-sm italic text-muted-foreground` |
| `loadingStyle` `#C9A84C` (137) | `px-4 py-10 text-center text-xs tracking-wide text-primary` (a `Skeleton` list is optional) |
| `errorStyle` `#C04040` (146) | `px-4 py-10 text-center text-sm italic text-destructive` |
| `messageAreaStyle` (155) | `<ScrollArea className="flex-1">` wrapping `flex flex-col gap-2.5 p-4` |
| `inputAreaStyle` (164) | `flex shrink-0 gap-2 border-t border-border px-3.5 py-3` |
| `textareaStyle` faint-white/`#F0DDB4` (172) | `<Textarea rows={1} className="min-h-9 max-h-24 flex-1 resize-none" />` (uses built-in `bg-input`/`border-input`/focus ring) |
| `sendButtonStyle` gold gradient/`#07030A` (185) | `<Button size="sm" className="shrink-0">` (default = amber); keep `disabled={!draft.trim() || sendMutation.isPending}` |

**`ConversationCard`:** keep the `<button>` + `onSelect`; class it
`cn('flex w-full cursor-pointer items-center gap-3 border-b border-l-[3px] border-border/50 px-3.5 py-3 text-left', selected ? 'border-l-primary bg-accent' : 'border-l-transparent hover:bg-accent/50')`.
Inside: keep `<Avatar displayName={…} avatarUrl={…} size={40} />` (the Slice-4 wrapper that internally
renders shadcn `Avatar`/`AvatarImage`/`AvatarFallback` while keeping the `size` prop inline per §8b —
do **not** re-implement). Name → `truncate text-sm font-medium text-foreground`; timestamp →
`shrink-0 text-[10px] text-muted-foreground`; preview → `truncate text-xs text-muted-foreground`
(keep the `you:`-prefix + 40-char slice logic); unread → `<Badge className="min-w-5 shrink-0
justify-center rounded-full px-1.5">{conversation.unreadCount}</Badge>`.

**`MessageBubble`:** outer `cn('flex', mine ? 'justify-end' : 'justify-start')`; inner
`cn('max-w-[75%] rounded-lg border px-3 py-2', mine ? 'border-primary/30 bg-primary/15' :
'border-border bg-muted')`; body `whitespace-pre-wrap break-words text-sm leading-snug
text-foreground`; time `mt-1 text-right text-[10px] text-muted-foreground`.

**Imports to add:** `import { Button } from '@/components/ui/button'`, `Badge`, `Textarea`,
`{ ScrollArea } from '@/components/ui/scroll-area'`, and `{ MessageCircle, X } from 'lucide-react'`.
Remove the now-unused `CSSProperties` import and every module-level `*Style` constant.

## i18n keys to preserve (all `t('…')` calls — verbatim, no new keys)

| Key | bg / en | Used in |
|---|---|---|
| `messages.title` | `СЪОБЩЕНИЯ` / `MESSAGES` | inbox header title + all 3 close/FAB `aria-label`s |
| `messages.loading` | `ЗАРЕЖДАНЕ…` / `LOADING…` | inbox + thread loading state |
| `messages.error` | `Грешка при зареждане…` / `Could not load messages.` | inbox + thread error state |
| `messages.noConversations` | `Нямате разговори все още.` / `No conversations yet.` | empty inbox |
| `messages.you` | `Вие` / `You` | `ConversationCard` preview prefix |
| `messages.inputPlaceholder` | `Напишете съобщение…` / `Write a message…` | draft `Textarea` placeholder |
| `messages.sending` | `Изпращане…` / `Sending…` | send button (pending) |
| `messages.send` | `ИЗПРАТИ` / `SEND` | send button (idle) |

(`messages.selectConversation` / `messages.sendMessage` are NOT used here — do not introduce them.)

## Slice-specific gotchas

- **No Dialog/Sheet — ever.** They portal a scroll-locking modal; that is the exact UX this widget
  must avoid. The shell stays a plain `pointer-events` container.
- **ScrollArea auto-scroll-to-bottom:** the old `scrollRef.current.scrollTop = scrollHeight` targets
  a native `overflow:auto` div; shadcn `ScrollArea`'s scrollable node is the inner
  `[data-slot="scroll-area-viewport"]`, not the root. Replace the ref approach with a sentinel: render
  `<div ref={endRef} />` as the last child of the message list and, in the existing `useEffect([thread])`,
  call `endRef.current?.scrollIntoView({ block: 'end' })`. (Alternatively `ref` the viewport via
  `[data-slot="scroll-area-viewport"]`, but the sentinel is simpler and survives ScrollArea internals.)
- **Textarea min-height:** shadcn `Textarea` defaults taller than the old `rows={1}` bar; pin it with
  `min-h-9 max-h-24 resize-none` so the single-line input area keeps its compact height and grows only
  modestly. `rows={1}` stays.
- **FAB shape:** `Button size="icon"` is a compact square (mira `h-7`); override with
  `size-[52px] rounded-full` to keep the 52px circle. The badge keeps `pointer-events-none` so it
  never eats the toggle click.
- **Two `['inbox']` queries** (widget badge + panel) are intentional and dedupe by key — keep both at
  30 s; do not consolidate.
- **Avatar dependency:** this slice assumes Slice 4 already migrated `Avatar.tsx`. If Slice 4 is not
  yet merged, the old size-prop `Avatar` still renders fine — just don't fork it here.

## Verification (§10)

1. **Build gate:** `npm --prefix VirtualBar.Web run build` (green) and `… run lint` (clean).
2. **Dev server + backend:** `npm run dev` (5173) and `dotnet run` in `VirtualBar.Api` (5000) — chat
   needs live data (inbox, threads, send). Use the **`e2e-tester`** agent for screenshots/flows.
3. **Non-blocking UX — the load-bearing check (do in BOTH bg + en):**
   - Open the FAB → inbox panel appears; open a conversation → chat window appears to its left.
   - **Page still scrolls while open:** mouse-wheel over the page area *behind/around* the widget —
     the underlying page scrolls (it would be frozen if this were a Dialog).
   - **Page still clickable while open:** click a NavBar item / a bottle card behind the widget — it
     responds. The empty gaps of the widget container do not intercept clicks (pointer-events:none).
   - **Assert NO scroll-lock:** `document.body` has **no** `overflow:hidden` and **no**
     `data-scroll-locked` attribute, and there is **no** Radix backdrop/`[data-radix-...-overlay]` in
     the DOM. (Their presence = an accidental Dialog conversion → fail.)
   - **Radix portals unaffected:** open `NotificationBell` (Popover) while the chat is open — it
     renders and is clickable (it portals to `document.body`, outside the pointer-events container).
4. **Functionality:** Enter sends; Shift+Enter inserts a newline; send button disabled while empty/
   pending and shows `messages.sending`; opening a thread auto-marks unread messages read (FAB +
   inbox unread counts drop after the 30 s poll / invalidation); list auto-scrolls to the newest
   message; close buttons (`X`) close window then inbox.
5. **Theme:** amber `bg-primary` FAB + Inter (no serif), `bg-popover` panels over the room photo,
   destructive unread badge, mine/theirs bubbles legible — verified identically in bg and en.

## Acceptance criteria

- Build + lint green; no console errors in either language.
- The custom fixed shell and the `pointer-events:none/auto` trick are intact; **no** `Dialog`/`Sheet`,
  **no** body scroll-lock, **no** focus-trap — the page scrolls and stays clickable while the widget
  is open (verified bg + en).
- All inline `*Style` constants and the hardcoded hex are gone, replaced by §6 tokens; the inline SVG
  and `×` glyphs are replaced by lucide `MessageCircle`/`X`; FAB uses `bg-primary`.
- Toggle FAB, inbox `ScrollArea` + `ConversationCard` rows (shadcn `Avatar` + unread `Badge`), chat
  `ScrollArea` + `MessageBubble`, and `Textarea` + send `Button` all render on the new amber/stone/
  Inter dark theme.
- Polling (30 s), auto-mark-as-read on thread view, Enter-to-send/Shift+Enter, and every
  `ChatContext` (`inboxOpen`, `activeUserId`, `openChat`, `closeChat`, `toggleInbox`, `closeInbox`)
  and `messagesApi` (`getInbox`, `getConversation`, `sendMessage`, `markRead`) call are preserved.
- All 8 `messages.*` i18n keys still resolve in both languages; no keys added or removed.

## Dependencies

- **Requires Slice 4** (`04-shared-chrome.md`) for the migrated shared `Avatar` wrapper used by
  `ConversationCard`. Uses primitives from Slice 2 (`Button`, `Badge`, `Textarea`, `ScrollArea`,
  `Avatar`). No backend or i18n-file changes. Independent of Slices 6–13.
