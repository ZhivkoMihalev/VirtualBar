import { client } from './client'
import type { NewsPost, NewsPostTranslation } from '../types'

export interface CreateNewsPostPayload {
  coverImageUrl?: string
  translations: NewsPostTranslation[]
}

export async function getNewsPosts(lang: string): Promise<NewsPost[]> {
  const { data } = await client.get<NewsPost[]>('/news', { params: { lang } })
  return data
}

export async function getNewsPost(id: string, lang: string): Promise<NewsPost> {
  const { data } = await client.get<NewsPost>(`/news/${id}`, { params: { lang } })
  return data
}

export async function createNewsPost(payload: CreateNewsPostPayload): Promise<NewsPost> {
  const { data } = await client.post<NewsPost>('/news', payload)
  return data
}

export async function updateNewsPost(id: string, payload: CreateNewsPostPayload): Promise<NewsPost> {
  const { data } = await client.put<NewsPost>(`/news/${id}`, payload)
  return data
}

export async function deleteNewsPost(id: string): Promise<void> {
  await client.delete(`/news/${id}`)
}

export async function uploadNewsCover(file: File): Promise<string> {
  const formData = new FormData()
  formData.append('file', file)
  const res = await client.post<{ url: string }>('/news/upload-cover', formData, {
    headers: { 'Content-Type': 'multipart/form-data' },
  })
  return res.data.url
}
