# Slice 3 (remainder) — Auth pages: visual verification

> Read `00-OVERVIEW.md` first. The auth **code is already written and the build is green.**
> Only verification (and any small polish it surfaces) remains. This slice is the gate that
> confirms the reference RHF+zod+Form+i18n+token pattern renders correctly before it gets copied
> across the rest of the app.

## Context recap

In Slice 3 the four auth pages were rewritten onto shadcn. They are the **reference
implementation** every other validated form copies (§8a of the overview). What was built:

- `src/components/AuthLayout.tsx` — shared shell: centered `max-w-md`, `LanguageSwitcher` top-right,
  shadcn `Card` (roomy `--card-spacing: spacing(6)`), enlarged `CardTitle`, `CardDescription`
  subtitle, footer slot. The page background is transparent so the fixed room photo shows behind.
- `src/lib/validation.ts` — `EMAIL_REGEX`, `PASSWORD_REGEX`, `type TFn`.
- `src/pages/LoginPage.tsx`, `RegisterPage.tsx`, `ForgotPasswordPage.tsx`, `ResetPasswordPage.tsx`
  — each uses `useForm` + `zodResolver(makeSchema(t))`, shadcn `Form`/`FormField`/`FormControl`/
  `FormMessage`, the standard error box, and reuses the pre-existing i18n keys.
- A custom `--success` token was added to `index.css` (used by the forgot-password success state).

## Goal

Confirm — visually and functionally, in **both languages** — that the new amber/stone/Inter **dark**
theme renders, the client-side zod validation works and is localized, and there are no console
errors. Decide whether mira's compact `h-7` controls need a height bump on these focal pages.

## Steps

1. **Build gate (should already pass):** `npm --prefix VirtualBar.Web run build` and
   `npm --prefix VirtualBar.Web run lint`.
2. **Run the dev server:** `npm run dev` in `VirtualBar.Web` → http://localhost:5173. (No backend
   needed — auth pages render and validate client-side; only a *successful* submit calls the API.)
3. **Use the `e2e-tester` agent** (Playwright is installed; `npx playwright install chromium` if
   needed) to screenshot at 1280×800, in **Bulgarian (default)**:
   - `/login`, `/register`, `/forgot-password`, `/reset-password?email=test@example.com&token=abc123`
4. **Validation checks (client-side, no backend):** on `/login`, submit an invalid email
   (`notanemail`) + short password → per-field zod messages appear under the fields; then valid
   email + empty password → "required" messages. Confirm messages are the localized i18n strings.
5. **Language toggle:** switch БГ→EN via the top-right `LanguageSwitcher`, confirm labels,
   placeholders, and validation messages all change; switch back.
6. **Console:** capture and report any errors/warnings (React errors, missing modules, etc.).

## Acceptance criteria

- Build + lint green.
- Dark theme active (dark stone surfaces, **not** a light page); **Inter** (sans, not serif); amber
  primary submit button; the `Card` clearly sits above the room-photo background.
- zod validation renders inline, correctly, and re-localizes on language switch.
- No console errors.
- A deliberate decision recorded on **control sizing**: mira defaults are compact (`h-7` ≈ 28px,
  `Button size="lg"` ≈ h-8). If the auth inputs/buttons look too small for a focal page, bump them
  locally (e.g. add `h-9`/`h-10` to the `Input`s and the submit `Button` via className, or introduce
  a shared `className` in `AuthLayout`/the pages). Keep the change token-faithful (no hardcoded colors).

## Likely follow-ups (only if verification surfaces them)

- If controls feel cramped: add `className="h-9"` to auth `Input`s and `className="h-10 w-full"` to
  submit `Button`s (overriding mira's compactness for the auth surface only).
- If the `Card` title/spacing looks off against the room photo: tune `AuthLayout` (e.g. add a subtle
  `bg-card/95 backdrop-blur` to the Card so text stays legible over the photo).
- If the success/error boxes need more presence, add a lucide icon (`CircleCheck`, `OctagonX`).

## Dependencies

None. This closes Slice 3. Proceed to `04-shared-chrome.md` next (it touches every page, so it
should land before the page slices).
