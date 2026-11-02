import type { PublicWishListItem, WishListItem } from '@/types'

let sequence = 0
const nextId = () => `00000000-0000-4000-9000-${String(++sequence).padStart(12, '0')}`

/** One of the signed-in collector's own wishes: a category-only wish unless the test says otherwise. */
export function wishListItem(overrides: Partial<WishListItem> = {}): WishListItem {
  return {
    id: nextId(),
    bottleName: null,
    distilleryId: null,
    distilleryName: null,
    category: 'Whisky',
    productId: null,
    productName: null,
    imageUrl: null,
    createdAt: '2026-11-01T10:00:00Z',
    ...overrides,
  }
}

/** A wish as the marketplace "Looking for" tab lists it, with the collector who posted it. */
export function publicWishListItem(overrides: Partial<PublicWishListItem> = {}): PublicWishListItem {
  return {
    ...wishListItem(),
    userId: nextId(),
    userDisplayName: 'Collector',
    ...overrides,
  }
}
