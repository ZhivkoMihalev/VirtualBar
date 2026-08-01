export type SpiritCategory = 'Whisky' | 'Rum' | 'Cognac' | 'Vodka' | 'Gin' | 'Tequila' | 'Brandy' | 'Other'
export type BottleCondition = 'Sealed' | 'Opened' | 'Empty'

export type FlavorTag =
  | 'Smoky' | 'Peaty' | 'Medicinal' | 'Maritime' | 'Vanilla' | 'Caramel' | 'Toffee'
  | 'Honey' | 'Chocolate' | 'Coffee' | 'Nutty' | 'Malty' | 'Creamy' | 'Fruity'
  | 'Citrus' | 'TropicalFruit' | 'DriedFruit' | 'Berry' | 'Floral' | 'Herbal'
  | 'Grassy' | 'Spicy' | 'Pepper' | 'Cinnamon' | 'Oak' | 'Sherry' | 'Leather' | 'Tobacco'

export interface Distillery {
  id: string
  name: string
  country: string | null
  region: string | null
  categories: string[]
}

export interface User {
  id: string
  email: string
  displayName: string
  bio?: string
  avatarUrl?: string
  country?: string
  city?: string
  createdAt: string
  isAdmin: boolean
}

export interface Bottle {
  id: string
  userId: string
  userDisplayName: string
  name: string
  distilleryId: string | null
  distilleryName: string | null
  productId?: string | null
  region?: string
  country?: string
  category: SpiritCategory
  age?: number
  vintageYear?: number
  abvPercent?: number
  volumeMl?: number
  condition: BottleCondition
  description?: string
  isLimited: boolean
  isForSale: boolean
  askingPrice?: number
  currency?: string
  images: BottleImage[]
  likesCount: number
  commentsCount: number
  averageScore?: number | null
  reviewsCount: number
  likedByMe: boolean
  createdAt: string
}

export interface BottleImage {
  id: string
  url: string
  isPrimary: boolean
  sortOrder: number
}

export interface BottleComment {
  id: string
  bottleId: string
  userId: string
  userDisplayName: string
  userAvatarUrl?: string
  content: string
  createdAt: string
}

export interface BottleReview {
  id: string
  bottleId: string
  userId: string
  userDisplayName: string
  userAvatarUrl: string | null
  score: number
  nose: string | null
  palate: string | null
  finish: string | null
  summary: string | null
  flavors: FlavorTag[]
  createdAt: string
  updatedAt: string
}

export interface BottleReviewsSummary {
  averageScore: number | null
  reviewsCount: number
  topFlavors: FlavorTag[]
  reviews: BottleReview[]
  myReview: BottleReview | null
}

export interface ReviewPayload {
  score: number
  nose?: string | null
  palate?: string | null
  finish?: string | null
  summary?: string | null
  flavors?: FlavorTag[] | null
}

export interface Message {
  id: string
  senderId: string
  senderDisplayName: string
  receiverId: string
  content: string
  isRead: boolean
  createdAt: string
}

export interface AuthResponse {
  token: string
  user: User
}

export interface UserProfile {
  id: string
  displayName: string
  bio?: string
  avatarUrl?: string
  country?: string
  city?: string
  bottleCount: number
  followerCount: number
  followingCount: number
  isFollowedByMe: boolean
}

export interface UserSearchResult {
  id: string
  displayName: string
  avatarUrl?: string
  bio?: string
  country?: string
  bottleCount: number
  followerCount: number
}

export interface NewsPostTranslation {
  languageCode: string
  title: string
  content: string
}

export interface NewsPost {
  id: string
  title: string
  content: string
  coverImageUrl?: string
  authorId: string
  authorDisplayName: string
  createdAt: string
  updatedAt: string
  translations: NewsPostTranslation[]
}

export type FeedItemType = 'News' | 'NewBottle' | 'ForSale'

export interface FeedItem {
  type: FeedItemType
  timestamp: string
  postId?: string
  postTitle?: string
  postContent?: string
  postCoverImageUrl?: string
  postAuthorDisplayName?: string
  bottleId?: string
  bottleName?: string
  bottleCategory?: string
  bottlePrimaryImageUrl?: string
  bottleUserId?: string
  bottleUserDisplayName?: string
  askingPrice?: number
  currency?: string
}

export interface ConversationSummary {
  otherUserId: string
  otherUserDisplayName: string
  otherUserAvatarUrl?: string
  lastMessageContent: string
  lastMessageAt: string
  lastMessageIsFromMe: boolean
  unreadCount: number
}

export interface UpdatedProfile {
  displayName: string
  bio?: string
  avatarUrl?: string
  country?: string
  city?: string
}

export interface AddBottlePayload {
  name: string
  distilleryId?: string | null
  productId?: string | null
  region?: string
  country?: string
  category: SpiritCategory
  age?: number
  vintageYear?: number
  abvPercent?: number
  volumeMl?: number
  condition: BottleCondition
  description?: string
  isLimited: boolean
}

export interface MarketplaceFilters {
  search?: string
  category?: string
  sort?: 'price_asc' | 'price_desc' | 'newest'
}

export interface BarcodeProduct {
  productId?: string | null
  name: string
  brand?: string
  imageUrl?: string
  volumeMl?: number
  abvPercent?: number
}

/**
 * Mirrors ProductDto one-for-one. The API always serialises every field and uses null for "unknown",
 * so the properties are required-but-nullable rather than optional — optional would let a caller
 * assemble a half-built object and still satisfy the type.
 */
