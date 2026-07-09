import { client } from './client'
import type { CollectionValue, PriceEstimate } from '../types'

// Reads through the server-side cache. A 204 means "no estimate cached yet" → the UI renders "—".
// The request path never triggers a billed Claude call; pre-warm / async refresh populates the cache.
export const getBottleEstimate = (bottleId: string): Promise<PriceEstimate | null> =>
  client
    .get<PriceEstimate>(`/prices/bottle/${bottleId}`)
    .then(r => (r.status === 204 || !r.data ? null : r.data))

export const getCollectionValue = () =>
  client.get<CollectionValue>('/prices/collection').then(r => r.data)
