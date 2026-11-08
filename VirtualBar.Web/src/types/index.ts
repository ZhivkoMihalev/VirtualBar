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
  /** The EAN/UPC on the bottle (#49), 8 to 14 digits. */
  barcode?: string | null
  condition: BottleCondition
  quantity: number
  description?: string
  isLimited: boolean
  isForSale: boolean
  forSaleQuantity: number
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

/** A successful POST /auth/login, or POST /auth/register when no email confirmation is needed. */
export interface AuthResponse {
  token: string
  user: User
}

/**
 * POST /auth/register. With email confirmation on, the new account cannot sign in until the link in the email is
 * opened: the token is empty, the user is null and requiresEmailConfirmation is true. An email that already has an
 * account gets exactly the same answer, so the API never reveals which addresses are registered.
 */
export type RegisterResponse =
  | (AuthResponse & { requiresEmailConfirmation: false })
  | { token: string; user: null; requiresEmailConfirmation: true }

/** What AuthContext.register did: signed the user in, or left them to confirm their email first. */
export type RegisterOutcome = 'signedIn' | 'emailConfirmationRequired'

/** Body of an API error response. `reason` is present only where the API defines one, such as the auth endpoints. */
export interface ApiErrorBody {
  error?: string
  reason?: string
}

/** The languages the API writes its emails in (backend: EmailService.NormalizeLanguage). */
export type EmailLanguage = 'bg' | 'en'

/** The reasons the auth endpoints send (backend: AuthErrorReasons). Each one has a translation under `authErrors`. */
export type AuthErrorReason = 'InvalidCredentials' | 'LockedOut' | 'EmailNotConfirmed' | 'EmailTaken'

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

export type FeedItemType = 'News' | 'NewBottle' | 'ForSale' | 'Reviewed'

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
  /** The bottle's owner; on a `Reviewed` item the reviewer is the actor instead. */
  bottleUserId?: string
  bottleUserDisplayName?: string
  askingPrice?: number
  currency?: string
  /** The reviewer of a `Reviewed` item. */
  actorUserId?: string | null
  actorDisplayName?: string | null
  actorAvatarUrl?: string | null
  reviewId?: string | null
  reviewScore?: number | null
  /** The start of the review's summary, already shortened by the API. */
  reviewSummary?: string | null
  /** At most three of the review's flavors. */
  reviewFlavors?: FlavorTag[] | null
  /** The catalog product the bottle is linked to, when it is not deleted. */
  productId?: string | null
  productName?: string | null
  /** The product's brand, or its distillery when it has none. */
  productBrand?: string | null
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
  /** 8 to 14 digits (#49); anything else is refused, so the form sends only a valid one (`toBarcode`). */
  barcode?: string | null
  condition: BottleCondition
  quantity: number
  description?: string
  isLimited: boolean
}

// Update takes the same shape as add: the API assigns every field blindly, so even a
// quantity-only edit has to echo the bottle back in full or it would blank the rest.
export type UpdateBottlePayload = AddBottlePayload

export interface MarketplaceFilters {
  search?: string
  category?: string
  sort?: 'price_asc' | 'price_desc' | 'newest'
}

/** The catalog product of a barcode (#49): the API answers only from the platform's own data, so always a product. */
export interface BarcodeProduct {
  productId: string
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
  /** The year of distillation of a vintage-dated product (#59); sent by the search and the product page. */
  vintageYear?: number | null
  /** The year of a yearly edition (#59); never set together with `vintageYear`. */
  releaseYear?: number | null
  abvPercent: number | null
  volumeMl: number | null
  barcode: string | null
  imageUrl: string | null
}

/** One page of a list; `hasMore` comes from the server fetching one row past the page. */
export interface PagedResult<T> {
  items: T[]
  hasMore: boolean
}

/**
 * The order of `/catalog` (the API's `ProductSort`, sent by name). `Name` is the default without a search term and
 * `Relevance` (name starts with the term first) with one; `TopRated` ranks products with 3+ reviews first (#56).
 */
export type ProductSort = 'Name' | 'Relevance' | 'TopRated' | 'MostReviewed' | 'Popular' | 'ForSale'

/** A product on a `/catalog` card, with its community counts over every linked bottle. */
export interface ProductCard extends Product {
  ownersCount: number
  averageScore: number | null
  reviewsCount: number
  listingsCount: number
}

export interface ProductOwner {
  userId: string
  displayName: string
  avatarUrl: string | null
  /** Physical bottles: the sum of `quantity` over the owner's linked bottles. */
  bottleCount: number
}

/** A linked bottle that is listed for sale; `askingPrice` is per bottle. */
export interface ProductListing {
  bottleId: string
  sellerId: string
  sellerDisplayName: string
  sellerAvatarUrl: string | null
  forSaleQuantity: number
  askingPrice: number | null
  currency: string | null
  condition: BottleCondition
  vintageYear: number | null
  forSaleAt: string | null
}

/** The `/product/:id` page: the product plus aggregates over every non-deleted linked bottle. */
/** Another vintage or edition of a product's line (#59), with the same kind of year as the product listing it. */
export interface ProductYear {
  id: string
  name: string
  year: number
  averageScore: number | null
  listingsCount: number
}

export interface ProductDetail extends Product {
  /** The year of distillation of a vintage-dated product; never set together with `releaseYear` (#59). */
  vintageYear: number | null
  /** The year of a yearly edition; it links the series but is not the bottle's vintage. */
  releaseYear: number | null
  /** The other vintages, or the other editions, of the product's line, oldest first; empty when undated. */
  otherYears: ProductYear[]
  description: string | null
  averageScore: number | null
  reviewsCount: number
  topFlavors: FlavorTag[]
  /** All distinct owners; `owners` holds at most the top 20. */
  ownersCount: number
  owners: ProductOwner[]
  /** How many current listings there are; sent to everyone, like the catalog card's `listingsCount`. */
  listingsCount: number
  /** The current listings, newest first; always empty for a visitor (#102). */
  listings: ProductListing[]
  /** The newest 5; the rest come from the paged product reviews endpoint. */
  latestReviews: BottleReview[]
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
  /** A catalog pick's vintage (#59); a barcode hit does not know it (the API still fills the bottle's from it). */
  vintageYear?: number | null
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
  /** The requested year of distillation (#59). */
  vintageYear?: number | null
  /** The requested year of a yearly edition (#59). */
  releaseYear?: number | null
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
  /** A wish for this exact catalog product: only listings of bottles linked to it match. */
  productId: string | null
  /** Null when the product was removed from the catalog. */
  productName: string | null
  imageUrl: string | null
  createdAt: string
}

export interface PublicWishListItem {
  id: string
  bottleName: string | null
  distilleryId: string | null
  distilleryName: string | null
  category: SpiritCategory | null
  productId: string | null
  productName: string | null
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
  quantity: number
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

export interface AuthContextValue {
  user: User | null
  token: string | null
  isAuthenticated: boolean
  login: (email: string, password: string) => Promise<void>
  register: (email: string, password: string, displayName: string, language: EmailLanguage) => Promise<RegisterOutcome>
  logout: () => void
  updateUser: (patch: Partial<User>) => void
}

export interface ChatContextValue {
  inboxOpen: boolean
  openInbox: () => void
  closeInbox: () => void
  toggleInbox: () => void
  activeUserId: string | null
  openChat: (userId: string) => void
  closeChat: () => void
}
