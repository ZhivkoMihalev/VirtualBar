import { useTranslation } from 'react-i18next'
import type { PriceConfidence, PriceEstimate } from '../types'
import { Badge } from '@/components/ui/badge'

const CONFIDENCE_VARIANT: Record<PriceConfidence, 'success' | 'secondary' | 'outline'> = {
  High: 'success',
  Medium: 'secondary',
  Low: 'outline',
}

/**
 * An indicative market value: min–max range, confidence, where it came from, "as of" date and the
 * cited sources. `null` (nothing cached yet) renders "—", never a made-up number. Data loading stays
 * with the caller, so a bottle and a catalog product can share this view.
 */
export default function PriceEstimateView({
  estimate,
  isLoading,
}: {
  estimate: PriceEstimate | null | undefined
  isLoading: boolean
}) {
  const { t, i18n } = useTranslation()

  const formatDate = (iso: string) =>
    new Date(iso).toLocaleDateString(i18n.language === 'bg' ? 'bg-BG' : 'en-GB', { dateStyle: 'medium' })

  return (
    <div className="mb-5 rounded-md border border-primary/15 bg-primary/[0.04] p-4">
      <div className="mb-2 flex items-center justify-between gap-2">
        <span className="text-xs font-medium uppercase tracking-wide text-muted-foreground">
          {t('collectionValue.estimateLabel')}
        </span>
        <Badge variant="outline" className="text-[10px] uppercase">
          {t('collectionValue.indicative')}
        </Badge>
      </div>

      {isLoading ? (
        <div className="text-sm text-muted-foreground">{t('collectionValue.loading')}</div>
      ) : !estimate ? (
        <div className="flex items-baseline gap-2">
          <span className="font-heading text-2xl font-semibold text-muted-foreground">
            {t('collectionValue.empty')}
          </span>
          <span className="text-xs text-muted-foreground/80">{t('collectionValue.noEstimate')}</span>
        </div>
      ) : (
        <>
          <div className="flex flex-wrap items-center gap-3">
            <span className="font-heading text-2xl font-semibold text-primary">
              {t('collectionValue.range', {
                currency: estimate.currency,
                low: (estimate.lowEstimate ?? estimate.estimatedPrice).toLocaleString(),
                high: (estimate.highEstimate ?? estimate.estimatedPrice).toLocaleString(),
              })}
            </span>
            <Badge variant={CONFIDENCE_VARIANT[estimate.confidence]}>
              {t('collectionValue.confidenceLabel')}: {t(`collectionValue.confidence${estimate.confidence}`)}
            </Badge>
          </div>

          <div className="mt-1.5 text-xs text-muted-foreground">
            {t('collectionValue.sourceLabel')}:{' '}
            {estimate.source === 'ClaudeResearch'
              ? t('collectionValue.sourceResearched')
              : t('collectionValue.sourceCommunity')}
            {' · '}
            {t('collectionValue.asOf', { date: formatDate(estimate.asOf) })}
          </div>

          {estimate.sources.length > 0 && (
            <div className="mt-3 border-t border-primary/10 pt-3">
              <div className="mb-1.5 text-xs font-medium uppercase tracking-wide text-muted-foreground">
                {t('collectionValue.sources')}
              </div>
              <ul className="flex flex-col gap-1">
                {estimate.sources.map(source => (
                  <li key={source.url}>
                    <a
                      href={source.url}
                      target="_blank"
                      rel="noopener noreferrer"
                      className="break-all text-sm text-primary underline underline-offset-2 hover:text-primary/80"
                    >
                      {source.title}
                    </a>
                  </li>
                ))}
              </ul>
            </div>
          )}
        </>
      )}
    </div>
  )
}
