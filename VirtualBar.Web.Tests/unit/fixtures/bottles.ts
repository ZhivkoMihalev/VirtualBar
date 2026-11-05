import type { Bottle, BottleReviewsSummary } from '@/types'

let sequence = 0
const nextId = () => `00000000-0000-4000-9000-${String(++sequence).padStart(12, '0')}`

/** A bottle in another collector's bar, not for sale and with no activity unless the test says otherwise. */
export function bottle(overrides: Partial<Bottle> = {}): Bottle {
  return {
    id: nextId(),
    userId: nextId(),
    userDisplayName: 'Seller',
    name: 'Lagavulin 16',
    distilleryId: null,
    distilleryName: 'Lagavulin',
    productId: null,
    category: 'Whisky',
    condition: 'Sealed',
    quantity: 1,
    isLimited: false,
    isForSale: false,
    forSaleQuantity: 0,
    images: [],
    likesCount: 0,
    commentsCount: 0,
    averageScore: null,
    reviewsCount: 0,
    likedByMe: false,
    createdAt: '2026-11-01T10:00:00Z',
    ...overrides,
  }
}

/** A bottle's reviews when nobody has reviewed it. */
export function noReviews(): BottleReviewsSummary {
  return { averageScore: null, reviewsCount: 0, topFlavors: [], reviews: [], myReview: null }
}
