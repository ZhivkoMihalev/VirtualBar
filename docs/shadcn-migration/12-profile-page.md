# Slice 12 — ProfilePage

> Read `00-OVERVIEW.md` first. This slice migrates the **profile editor** (avatar upload + display
> name / bio / country / city) onto shadcn `Input` / `Textarea` / `Label` / `Button`, and replaces
> the inline success/error banners with **Sonner toasts**. Per §3 these are mostly non-validated
> fields, so this is a *plain-inputs* migration — **not** an RHF+zod form.

## Context recap

`ProfilePage.tsx` (route `/profile`, reached from the NavBar) lets the signed-in user edit their own
profile. It is a simple `useState` form with two mutations (save profile, upload avatar), an instant
avatar preview, and three inline status messages (avatar-error, save-success, save-error). The only
real validation is "display name is required". Today it is fully inline-styled (Cinzel labels,
`#0A0502` inputs, gold-gradient submit, manual focus-border handlers). Per §3, trivial/low-validation
forms stay **plain shadcn inputs** (RHF+zod is reserved for auth / AddBottle / MakeOffer / WishList-add);
per §6 the success/error tokens already exist, but per the slice brief the messages become **toasts**.

## Goal

Re-skin ProfilePage to the amber/stone/Inter dark theme using shadcn form controls, keep the existing
`useState` form + light "display name required" guard, preserve the avatar upload + preview and the
`AuthContext.updateUser` sync, and route all feedback through Sonner (`toast.success` / `toast.error`)
instead of inline `<div>`s. No new i18n keys.

## Files to touch

- `src/pages/ProfilePage.tsx` — full re-skin (inputs → shadcn; inline messages → toasts; tokenize styles).
- No changes to `src/api/usersApi.ts`, `src/contexts/AuthContext.tsx`, `src/types/index.ts`, or i18n JSON.
- `<Toaster />` is **already mounted in `App.tsx`** (Slice 2, §5/§7) — do **not** add another.

## Current state (what `ProfilePage.tsx` does now)

- **State:** `displayName`, `bio`, `country`, `city` (seeded from `user?.*`), `avatarUrl`
  (seeded from `user?.avatarUrl`), and `saved` (boolean success flag).
- **`avatarMutation`** — `uploadAvatar(file)` → `onSuccess(data)`: `setAvatarUrl(data.avatarUrl)` +
  `updateUser({ avatarUrl })`. Error surfaced via an inline `<div>` (`profile.avatarError`).
- **`saveMutation`** — `updateProfile({ displayName, bio, country, city })` (each `.trim()` /
  `|| undefined`) → `onSuccess(data)`: `updateUser({...})`, `invalidateQueries(['profile', user.id])`,
  `setSaved(true)`. Error via inline `<div>` (`profile.errorMessage`); success via inline `<div>`
  (`profile.successMessage`) gated on `saved && !saveMutation.isError`.
- **`handleAvatarChange(file)`** — guards `!file`, `setSaved(false)`, `avatarMutation.mutate(file)`.
- **`handleSubmit(e)`** — `preventDefault`, **`if (!displayName.trim()) return`**, `setSaved(false)`,
  `saveMutation.mutate()`.
- **Inline constants / helpers:** `inputStyle`, `labelStyle`, `focusOn`/`focusOff` (manual focus
  border). All deleted.
- **Markup:** `NavBar`; header (small `profile.editTitle` label + `profile.editTitle` `<h1>` — the
  label is a duplicate of the title); a hand-drawn `borderTop` divider; an avatar row (`Avatar size={80}`
  + hidden `#avatar-input` file input triggered via `document.getElementById('avatar-input')?.click()`
  + a custom upload `<button>`); then the `<form>` with display-name / bio / country / city; the three
  status `<div>`s; and a gold-gradient submit `<button>` (`disabled` when pending or empty display name).

## Transformation plan

### 1. Form approach — keep `useState`, use shadcn inputs (DECISION, per §3 / §8a)

**Two options were considered:**

- **Option A — plain shadcn inputs over the existing `useState` form (CHOSEN).** Swap the raw
  `<input>`/`<textarea>`/`<label>` for `Input`/`Textarea`/`Label`, delete `focusOn`/`focusOff`
  (shadcn renders the focus ring), and keep `handleSubmit` with its `if (!displayName.trim()) return`
  guard plus the disabled-submit. Minimal, matches §3's rule that "trivial single-field inputs stay
  plain shadcn `Input`" — profile is **not** in the validated-forms list (auth / AddBottle /
  MakeOffer / WishList-add).
