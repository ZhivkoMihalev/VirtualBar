import { client } from './client'
import type { FeedItem } from '../types'

export async function getFeed(skip: number, take: number, lang: string): Promise<FeedItem[]> {
  const { data } = await client.get<FeedItem[]>('/feed', { params: { skip, take, lang } })
  return data
}
