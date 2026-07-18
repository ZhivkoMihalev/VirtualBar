import { client } from './client'
import type { BottleReview, BottleReviewsSummary, ReviewPayload } from '../types'

export async function getReviews(bottleId: string): Promise<BottleReviewsSummary> {
  const res = await client.get<BottleReviewsSummary>(`/bottles/${bottleId}/reviews`)
  return res.data
}

export async function addReview(bottleId: string, payload: ReviewPayload): Promise<BottleReview> {
  const res = await client.post<BottleReview>(`/bottles/${bottleId}/reviews`, payload)
  return res.data
}

export async function updateReview(
  bottleId: string,
  reviewId: string,
  payload: ReviewPayload,
): Promise<BottleReview> {
  const res = await client.put<BottleReview>(`/bottles/${bottleId}/reviews/${reviewId}`, payload)
  return res.data
}

export async function deleteReview(bottleId: string, reviewId: string): Promise<void> {
  await client.delete(`/bottles/${bottleId}/reviews/${reviewId}`)
}
