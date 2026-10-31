import type {
  BottleReview,
  PagedResult,
  Product,
  ProductCard,
  ProductDetail,
  ProductListing,
  ProductOwner,
} from '@/types'

let sequence = 0
const nextId = () => `00000000-0000-4000-8000-${String(++sequence).padStart(12, '0')}`

function product(overrides: Partial<Product> = {}): Product {
  return {
    id: nextId(),
    name: 'Lagavulin 16',
    brand: 'Lagavulin',
    distilleryId: null,
    distilleryName: 'Lagavulin',
    category: 'Whisky',
    country: null,
    region: null,
    age: null,
    abvPercent: null,
    volumeMl: null,
    barcode: null,
    imageUrl: null,
    ...overrides,
  }
}

/** A catalog card with no community activity unless the test gives it some. */
export function productCard(overrides: Partial<ProductCard> = {}): ProductCard {
  return { ownersCount: 0, averageScore: null, reviewsCount: 0, listingsCount: 0, ...product(), ...overrides }
}

/** A product page with nobody owning, selling or reviewing it unless the test says otherwise. */
export function productDetail(overrides: Partial<ProductDetail> = {}): ProductDetail {
  return {
    description: null,
    averageScore: null,
    reviewsCount: 0,
    topFlavors: [],
    ownersCount: 0,
    owners: [],
    listings: [],
    latestReviews: [],
    ...product(),
    ...overrides,
  }
}

export function owner(overrides: Partial<ProductOwner> = {}): ProductOwner {
  return { userId: nextId(), displayName: 'Owner', avatarUrl: null, bottleCount: 1, ...overrides }
}

export function listing(overrides: Partial<ProductListing> = {}): ProductListing {
  return {
    bottleId: nextId(),
    sellerId: nextId(),
    sellerDisplayName: 'Seller',
    sellerAvatarUrl: null,
    forSaleQuantity: 1,
    askingPrice: 150,
    currency: 'EUR',
    condition: 'Sealed',
    vintageYear: null,
    forSaleAt: '2026-10-30T10:00:00Z',
    ...overrides,
  }
}

/** A review whose summary is its `label`, so a test can find it by that text. */
export function review(label: string, overrides: Partial<BottleReview> = {}): BottleReview {
  return {
    id: nextId(),
    bottleId: nextId(),
    userId: nextId(),
    userDisplayName: `Reviewer ${label}`,
    userAvatarUrl: null,
    score: 85,
    nose: null,
    palate: null,
    finish: null,
    summary: label,
    flavors: [],
    createdAt: '2026-10-30T10:00:00Z',
    updatedAt: '2026-10-30T10:00:00Z',
    ...overrides,
  }
}

export function paged<T>(items: T[], hasMore = false): PagedResult<T> {
  return { items, hasMore }
}
