import { client } from './client'
import type { Bottle, BottleComment, AddBottlePayload, MarketplaceFilters, BarcodeProduct } from '../types'

export async function getMarketplace(filters: MarketplaceFilters): Promise<Bottle[]> {
  const params: Record<string, string> = {}
  if (filters.search) params.search = filters.search
  if (filters.category) params.category = filters.category
  if (filters.sort) params.sort = filters.sort
  const { data } = await client.get<Bottle[]>('/bottles/marketplace', { params })
  return data
}

export async function getBottlesByUser(userId: string): Promise<Bottle[]> {
  const res = await client.get<Bottle[]>('/bottles', { params: { userId } })
  return res.data
}

export async function addBottle(payload: AddBottlePayload): Promise<Bottle> {
  const res = await client.post<Bottle>('/bottles', payload)
  return res.data
}

export async function removeBottle(id: string): Promise<void> {
  await client.delete(`/bottles/${id}`)
}

export async function reorderBottles(bottleIds: string[]): Promise<void> {
  await client.put('/bottles/reorder', { bottleIds })
}

export async function uploadBottleImage(bottleId: string, file: File): Promise<void> {
  const form = new FormData()
  form.append('file', file)
  await client.post(`/bottles/${bottleId}/images`, form, {
    headers: { 'Content-Type': 'multipart/form-data' },
  })
}

export async function linkBottleImage(bottleId: string, url: string): Promise<void> {
  await client.post(`/bottles/${bottleId}/images/link`, { url })
}

export async function lookupBarcode(barcode: string): Promise<BarcodeProduct> {
  const res = await client.get<BarcodeProduct>(`/products/barcode/${encodeURIComponent(barcode)}`)
  return res.data
}

export async function listBottleForSale(bottleId: string, askingPrice: number, currency: string): Promise<void> {
  await client.post(`/bottles/${bottleId}/list-for-sale`, { askingPrice, currency })
}

export async function unlistBottleFromSale(bottleId: string): Promise<void> {
  await client.post(`/bottles/${bottleId}/unlist`)
}

export async function toggleBottleLike(bottleId: string): Promise<void> {
  await client.post(`/bottles/${bottleId}/likes`)
}

export async function getBottleComments(bottleId: string): Promise<BottleComment[]> {
  const res = await client.get<BottleComment[]>(`/bottles/${bottleId}/comments`)
  return res.data
}

export async function addBottleComment(bottleId: string, content: string): Promise<BottleComment> {
  const res = await client.post<BottleComment>(`/bottles/${bottleId}/comments`, { content })
  return res.data
}

export async function deleteBottleComment(bottleId: string, commentId: string): Promise<void> {
  await client.delete(`/bottles/${bottleId}/comments/${commentId}`)
}
