# 06 — Frontend

> Depends on: **04, 05** · Read `00-OVERVIEW.md` first.

## Goal
Earned-badge strips on the profile and the public bar, a progress view for the owner, the `BadgeEarned`
notification rendering, i18n in **bg + en**. Speakeasy styling; module-level `CSSProperties` constants.

## Build / extend
1. **Types (`src/types/index.ts`):** `BadgeType` string union (the 18 enum names), `UserBadge`
   (`badge: BadgeType; awardedAt: string`), `BadgeProgress` (`badge`, `threshold`, `current`, `earned`,
   `awardedAt?`). Notification type union += `'BadgeEarned'` (wherever notification types are typed).
2. **API — new `src/api/badgesApi.ts`** (named `{ client }` import):
   `getUserBadges(userId)`, `getMyProgress()`.
3. **`BadgeChip.tsx`** (new shared component): circular gold-bordered medallion — lucide icon (static
   `badgeType → icon` map, overview §7 default) + translated name underneath; `size` prop; a dimmed
   variant (`earned: false`) for the progress view. Static styles at module level; only the
   earned/unearned visual is computed inline.
4. **`ProfilePage.tsx`** (own profile): "Постижения" section —
   `useQuery({ queryKey: ['badges','progress'], queryFn: getMyProgress })`; earned chips first (gold),
   then unearned (dimmed) each with a thin gold progress bar `min(current, threshold)/threshold` and a
   "7/10" label. Catalog order within each group.
5. **`PublicBarPage.tsx`:** earned-only strip under the bar header —
   `useQuery({ queryKey: ['badges', userId], queryFn: ... })`; empty → render nothing (no empty-state
   noise on public pages).
6. **`NotificationBell.tsx`:** map `BadgeEarned` → text `t('notifications.badgeEarned')` + the
   **translated badge name** resolved from `resourceName` (the `BadgeType` string) via the `badges`
   namespace; click → navigate to own profile (badges section). No actor emphasis (actor == recipient).
7. **i18n (`bg.json` + `en.json`):**
   - new `badges` namespace: section titles (`title`, `progressTitle`), `earnedOn`, and **per badge**
     `<EnumName>.name` + `<EnumName>.description` (18 × 2 keys per language — e.g.
     `"Collector10": { "name": "Колекционер", "description": "10 бутилки в бара" }`).
   - `notifications` namespace: add `badgeEarned` (bg: "Спечелихте значка:").
8. **Query invalidation:** none required — badges refresh on next fetch (`staleTime` 30 s global);
   the bell already polls every 30 s and delivers the earn moment.

## Gate
`npm --prefix VirtualBar.Web run build` → clean "✓ built in ..."; exercise in **bg + en**: add a bottle
with a fresh user → `FirstBottle` appears in the bell within a poll cycle and on the profile; profile
shows 18 rows with correct progress ("7/10"); another user's public bar shows earned-only strip; empty
public strip renders nothing; zero console errors.
