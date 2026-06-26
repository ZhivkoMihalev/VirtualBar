import { useState } from 'react'
import type { CSSProperties } from 'react'
import { Navigate } from 'react-router-dom'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import type { TFunction } from 'i18next'
import { useAuth } from '../contexts/AuthContext'
import NavBar from '../components/NavBar'
import {
  getReceivedOffers,
  getSentOffers,
  acceptOffer,
  declineOffer,
  withdrawOffer,
} from '../api/offersApi'
import type { Offer, OfferStatus } from '../types'

type Tab = 'received' | 'sent'

const STATUS_COLORS: Record<OfferStatus, { fg: string; bg: string; border: string }> = {
  Pending: { fg: '#E8C870', bg: 'rgba(201,168,76,0.12)', border: 'rgba(201,168,76,0.45)' },
  Accepted: { fg: '#6ABF8A', bg: 'rgba(74,154,106,0.12)', border: 'rgba(74,154,106,0.45)' },
  Declined: { fg: '#D46A6A', bg: 'rgba(192,64,64,0.12)', border: 'rgba(192,64,64,0.45)' },
  Withdrawn: { fg: '#9A8E78', bg: 'rgba(154,142,120,0.12)', border: 'rgba(154,142,120,0.35)' },
}

const STATUS_KEYS: Record<OfferStatus, string> = {
  Pending: 'offers.statusPending',
  Accepted: 'offers.statusAccepted',
  Declined: 'offers.statusDeclined',
  Withdrawn: 'offers.statusWithdrawn',
}

const tabRowStyle: CSSProperties = {
  display: 'flex',
  gap: 8,
  marginBottom: 32,
  borderBottom: '1px solid rgba(201,168,76,0.12)',
}

const cardStyle: CSSProperties = {
  padding: '20px 24px',
  background: 'rgba(201,168,76,0.04)',
  border: '1px solid rgba(201,168,76,0.12)',
  borderRadius: 6,
}

const acceptBtnStyle: CSSProperties = {
  fontFamily: 'Cinzel, serif',
  fontSize: 10,
  letterSpacing: '0.15em',
  textTransform: 'uppercase',
  color: '#07030A',
  background: 'linear-gradient(135deg, #C9A84C, #E8C870)',
  border: 'none',
  padding: '9px 18px',
  borderRadius: 2,
}

const declineBtnStyle: CSSProperties = {
  fontFamily: 'Cinzel, serif',
  fontSize: 10,
  letterSpacing: '0.15em',
  textTransform: 'uppercase',
  color: '#C04040',
  background: 'transparent',
  border: '1px solid rgba(192,64,64,0.45)',
  padding: '9px 18px',
  borderRadius: 2,
}

const withdrawBtnStyle: CSSProperties = {
  fontFamily: 'Cinzel, serif',
  fontSize: 10,
  letterSpacing: '0.15em',
  textTransform: 'uppercase',
  color: '#B09868',
  background: 'transparent',
  border: '1px solid rgba(201,168,76,0.3)',
  padding: '9px 18px',
  borderRadius: 2,
}

function formatDate(iso: string): string {
  return new Date(iso).toLocaleDateString(undefined, { year: 'numeric', month: 'short', day: 'numeric' })
}

function TabButton({ label, active, onClick }: { label: string; active: boolean; onClick: () => void }) {
  return (
    <button
      role="tab"
      aria-selected={active}
      onClick={onClick}
      style={{
        fontFamily: 'Cinzel, serif',
        fontSize: 12,
        letterSpacing: '0.2em',
        textTransform: 'uppercase',
        color: active ? '#E8C870' : '#7A6040',
        background: 'transparent',
        border: 'none',
        borderBottom: active ? '2px solid #C9A84C' : '2px solid transparent',
        padding: '12px 20px',
        cursor: 'pointer',
        marginBottom: -1,
      }}
    >
      {label}
    </button>
  )
}

function StatusBadge({ status, t }: { status: OfferStatus; t: TFunction }) {
  const c = STATUS_COLORS[status]
  return (
    <span style={{
      fontFamily: 'Cinzel, serif',
      fontSize: 9,
      letterSpacing: '0.15em',
      textTransform: 'uppercase',
      color: c.fg,
      background: c.bg,
      border: `1px solid ${c.border}`,
      padding: '4px 12px',
      borderRadius: 12,
      whiteSpace: 'nowrap',
    }}>
      {t(STATUS_KEYS[status])}
    </span>
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
  actions: React.ReactNode
  t: TFunction
}) {
  return (
    <div style={cardStyle}>
      <div style={{ display: 'flex', alignItems: 'flex-start', justifyContent: 'space-between', gap: 16, flexWrap: 'wrap' }}>
        <div style={{ flex: 1, minWidth: 200 }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 10, marginBottom: 6, flexWrap: 'wrap' }}>
            <span style={{ fontFamily: 'Playfair Display, serif', fontSize: 20, fontWeight: 700, color: '#E8C870' }}>
              {offer.bottleName}
            </span>
            <StatusBadge status={offer.status} t={t} />
          </div>
          <div style={{ fontFamily: 'Cormorant Garamond, serif', fontSize: 16, fontStyle: 'italic', color: '#C9A84C', marginBottom: 10 }}>
            {t('offers.by', { name: counterpartyName })}
          </div>
          {offer.message && (
            <p style={{ fontFamily: 'Cormorant Garamond, serif', fontSize: 16, color: '#E8D4A0', lineHeight: 1.55, margin: '0 0 10px' }}>
              “{offer.message}”
            </p>
          )}
          <div style={{ fontFamily: 'Cormorant Garamond, serif', fontSize: 13, color: '#7A6040' }}>
            {formatDate(offer.createdAt)}
          </div>
        </div>

        <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'flex-end', gap: 12 }}>
          <span style={{ fontFamily: 'Playfair Display, serif', fontSize: 24, fontWeight: 700, color: '#6ABF8A', whiteSpace: 'nowrap' }}>
            {offer.currency} {offer.offeredPrice.toLocaleString()}
          </span>
          {actions}
        </div>
      </div>
    </div>
  )
}

