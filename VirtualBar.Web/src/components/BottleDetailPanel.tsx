import { useState } from 'react'
import type { CSSProperties } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import type { TFunction } from 'i18next'
import { toggleBottleLike, getBottleComments, addBottleComment, deleteBottleComment, listBottleForSale, unlistBottleFromSale, removeBottle } from '../api/bottlesApi'
import { createOffer } from '../api/offersApi'
import type { Bottle } from '../types'
import { CATEGORY_COLORS, BottleSvg } from './BarShelf'

const inputStyle: CSSProperties = {
  background: '#0A0502',
  border: '1px solid rgba(201,168,76,0.2)',
  color: '#F0DDB4',
  fontFamily: 'Cormorant Garamond, serif',
  fontSize: 16,
  padding: '10px 14px',
  borderRadius: 4,
  outline: 'none',
  width: '100%',
}

function focusOn(e: React.FocusEvent<HTMLInputElement | HTMLTextAreaElement>) {
  e.currentTarget.style.border = '1px solid rgba(201,168,76,0.5)'
}

function focusOff(e: React.FocusEvent<HTMLInputElement | HTMLTextAreaElement>) {
  e.currentTarget.style.border = '1px solid rgba(201,168,76,0.2)'
}

function formatRelativeTime(iso: string, t: TFunction): string {
  const seconds = Math.floor((Date.now() - new Date(iso).getTime()) / 1000)

  if (seconds < 60) return t('bottle.justNow')

  const units: { limit: number; div: number; key: string }[] = [
    { limit: 3600,      div: 60,      key: 'minutesAgo' },
    { limit: 86400,     div: 3600,    key: 'hoursAgo'   },
    { limit: 604800,    div: 86400,   key: 'daysAgo'    },
    { limit: 2592000,   div: 604800,  key: 'weeksAgo'   },
    { limit: 31536000,  div: 2592000, key: 'monthsAgo'  },
    { limit: Infinity,  div: 31536000, key: 'yearsAgo'  },
  ]

  for (const unit of units) {
    if (seconds < unit.limit) {
      const count = Math.floor(seconds / unit.div)
      return t(`bottle.${unit.key}`, { count })
    }
  }

  return t('bottle.justNow')
}

const sectionLabelStyle: CSSProperties = {
  fontFamily: 'Cinzel, serif',
  fontSize: 9,
  letterSpacing: '0.2em',
  color: '#7A6040',
  textTransform: 'uppercase',
  marginBottom: 12,
}

function LikesSection({ bottle, userId }: { bottle: Bottle; userId: string }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()

  const mutation = useMutation({
    mutationFn: () => toggleBottleLike(bottle.id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['bottles', userId] })
    },
  })

  const liked = bottle.likedByMe

  return (
    <div
      style={{
        paddingTop: 24,
        marginTop: 4,
        borderTop: '1px solid rgba(201,168,76,0.1)',
        display: 'flex',
        alignItems: 'center',
        gap: 12,
      }}
    >
      <button
        onClick={() => mutation.mutate()}
        disabled={mutation.isPending}
        aria-label={liked ? 'Unlike' : 'Like'}
        style={{
          display: 'flex',
          alignItems: 'center',
          gap: 8,
          background: 'transparent',
          border: 'none',
          cursor: mutation.isPending ? 'wait' : 'pointer',
          padding: 0,
          opacity: mutation.isPending ? 0.6 : 1,
          transition: 'opacity 0.2s ease',
        }}
      >
        <span
          style={{
            fontSize: 22,
            lineHeight: 1,
            color: liked ? '#E8C870' : 'transparent',
            WebkitTextStroke: liked ? '0' : '1.3px #C9A84C',
            textShadow: liked ? '0 0 10px rgba(201,168,76,0.6)' : 'none',
            transition: 'all 0.2s ease',
          }}
        >
          ♥
        </span>
        <span style={{ fontFamily: 'Cormorant Garamond, serif', fontSize: 17, color: '#E8D4A0' }}>
          {t('bottle.likes', { count: bottle.likesCount })}
        </span>
      </button>
    </div>
  )
}

