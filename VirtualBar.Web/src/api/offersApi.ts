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

/** The buyer confirms receiving the bottle of an accepted deal: it moves to their bar (#130). */
export const confirmReceipt = (id: string) =>
  client.patch<Offer>(`/offers/${id}/confirm-receipt`).then(r => r.data)

/** Either side ends an accepted deal before the buyer confirmed receiving the bottle (#130). */
export const cancelDeal = (id: string) =>
  client.patch<Offer>(`/offers/${id}/cancel`).then(r => r.data)