function ReceivedTab() {
  const { t } = useTranslation()
  const queryClient = useQueryClient()

  const { data: offers = [], isLoading, isError } = useQuery({
    queryKey: ['offers', 'received'],
    queryFn: getReceivedOffers,
  })

  const acceptMutation = useMutation({
    mutationFn: (id: string) => acceptOffer(id),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['offers', 'received'] }),
  })

  const declineMutation = useMutation({
    mutationFn: (id: string) => declineOffer(id),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['offers', 'received'] }),
  })

  const pending = acceptMutation.isPending || declineMutation.isPending

  if (isLoading) return <StateMessage text={t('offers.loading')} />
  if (isError) return <StateMessage text={t('offers.errorRespond')} error />
  if (offers.length === 0) return <StateMessage text={t('offers.emptyReceived')} />

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      {(acceptMutation.isError || declineMutation.isError) && (
        <div style={{ fontFamily: 'Cormorant Garamond, serif', fontSize: 15, color: '#C04040' }}>
          {t('offers.errorRespond')}
        </div>
      )}
      {offers.map(offer => (
        <OfferCard
          key={offer.id}
          offer={offer}
          counterpartyName={offer.buyerDisplayName}
          t={t}
          actions={offer.status === 'Pending' ? (
            <div style={{ display: 'flex', gap: 8 }}>
              <button
                onClick={() => acceptMutation.mutate(offer.id)}
                disabled={pending}
                style={{ ...acceptBtnStyle, cursor: pending ? 'wait' : 'pointer', opacity: pending ? 0.6 : 1 }}
              >
                {t('offers.accept')}
              </button>
              <button
                onClick={() => declineMutation.mutate(offer.id)}
                disabled={pending}
                style={{ ...declineBtnStyle, cursor: pending ? 'wait' : 'pointer', opacity: pending ? 0.6 : 1 }}
              >
                {t('offers.decline')}
              </button>
            </div>
          ) : null}
        />
      ))}
    </div>
  )
}

function SentTab() {
  const { t } = useTranslation()
  const queryClient = useQueryClient()

  const { data: offers = [], isLoading, isError } = useQuery({
    queryKey: ['offers', 'sent'],
    queryFn: getSentOffers,
  })

  const withdrawMutation = useMutation({
    mutationFn: (id: string) => withdrawOffer(id),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['offers', 'sent'] }),
  })

  if (isLoading) return <StateMessage text={t('offers.loading')} />
  if (isError) return <StateMessage text={t('offers.errorRespond')} error />
  if (offers.length === 0) return <StateMessage text={t('offers.emptySent')} />

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      {withdrawMutation.isError && (
        <div style={{ fontFamily: 'Cormorant Garamond, serif', fontSize: 15, color: '#C04040' }}>
          {t('offers.errorRespond')}
        </div>
      )}
      {offers.map(offer => (
        <OfferCard
          key={offer.id}
          offer={offer}
          counterpartyName={offer.sellerDisplayName}
          t={t}
          actions={offer.status === 'Pending' ? (
            <button
              onClick={() => withdrawMutation.mutate(offer.id)}
              disabled={withdrawMutation.isPending}
              style={{ ...withdrawBtnStyle, cursor: withdrawMutation.isPending ? 'wait' : 'pointer', opacity: withdrawMutation.isPending ? 0.6 : 1 }}
            >
              {t('offers.withdraw')}
            </button>
          ) : null}
        />
      ))}
    </div>
  )
}

function StateMessage({ text, error }: { text: string; error?: boolean }) {
  return (
    <div style={{
      textAlign: 'center',
      padding: '60px 0',
      fontFamily: 'Cormorant Garamond, serif',
      fontSize: 18,
      fontStyle: 'italic',
      color: error ? '#C04040' : '#B09868',
    }}>
      {text}
    </div>
  )
}

export default function OffersPage() {
  const { t } = useTranslation()
  const { user, isLoading } = useAuth()
  const [tab, setTab] = useState<Tab>('received')

  if (isLoading) return null
  if (!user) return <Navigate to="/login" replace />

  return (
    <div style={{ minHeight: '100vh', color: '#F0DDB4' }}>
      <NavBar />

      <main style={{ maxWidth: 880, margin: '0 auto', padding: '40px 40px' }}>
        <div style={{ marginBottom: 28 }}>
          <div style={{ fontFamily: 'Cinzel, serif', fontSize: 13, letterSpacing: '0.4em', color: '#B09868', marginBottom: 8 }}>
            {t('nav.offers')}
          </div>
          <h1 style={{ fontFamily: 'Playfair Display, serif', fontSize: 38, fontWeight: 700, color: '#E8C870', margin: 0, lineHeight: 1.1 }}>
            {t('offers.title')}
          </h1>
        </div>

        <div style={tabRowStyle} role="tablist">
          <TabButton label={t('offers.tabReceived')} active={tab === 'received'} onClick={() => setTab('received')} />
          <TabButton label={t('offers.tabSent')} active={tab === 'sent'} onClick={() => setTab('sent')} />
        </div>

        {tab === 'received' ? <ReceivedTab /> : <SentTab />}
      </main>
    </div>
  )
}
