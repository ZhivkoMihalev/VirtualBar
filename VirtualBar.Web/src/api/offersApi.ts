import { client } from './client'
import type { Offer } from '../types'

export interface CreateOfferRequest {
  bottleId: string
  offeredPrice: number
  currency: string
  message?: string
}

export const createOffer = (data: CreateOfferRequest) =>
  client.post<Offer>('/offers', data).then(r => r.data)

export const getReceivedOffers = () =>
  client.get<Offer[]>('/offers/received').then(r => r.data)

export const getSentOffers = () =>
  client.get<Offer[]>('/offers/sent').then(r => r.data)

export const acceptOffer = (id: string) =>
  client.patch<Offer>(`/offers/${id}/accept`).then(r => r.data)

export const declineOffer = (id: string) =>
  client.patch<Offer>(`/offers/${id}/decline`).then(r => r.data)

export const withdrawOffer = (id: string) =>
  client.patch<Offer>(`/offers/${id}/withdraw`).then(r => r.data)