- **Option B — RHF + zod via shadcn `Form` (REJECTED).** A 1-rule schema (`displayName: z.string().
  min(1, t('...'))`) over `FormField`/`FormMessage`, mirroring the auth pages (§8a). Correct machinery
  but **overkill** for one trivial required field with three free-text optionals; it would also force a
  new "display name required" i18n key (none exists today). Rejected to honour §3.

**→ Implement Option A.** This keeps the diff small and the page's behaviour identical.

### 2. Field → shadcn map (§6 / §7)

| Field | Old | New |
|---|---|---|
| section divider | `borderTop` `<div>` | `<Separator className="my-8" />` (§7) |
| label (all) | `labelStyle` (Cinzel gold uppercase) | `<Label htmlFor="…">` (optionally `className="text-xs uppercase tracking-wide text-muted-foreground"`) |
| avatar upload button | custom gold-outline `<button>` | `<Button type="button" variant="outline" size="sm">` (optional lucide `<ImagePlus />`) |
| display name | `<input required>` (`inputStyle`) | `<Input id="displayName" required>` |
| bio | `<textarea rows={4}>` | `<Textarea id="bio" rows={4}>` |
| country | `<input>` | `<Input id="country">` |
| city | `<input>` | `<Input id="city">` |
| submit | gold-gradient `<button>` | `<Button type="submit" size="lg" className="w-full" disabled={saveMutation.isPending || !displayName.trim()}>` |

Each control keeps its existing `value` / `onChange={(e) => setX(e.target.value)}`. **Drop the
`setSaved(false)` calls** inside every `onChange` — `saved` state is removed in §3 below. Pair each
`<Label htmlFor>` with the matching `<Input id>`/`<Textarea id>` for the a11y win mira gives for free.
Header eyebrow/title and page wrapper tokenize like OffersPage (`text-foreground`,
`text-xs font-medium uppercase tracking-widest text-muted-foreground`,
`font-heading text-2xl font-semibold text-foreground`). Consider dropping the duplicate eyebrow label
since it repeats the `<h1>` text — optional cleanup.

### 3. Inline messages → Sonner toasts (§7, sonner already wired)

Delete the `saved` state, every `setSaved(...)` call, and all three status `<div>`s. Add
`import { toast } from 'sonner'` and fire toasts from the mutation callbacks:

```ts
import { toast } from 'sonner'

const avatarMutation = useMutation({
  mutationFn: (file: File) => uploadAvatar(file),
  onSuccess: (data: UpdatedProfile) => {
    setAvatarUrl(data.avatarUrl)
    updateUser({ avatarUrl: data.avatarUrl })
  },
  onError: () => toast.error(t('profile.avatarError')),
})

const saveMutation = useMutation({
  mutationFn: () => updateProfile({
    displayName: displayName.trim(),
    bio: bio.trim() || undefined,
    country: country.trim() || undefined,
    city: city.trim() || undefined,
  }),
  onSuccess: (data: UpdatedProfile) => {
    updateUser({
      displayName: data.displayName, bio: data.bio,
      country: data.country, city: data.city, avatarUrl: data.avatarUrl,
    })
    if (user?.id) queryClient.invalidateQueries({ queryKey: ['profile', user.id] })
    toast.success(t('profile.successMessage'))
  },
  onError: () => toast.error(t('profile.errorMessage')),
})
```

`handleSubmit` simplifies to: `e.preventDefault(); if (!displayName.trim()) return;
saveMutation.mutate()`. `handleAvatarChange` simplifies to: `if (!file) return;
avatarMutation.mutate(file)`. The avatar **success** stays silent (preview + NavBar avatar update is
the feedback; there is no `profile.avatarSuccess` key — do not invent one); only avatar **error**
toasts. `updateUser` (AuthContext) is untouched — it patches `user` + `localStorage`, so the NavBar
avatar/name refresh instantly.

### 4. Avatar upload + preview (preserve behaviour)

Keep `Avatar` (`displayName`, `avatarUrl ?? undefined`, `size={80}`) — `Avatar` is migrated in Slice 4;
here we only **consume** it. Keep the hidden `<input type="file" accept="image/*">` and the trigger
button. Prefer a `useRef<HTMLInputElement>(null)` + `inputRef.current?.click()` over
`document.getElementById('avatar-input')` (cleaner, avoids a global id), but either works. Local
`avatarUrl` state stays — it drives the instant preview before the context round-trips.

## i18n keys to preserve (profile namespace)

Every key already exists in `bg.json`/`en.json` — **do not add or rename any.** Used by this page:

