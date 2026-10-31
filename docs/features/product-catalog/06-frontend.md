# Slice 06 — Frontend: ProductSelect, add-form, admin queue, profile, bell, i18n

> Prereq: `00-OVERVIEW.md`, slices 03–05 running locally. NEW components use **shadcn/ui + Tailwind
> tokens** (project memory: new files shadcn, old files keep their style — match the file you touch).
> All strings via `useTranslation()` — bg **and** en.

## Files — create

### `src/api/productsApi.ts`
`searchProducts(search: string, category?: SpiritCategory, limit?: number): Promise<Product[]>` →
`GET /api/products`. If the existing barcode lookup fn (`lookupBarcode`) lives in another module,
leave it there — do not refactor old files in this slice.

### `src/api/productRequestsApi.ts`
`createProductRequest(payload)`, `getMyProductRequests()`, `withdrawProductRequest(id)`,
`getProductRequests(status?)`, `approveProductRequest(id, payload)`,
`rejectProductRequest(id, payload)` — named `{ client }` import, typed returns, `.data` unwrap.

### `src/components/ProductSelect.tsx`
shadcn `Popover` + `Command` combobox (reference `DistillerySelect` for UX semantics, not for
implementation style):
- Props: `{ onSelect(product: Product): void, category?: SpiritCategory }` — a *picker*, not a
  controlled value field (the form owns the values after the pick).
- Debounce 250 ms; query enabled at ≥ 2 chars; `queryKey: ['products', 'search', term, category ?? 'all']`,
  `staleTime` default (30 s global is fine).
- Option row: name (+ age suffix like "12 YO" when set) on line 1; distillery/brand · volume ·
  category muted on line 2. A 32 px rounded thumbnail leads the row when `imageUrl` is set (plain
  `<img>` with `object-cover`; no image → text-only row, no placeholder box).
- Empty state (≥ 2 chars, no results): `products.noResults` + the notice `products.requestNotice`
  ("Няма го в каталога — запази бутилката и ще изпратим заявка за добавяне.") — communicates the
  server-side auto-request so nothing feels lost.

### `src/pages/ProductRequestsAdminPage.tsx` + route + guard
- `App.tsx`: add an `AdminRoute` wrapper next to `ProtectedRoute`:
  `isLoading → fallback; !isAuthenticated → /login; !user.isAdmin → Navigate '/' replace`.
  Route: `/admin/product-requests` (lazy import, same pattern as the other pages).
- Page: NavBar on top; status filter tabs (`Pending` default / `Approved` / `Rejected` — shadcn
  `Tabs`); `queryKey: ['productRequests', 'admin', status]`.
- Row: proposed name + meta (category, distillery/brand, age, ABV, volume, barcode), requester
  display name + date, `UserNote` when present.
- **Approve dialog** (shadcn `Dialog` + `react-hook-form`, same form stack as the Dashboard
  add-form): all product fields pre-filled from the request, editable; `DistillerySelect` for the
  distillery; a secondary mode "Свържи със съществуващ" with a `ProductSelect` that fills
  `existingProductId` (fields disabled in that mode). When the request has a `sourceBottleId`, a
  checkbox `productRequests.useSourceImage` ("Използвай снимката на бутилката-източник") — default
  **checked** — sends `useSourceBottleImage`; hidden in the link-existing mode. Submit →
  `approveProductRequest` → invalidate `['productRequests']` + toast.
- **Reject dialog**: optional `adminNote` textarea → `rejectProductRequest` → invalidate + toast.

## Files — modify

### `src/types/index.ts` (single shared types file — no per-file types)
- `Product`, `ProductRequest`, `ProductRequestStatus = 'Pending' | 'Approved' | 'Rejected'`,
  request/response payload types.
