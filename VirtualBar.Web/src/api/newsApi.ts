import { client } from './client'
import type { NewsPost } from '../types'

export interface CreateNewsPostPayload {
  title: string
  excerpt: string
  content: string
  coverImageUrl?: string
}

export interface UpdateNewsPostPayload {
  title?: string
  excerpt?: string
  content?: string
  coverImageUrl?: string
}

export async function getNewsPosts(skip = 0, take = 20): Promise<NewsPost[]> {
  const { data } = await client.get<NewsPost[]>('/news', { params: { skip, take } })
  return data
}

export async function getNewsPost(id: string): Promise<NewsPost> {
  const { data } = await client.get<NewsPost>(`/news/${id}`)
  return data
}

export async function createNewsPost(payload: CreateNewsPostPayload): Promise<NewsPost> {
  const { data } = await client.post<NewsPost>('/news', payload)
  return data
}

export async function updateNewsPost(id: string, payload: UpdateNewsPostPayload): Promise<NewsPost> {
  const { data } = await client.put<NewsPost>(`/news/${id}`, payload)
  return data
}

export async function deleteNewsPost(id: string): Promise<void> {
  await client.delete(`/news/${id}`)
}