- `profile.editTitle` (eyebrow label **and** `<h1>`)
- `profile.avatar` (avatar field label)
- `profile.uploadAvatar` (upload button)
- `profile.avatarError` → `toast.error` on avatar upload failure
- `profile.displayName`, `profile.bio`, `profile.country`, `profile.city` (field labels)
- `profile.successMessage` → `toast.success` on save
- `profile.errorMessage` → `toast.error` on save failure
- `profile.saving` / `profile.save` (submit button label, `saveMutation.isPending ? saving : save`)

**Present in the namespace but NOT used here (leave untouched):** `profile.cancel`,
`profile.editProfile` (a duplicate of `editTitle`).

## Slice-specific gotchas

- **One Toaster only.** It is already in `App.tsx` (§5/§7); `sonner.tsx` has `theme="dark"` hardcoded
  (§9). Just `import { toast } from 'sonner'` and fire — never mount a second `<Toaster />`.
- **Keep the light guard.** Display name is required: keep `if (!displayName.trim()) return` and the
  `disabled={… || !displayName.trim()}` on submit. Do **not** add zod (Option B was rejected).
- **Remove `saved` entirely.** Verify no lingering reference after deleting the success `<div>` and the
  `onChange` `setSaved(false)` calls, or TS `noUnusedLocals` (§9) will fail the build.
- **`Avatar` is Slice 4's component.** Don't restyle its internals here; just pass props. Its
  `size`-from-prop inline style is a deliberate §8b "stays inline" case.
- **`UpdatedProfile.avatarUrl` is optional.** `setAvatarUrl(data.avatarUrl)` may set `undefined`;
  `Avatar` accepts `avatarUrl?: string`, so pass `avatarUrl ?? undefined` as today.
- **mira is compact (§4).** This is a focal single-column editor; if `h-7` inputs feel cramp,
  bump with `className="h-9"` (same call the auth pages may make — see `03-auth-pages-remaining.md`).
  Keep it token-faithful (no hardcoded hex).
- **Backend required for verification.** Save and avatar upload hit the API (`PUT /users/me`,
  `POST /users/me/avatar`), so run `dotnet run` for this page.

## Verification (§10)

1. `npm --prefix VirtualBar.Web run build` (green) and `npm --prefix VirtualBar.Web run lint` (clean).
2. `npm run dev` + backend (`dotnet run` in `VirtualBar.Api`); sign in; open `/profile`.
3. **bg (default):** edit **ИМЕ / БИОГРАФИЯ / ДЪРЖАВА / ГРАД**, click **ЗАПАЗИ** → green success toast
   **"Профилът е обновен."**; NavBar name updates. Upload an avatar → preview swaps immediately and the
   NavBar avatar updates (`updateUser`). Clear **ИМЕ** → submit is disabled (light guard). Stop the API
   and save → red error toast **"Грешка при запазване. Опитайте отново."**; force an avatar upload
   failure → **"Грешка при качване на снимката."**
4. **en:** switch БГ→EN via `LanguageSwitcher`; labels, button (`Save`/`Saving…`), and all toasts
   re-localize ("Profile updated." / "Could not save changes…" / "Could not upload photo.").
5. **a11y:** each `Label htmlFor` focuses its `Input`; focus ring visible (mira); toasts are announced
   and auto-dismiss. No console errors.

## Acceptance criteria (§10)

- Build + lint green; dark amber/stone theme; **Inter** (no serif) on the page.
- Display name / bio / country / city use shadcn `Input`/`Textarea` + `Label`; submit + upload use
  `Button`; divider uses `Separator`.
- **Form stays `useState`** (Option A) with the display-name-required guard intact — no RHF/zod added.
- All three inline status `<div>`s are gone; feedback is **Sonner toasts** (`profile.successMessage`,
  `profile.errorMessage`, `profile.avatarError`); `saved` state removed.
- Avatar upload + instant preview + `AuthContext.updateUser` sync preserved; `['profile', user.id]`
  invalidation preserved.
- All listed `profile.*` keys preserved; **no new keys**; verified in **bg + en**.
- All hardcoded hex gone from `ProfilePage.tsx` (only §6 tokens / component variants remain).

## Dependencies

Depends on **Slice 4** (shared chrome — `NavBar`, and `Avatar`, which this page consumes) and on
**Slice 2** (primitives: `Input`, `Textarea`, `Label`, `Button`, `Separator`, and the `sonner`
`Toaster`). Independent of the Offers/Marketplace slices. After this, `13-browse-publicbar.md` is next
by ID order; the final `14-cleanup.md` removes the serif `<link>` once every page is migrated.