function CommentsSection({ bottle, currentUserId }: { bottle: Bottle; currentUserId: string }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const [draft, setDraft] = useState('')

  const { data: comments = [], isLoading, isError } = useQuery({
    queryKey: ['comments', bottle.id],
    queryFn: () => getBottleComments(bottle.id),
  })

  const addMutation = useMutation({
    mutationFn: (content: string) => addBottleComment(bottle.id, content),
    onSuccess: () => {
      setDraft('')
      queryClient.invalidateQueries({ queryKey: ['comments', bottle.id] })
      queryClient.invalidateQueries({ queryKey: ['bottles', bottle.userId] })
    },
  })

  const deleteMutation = useMutation({
    mutationFn: (commentId: string) => deleteBottleComment(bottle.id, commentId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['comments', bottle.id] })
      queryClient.invalidateQueries({ queryKey: ['bottles', bottle.userId] })
    },
  })

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault()
    const content = draft.trim()
    if (!content) return
    addMutation.mutate(content)
  }

  return (
    <div style={{ paddingTop: 24, marginTop: 24, borderTop: '1px solid rgba(201,168,76,0.1)' }}>
      <div style={sectionLabelStyle}>{t('bottle.comments')}</div>

      <div style={{ maxHeight: 320, overflowY: 'auto', display: 'flex', flexDirection: 'column', gap: 14 }}>
        {isLoading && (
          <div style={{ fontFamily: 'Cormorant Garamond, serif', fontSize: 15, color: '#B09868' }}>
            {t('bottle.loadingComments')}
          </div>
        )}

        {isError && (
          <div style={{ fontFamily: 'Cormorant Garamond, serif', fontSize: 15, color: '#C04040' }}>
            {t('bottle.errorComments')}
          </div>
        )}

        {!isLoading && !isError && comments.length === 0 && (
          <div style={{ fontFamily: 'Cormorant Garamond, serif', fontSize: 16, fontStyle: 'italic', color: '#B09868' }}>
            {t('bottle.noComments')}
          </div>
        )}

        {!isLoading && !isError && comments.map(comment => (
          <div key={comment.id} style={{ display: 'flex', gap: 4, flexDirection: 'column' }}>
            <div style={{ display: 'flex', alignItems: 'baseline', justifyContent: 'space-between', gap: 8 }}>
              <div style={{ display: 'flex', alignItems: 'baseline', gap: 8 }}>
                <span style={{ fontFamily: 'Cormorant Garamond, serif', fontSize: 16, color: '#E8C870' }}>
                  {comment.userDisplayName}
                </span>
                <span style={{ fontFamily: 'Cormorant Garamond, serif', fontSize: 13, color: '#7A6040' }}>
                  {formatRelativeTime(comment.createdAt, t)}
                </span>
              </div>
              {comment.userId === currentUserId && (
                <button
                  onClick={() => deleteMutation.mutate(comment.id)}
                  disabled={deleteMutation.isPending}
                  aria-label="Delete comment"
                  style={{
                    background: 'transparent',
                    border: 'none',
                    color: '#7A6040',
                    fontSize: 16,
                    lineHeight: 1,
                    cursor: deleteMutation.isPending ? 'wait' : 'pointer',
                    padding: '0 2px',
                  }}
                >
                  ×
                </button>
              )}
            </div>
            <p style={{ fontFamily: 'Cormorant Garamond, serif', fontSize: 16, color: '#E8D4A0', lineHeight: 1.5, margin: 0 }}>
              {comment.content}
            </p>
          </div>
        ))}
      </div>

      <form onSubmit={handleSubmit} style={{ marginTop: 16 }}>
        <textarea
          value={draft}
          onChange={e => setDraft(e.target.value)}
          onFocus={focusOn}
          onBlur={focusOff}
          rows={2}
          placeholder={t('bottle.commentPlaceholder')}
          style={{ ...inputStyle, resize: 'vertical', marginBottom: 10 }}
        />
        {addMutation.isError && (
          <div style={{ fontFamily: 'Cormorant Garamond, serif', fontSize: 14, color: '#C04040', marginBottom: 10 }}>
            {t('bottle.errorComment')}
          </div>
        )}
        <button
          type="submit"
          disabled={addMutation.isPending || !draft.trim()}
          style={{
            fontFamily: 'Cinzel, serif',
            fontSize: 11,
            letterSpacing: '0.2em',
            textTransform: 'uppercase',
            color: '#07030A',
            background: 'linear-gradient(135deg, #C9A84C, #E8C870)',
            border: 'none',
            padding: '10px 22px',
            borderRadius: 2,
            cursor: addMutation.isPending || !draft.trim() ? 'not-allowed' : 'pointer',
            opacity: addMutation.isPending || !draft.trim() ? 0.6 : 1,
          }}
        >
          {addMutation.isPending ? t('bottle.posting') : t('bottle.post')}
        </button>
      </form>
    </div>
  )
}

