---
paths:
  - "VirtualBar.Web/**"
  - "VirtualBar.Web.Tests/unit/**"
  - "package.json"
  - "VirtualBar.Web.Tests/package.json"
---

# Frontend conventions

## Auth
- JWT stored in `localStorage` (`vbar_token` + `vbar_user`). `useAuth()` exposes `user`, `login`, `logout`.
- The Axios `client` in `src/api/client.ts` attaches the token as a Bearer header on every request and redirects to
  `/login` on 401.
- The `user` object includes `isAdmin: boolean` — set from the JWT claim emitted by `AuthService`.

## API layer, data fetching and types
- **API layer (`src/api/`):** one file per domain (e.g., `bottlesApi.ts`, `usersApi.ts`, `notificationsApi.ts`).
  - All use named import `{ client }` from `./client`.
  - Return typed data directly (axios `.data` unwrapping).
- **Data fetching:** TanStack Query (`useQuery` / `useMutation`). `queryClient.invalidateQueries` after mutations.
  Global `staleTime: 30_000`.
- **Types:** All shared TypeScript types in `src/types/index.ts`. No per-file type definitions.

## Contexts
- Each provider file exports only its component; the context object and hook live in a separate `.ts` file (Vite fast
  refresh needs component-only files — ESLint `react-refresh/only-export-components`).
- Value types (`AuthContextValue`, `ChatContextValue`) are in `src/types/index.ts`.
- `src/contexts/AuthContext.tsx` (`AuthProvider`) + `src/contexts/useAuth.ts` (`useAuth()`) — `user`, `token`,
  `isAuthenticated`, `login`, `register`, `logout`, `updateUser`.
  - The saved session is read from `localStorage` while the state is created, so the first render already knows who
    is logged in and there is no app-wide loading state.
  - `login`/`register` must not add one: swapping the routes for a spinner unmounts the form and loses the error a
    failed attempt sets on it (the E2E "Login Validation" tests pin this).
  - The forms show their own pending state through `useMutation`.
- `src/contexts/ChatContext.tsx` (`ChatProvider`) + `src/contexts/useChat.ts` (`useChat()`) — `inboxOpen`,
  `openInbox`, `closeInbox`, `toggleInbox`, `activeUserId`, `openChat(userId)`, `closeChat`.
  - Used by NavBar, NotificationBell, MarketplacePage, PublicBarPage, ChatWidget.
- The same rule applies to any component file: shared constants go to their own module (e.g. the bottle colors
  `CATEGORY_COLORS` / `LIQUID_LEVEL` / `CAP_COLOR` in `src/lib/bottlePalette.ts`).
- Generated shadcn files in `src/components/ui/` are exempt in `eslint.config.js`.

## Internationalisation
- `react-i18next` + `i18next-browser-languagedetector`. Bulgarian is the default language (`fallbackLng: 'bg'`).
  English is optional.
- The choice made in the `LanguageSwitcher` is saved in `localStorage` under `vbar_lang` and wins on every later load.
- Detection reads only that key (never the browser language), so a first visit is always Bulgarian, and
  `supportedLngs` sends an unknown saved value to Bulgarian too.
- Do not add `lng` to the init options: it overrides detection, which is how the choice used to be forgotten on reload
  (#34).
- Translation files: `src/i18n/bg.json` and `src/i18n/en.json`. i18next is initialised in `src/i18n/index.ts` and
  imported as the first line of `src/main.tsx`.
- Every page and component uses `const { t } = useTranslation()`.
- The `LanguageSwitcher` component (`src/components/LanguageSwitcher.tsx`) renders a speakeasy-styled dropdown (БГ/EN)
  placed inside the NavBar of every page.

Current translation namespaces:
- `nav`, `lang`, `login`, `register`, `dashboard`, `addBottle`, `browse`, `marketplace`, `publicBar`, `bottle`,
  `messages`, `profile`, `footer`, `hero`, `home`, `barShelf`, `notifications`, `wishList`, `distillerySelect`,
  `offers`, `collectionValue`, `reviews`
- `flavors`: 28 keys = `FlavorTag` enum member names
- `badges`: `title`/`progressTitle`/`earnedOn` + 19 × `<EnumName>.{name,description}`, keys = `BadgeType` enum member
  names
- `authErrors`: 4 keys = `AuthErrorReasons` values; login/register show these instead of the API's English `error` text
- `confirmEmail`: the "check your email" screen, the `/confirm-email` page and the "send a new link" button
- `catalog`: the `/catalog` page; `owners`/`forSale`/`reviews` are `_one`/`_other` plurals
- `product`: the `/product/:id` page
- `products`: the product picker `ProductSelect` and product links
- `productRequests`: the admin queue at `/admin/product-requests` and a collector's own requests on the profile
- `forgotPassword`, `resetPassword`: the password reset pages

## App shell and visual theme
- **App shell (`src/App.tsx`):** fixed background image (`/public/bg-room.png`) + darkening overlay, both
  `position: fixed; z-index: -1/-2`; content wrapper wrapped in `ChatProvider`; `<ChatWidget />` before `<Footer />`.
- **Visual theme:** Tailwind CSS 4 + inline styles for the speakeasy aesthetic.
  - Background: `#07030A` / `stone-950` / `stone-900`
  - Accent gold: `#C9A84C` / `#E8C870`
  - Text: `stone-100` / `stone-300`
  - Cards/surfaces: dark semi-transparent with amber borders
  - Fonts: Playfair Display (headings), Cormorant Garamond, Cinzel (labels/nav)
- **Two generations coexist:** newer files use the shadcn/ui components in `src/components/ui/` with the Tailwind theme
  tokens (`text-primary` is the gold accent, `cn()` for conditional classes); older ones use module-level
  `CSSProperties` constants. Match the file being edited.
- **Static inline styles:** Module-level `CSSProperties` constants (never created inside render). Dynamic values (hover
  state, `item.isRead` background) stay inline.
- Design record of the move to shadcn/ui: `docs/features/shadcn-migration/`.

## npm workspaces
- The frontend is two **npm workspaces**, the JS counterpart of a solution with a test project. One `npm install` in
  the repo root installs both into the root `node_modules`, from the single root `package-lock.json`.
- `VirtualBar.Web.Tests` declares `virtualbar.web` as a dependency, the way `VirtualBar.Tests` references the backend
  projects.
- It deliberately does **not** list the app's runtime packages (`react`, `react-dom`, `@tanstack/react-query`,
  `react-router-dom`, `i18next`, …): it uses the app's own copies, so a component and its test share one React and
  one query-client context. Listing one of them separately could install a second copy, which breaks hooks.
