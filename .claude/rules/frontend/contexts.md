---
paths:
  - "VirtualBar.Web/**"
  - "VirtualBar.Web.Tests/unit/**"
---

# Contexts

**Contexts:** each provider file exports only its component; the context object and hook live in a separate `.ts` file (Vite fast refresh needs component-only files — ESLint `react-refresh/only-export-components`). Value types (`AuthContextValue`, `ChatContextValue`) are in `src/types/index.ts`.
- `src/contexts/AuthContext.tsx` (`AuthProvider`) + `src/contexts/useAuth.ts` (`useAuth()`) — `user`, `token`, `isAuthenticated`, `login`, `register`, `logout`, `updateUser`. The saved session is read from `localStorage` while the state is created, so the first render already knows who is logged in and there is no app-wide loading state. `login`/`register` must not add one: swapping the routes for a spinner unmounts the form and loses the error a failed attempt sets on it (the E2E "Login Validation" tests pin this). The forms show their own pending state through `useMutation`.
- `src/contexts/ChatContext.tsx` (`ChatProvider`) + `src/contexts/useChat.ts` (`useChat()`) — `inboxOpen`, `openInbox`, `closeInbox`, `toggleInbox`, `activeUserId`, `openChat(userId)`, `closeChat`. Used by NavBar, NotificationBell, MarketplacePage, PublicBarPage, ChatWidget.

The same rule applies to any component file: shared constants go to their own module (e.g. the bottle colors `CATEGORY_COLORS` / `LIQUID_LEVEL` / `CAP_COLOR` in `src/lib/bottlePalette.ts`). Generated shadcn files in `src/components/ui/` are exempt in `eslint.config.js`.