const CURRENCIES = ['USD', 'EUR', 'GBP', 'BGN', 'CHF', 'JPY', 'CAD', 'AUD']

function SaleSection({ bottle, userId }: { bottle: Bottle; userId: string }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const [price, setPrice] = useState(bottle.askingPrice?.toString() ?? '')
  const [currency, setCurrency] = useState(bottle.currency ?? 'USD')

  const invalidate = () => {
    queryClient.invalidateQueries({ queryKey: ['bottles', userId] })
    queryClient.invalidateQueries({ queryKey: ['marketplace'] })
  }

  const listMutation = useMutation({
    mutationFn: () => listBottleForSale(bottle.id, Number(price), currency),
    onSuccess: invalidate,
  })

  const unlistMutation = useMutation({
    mutationFn: () => unlistBottleFromSale(bottle.id),
    onSuccess: invalidate,
  })

  return (
    <div style={{
      padding: '16px 20px',
      background: bottle.isForSale ? 'rgba(74,154,106,0.06)' : 'rgba(201,168,76,0.04)',
      border: `1px solid ${bottle.isForSale ? 'rgba(74,154,106,0.25)' : 'rgba(201,168,76,0.12)'}`,
      borderRadius: 4,
      marginBottom: 20,
    }}>
      <div style={{ fontFamily: 'Cinzel, serif', fontSize: 9, letterSpacing: '0.2em', color: '#7A6040', textTransform: 'uppercase', marginBottom: 12 }}>
        {t('bottle.saleLabel')}
      </div>

      {bottle.isForSale ? (
        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 12, flexWrap: 'wrap' }}>
          <span style={{ fontFamily: 'Playfair Display, serif', fontSize: 22, color: '#6ABF8A', fontWeight: 700 }}>
            {bottle.currency ?? 'USD'} {bottle.askingPrice?.toLocaleString() ?? '—'}
          </span>
          <button
            onClick={() => unlistMutation.mutate()}
            disabled={unlistMutation.isPending}
            style={{
              fontFamily: 'Cinzel, serif',
              fontSize: 10,
              letterSpacing: '0.15em',
              color: '#C04040',
              background: 'transparent',
              border: '1px solid rgba(192,64,64,0.4)',
              padding: '8px 16px',
              borderRadius: 2,
              cursor: unlistMutation.isPending ? 'wait' : 'pointer',
              opacity: unlistMutation.isPending ? 0.6 : 1,
            }}
          >
            {unlistMutation.isPending ? '···' : t('bottle.removeFromSale')}
          </button>
        </div>
      ) : (
        <div style={{ display: 'flex', gap: 8, alignItems: 'center', flexWrap: 'wrap' }}>
          <input
            type="number"
            min={0}
            step={0.01}
            value={price}
            onChange={e => setPrice(e.target.value)}
            onFocus={focusOn}
            onBlur={focusOff}
            placeholder={t('bottle.askingPrice')}
            style={{ ...inputStyle, width: 140, flexShrink: 0 }}
          />
          <select
            value={currency}
            onChange={e => setCurrency(e.target.value)}
            style={{
              ...inputStyle,
              width: 88,
              flexShrink: 0,
              cursor: 'pointer',
              appearance: 'none' as const,
              paddingRight: 10,
            }}
          >
            {CURRENCIES.map(c => <option key={c} value={c}>{c}</option>)}
          </select>
          <button
            onClick={() => listMutation.mutate()}
            disabled={listMutation.isPending || !price || Number(price) <= 0}
            style={{
              fontFamily: 'Cinzel, serif',
              fontSize: 10,
              letterSpacing: '0.15em',
              color: '#07030A',
              background: 'linear-gradient(135deg, #C9A84C, #E8C870)',
              border: 'none',
              padding: '10px 16px',
              borderRadius: 2,
              cursor: listMutation.isPending || !price || Number(price) <= 0 ? 'not-allowed' : 'pointer',
              opacity: listMutation.isPending || !price || Number(price) <= 0 ? 0.6 : 1,
              whiteSpace: 'nowrap' as const,
            }}
          >
            {listMutation.isPending ? '···' : t('bottle.listForSale')}
          </button>
        </div>
      )}

      {(listMutation.isError || unlistMutation.isError) && (
        <div style={{ fontFamily: 'Cormorant Garamond, serif', fontSize: 14, color: '#C04040', marginTop: 10 }}>
          {t('bottle.errorSale')}
        </div>
      )}
    </div>
  )
}

