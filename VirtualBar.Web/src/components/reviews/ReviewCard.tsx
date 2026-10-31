import { useTranslation } from 'react-i18next'
import type { BottleReview } from '../../types'
import { NOTE_FIELDS, formatRelativeTime, hasAnyNote } from '../../lib/reviewFormat'
import Avatar from '../Avatar'
import { Badge } from '@/components/ui/badge'

export default function ReviewCard({ review }: { review: BottleReview }) {
  const { t } = useTranslation()
  const edited = new Date(review.updatedAt).getTime() > new Date(review.createdAt).getTime()

  return (
    <div className="flex flex-col gap-2.5 rounded-md border border-primary/10 bg-primary/[0.03] p-4">
      <div className="flex items-start justify-between gap-2">
        <div className="flex items-center gap-2.5">
          <Avatar displayName={review.userDisplayName} avatarUrl={review.userAvatarUrl} size={32} />
          <div className="flex flex-col">
            <span className="text-sm font-medium text-primary">{review.userDisplayName}</span>
            <span className="text-xs text-muted-foreground">
              {formatRelativeTime(review.createdAt, t)}
              {edited && ` · ${t('reviews.edited')}`}
            </span>
          </div>
        </div>
        <Badge variant="outline" className="border-primary/40 text-primary">
          <span className="font-heading text-sm font-semibold">{review.score}</span>
          <span className="text-muted-foreground">/100</span>
        </Badge>
      </div>

      {hasAnyNote(review) && (
        <div className="flex flex-col gap-1.5">
          {NOTE_FIELDS.map(field => {
            const value = review[field]
            if (!value || !value.trim()) return null
            return (
              <div key={field} className="text-sm leading-relaxed">
                <span className="mr-1.5 text-xs font-medium uppercase tracking-wide text-muted-foreground">
                  {t(`reviews.${field}`)}
                </span>
                <span className="text-foreground">{value}</span>
              </div>
            )
          })}
        </div>
      )}

      {review.flavors.length > 0 && (
        <div className="flex flex-wrap gap-1.5">
          {review.flavors.map(flavor => (
            <Badge key={flavor} variant="secondary">
              {t(`flavors.${flavor}`)}
            </Badge>
          ))}
        </div>
      )}
    </div>
  )
}
