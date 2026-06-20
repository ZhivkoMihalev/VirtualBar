import { client } from './client'
import type { UserProfile, UserSearchResult } from '../types'

export async function getUserProfile(userId: string): Promise<UserProfile> {
  const { data } = await client.get<UserProfile>(`/users/${userId}`)
  return data
}

export async function searchUsers(q?: string): Promise<UserSearchResult[]> {
  const { data } = await client.get<UserSearchResult[]>('/users', { params: q ? { q } : undefined })
  return data
}

export async function followUser(userId: string): Promise<void> {
  await client.post(`/users/${userId}/follow`)
}

export async function unfollowUser(userId: string): Promise<void> {
  await client.delete(`/users/${userId}/follow`)
}