function DeleteSection({ bottle, onDelete }: { bottle: Bottle; onDelete?: () => void }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const [confirming, setConfirming] = useState(false)

  const deleteMutation = useMutation({
    mutationFn: () => removeBottle(bottle.id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['bottles', bottle.userId] })
      onDelete?.()
    },
  })

  return (
    <div style={{ marginBottom: 20 }}>
      {!confirming ? (
        <button
          onClick={() => setConfirming(true)}
          style={{
            fontFamily: 'Cinzel, serif',
            fontSize: 10,
            letterSpacing: '0.15em',
            textTransform: 'uppercase',
            color: '#C04040',
            background: 'rgba(192,64,64,0.07)',
            border: '1px solid rgba(192,64,64,0.45)',
            padding: '10px 20px',
            borderRadius: 2,
            cursor: 'pointer',
            width: '100%',
          }}
        >
          {t('bottle.remove')}
        </button>
      ) : (
        <div style={{
          display: 'flex',
          alignItems: 'center',
          gap: 10,
          padding: '12px 16px',
          background: 'rgba(180,60,60,0.05)',
          border: '1px solid rgba(180,60,60,0.2)',
          borderRadius: 4,
          flexWrap: 'wrap',
        }}>
          <span style={{ fontFamily: 'Cormorant Garamond, serif', fontSize: 15, color: '#C04040', flex: 1, minWidth: 160 }}>
            {t('bottle.removeConfirmText')}
          </span>
          <button
            onClick={() => deleteMutation.mutate()}
            disabled={deleteMutation.isPending}
            style={{
              fontFamily: 'Cinzel, serif',
              fontSize: 9,
              letterSpacing: '0.15em',
              textTransform: 'uppercase',
              color: '#C04040',
              background: 'transparent',
              border: '1px solid rgba(192,64,64,0.5)',
              padding: '8px 14px',
              borderRadius: 2,
              cursor: deleteMutation.isPending ? 'wait' : 'pointer',
              opacity: deleteMutation.isPending ? 0.6 : 1,
              whiteSpace: 'nowrap',
            }}
          >
            {deleteMutation.isPending ? t('bottle.removing') : t('bottle.removeConfirm')}
          </button>
          <button
            onClick={() => setConfirming(false)}
            disabled={deleteMutation.isPending}
            style={{
              fontFamily: 'Cinzel, serif',
              fontSize: 9,
              letterSpacing: '0.15em',
              textTransform: 'uppercase',
              color: '#B09868',
              background: 'transparent',
              border: '1px solid rgba(201,168,76,0.2)',
              padding: '8px 14px',
              borderRadius: 2,
              cursor: deleteMutation.isPending ? 'wait' : 'pointer',
              opacity: deleteMutation.isPending ? 0.6 : 1,
            }}
          >
            {t('bottle.removeCancel')}
          </button>
        </div>
      )}
      {deleteMutation.isError && (
        <div style={{ fontFamily: 'Cormorant Garamond, serif', fontSize: 14, color: '#C04040', marginTop: 8 }}>
          {t('bottle.removeError')}
        </div>
      )}
    </div>
  )
}

