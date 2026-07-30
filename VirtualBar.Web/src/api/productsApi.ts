import { client } from './client'
import type { Product, SpiritCategory } from '../types'

export async function searchProducts(
  search: string,
  category?: SpiritCategory,
  limit?: number,
): Promise<Product[]> {
  const params: Record<string, string | number> = { search }
  if (category) params.category = category
  // Explicit null check: 0 is falsy but is still a value the caller meant to send.
  if (limit != null) params.limit = limit
  const { data } = await client.get<Product[]>('/products', { params })
  return data
}