- `Bottle`: `productId?: string | null`. `AddBottlePayload`/update payload: `productId?: string | null`.
- The barcode lookup result type: `productId?: string | null` (additive).
- `NotificationType` union: append `'ProductRequestApproved' | 'ProductRequestRejected'`.
- `AuthUser` already has `isAdmin` — no change.

### `src/pages/DashboardPage.tsx` (add-bottle form — OLD file, match its existing style)
- `ProductSelect` block at the top of the form, above the barcode lookup: label
  `products.searchLabel` ("Намери в каталога").
- On pick: `setValue` for `name`, `category`, `distilleryId`, `age`, `abvPercent`, `volumeMl`,
  `country`, `region` (overwrite — the user explicitly chose the product), remember
  `productId` in local state, show a linked chip (`products.linkedChip`, gold border, X to unlink).
- Remember `product.imageUrl` in local state too; extend the create mutation's existing image
  chain: `imageFile` upload → else `barcodeImageUrl` link → **else `productImageUrl` link** (same
  `linkBottleImage` call). Cleared together with `productId` (unlink / identity-field edit).
- Clear `productId` when the user subsequently edits an identity field (`name`, `category`, `age`,
  `volumeMl`) — a `watch`-based effect comparing against the picked snapshot.
- Barcode lookup success carrying `productId` → set it too (same chip).
- Include `productId` in the add payload.

### `src/pages/ProfilePage.tsx` — "Моите заявки" section
Below achievements: list own requests (`queryKey: ['productRequests', 'mine']`) with status chips
(shadcn `Badge`: Pending amber outline / Approved success / Rejected destructive), `adminNote`
shown when rejected, withdraw button (X, `AlertDialog` confirm) on pending ones →
`withdrawProductRequest` + invalidate. Empty → render nothing (the PublicBar badges-strip rule).

### `src/components/NavBar.tsx`
Admin-only item (render when `user?.isAdmin`): `nav.productRequests` ("ЗАЯВКИ") →
`/admin/product-requests`. Style-match the existing nav items exactly.

### `src/components/NotificationBell.tsx`
Two new cases in the type→(text, onClick) mapping:
- `ProductRequestApproved` → `notifications.productRequestApproved` with `{{name: resourceName}}`,
  navigate `/profile`.
- `ProductRequestRejected` → `notifications.productRequestRejected`, navigate `/profile`.

### `src/i18n/bg.json` + `src/i18n/en.json`
- New namespace `products`: `searchLabel`, `searchPlaceholder`, `noResults`, `requestNotice`,
  `linkedChip`, `unlink`.
- New namespace `productRequests`: `title`, `mineTitle`, `empty`, `status.Pending/Approved/Rejected`,
  `approve`, `reject`, `adminNote`, `userNote`, `withdraw`, `withdrawConfirm`, `linkExisting`,
  `useSourceImage`,
  `requestedBy`, `approveTitle`, `rejectTitle`, plus form labels reused from `addBottle` where they
  exist (do not duplicate keys that `addBottle` already has — reference them).
- `nav.productRequests`; `notifications.productRequestApproved`, `notifications.productRequestRejected`.
- Keep bg as the primary voice (Cyrillic, speakeasy tone), en mirroring.

## Verification (this slice)
`npm --prefix VirtualBar.Web run build` → clean "✓ built in …". Manual pass in **bg + en**:
1. Add-form: search "Macallan" → pick → fields fill + chip; edit name → chip clears.
2. Add an unknown bottle → appears in admin queue (`/admin/product-requests` as the admin).
3. Approve with edits → requester gets a bell item → navigates to `/profile`; the source bottle in
   the requester's dashboard now carries `productId`.
4. Reject with a note → bell + note visible under "Моите заявки".
5. Non-admin hitting `/admin/product-requests` → redirected home; API returns 403 regardless.
6. Request born from a bottle WITH a photo → approve with the image checkbox → the product shows a
   thumbnail in `ProductSelect`; a new bottle added from that pick without an own photo receives
   the product image via `linkBottleImage`.
