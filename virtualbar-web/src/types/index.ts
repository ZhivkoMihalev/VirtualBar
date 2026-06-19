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
}

export interface Bottle {
  id: string
  userId: string
  userName: string
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
