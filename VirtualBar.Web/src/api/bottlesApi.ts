import { client } from './client'
import type { Bottle, SpiritCategory, BottleCondition } from '../types'

export interface AddBottlePayload {
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

export interface BarcodeProduct {
  name: string
  brand?: string
  imageUrl?: string
  volumeMl?: number
  abvPercent?: number
}

export async function lookupBarcode(barcode: string): Promise<BarcodeProduct> {
  const res = await client.get<BarcodeProduct>(`/products/barcode/${encodeURIComponent(barcode)}`)
  return res.data
}