export interface Product {
  id: string
  name: string
  brand: string | null
  distilleryId: string | null
  distilleryName: string | null
  category: SpiritCategory
  country: string | null
  region: string | null
  age: number | null
  abvPercent: number | null
  volumeMl: number | null
  barcode: string | null
  imageUrl: string | null
}

/**
 * What the add-bottle form knows about the catalog entry it is linking to. A catalog pick supplies a
 * whole {@link Product}; a barcode hit only proves the id and an image, so the rest is genuinely
 * absent instead of being faked from the form's own inputs.
 */
export interface LinkedProduct {
  id: string
  name: string
  category: SpiritCategory
  imageUrl: string | null
  country?: string | null
  region?: string | null
}

export type ProductRequestStatus = 'Pending' | 'Approved' | 'Rejected'

export interface ProductRequest {
  id: string
  name: string
  brand?: string | null
  distilleryId?: string | null
  distilleryName?: string | null
  category: SpiritCategory
  age?: number | null
  abvPercent?: number | null
  volumeMl?: number | null
  barcode?: string | null
  country?: string | null
  region?: string | null
  userNote?: string | null
  status: ProductRequestStatus
  adminNote?: string | null
  requesterId: string
  requesterDisplayName: string
  resolvedProductId?: string | null
  sourceBottleId?: string | null
  createdAt: string
  respondedAt?: string | null
}

export interface CreateProductRequestPayload {
  name: string
  brand?: string | null
  distilleryId?: string | null
  category: SpiritCategory
  age?: number | null
  abvPercent?: number | null
  volumeMl?: number | null
  barcode?: string | null
  country?: string | null
  region?: string | null
  userNote?: string | null
  sourceBottleId?: string | null
}

export interface ApproveProductRequestPayload {
  existingProductId?: string | null
  name?: string | null
  brand?: string | null
  distilleryId?: string | null
  category?: SpiritCategory | null
  age?: number | null
  abvPercent?: number | null
  volumeMl?: number | null
  barcode?: string | null
  country?: string | null
  region?: string | null
  imageUrl?: string | null
  description?: string | null
  adminNote?: string | null
  useSourceBottleImage: boolean
}

export interface RejectProductRequestPayload {
  adminNote?: string | null
}

export interface CreateNewsPostPayload {
  coverImageUrl?: string
  translations: NewsPostTranslation[]
}

export interface WishListItem {
  id: string
  bottleName: string | null
  distilleryId: string | null
  distilleryName: string | null
  category: SpiritCategory | null
  imageUrl: string | null
  createdAt: string
}

export interface PublicWishListItem {
  id: string
  bottleName: string | null
  distilleryId: string | null
  distilleryName: string | null
  category: SpiritCategory | null
  imageUrl: string | null
  userId: string
  userDisplayName: string
  createdAt: string
}

export type BadgeType =
  | 'FirstBottle' | 'Collector5' | 'Collector10' | 'Collector25' | 'Collector50' | 'Collector100'
  | 'Explorer3' | 'Explorer5' | 'LimitedHunter'
  | 'Liked10' | 'Liked50' | 'Liked100'
  | 'FirstFollower' | 'Popular10' | 'Influencer50'
  | 'FirstListing' | 'FirstSale' | 'FirstPurchase'
  | 'FirstCatalogProduct'

export interface UserBadge {
  badge: BadgeType
  awardedAt: string
}

export interface BadgeProgress {
  badge: BadgeType
  threshold: number
  current: number
  earned: boolean
  awardedAt: string | null
}

export type NotificationType = 'BottleLiked' | 'BottleCommented' | 'NewFollower' | 'NewMessage' | 'NewBottleFromFollowing' | 'BottleListedForSale' | 'WishListMatch' | 'OfferReceived' | 'OfferAccepted' | 'OfferDeclined' | 'BottleReviewed' | 'BadgeEarned' | 'ProductRequestApproved' | 'ProductRequestRejected'

export interface NotificationItem {
  id: string
  type: NotificationType
  actorId: string
  actorDisplayName: string
  resourceId?: string
  resourceName?: string
  isRead: boolean
  createdAt: string
}

export interface NotificationSummary {
  notifications: NotificationItem[]
  unreadCount: number
}

export type PriceConfidence = 'Low' | 'Medium' | 'High'
export type PriceSource = 'ClaudeResearch' | 'Internal'

export interface PriceCitation {
  url: string
  title: string
}

export interface PriceEstimate {
  estimatedPrice: number
  lowEstimate: number | null
  highEstimate: number | null
  currency: string
  sampleSize: number
  source: PriceSource
  confidence: PriceConfidence
  asOf: string
  sources: PriceCitation[]
}

export interface BottlePriceLine {
  bottleId: string
  name: string
  estimatedPrice: number | null
  currency: string
  confidence: PriceConfidence
  source: PriceSource
  countedInTotal: boolean
}

export interface CollectionValue {
  totalValue: number
  currency: string
  pricedCount: number
  totalCount: number
  items: BottlePriceLine[]
}

export type OfferStatus = 'Pending' | 'Accepted' | 'Declined' | 'Withdrawn'

export interface Offer {
  id: string
  bottleId: string
  bottleName: string
  buyerId: string
  buyerDisplayName: string
  sellerId: string
  sellerDisplayName: string
  offeredPrice: number
  currency: string
  message: string | null
  status: OfferStatus
  respondedAt: string | null
  createdAt: string
}
