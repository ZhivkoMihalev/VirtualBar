import { client } from './client'
import type {
  ApproveProductRequestPayload,
  CreateProductRequestPayload,
  ProductRequest,
  ProductRequestStatus,
  RejectProductRequestPayload,
} from '../types'

export async function createProductRequest(
  payload: CreateProductRequestPayload,
): Promise<ProductRequest> {
  const { data } = await client.post<ProductRequest>('/product-requests', payload)
  return data
}

export async function getMyProductRequests(): Promise<ProductRequest[]> {
  const { data } = await client.get<ProductRequest[]>('/product-requests/mine')
  return data
}

export async function withdrawProductRequest(id: string): Promise<void> {
  await client.delete(`/product-requests/${id}`)
}

export async function getProductRequests(status?: ProductRequestStatus): Promise<ProductRequest[]> {
  const { data } = await client.get<ProductRequest[]>('/product-requests', {
    params: status ? { status } : undefined,
  })
  return data
}

export async function approveProductRequest(
  id: string,
  payload: ApproveProductRequestPayload,
): Promise<ProductRequest> {
  const { data } = await client.patch<ProductRequest>(`/product-requests/${id}/approve`, payload)
  return data
}

export async function rejectProductRequest(
  id: string,
  payload: RejectProductRequestPayload,
): Promise<ProductRequest> {
  const { data } = await client.patch<ProductRequest>(`/product-requests/${id}/reject`, payload)
  return data
}
