import { client } from './client'
import type { WishListItem, PublicWishListItem } from '../types'

export interface AddWishListItemRequest {
  bottleName?: string
  distillery?: string
  category?: string
  imageUrl?: string
}

function formDataOf(file: File): FormData {
  const fd = new FormData()
  fd.append('file', file)
  return fd
}

export const getAllWishListItems = () =>
  client.get<PublicWishListItem[]>('/wishlist/all').then(r => r.data)

export const addWishListItem = (data: AddWishListItemRequest) =>
  client.post<WishListItem>('/wishlist', data).then(r => r.data)

export const removeWishListItem = (id: string) =>
  client.delete(`/wishlist/${id}`)

export const uploadWishListImage = (file: File): Promise<{ url: string }> =>
  client.post<{ url: string }>('/wishlist/image', formDataOf(file), {
    headers: { 'Content-Type': 'multipart/form-data' },
  }).then(r => r.data)
