import { useTranslation } from 'react-i18next'
import type { FlavorTag } from '../../types'
import { Badge } from '@/components/ui/badge'

/**
 * The score summary above a list of reviews: average out of 100, review count and the top flavors.
 * Used for a single bottle and for a whole catalog product.
 */
export default function ReviewsAggregate({
  averageScore,
  reviewsCount,
  topFlavors,
  emptyLabel,
}: {
  averageScore: number | null
  reviewsCount: number
  topFlavors: FlavorTag[]
  /** Shown instead of the count when there are no reviews; defaults to the bottle's "be the first" invite. */
  emptyLabel?: string
}) {
  const { t } = useTranslation()

  return (
    <div className="mb-5 flex flex-wrap items-center gap-x-6 gap-y-3 rounded-md border border-primary/15 bg-primary/[0.04] p-4">
      <div className="flex flex-col items-center">
        <div className="flex items-baseline gap-1">
          {averageScore != null ? (
            <>
              <span className="font-heading text-4xl font-bold text-primary">{averageScore.toFixed(1)}</span>
              <span className="text-sm text-muted-foreground">/100</span>
            </>
          ) : (
            <span className="font-heading text-4xl font-bold text-muted-foreground">—</span>
          )}
        </div>
        {averageScore != null && (
          <span className="text-[10px] uppercase tracking-wide text-muted-foreground">
            {t('reviews.average')}
          </span>
        )}
      </div>

      <div className="flex flex-col gap-1.5">
        <span className="text-sm text-muted-foreground">
          {reviewsCount > 0 ? t('reviews.count', { count: reviewsCount }) : (emptyLabel ?? t('reviews.invite'))}
        </span>
        {topFlavors.length > 0 && (
          <div className="flex flex-wrap gap-1.5">
            {topFlavors.map(flavor => (
              <Badge key={flavor} variant="outline" className="border-primary/30 text-primary">
                {t(`flavors.${flavor}`)}
              </Badge>
            ))}
          </div>
        )}
      </div>
    </div>
  )
}
