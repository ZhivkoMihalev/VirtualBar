import type { ReactNode } from 'react'
import { Navigate } from 'react-router-dom'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import type { TFunction } from 'i18next'
import { isAxiosError } from 'axios'
import { Check, X, Undo2, PackageCheck, Ban } from 'lucide-react'
import { useAuth } from '../contexts/useAuth'
import NavBar from '../components/NavBar'
import {
  getReceivedOffers,
  getSentOffers,
  acceptOffer,
  declineOffer,
  withdrawOffer,
  confirmReceipt,
  cancelDeal,
} from '../api/offersApi'
import type { ApiErrorBody, Offer, OfferStatus } from '../types'
import { cn } from '@/lib/utils'
import { Button } from '@/components/ui/button'
import { Badge } from '@/components/ui/badge'
import { Card, CardContent } from '@/components/ui/card'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs'

/** The `reason` the API sends when every bottle of an entry is reserved for buyers (`OfferErrorReasons`). */
const NO_BOTTLE_LEFT = 'NoBottleLeft'

const STATUS_KEYS: Record<OfferStatus, string> = {
  Pending: 'offers.statusPending',
  AwaitingReceipt: 'offers.statusAwaitingReceipt',
  Accepted: 'offers.statusAccepted',
  Declined: 'offers.statusDeclined',
  Withdrawn: 'offers.statusWithdrawn',
  Cancelled: 'offers.statusCancelled',
}

const STATUS_VARIANTS: Record<OfferStatus, 'warning' | 'success' | 'destructive' | 'secondary'> = {
  Pending: 'warning',
  AwaitingReceipt: 'warning',
  Accepted: 'success',
  Declined: 'destructive',
  Withdrawn: 'secondary',
  Cancelled: 'secondary',
}

/**
 * A deal moves bottles between bars (#130): accepting reserves one and takes it off sale, confirming moves it, and
 * cancelling frees it. So every step refreshes the offers, the bars, the marketplace and the collection values.
 */
function useDealInvalidation() {
  const queryClient = useQueryClient()
  return () => {
    queryClient.invalidateQueries({ queryKey: ['offers'] })
    queryClient.invalidateQueries({ queryKey: ['bottles'] })
    queryClient.invalidateQueries({ queryKey: ['marketplace'] })
    queryClient.invalidateQueries({ queryKey: ['collectionValue'] })
  }
}

/** A refusal says why when the API gives the reason that no bottle is left (#130); anything else is generic. */
function respondError(error: unknown, t: TFunction): string {
  return isAxiosError<ApiErrorBody>(error) && error.response?.data?.reason === NO_BOTTLE_LEFT
    ? t('offers.errorNoBottleLeft')
    : t('offers.errorRespond')
}

function ErrorBanner({ text }: { text: string }) {
  return (
    <div role="alert" className="rounded-md border border-destructive/40 bg-destructive/10 px-3 py-2 text-sm text-destructive">
      {text}
    </div>
  )
}

function formatDate(iso: string): string {
  return new Date(iso).toLocaleDateString(undefined, { year: 'numeric', month: 'short', day: 'numeric' })
}

function StateMessage({ text, error }: { text: string; error?: boolean }) {
  return (
    <div className={cn('py-16 text-center text-sm italic', error ? 'text-destructive' : 'text-muted-foreground')}>
      {text}
    </div>
  )
}

function OfferCard({
  offer,
  counterpartyName,
  actions,
  t,
}: {
  offer: Offer
  counterpartyName: string
  actions: ReactNode
  t: TFunction
}) {
  return (
    <Card>
      <CardContent className="flex flex-wrap items-start justify-between gap-4">
        <div className="min-w-[200px] flex-1">
          <div className="mb-1.5 flex flex-wrap items-center gap-2.5">
            <span className="font-heading text-lg font-semibold text-foreground">{offer.bottleName}</span>
            <Badge variant={STATUS_VARIANTS[offer.status]}>{t(STATUS_KEYS[offer.status])}</Badge>
          </div>
          <div className="mb-2.5 text-sm text-muted-foreground">
            {t('offers.by', { name: counterpartyName })}
          </div>
          {offer.message && (
            <p className="mb-2.5 text-sm italic leading-relaxed text-foreground/90">
              “{offer.message}”
            </p>
          )}
          <div className="text-xs text-muted-foreground">{formatDate(offer.createdAt)}</div>
        </div>

        <div className="flex flex-col items-end gap-3">
          <span className="whitespace-nowrap font-heading text-xl font-semibold text-success">
            {offer.currency} {offer.offeredPrice.toLocaleString()}
          </span>
          {actions}
        </div>
      </CardContent>
    </Card>
  )
}

