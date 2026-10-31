# 06 — Frontend

> Depends on: **04, 05** · Read `00-OVERVIEW.md` first.

## Goal
The `ReviewsSection` in the bottle panel (aggregate header + my-review form + review list), the ★ score
badge on bottle cards, the new notification text, i18n in **bg + en**. Speakeasy styling; module-level
`CSSProperties` constants (never created inside render).

## Build / extend
1. **Types (`src/types/index.ts`)** — no per-file types:
   - `FlavorTag` string union (the 28 enum names — enums arrive as strings via `JsonStringEnumConverter`)
   - `BottleReview`, `BottleReviewsSummary` (mirror the DTOs; camelCase)
   - `Bottle` += `averageScore?: number | null; reviewsCount: number`
2. **API layer** — new `src/api/reviewsApi.ts` (named `{ client }` import, typed returns):
   `getReviews(bottleId)`, `addReview(bottleId, payload)`, `updateReview(bottleId, reviewId, payload)`,
   `deleteReview(bottleId, reviewId)`.
3. **`ReviewsSection`** in `BottleDetailPanel.tsx` — placed **above `CommentsSection`** (ratings first,
   discussion below), following the `CommentsSection` component pattern:
   - `useQuery({ queryKey: ['reviews', bottle.id], queryFn: ... })`.
   - **Aggregate header:** large gold average (e.g. `92.4`) + `/100`, review count, top-3 flavor chips.
     No reviews → em-dash + invite text.
   - **My review (authenticated):** if `myReview == null` → form: score number input 0–100 with
     quick-pick chips (70/80/85/90/95), collapsible "tasting note" block (nose/palate/finish/summary
     textareas), flavor chip multi-select (toggle, max 5 — disable unselected chips at 5, gold when
     selected). If `myReview` exists → prefilled edit form + delete button (confirm before delete).
   - Mutations invalidate `['reviews', bottle.id]` **and** the bottles list queries (card badges must
     refresh — reuse the invalidation keys `CommentsSection`/`onDelete` already use).
   - `409` on create → surface the "already reviewed" i18n message (stale cache edge).
   - Review list: `Avatar` + display name + score badge + notes (labelled nose/palate/finish/summary,
     only non-empty ones) + flavor chips + date (+ "edited" when `updatedAt > createdAt`).
4. **Card badge** — small gold `★ 92` chip (rounded `averageScore`, hidden when `reviewsCount === 0`)
   on every bottle-card surface: `BarShelf.tsx` + the card renderers in Browse / Marketplace / PublicBar /
   Dashboard pages (overview §7 default: all).
5. **`NotificationBell.tsx`** — map the new `BottleReviewed` type: same click behavior as
   `BottleLiked`/`BottleCommented` (bottle-type notification), text from i18n.
6. **i18n (`src/i18n/bg.json` + `en.json`)**:
   - new `reviews` namespace: section title, average label, count, write/edit/delete/save/cancel,
     score label, nose/palate/finish/summary labels, flavors label + "max 5" hint, empty state,
     already-reviewed error, delete confirm.
   - new `flavors` namespace: **28 keys = enum names** (`"Peaty": "Торфен"`, `"TropicalFruit": "Тропически плодове"`, …).
   - `notifications` namespace: add the `bottleReviewed` key (e.g. bg: "оцени бутилката ви").

## Gate
`npm --prefix VirtualBar.Web run build` → clean "✓ built in ..."; exercise in **bg + en**: rate a bottle
(quick score only), edit into a full tasting note with flavors, delete; verify the aggregate header, the
card badges refresh, the owner notification, and the anonymous view (public bar — list visible, no form);
zero console errors.
