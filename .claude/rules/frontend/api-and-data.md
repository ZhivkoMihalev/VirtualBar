---
paths:
  - "VirtualBar.Web/**"
  - "VirtualBar.Web.Tests/unit/**"
---

# Auth, API layer, data fetching and types

**Auth:** JWT stored in `localStorage` (`vbar_token` + `vbar_user`). `useAuth()` exposes `user`, `login`, `logout`. The Axios `client` in `src/api/client.ts` attaches the token as a Bearer header on every request and redirects to `/login` on 401. The `user` object includes `isAdmin: boolean` — set from the JWT claim emitted by `AuthService`.

**API layer (`src/api/`):** One file per domain (e.g., `bottlesApi.ts`, `usersApi.ts`, `notificationsApi.ts`). All use named import `{ client }` from `./client`. Return typed data directly (axios `.data` unwrapping).

**Data fetching:** TanStack Query (`useQuery` / `useMutation`). `queryClient.invalidateQueries` after mutations. Global `staleTime: 30_000`.

**Types:** All shared TypeScript types in `src/types/index.ts`. No per-file type definitions.
