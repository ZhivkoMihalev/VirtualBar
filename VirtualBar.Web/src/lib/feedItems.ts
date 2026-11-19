import type { FeedItem } from '../types'

/** The key a feed item renders under: one item, one key, on whichever page of the feed it arrives (#127). */
export function feedItemKey(item: FeedItem): string {
  if (item.type === 'News') return `news-${item.postId}`
  if (item.type === 'Reviewed') return `review-${item.reviewId}`
  return `${item.type}-${item.bottleId}`
}

/**
 * The feed's pages as one list with every item once. An item that arrives while the collector reads pushes the older
 * ones down, so the next page can start with items already shown; the first one shown stays (#127).
 */
export function uniqueFeedItems(pages: FeedItem[][]): FeedItem[] {
  const seen = new Set<string>()
  return pages.flat().filter(item => {
    const key = feedItemKey(item)
    if (seen.has(key)) return false
    seen.add(key)
    return true
  })
}
