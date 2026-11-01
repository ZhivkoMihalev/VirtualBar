import type { FeedItem } from '@/types'

let sequence = 0
const nextId = () => `00000000-0000-4000-9000-${String(++sequence).padStart(12, '0')}`

/**
 * A followed collector's review of another collector's bottle that is not linked to the catalog, with no summary and
 * no flavors, unless the test says otherwise.
 */
export function reviewedItem(overrides: Partial<FeedItem> = {}): FeedItem {
  return {
    type: 'Reviewed',
    timestamp: '2026-10-31T10:00:00Z',
    reviewId: nextId(),
    reviewScore: 92,
    reviewSummary: null,
    reviewFlavors: [],
    actorUserId: nextId(),
    actorDisplayName: 'Reviewer',
    actorAvatarUrl: null,
    bottleId: nextId(),
    bottleName: 'Old Pulteney 12',
    bottleCategory: 'Whisky',
    bottleUserId: nextId(),
    bottleUserDisplayName: 'Owner',
    productId: null,
    productName: null,
    productBrand: null,
    ...overrides,
  }
}
