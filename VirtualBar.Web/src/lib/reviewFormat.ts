import type { TFunction } from 'i18next'
import type { BottleReview } from '../types'

/** The free-text tasting notes of a review, in display order. */
export const NOTE_FIELDS = ['nose', 'palate', 'finish', 'summary'] as const

export type NoteField = (typeof NOTE_FIELDS)[number]

export function hasAnyNote(review: BottleReview): boolean {
  return NOTE_FIELDS.some(field => (review[field] ?? '').trim() !== '')
}

export function formatRelativeTime(iso: string, t: TFunction): string {
  const seconds = Math.floor((Date.now() - new Date(iso).getTime()) / 1000)

  if (seconds < 60) return t('bottle.justNow')

  const units: { limit: number; div: number; key: string }[] = [
    { limit: 3600, div: 60, key: 'minutesAgo' },
    { limit: 86400, div: 3600, key: 'hoursAgo' },
    { limit: 604800, div: 86400, key: 'daysAgo' },
    { limit: 2592000, div: 604800, key: 'weeksAgo' },
    { limit: 31536000, div: 2592000, key: 'monthsAgo' },
    { limit: Infinity, div: 31536000, key: 'yearsAgo' },
  ]

  for (const unit of units) {
    if (seconds < unit.limit) {
      const count = Math.floor(seconds / unit.div)
      return t(`bottle.${unit.key}`, { count })
    }
  }

  return t('bottle.justNow')
}
