import { client } from './client'
import type { UserBadge, BadgeProgress } from '../types'

export async function getUserBadges(userId: string): Promise<UserBadge[]> {
  const res = await client.get<UserBadge[]>(`/badges/user/${userId}`)
  return res.data
}

export async function getMyProgress(): Promise<BadgeProgress[]> {
  const res = await client.get<BadgeProgress[]>('/badges/progress')
  return res.data
}
