import { client } from './client'
import type {
  BottleReview,
  PagedResult,
  Product,
  ProductCard,
  ProductDetail,
  SpiritCategory,
} from '../types'

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

export interface BrowseProductsParams {
  /** Omitted or empty lists the whole catalog; the server rejects 1 character, so never send one. */
  search?: string
  category?: SpiritCategory
  page: number
  pageSize?: number
}

export async function browseProducts({
  search,
  category,
  page,
  pageSize,
}: BrowseProductsParams): Promise<PagedResult<ProductCard>> {
  const params: Record<string, string | number> = { page }
  if (search) params.search = search
  if (category) params.category = category
  if (pageSize != null) params.pageSize = pageSize
  const { data } = await client.get<PagedResult<ProductCard>>('/products/browse', { params })
  return data
}

export async function getProduct(id: string): Promise<ProductDetail> {
  const { data } = await client.get<ProductDetail>(`/products/${id}`)
  return data
}

export async function getProductReviews(
  id: string,
  page: number,
  pageSize?: number,
): Promise<PagedResult<BottleReview>> {
  const params: Record<string, number> = { page }
  if (pageSize != null) params.pageSize = pageSize
  const { data } = await client.get<PagedResult<BottleReview>>(`/products/${id}/reviews`, { params })
  return data
}