const offerLabelStyle: CSSProperties = {
  fontFamily: 'Cinzel, serif',
  fontSize: 11,
  letterSpacing: '0.2em',
  color: '#B09868',
  textTransform: 'uppercase',
  marginBottom: 6,
  display: 'block',
}

const offerOverlayStyle: CSSProperties = {
  position: 'fixed',
  inset: 0,
  zIndex: 60,
  display: 'flex',
  alignItems: 'center',
  justifyContent: 'center',
  padding: '24px 16px',
}

const offerCardStyle: CSSProperties = {
  position: 'relative',
  width: '100%',
  maxWidth: 440,
  background: 'linear-gradient(180deg, #0F0604, #130805)',
  border: '1px solid rgba(201,168,76,0.22)',
  borderRadius: 8,
  padding: 28,
  animation: 'fadeInUp 0.22s ease-out',
  boxShadow: '0 32px 80px rgba(0,0,0,0.7), 0 0 0 1px rgba(201,168,76,0.08)',
}

const offerSubmitStyle: CSSProperties = {
  flex: 1,
  fontFamily: 'Cinzel, serif',
  fontSize: 12,
  letterSpacing: '0.2em',
  textTransform: 'uppercase',
  color: '#07030A',
  background: 'linear-gradient(135deg, #C9A84C, #E8C870)',
  border: 'none',
  padding: '12px',
  borderRadius: 2,
}

const offerCancelStyle: CSSProperties = {
  fontFamily: 'Cinzel, serif',
  fontSize: 12,
  letterSpacing: '0.2em',
  textTransform: 'uppercase',
  color: '#B09868',
  background: 'transparent',
  border: '1px solid rgba(201,168,76,0.2)',
  padding: '12px 22px',
  borderRadius: 2,
  cursor: 'pointer',
}

