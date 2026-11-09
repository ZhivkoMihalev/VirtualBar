import type { Offer } from '@/types'

let sequence = 0
const nextId = () => `00000000-0000-4000-a000-${String(++sequence).padStart(12, '0')}`

/** A pending offer of 100 EUR from Buyer to Seller, unless the test says otherwise. */
export function offer(overrides: Partial<Offer> = {}): Offer {
  return {
    id: nextId(),
    bottleId: nextId(),
    bottleName: 'Lagavulin 16',
    buyerId: nextId(),
    buyerDisplayName: 'Buyer',
    sellerId: nextId(),
    sellerDisplayName: 'Seller',
    offeredPrice: 100,
    currency: 'EUR',
    message: null,
    status: 'Pending',
    respondedAt: null,
    completedAt: null,
    cancelledAt: null,
    createdAt: '2026-11-01T10:00:00Z',
    ...overrides,
  }
}