function ReceivedTab() {
  const { t } = useTranslation()
  const invalidate = useDealInvalidation()

  const { data: offers = [], isLoading, isError } = useQuery({
    queryKey: ['offers', 'received'],
    queryFn: getReceivedOffers,
  })

  const acceptMutation = useMutation({ mutationFn: (id: string) => acceptOffer(id), onSuccess: invalidate })
  const declineMutation = useMutation({ mutationFn: (id: string) => declineOffer(id), onSuccess: invalidate })
  const cancelMutation = useMutation({ mutationFn: (id: string) => cancelDeal(id), onSuccess: invalidate })

  const pending = acceptMutation.isPending || declineMutation.isPending || cancelMutation.isPending
  const error = acceptMutation.error ?? declineMutation.error ?? cancelMutation.error

  if (isLoading) return <StateMessage text={t('offers.loading')} />
  if (isError) return <StateMessage text={t('offers.errorRespond')} error />
  if (offers.length === 0) return <StateMessage text={t('offers.emptyReceived')} />

  return (
    <div className="flex flex-col gap-4">
      {error && <ErrorBanner text={respondError(error, t)} />}
      {offers.map(offer => (
        <OfferCard
          key={offer.id}
          offer={offer}
          counterpartyName={offer.buyerDisplayName}
          t={t}
          actions={
            offer.status === 'Pending' ? (
              <div className="flex max-w-[260px] flex-col items-end gap-2">
                <div className="flex gap-2">
                  <Button size="sm" onClick={() => acceptMutation.mutate(offer.id)} disabled={pending}>
                    <Check className="size-3.5" />
                    {t('offers.accept')}
                  </Button>
                  <Button
                    size="sm"
                    variant="destructive"
                    onClick={() => declineMutation.mutate(offer.id)}
                    disabled={pending}
                  >
                    <X className="size-3.5" />
                    {t('offers.decline')}
                  </Button>
                </div>
                <p className="text-right text-xs text-muted-foreground">{t('offers.acceptHint')}</p>
              </div>
            ) : offer.status === 'AwaitingReceipt' ? (
              <div className="flex max-w-[260px] flex-col items-end gap-2">
                <p className="text-right text-xs text-muted-foreground">{t('offers.awaitingSeller')}</p>
                <Button size="sm" variant="outline" onClick={() => cancelMutation.mutate(offer.id)} disabled={pending}>
                  <Ban className="size-3.5" />
                  {t('offers.cancelDeal')}
                </Button>
              </div>
            ) : null
          }
        />
      ))}
    </div>
  )
}

function SentTab() {
  const { t } = useTranslation()
  const invalidate = useDealInvalidation()

  const { data: offers = [], isLoading, isError } = useQuery({
    queryKey: ['offers', 'sent'],
    queryFn: getSentOffers,
  })

  const withdrawMutation = useMutation({ mutationFn: (id: string) => withdrawOffer(id), onSuccess: invalidate })
  const confirmMutation = useMutation({ mutationFn: (id: string) => confirmReceipt(id), onSuccess: invalidate })
  const cancelMutation = useMutation({ mutationFn: (id: string) => cancelDeal(id), onSuccess: invalidate })

  const pending = withdrawMutation.isPending || confirmMutation.isPending || cancelMutation.isPending
  const error = withdrawMutation.error ?? confirmMutation.error ?? cancelMutation.error

  if (isLoading) return <StateMessage text={t('offers.loading')} />
  if (isError) return <StateMessage text={t('offers.errorRespond')} error />
  if (offers.length === 0) return <StateMessage text={t('offers.emptySent')} />

  return (
    <div className="flex flex-col gap-4">
      {error && <ErrorBanner text={respondError(error, t)} />}
      {offers.map(offer => (
        <OfferCard
          key={offer.id}
          offer={offer}
          counterpartyName={offer.sellerDisplayName}
          t={t}
          actions={
            offer.status === 'Pending' ? (
              <Button
                size="sm"
                variant="outline"
                onClick={() => withdrawMutation.mutate(offer.id)}
                disabled={pending}
              >
                <Undo2 className="size-3.5" />
                {t('offers.withdraw')}
              </Button>
            ) : offer.status === 'AwaitingReceipt' ? (
              <div className="flex max-w-[260px] flex-col items-end gap-2">
                <p className="text-right text-xs text-muted-foreground">{t('offers.awaitingBuyer')}</p>
                <div className="flex flex-wrap justify-end gap-2">
                  <Button size="sm" onClick={() => confirmMutation.mutate(offer.id)} disabled={pending}>
                    <PackageCheck className="size-3.5" />
                    {t('offers.confirmReceipt')}
                  </Button>
                  <Button size="sm" variant="outline" onClick={() => cancelMutation.mutate(offer.id)} disabled={pending}>
                    <Ban className="size-3.5" />
                    {t('offers.cancelDeal')}
                  </Button>
                </div>
              </div>
            ) : null
          }
        />
      ))}
    </div>
  )
}

export default function OffersPage() {
  const { t } = useTranslation()
  const { user } = useAuth()

  if (!user) return <Navigate to="/login" replace />

  return (
    <div className="min-h-screen text-foreground">
      <NavBar />

      <main className="mx-auto max-w-[880px] px-6 py-10 sm:px-10">
        <div className="mb-7">
          <div className="mb-2 text-xs font-medium uppercase tracking-widest text-muted-foreground">
            {t('nav.offers')}
          </div>
          <h1 className="font-heading text-3xl font-bold text-primary sm:text-4xl">{t('offers.title')}</h1>
        </div>

        <Tabs defaultValue="received" className="gap-6">
          <TabsList variant="line">
            <TabsTrigger value="received">{t('offers.tabReceived')}</TabsTrigger>
            <TabsTrigger value="sent">{t('offers.tabSent')}</TabsTrigger>
          </TabsList>
          <TabsContent value="received">
            <ReceivedTab />
          </TabsContent>
          <TabsContent value="sent">
            <SentTab />
          </TabsContent>
        </Tabs>
      </main>
    </div>
  )
}