function MakeOfferSection({ bottle }: { bottle: Bottle }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const [open, setOpen] = useState(false)
  const [price, setPrice] = useState(bottle.askingPrice?.toString() ?? '')
  const [currency, setCurrency] = useState(bottle.currency ?? 'USD')
  const [message, setMessage] = useState('')

  const mutation = useMutation({
    mutationFn: () => createOffer({
      bottleId: bottle.id,
      offeredPrice: Number(price),
      currency,
      message: message.trim() || undefined,
    }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['offers'] })
      setOpen(false)
      setMessage('')
    },
  })

  const canSubmit = !!price && Number(price) > 0 && !mutation.isPending

  return (
    <div style={{ marginBottom: 20 }}>
      <button
        onClick={() => setOpen(true)}
        style={{
          width: '100%',
          fontFamily: 'Cinzel, serif',
          fontSize: 12,
          letterSpacing: '0.2em',
          textTransform: 'uppercase',
          color: '#07030A',
          background: 'linear-gradient(135deg, #C9A84C, #E8C870)',
          border: 'none',
          padding: '13px',
          borderRadius: 2,
          cursor: 'pointer',
          boxShadow: '0 4px 20px rgba(201,168,76,0.25)',
        }}
      >
        {t('offers.makeOffer')}
      </button>

      {open && (
        <div style={offerOverlayStyle}>
          <div onClick={() => setOpen(false)} style={{ position: 'absolute', inset: 0, background: 'rgba(4,2,1,0.88)' }} />

          <div style={offerCardStyle}>
            <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 20 }}>
              <div style={{ fontFamily: 'Cinzel, serif', fontSize: 13, letterSpacing: '0.25em', color: '#C9A84C', textTransform: 'uppercase' }}>
                {t('offers.offerModalTitle')}
              </div>
              <button
                onClick={() => setOpen(false)}
                style={{ background: 'transparent', border: 'none', color: '#B09868', fontSize: 24, cursor: 'pointer', lineHeight: 1 }}
              >
                ×
              </button>
            </div>

            <div style={{ fontFamily: 'Playfair Display, serif', fontSize: 20, color: '#E8C870', marginBottom: 20 }}>
              {bottle.name}
            </div>

            <div style={{ display: 'flex', gap: 12, marginBottom: 18 }}>
              <div style={{ flex: 1 }}>
                <label style={offerLabelStyle}>{t('offers.price')}</label>
                <input
                  type="number"
                  min={0}
                  step={0.01}
                  value={price}
                  onChange={e => setPrice(e.target.value)}
                  onFocus={focusOn}
                  onBlur={focusOff}
                  style={inputStyle}
                />
              </div>
              <div style={{ width: 110 }}>
                <label style={offerLabelStyle}>{t('offers.currency')}</label>
                <select
                  value={currency}
                  onChange={e => setCurrency(e.target.value)}
                  style={{ ...inputStyle, cursor: 'pointer', appearance: 'none' }}
                >
                  {CURRENCIES.map(c => <option key={c} value={c}>{c}</option>)}
                </select>
              </div>
            </div>

            <label style={offerLabelStyle}>{t('offers.message')}</label>
            <textarea
              value={message}
              onChange={e => setMessage(e.target.value)}
              onFocus={focusOn}
              onBlur={focusOff}
              rows={3}
              placeholder={t('offers.messagePlaceholder')}
              style={{ ...inputStyle, resize: 'vertical', marginBottom: 20 }}
            />

            {mutation.isError && (
              <div style={{ fontFamily: 'Cormorant Garamond, serif', fontSize: 14, color: '#C04040', marginBottom: 16 }}>
                {t('offers.errorCreate')}
              </div>
            )}

            <div style={{ display: 'flex', gap: 12 }}>
              <button
                onClick={() => mutation.mutate()}
                disabled={!canSubmit}
                style={{
                  ...offerSubmitStyle,
                  cursor: canSubmit ? 'pointer' : 'not-allowed',
                  opacity: canSubmit ? 1 : 0.6,
                }}
              >
                {mutation.isPending ? t('offers.submitting') : t('offers.submit')}
              </button>
              <button onClick={() => setOpen(false)} disabled={mutation.isPending} style={offerCancelStyle}>
                {t('offers.cancel')}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  )
}

function DetailRow({ label, value }: { label: string; value: string | number }) {
  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
      <span style={{ fontFamily: 'Cinzel, serif', fontSize: 9, letterSpacing: '0.2em', color: '#7A6040', textTransform: 'uppercase' }}>
        {label}
      </span>
      <span style={{ fontFamily: 'Cormorant Garamond, serif', fontSize: 17, color: '#E8D4A0' }}>
        {value}
      </span>
    </div>
  )
}

