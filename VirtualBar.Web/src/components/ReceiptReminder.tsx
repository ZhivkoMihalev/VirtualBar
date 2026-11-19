import { useState } from 'react'
import { Link } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { PackageCheck } from 'lucide-react'
import { confirmReceipt, getSentOffers } from '../api/offersApi'
import { useAuth } from '../contexts/useAuth'
import { Button } from '@/components/ui/button'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'

/** The session whose reminder the buyer put off with "Not yet". A new login is a new token, so it asks again. */
export const RECEIPT_REMINDER_KEY = 'vbar_receipt_reminder_dismissed'

function readDismissed(): string | null {
  try {
    return sessionStorage.getItem(RECEIPT_REMINDER_KEY)
  } catch {
    return null
  }
}

function writeDismissed(token: string) {
  try {
    sessionStorage.setItem(RECEIPT_REMINDER_KEY, token)
  } catch {
    // Without storage the reminder only stays closed until the page reloads.
  }
}

/**
 * Asks a buyer whose accepted offers wait for them (#130) whether the bottle arrived, when they log in and when the
 * app opens with a saved session. "Получих бутилката" completes that deal; "Още не" puts the reminder off until the
 * next login. It renders nothing for a visitor, or when no purchase waits.
 */
export default function ReceiptReminder() {
  const { t } = useTranslation()
  const { isAuthenticated, token } = useAuth()
  const queryClient = useQueryClient()
  const [dismissed, setDismissed] = useState(readDismissed)

  const { data: sent = [] } = useQuery({
    queryKey: ['offers', 'sent'],
    queryFn: getSentOffers,
    enabled: isAuthenticated,
  })

  const confirmMutation = useMutation({
    mutationFn: (id: string) => confirmReceipt(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['offers'] })
      queryClient.invalidateQueries({ queryKey: ['bottles'] })
      queryClient.invalidateQueries({ queryKey: ['collectionValue'] })
    },
  })

  const awaiting = sent.filter(offer => offer.status === 'AwaitingReceipt')
  const open = isAuthenticated && token != null && dismissed !== token && awaiting.length > 0

  const putOff = () => {
    if (!token) return
    writeDismissed(token)
    setDismissed(token)
  }

  return (
    <Dialog open={open} onOpenChange={next => !next && putOff()}>
      <DialogContent className="w-[calc(100%-2rem)] max-w-[480px] sm:max-w-[480px]">
        <DialogHeader>
          <DialogTitle className="font-heading text-xl text-primary">{t('offers.reminderTitle')}</DialogTitle>
          <DialogDescription>{t('offers.reminderText')}</DialogDescription>
        </DialogHeader>

        <ul className="flex flex-col gap-3">
          {awaiting.map(offer => (
            <li
              key={offer.id}
              className="flex flex-wrap items-center justify-between gap-3 rounded-md border border-primary/15 bg-primary/[0.04] p-3"
            >
              <div className="min-w-0">
                <div className="font-heading font-semibold text-foreground">{offer.bottleName}</div>
                <div className="text-xs text-muted-foreground">
                  {t('offers.reminderFrom', {
                    name: offer.sellerDisplayName,
                    date: new Date(offer.respondedAt ?? offer.createdAt).toLocaleDateString(),
                  })}
                </div>
              </div>
              <Button size="sm" onClick={() => confirmMutation.mutate(offer.id)} disabled={confirmMutation.isPending}>
                <PackageCheck className="size-3.5" />
                {t('offers.confirmReceipt')}
              </Button>
            </li>
          ))}
        </ul>

        {confirmMutation.isError && <p className="text-sm text-destructive">{t('offers.errorRespond')}</p>}

        <DialogFooter className="gap-2 sm:justify-between">
          <Button asChild variant="link" className="px-0">
            <Link to="/offers" onClick={putOff}>
              {t('offers.reminderAll')}
            </Link>
          </Button>
          <Button variant="outline" onClick={putOff}>
            {t('offers.reminderLater')}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
