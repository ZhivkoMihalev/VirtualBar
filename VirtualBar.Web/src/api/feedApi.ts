import { client } from './client'
import type { FeedItem } from '../types'

export async function getFeed(skip = 0, take = 20): Promise<FeedItem[]> {
  const { data } = await client.get<FeedItem[]>('/feed', { params: { skip, take } })
  return data
}