export default function BottleDetailPanel({
  bottle,
  userId,
  currentUserId,
  onClose,
  onDelete,
}: {
  bottle: Bottle
  userId: string
  currentUserId: string
  onClose: () => void
  onDelete?: () => void
}) {
  const { t } = useTranslation()
  const col = CATEGORY_COLORS[bottle.category]
  const primaryImage = bottle.images.find(i => i.isPrimary) ?? bottle.images[0]
  const galleryImages = bottle.images.filter(i => !i.isPrimary).sort((a, b) => a.sortOrder - b.sortOrder)

  return (
    <div style={{ position: 'fixed', inset: 0, zIndex: 50, display: 'flex', alignItems: 'center', justifyContent: 'center', padding: '24px 16px' }}>
      <div onClick={onClose} style={{ position: 'absolute', inset: 0, background: 'rgba(4,2,1,0.88)' }} />

      <div
        style={{
          position: 'relative',
          width: '100%',
          maxWidth: 680,
          maxHeight: '90vh',
          background: 'linear-gradient(180deg, #0F0604, #130805)',
          border: '1px solid rgba(201,168,76,0.22)',
          borderRadius: 8,
          overflowY: 'auto',
          animation: 'fadeInUp 0.22s ease-out',
          boxShadow: '0 32px 80px rgba(0,0,0,0.7), 0 0 0 1px rgba(201,168,76,0.08)',
        }}
      >
        <div style={{ position: 'relative', width: '100%', height: 280, background: '#0A0402', overflow: 'hidden', flexShrink: 0 }}>
          {primaryImage ? (
            <img
              src={primaryImage.url}
              alt={bottle.name}
              style={{ width: '100%', height: '100%', objectFit: 'cover', objectPosition: 'center top' }}
            />
          ) : (
            <div style={{ width: '100%', height: '100%', display: 'flex', alignItems: 'center', justifyContent: 'center', background: `radial-gradient(ellipse at 50% 80%, ${col.glow}18 0%, transparent 65%)` }}>
              <div style={{ width: 90 }}>
                <BottleSvg category={bottle.category} condition={bottle.condition} />
              </div>
            </div>
          )}

          <div style={{ position: 'absolute', inset: 0, background: 'linear-gradient(to bottom, transparent 40%, rgba(15,6,4,0.95) 100%)' }} />

          <button
            onClick={onClose}
            style={{
              position: 'absolute',
              top: 16,
              right: 16,
              width: 34,
              height: 34,
              borderRadius: '50%',
              background: 'rgba(10,4,2,0.75)',
              border: '1px solid rgba(201,168,76,0.3)',
              color: '#C9A84C',
              fontSize: 20,
              lineHeight: 1,
              cursor: 'pointer',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
            }}
          >
            ×
          </button>

          <div style={{ position: 'absolute', bottom: 0, left: 0, right: 0, padding: '0 28px 20px' }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginBottom: 6, flexWrap: 'wrap' }}>
              <span style={{
                fontFamily: 'Cormorant Garamond, serif',
                fontSize: 12,
                padding: '3px 10px',
                borderRadius: 10,
                background: `${col.glass}22`,
                border: `1px solid ${col.glass}66`,
                color: col.glass,
                letterSpacing: '0.05em',
              }}>
                {col.label}
              </span>
              <span style={{
                fontFamily: 'Cormorant Garamond, serif',
                fontSize: 12,
                padding: '3px 10px',
                borderRadius: 10,
                background: 'rgba(201,168,76,0.08)',
                border: '1px solid rgba(201,168,76,0.25)',
                color: '#C9A84C',
              }}>
                {t(`addBottle.condition${bottle.condition}`)}
              </span>
              {bottle.isLimited && (
                <span style={{ fontFamily: 'Cormorant Garamond, serif', fontSize: 12, color: '#E8C870', letterSpacing: '0.05em' }}>{t('bottle.limited')}</span>
              )}
              {bottle.isForSale && (
                <span style={{ fontFamily: 'Cormorant Garamond, serif', fontSize: 12, color: '#4A9A6A' }}>{t('bottle.forSale')}</span>
              )}
            </div>

            <h2 style={{ fontFamily: 'Playfair Display, serif', fontSize: 26, fontWeight: 700, color: '#E8C870', margin: '0 0 4px', lineHeight: 1.15 }}>
              {bottle.name}
            </h2>
            {bottle.distilleryName && (
              <div style={{ fontFamily: 'Cormorant Garamond, serif', fontSize: 16, fontStyle: 'italic', color: '#C9A84C' }}>
                {bottle.distilleryName}
              </div>
            )}
          </div>
        </div>

        <div style={{ padding: '24px 28px 40px' }}>

          {(bottle.age != null || bottle.abvPercent != null || bottle.volumeMl != null || bottle.vintageYear != null) && (
            <div style={{
              display: 'grid',
              gridTemplateColumns: 'repeat(auto-fit, minmax(90px, 1fr))',
              gap: '16px 24px',
              padding: '18px 20px',
              background: 'rgba(201,168,76,0.04)',
              border: '1px solid rgba(201,168,76,0.1)',
              borderRadius: 4,
              marginBottom: 20,
            }}>
              {bottle.age != null && <DetailRow label={t('bottle.age')} value={`${bottle.age} yr`} />}
              {bottle.abvPercent != null && <DetailRow label={t('bottle.abv')} value={`${bottle.abvPercent}%`} />}
              {bottle.volumeMl != null && <DetailRow label={t('bottle.volume')} value={`${bottle.volumeMl} ml`} />}
              {bottle.vintageYear != null && <DetailRow label={t('bottle.vintage')} value={bottle.vintageYear} />}
            </div>
          )}

          {(bottle.region || bottle.country) && (
            <div style={{ marginBottom: 20 }}>
              <div style={{ fontFamily: 'Cinzel, serif', fontSize: 9, letterSpacing: '0.2em', color: '#7A6040', textTransform: 'uppercase', marginBottom: 6 }}>
                {t('bottle.origin')}
              </div>
              <div style={{ fontFamily: 'Cormorant Garamond, serif', fontSize: 16, color: '#E8D4A0' }}>
                {[bottle.region, bottle.country].filter(Boolean).join(', ')}
              </div>
            </div>
          )}

          {bottle.userId === currentUserId ? (
            <>
              <SaleSection bottle={bottle} userId={userId} />
              <DeleteSection bottle={bottle} onDelete={onDelete ?? onClose} />
            </>
          ) : (
            <>
              {bottle.isForSale && bottle.askingPrice != null && (
                <div style={{
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'space-between',
                  padding: '14px 20px',
                  background: 'rgba(74,154,106,0.06)',
                  border: '1px solid rgba(74,154,106,0.25)',
                  borderRadius: 4,
                  marginBottom: 20,
                }}>
                  <span style={{ fontFamily: 'Cinzel, serif', fontSize: 10, letterSpacing: '0.2em', color: '#4A9A6A', textTransform: 'uppercase' }}>
                    {t('bottle.askingPrice')}
                  </span>
                  <span style={{ fontFamily: 'Playfair Display, serif', fontSize: 22, color: '#6ABF8A', fontWeight: 700 }}>
                    {bottle.currency ?? 'USD'} {bottle.askingPrice.toLocaleString()}
                  </span>
                </div>
              )}
              {currentUserId && <MakeOfferSection bottle={bottle} />}
            </>
          )}

          {bottle.description && (
            <div style={{ marginBottom: 20 }}>
              <div style={{ fontFamily: 'Cinzel, serif', fontSize: 9, letterSpacing: '0.2em', color: '#7A6040', textTransform: 'uppercase', marginBottom: 8 }}>
                {t('bottle.notes')}
              </div>
              <p style={{ fontFamily: 'Cormorant Garamond, serif', fontSize: 17, color: '#C9A84C', lineHeight: 1.65, margin: 0, fontStyle: 'italic' }}>
                {bottle.description}
              </p>
            </div>
          )}

          {galleryImages.length > 0 && (
            <div>
              <div style={{ fontFamily: 'Cinzel, serif', fontSize: 9, letterSpacing: '0.2em', color: '#7A6040', textTransform: 'uppercase', marginBottom: 8 }}>
                {t('bottle.gallery')}
              </div>
              <div style={{ display: 'flex', gap: 8, overflowX: 'auto', scrollbarWidth: 'none' }}>
                {galleryImages.map(img => (
                  <img
                    key={img.id}
                    src={img.url}
                    alt=""
                    style={{ width: 80, height: 80, objectFit: 'cover', borderRadius: 3, border: '1px solid rgba(201,168,76,0.15)', flexShrink: 0 }}
                  />
                ))}
              </div>
            </div>
          )}

          <LikesSection bottle={bottle} userId={userId} />

          <CommentsSection bottle={bottle} currentUserId={currentUserId} />
        </div>
      </div>
    </div>
  )
}
