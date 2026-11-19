import { describe, expect, it } from 'vitest'
import { feedItemKey, uniqueFeedItems } from '@/lib/feedItems'
import type { FeedItem } from '@/types'
import { reviewedItem } from '../fixtures/feed'

const news = (postId: string): FeedItem => ({ type: 'News', timestamp: '2026-11-01T10:00:00Z', postId })
const bottle = (type: 'NewBottle' | 'ForSale', bottleId: string): FeedItem => ({
  type,
  timestamp: '2026-11-01T10:00:00Z',
  bottleId,
})

describe('feedItemKey', () => {
  it('keys news by post, reviews by review, and bottle activity by type and bottle', () => {
    expect(feedItemKey(news('p1'))).toBe('news-p1')
    expect(feedItemKey(reviewedItem({ reviewId: 'r1' }))).toBe('review-r1')
    expect(feedItemKey(bottle('NewBottle', 'b1'))).toBe('NewBottle-b1')
    expect(feedItemKey(bottle('ForSale', 'b1'))).toBe('ForSale-b1')
  })
})

describe('uniqueFeedItems (#127)', () => {
  it('joins the pages in order and keeps the first of each repeated item', () => {
    const first = news('p1')
    const repeated = bottle('ForSale', 'b1')

    const items = uniqueFeedItems([
      [first, repeated],
      [{ ...repeated, timestamp: '2026-10-31T10:00:00Z' }, bottle('NewBottle', 'b1')],
    ])

    expect(items).toEqual([first, repeated, bottle('NewBottle', 'b1')])
  })

  it('returns nothing for no pages', () => {
    expect(uniqueFeedItems([])).toEqual([])
  })
})
