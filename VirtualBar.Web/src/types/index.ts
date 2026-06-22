export type SpiritCategory = 'Whisky' | 'Rum' | 'Cognac' | 'Vodka' | 'Gin' | 'Tequila' | 'Brandy' | 'Other'
export type BottleCondition = 'Sealed' | 'Opened' | 'Empty'

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
  distillery?: string
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
  distillery?: string
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
  name: string
  brand?: string
  imageUrl?: string
  volumeMl?: number
  abvPercent?: number
}

export interface CreateNewsPostPayload {
  coverImageUrl?: string
  translations: NewsPostTranslation[]
}

export type NotificationType = 'BottleLiked' | 'BottleCommented' | 'NewFollower' | 'NewMessage' | 'NewBottleFromFollowing' | 'BottleListedForSale'

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
