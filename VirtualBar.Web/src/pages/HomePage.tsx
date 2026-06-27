import { useEffect, useState } from 'react'
import type { CSSProperties } from 'react'
import { Link } from 'react-router-dom'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { useAuth } from '../contexts/AuthContext'
import {
  createNewsPost,
  updateNewsPost,
  deleteNewsPost,
  getNewsPost,
  uploadNewsCover,
} from '../api/newsApi'
import { getFeed } from '../api/feedApi'
import type { CreateNewsPostPayload } from '../types'
import type { FeedItem, NewsPost } from '../types'
import NavBar from '../components/NavBar'

const LANGS = ['bg', 'en'] as const
type LangCode = (typeof LANGS)[number]


interface TranslationDraft {
  title: string
  content: string
}

function emptyDrafts(): Record<LangCode, TranslationDraft> {
  return {
    bg: { title: '', content: '' },
    en: { title: '', content: '' },
  }
}

function formatDate(iso: string) {
  return new Date(iso).toLocaleDateString('en-US', { year: 'numeric', month: 'long', day: 'numeric' })
}

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

const labelStyle: CSSProperties = {
  fontFamily: 'Cinzel, serif',
  fontSize: 11,
  letterSpacing: '0.2em',
  color: '#B09868',
  textTransform: 'uppercase',
  marginBottom: 6,
  display: 'block',
}

function focusOn(e: React.FocusEvent<HTMLInputElement | HTMLTextAreaElement>) {
  e.currentTarget.style.border = '1px solid rgba(201,168,76,0.5)'
}

function focusOff(e: React.FocusEvent<HTMLInputElement | HTMLTextAreaElement>) {
  e.currentTarget.style.border = '1px solid rgba(201,168,76,0.2)'
}

const heroLabelStyle: CSSProperties = {
  fontFamily: 'Cinzel, serif',
  fontSize: 'clamp(10px, 3vw, 12px)',
  letterSpacing: 'clamp(0.2em, 1.2vw, 0.5em)',
  color: '#B09868',
  marginBottom: 18,
}

const heroTitleStyle: CSSProperties = {
  fontFamily: 'Playfair Display, serif',
  fontSize: 'clamp(34px, 11vw, 64px)',
  fontWeight: 700,
  color: '#E8C870',
  margin: 0,
  lineHeight: 1.05,
  letterSpacing: '0.04em',
}

const heroDividerStyle: CSSProperties = {
  width: 180,
  height: 2,
  margin: '24px auto',
  background: 'linear-gradient(90deg, transparent, #C9A84C, transparent)',
}

const heroSubtitleStyle: CSSProperties = {
  fontFamily: 'Cormorant Garamond, serif',
  fontSize: 'clamp(17px, 4.5vw, 21px)',
  fontStyle: 'italic',
  color: '#C9A84C',
  maxWidth: 620,
  margin: '0 auto',
  lineHeight: 1.5,
}

function Hero() {
  const { t } = useTranslation()
  return (
    <header style={{ textAlign: 'center', padding: 'clamp(40px, 9vw, 64px) 24px clamp(28px, 6vw, 48px)', position: 'relative' }}>
      <div style={heroLabelStyle}>{t('hero.vol')}</div>
      <h1 style={heroTitleStyle}>{t('hero.title')}</h1>
      <div style={heroDividerStyle} />
      <p style={heroSubtitleStyle}>{t('hero.subtitle')}</p>
    </header>
  )
}

const skeletonCardStyle: CSSProperties = {
  background: 'rgba(201,168,76,0.04)',
  borderLeft: '3px solid rgba(201,168,76,0.2)',
  borderRadius: 6,
  padding: 28,
  animation: 'shimmer 1.6s ease-in-out infinite',
}

const skeletonLine1Style: CSSProperties = { height: 12, width: 110, background: 'rgba(201,168,76,0.18)', borderRadius: 2, marginBottom: 18 }
const skeletonLine2Style: CSSProperties = { height: 28, width: '70%', background: 'rgba(201,168,76,0.14)', borderRadius: 2, marginBottom: 16 }
const skeletonLine3Style: CSSProperties = { height: 14, width: '100%', background: 'rgba(201,168,76,0.08)', borderRadius: 2, marginBottom: 8 }
const skeletonLine4Style: CSSProperties = { height: 14, width: '85%', background: 'rgba(201,168,76,0.08)', borderRadius: 2, marginBottom: 8 }
const skeletonLine5Style: CSSProperties = { height: 14, width: '60%', background: 'rgba(201,168,76,0.08)', borderRadius: 2 }

function SkeletonCard() {
  return (
    <div style={skeletonCardStyle}>
      <div style={skeletonLine1Style} />
      <div style={skeletonLine2Style} />
      <div style={skeletonLine3Style} />
      <div style={skeletonLine4Style} />
      <div style={skeletonLine5Style} />
    </div>
  )
}

const PREVIEW_LENGTH = 280

function NewsPostCard({
  post,
  isAdmin,
  onEdit,
  onDelete,
}: {
  post: NewsPost
  isAdmin: boolean
  onEdit: () => void
  onDelete: () => void
}) {
  const { t } = useTranslation()
  const [hover, setHover] = useState(false)
  const [expanded, setExpanded] = useState(false)

  const isLong = post.content.length > PREVIEW_LENGTH
  const lastSpace = post.content.lastIndexOf(' ', PREVIEW_LENGTH)
  const displayContent = !expanded && isLong
    ? post.content.slice(0, lastSpace > 0 ? lastSpace : PREVIEW_LENGTH) + '…'
    : post.content

  return (
    <article
      onMouseEnter={() => setHover(true)}
      onMouseLeave={() => setHover(false)}
      style={{
        background: 'rgba(15,8,5,0.7)',
        borderLeft: '3px solid #C9A84C',
        border: '1px solid rgba(201,168,76,0.12)',
        borderLeftWidth: 3,
        borderLeftColor: '#C9A84C',
        borderRadius: 6,
        overflow: 'hidden',
        transform: hover ? 'translateY(-4px)' : 'translateY(0)',
        boxShadow: hover ? '0 12px 36px rgba(0,0,0,0.5)' : '0 4px 14px rgba(0,0,0,0.3)',
        transition: 'transform 0.25s ease, box-shadow 0.25s ease',
        animation: 'fadeInUp 0.5s ease-out',
      }}
    >
      {post.coverImageUrl && (
        <div style={{ height: 220, overflow: 'hidden' }}>
          <img
            src={post.coverImageUrl}
            alt={post.title}
            style={{
              width: '100%',
              height: '100%',
              objectFit: 'cover',
              display: 'block',
              transform: hover ? 'scale(1.04)' : 'scale(1)',
              transition: 'transform 0.4s ease',
            }}
          />
        </div>
      )}

      <div style={{ padding: '28px 32px' }}>
        <div
          style={{
            display: 'flex',
            alignItems: 'center',
            gap: 10,
            fontFamily: 'Cinzel, serif',
            fontSize: 11,
            letterSpacing: '0.3em',
            color: '#C9A84C',
            marginBottom: 16,
          }}
        >
          <span style={{ width: 24, height: 1, background: '#C9A84C' }} />
          {t('hero.title')}
        </div>

        <h2
          style={{
            fontFamily: 'Playfair Display, serif',
            fontSize: 28,
            fontWeight: 700,
            color: '#E8C870',
            margin: '0 0 14px',
            lineHeight: 1.2,
          }}
        >
          {post.title}
        </h2>

        <p
          style={{
            fontFamily: 'Cormorant Garamond, serif',
            fontSize: 18,
            color: '#D8C9A8',
            lineHeight: 1.55,
            margin: '0 0 12px',
            whiteSpace: 'pre-wrap',
          }}
        >
          {displayContent}
        </p>

        {isLong && (
          <button
            onClick={() => setExpanded(e => !e)}
            style={{
              fontFamily: 'Cinzel, serif',
              fontSize: 10,
              letterSpacing: '0.2em',
              color: '#C9A84C',
              background: 'transparent',
              border: 'none',
              padding: '0 0 20px',
              cursor: 'pointer',
              display: 'flex',
              alignItems: 'center',
              gap: 7,
            }}
          >
            <span style={{ width: 16, height: 1, background: '#C9A84C', display: 'inline-block' }} />
            {expanded ? t('home.readLess') : t('home.readMore')}
            <span style={{ fontSize: 9, transform: expanded ? 'rotate(180deg)' : 'none', display: 'inline-block', transition: 'transform 0.2s' }}>▼</span>
          </button>
        )}

        <div
          style={{
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'space-between',
            borderTop: '1px solid rgba(201,168,76,0.1)',
            paddingTop: 16,
          }}
        >
          <div
            style={{
              fontFamily: 'Cormorant Garamond, serif',
              fontStyle: 'italic',
              fontSize: 15,
              color: '#B09868',
            }}
          >
            {t('home.authorBy', { name: post.authorDisplayName })} &middot; {formatDate(post.createdAt)}
          </div>

          {isAdmin && (
            <div style={{ display: 'flex', gap: 10 }}>
              <button
                onClick={onEdit}
                aria-label="Edit post"
                style={{
                  fontFamily: 'Cinzel, serif',
                  fontSize: 11,
                  letterSpacing: '0.15em',
                  color: '#C9A84C',
                  background: 'transparent',
                  border: '1px solid rgba(201,168,76,0.3)',
                  padding: '5px 14px',
                  borderRadius: 2,
                  cursor: 'pointer',
                }}
              >
                {t('home.editBtn')}
              </button>
              <button
                onClick={onDelete}
                aria-label="Delete post"
                style={{
                  fontFamily: 'Cinzel, serif',
                  fontSize: 11,
                  letterSpacing: '0.15em',
                  color: '#C04040',
                  background: 'transparent',
                  border: '1px solid rgba(192,64,64,0.4)',
                  padding: '5px 14px',
                  borderRadius: 2,
                  cursor: 'pointer',
                }}
              >
                {t('home.deleteBtn')}
              </button>
            </div>
          )}
        </div>
      </div>
    </article>
  )
}

function BottleThumb({ url, category }: { url?: string; category?: string }) {
  if (url) {
    return (
      <div
        style={{
          width: 80,
          height: 80,
          flexShrink: 0,
          borderRadius: 4,
          overflow: 'hidden',
          border: '1px solid rgba(201,168,76,0.2)',
        }}
      >
        <img
          src={url}
          alt={category ?? 'Bottle'}
          style={{ width: '100%', height: '100%', objectFit: 'cover', display: 'block' }}
        />
      </div>
    )
  }

  return (
    <div
      style={{
        width: 80,
        height: 80,
        flexShrink: 0,
        borderRadius: 4,
        border: '1px solid rgba(201,168,76,0.2)',
        background: 'rgba(201,168,76,0.06)',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        fontFamily: 'Playfair Display, serif',
        fontSize: 30,
        color: '#C9A84C',
      }}
    >
      {category ? category.charAt(0).toUpperCase() : '\u{1F943}'}
    </div>
  )
}

function BottleActivityCard({ item }: { item: FeedItem }) {
  const { t } = useTranslation()
  const [hover, setHover] = useState(false)
  const forSale = item.type === 'ForSale'
  const accentColor = forSale ? '#E8C870' : '#C9A84C'

  const headerText = forSale ? t('home.listedForSale') : t('home.addedToCollection')
  const linkTo = forSale ? '/marketplace' : `/bar/${item.bottleUserId}`

  return (
    <article
      onMouseEnter={() => setHover(true)}
      onMouseLeave={() => setHover(false)}
      style={{
        background: 'rgba(15,8,5,0.7)',
        border: '1px solid rgba(201,168,76,0.12)',
        borderLeftWidth: 3,
        borderLeftStyle: 'solid',
        borderLeftColor: accentColor,
        borderRadius: 6,
        padding: '20px 24px',
        display: 'flex',
        alignItems: 'center',
        gap: 18,
        transform: hover ? 'translateY(-3px)' : 'translateY(0)',
        boxShadow: hover ? '0 10px 30px rgba(0,0,0,0.5)' : '0 4px 14px rgba(0,0,0,0.3)',
        transition: 'transform 0.25s ease, box-shadow 0.25s ease',
        animation: 'fadeInUp 0.5s ease-out',
      }}
    >
      <div style={{ flex: 1, minWidth: 0 }}>
        <div
          style={{
            display: 'flex',
            alignItems: 'center',
            gap: 8,
            fontFamily: 'Cormorant Garamond, serif',
            fontSize: 15,
            fontStyle: 'italic',
            color: '#B09868',
            marginBottom: 10,
          }}
        >
          <Link
            to={`/bar/${item.bottleUserId}`}
            style={{ color: '#C9A84C', textDecoration: 'none', fontStyle: 'normal' }}
          >
            {item.bottleUserDisplayName}
          </Link>
          <span>{headerText}</span>
          {forSale && (
            <span
              style={{
                fontFamily: 'Cinzel, serif',
                fontSize: 9,
                letterSpacing: '0.2em',
                color: '#07030A',
                background: 'linear-gradient(135deg, #C9A84C, #E8C870)',
                padding: '3px 8px',
                borderRadius: 2,
                fontStyle: 'normal',
              }}
            >
              {t('home.forSale')}
            </span>
          )}
        </div>

        <Link to={linkTo} style={{ textDecoration: 'none' }}>
          <h3
            style={{
              fontFamily: 'Playfair Display, serif',
              fontSize: 22,
              fontWeight: 700,
              color: '#E8C870',
              margin: '0 0 10px',
              lineHeight: 1.2,
            }}
          >
            {item.bottleName}
          </h3>
        </Link>

        <div style={{ display: 'flex', alignItems: 'center', gap: 12, flexWrap: 'wrap' }}>
          {item.bottleCategory && (
            <span
              style={{
                fontFamily: 'Cinzel, serif',
                fontSize: 10,
                letterSpacing: '0.2em',
                color: '#C9A84C',
                border: '1px solid rgba(201,168,76,0.3)',
                padding: '4px 10px',
                borderRadius: 2,
                textTransform: 'uppercase',
              }}
            >
              {item.bottleCategory}
            </span>
          )}

          {forSale && item.askingPrice != null && (
            <span
              style={{
                fontFamily: 'Cormorant Garamond, serif',
                fontSize: 18,
                fontWeight: 600,
                color: '#E8C870',
              }}
            >
              {item.currency} {item.askingPrice.toFixed(2)}
            </span>
          )}

          <span
            style={{
              fontFamily: 'Cormorant Garamond, serif',
              fontStyle: 'italic',
              fontSize: 14,
              color: '#8A7650',
            }}
          >
            {formatDate(item.timestamp)}
          </span>
        </div>
      </div>

      <BottleThumb url={item.bottlePrimaryImageUrl} category={item.bottleCategory} />
    </article>
  )
}

function PostFormPanel({
  mode,
  initial,
  pending,
  error,
  onSubmit,
  onClose,
}: {
  mode: 'create' | 'edit'
  initial?: NewsPost
  pending: boolean
  error: boolean
  onSubmit: (payload: CreateNewsPostPayload) => void
  onClose: () => void
}) {
  const { t } = useTranslation()
  const [coverImageUrl, setCoverImageUrl] = useState('')
  const [activeLang, setActiveLang] = useState<LangCode>('bg')
  const [drafts, setDrafts] = useState<Record<LangCode, TranslationDraft>>(emptyDrafts)
  const [validationError, setValidationError] = useState(false)
  const [coverUploading, setCoverUploading] = useState(false)
  const [coverUploadError, setCoverUploadError] = useState('')

  async function handleCoverUpload(e: React.ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0]
    if (!file) return
    setCoverUploading(true)
    setCoverUploadError('')
    try {
      const url = await uploadNewsCover(file)
      setCoverImageUrl(url)
    } catch {
      setCoverUploadError(t('home.coverUploadError'))
    } finally {
      setCoverUploading(false)
      e.target.value = ''
    }
  }

  useEffect(() => {
    if (!initial) {
      setDrafts(emptyDrafts())
      setCoverImageUrl('')
      setActiveLang('bg')
      return
    }

    const next = emptyDrafts()
    for (const tr of initial.translations) {
      if (tr.languageCode === 'bg' || tr.languageCode === 'en') {
        next[tr.languageCode] = { title: tr.title, content: tr.content }
      }
    }
    setDrafts(next)
    setCoverImageUrl(initial.coverImageUrl ?? '')
    setActiveLang('bg')
  }, [initial])

  function updateDraft(lc: LangCode, field: keyof TranslationDraft, value: string) {
    setDrafts((prev) => ({ ...prev, [lc]: { ...prev[lc], [field]: value } }))
  }

  const handleClose = () => {
    setDrafts(emptyDrafts())
    setCoverImageUrl('')
    setActiveLang('bg')
    setValidationError(false)
    setCoverUploading(false)
    setCoverUploadError('')
    onClose()
  }

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault()

    if (!drafts.bg.title.trim()) {
      setValidationError(true)
      setActiveLang('bg')
      return
    }
    setValidationError(false)

    const translations = LANGS.filter((lc) => drafts[lc].title.trim() || drafts[lc].content.trim()).map(
      (lc) => ({
        languageCode: lc,
        title: drafts[lc].title.trim(),
        content: drafts[lc].content.trim(),
      }),
    )

    onSubmit({
      coverImageUrl: coverImageUrl.trim() || undefined,
      translations,
    })
  }

  return (
    <div style={{ position: 'fixed', inset: 0, zIndex: 50 }}>
      <div onClick={handleClose} style={{ position: 'absolute', inset: 0, background: 'rgba(4,2,1,0.85)' }} />

      <div
        style={{
          position: 'absolute',
          right: 0,
          top: 0,
          width: 480,
          maxWidth: '100%',
          height: '100%',
          background: 'linear-gradient(180deg, #0F0604, #130805)',
          borderLeft: '1px solid rgba(201,168,76,0.2)',
          overflowY: 'auto',
          padding: 32,
          animation: 'fadeInUp 0.3s ease-out',
        }}
      >
        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 28 }}>
          <div style={{ fontFamily: 'Cinzel, serif', fontSize: 14, letterSpacing: '0.3em', color: '#C9A84C' }}>
            {mode === 'create' ? t('home.createPostTitle') : t('home.editPostTitle')}
          </div>
          <button
            onClick={handleClose}
            aria-label="Close panel"
            style={{ background: 'transparent', border: 'none', color: '#B09868', fontSize: 24, cursor: 'pointer', lineHeight: 1 }}
          >
            &times;
          </button>
        </div>

        <form onSubmit={handleSubmit}>
          <div style={{ display: 'flex', borderBottom: '1px solid rgba(201,168,76,0.15)', marginBottom: 20 }}>
            {LANGS.map((lc) => (
              <button
                key={lc}
                type="button"
                onClick={() => setActiveLang(lc)}
                style={{
                  fontFamily: 'Cinzel, serif',
                  fontSize: 11,
                  letterSpacing: '0.15em',
                  padding: '10px 20px',
                  background: 'transparent',
                  border: 'none',
                  borderBottom: activeLang === lc ? '2px solid #C9A84C' : '2px solid transparent',
                  color: activeLang === lc ? '#C9A84C' : '#7A6040',
                  cursor: 'pointer',
                  marginBottom: -1,
                }}
              >
                {t(`lang.${lc}`)}
              </button>
            ))}
          </div>

          <label style={labelStyle}>{t('home.postTitleLabel')}</label>
          <input
            value={drafts[activeLang].title}
            onChange={(e) => updateDraft(activeLang, 'title', e.target.value)}
            onFocus={focusOn}
            onBlur={focusOff}
            placeholder={t('home.postTitlePlaceholder')}
            style={{ ...inputStyle, marginBottom: 18 }}
          />

          <label style={labelStyle}>{t('home.postContentLabel')}</label>
          <textarea
            value={drafts[activeLang].content}
            onChange={(e) => updateDraft(activeLang, 'content', e.target.value)}
            onFocus={focusOn}
            onBlur={focusOff}
            rows={10}
            placeholder={t('home.postContentPlaceholder')}
            style={{ ...inputStyle, marginBottom: 18, resize: 'vertical', lineHeight: 1.5 }}
          />

          {/* Cover image upload */}
          <div style={{ marginBottom: 18 }}>
            <label style={labelStyle}>{t('home.postCoverLabel')}</label>

            {coverImageUrl && (
              <div style={{ marginBottom: 10, position: 'relative', display: 'inline-block' }}>
                <img
                  src={coverImageUrl}
                  alt="cover preview"
                  style={{
                    width: '100%',
                    maxHeight: 180,
                    objectFit: 'cover',
                    borderRadius: 4,
                    border: '1px solid rgba(201,168,76,0.2)',
                  }}
                />
                <button
                  type="button"
                  onClick={() => setCoverImageUrl('')}
                  style={{
                    position: 'absolute',
                    top: 8,
                    right: 8,
                    width: 28,
                    height: 28,
                    borderRadius: '50%',
                    background: 'rgba(10,4,2,0.8)',
                    border: '1px solid rgba(201,168,76,0.3)',
                    color: '#C9A84C',
                    fontSize: 16,
                    lineHeight: 1,
                    cursor: 'pointer',
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'center',
                  }}
                >
                  ×
                </button>
              </div>
            )}

            <div style={{ display: 'flex', gap: 8, alignItems: 'center' }}>
              <label
                style={{
                  fontFamily: 'Cinzel, serif',
                  fontSize: 10,
                  letterSpacing: '0.15em',
                  color: '#C9A84C',
                  border: '1px solid rgba(201,168,76,0.3)',
                  padding: '8px 16px',
                  borderRadius: 3,
                  cursor: coverUploading ? 'wait' : 'pointer',
                  opacity: coverUploading ? 0.6 : 1,
                  whiteSpace: 'nowrap' as const,
                }}
              >
                {coverUploading ? '···' : t('home.uploadCover')}
                <input
                  type="file"
                  accept="image/jpeg,image/png,image/webp,image/gif"
                  style={{ display: 'none' }}
                  onChange={handleCoverUpload}
                  disabled={coverUploading}
                />
              </label>

              <input
                value={coverImageUrl}
                onChange={(e) => setCoverImageUrl(e.target.value)}
                onFocus={focusOn}
                onBlur={focusOff}
                placeholder={t('home.postCoverUrlPlaceholder')}
                style={{ ...inputStyle, flex: 1 }}
              />
            </div>

            {coverUploadError && (
              <div style={{ fontFamily: 'Cormorant Garamond, serif', fontSize: 14, color: '#C04040', marginTop: 8 }}>
                {coverUploadError}
              </div>
            )}
          </div>

          {validationError && (
            <div
              style={{
                fontFamily: 'Cormorant Garamond, serif',
                fontSize: 15,
                color: '#D42020',
                marginBottom: 16,
              }}
            >
              {t('home.postValidationRequired')}
            </div>
          )}

          {error && (
            <div
              style={{
                fontFamily: 'Cormorant Garamond, serif',
                fontSize: 15,
                color: '#D42020',
                marginBottom: 16,
              }}
            >
              {t('home.errorSubmit')}
            </div>
          )}

          <button
            type="submit"
            disabled={pending}
            style={{
              width: '100%',
              fontFamily: 'Cinzel, serif',
              fontSize: 14,
              letterSpacing: '0.2em',
              textTransform: 'uppercase',
              color: '#07030A',
              background: 'linear-gradient(135deg, #C9A84C, #E8C870)',
              border: 'none',
              padding: '14px',
              borderRadius: 2,
              cursor: pending ? 'not-allowed' : 'pointer',
              opacity: pending ? 0.6 : 1,
              boxShadow: '0 4px 20px rgba(201,168,76,0.3)',
              marginBottom: 12,
            }}
          >
            {pending ? '…' : mode === 'create' ? t('home.submitPost') : t('home.updatePost')}
          </button>

          <button
            type="button"
            onClick={handleClose}
            style={{
              width: '100%',
              fontFamily: 'Cinzel, serif',
              fontSize: 12,
              letterSpacing: '0.2em',
              color: '#B09868',
              background: 'transparent',
              border: '1px solid rgba(201,168,76,0.2)',
              padding: '12px',
              borderRadius: 2,
              cursor: 'pointer',
            }}
          >
            {t('home.cancelPost')}
          </button>
        </form>
      </div>
    </div>
  )
}

function ConfirmDeleteDialog({
  pending,
  onConfirm,
  onCancel,
}: {
  pending: boolean
  onConfirm: () => void
  onCancel: () => void
}) {
  const { t } = useTranslation()
  return (
    <div
      style={{
        position: 'fixed',
        inset: 0,
        zIndex: 60,
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
      }}
    >
      <div onClick={onCancel} style={{ position: 'absolute', inset: 0, background: 'rgba(4,2,1,0.85)' }} />
      <div
        style={{
          position: 'relative',
          width: 380,
          maxWidth: '90%',
          background: 'linear-gradient(180deg, #0F0604, #130805)',
          border: '1px solid rgba(201,168,76,0.2)',
          borderRadius: 6,
          padding: 32,
          textAlign: 'center',
          animation: 'fadeInUp 0.25s ease-out',
        }}
      >
        <p
          style={{
            fontFamily: 'Cormorant Garamond, serif',
            fontSize: 18,
            fontStyle: 'italic',
            color: '#C9A84C',
            margin: '0 0 28px',
          }}
        >
          {t('home.confirmDelete')}
        </p>
        <div style={{ display: 'flex', gap: 12 }}>
          <button
            onClick={onCancel}
            style={{
              flex: 1,
              fontFamily: 'Cinzel, serif',
              fontSize: 12,
              letterSpacing: '0.2em',
              color: '#B09868',
              background: 'transparent',
              border: '1px solid rgba(201,168,76,0.2)',
              padding: '12px',
              borderRadius: 2,
              cursor: 'pointer',
            }}
          >
            {t('home.cancelBtn')}
          </button>
          <button
            onClick={onConfirm}
            disabled={pending}
            style={{
              flex: 1,
              fontFamily: 'Cinzel, serif',
              fontSize: 12,
              letterSpacing: '0.2em',
              color: '#F0DDB4',
              background: '#7A2020',
              border: '1px solid rgba(192,64,64,0.5)',
              padding: '12px',
              borderRadius: 2,
              cursor: pending ? 'not-allowed' : 'pointer',
              opacity: pending ? 0.6 : 1,
            }}
          >
            {pending ? '...' : t('home.confirmDeleteBtn')}
          </button>
        </div>
      </div>
    </div>
  )
}

export default function HomePage() {
  const { t, i18n } = useTranslation()
  const { user } = useAuth()
  const queryClient = useQueryClient()
  const isAdmin = user?.isAdmin === true
  const lang = i18n.language?.startsWith('bg') ? 'bg' : 'en'

  const [showCreatePanel, setShowCreatePanel] = useState(false)
  const [editingPost, setEditingPost] = useState<NewsPost | null>(null)
  const [confirmDelete, setConfirmDelete] = useState<string | null>(null)

  async function handleEditPost(postId: string) {
    const full = await getNewsPost(postId, lang)
    setEditingPost(full)
  }

  const { data: feed = [], isLoading, isError } = useQuery({
    queryKey: ['feed', lang],
    queryFn: () => getFeed(0, 50, lang),
  })

  const invalidate = () => queryClient.invalidateQueries({ queryKey: ['feed'] })

  const createMutation = useMutation({
    mutationFn: createNewsPost,
    onSuccess: () => {
      invalidate()
      setShowCreatePanel(false)
    },
  })

  const updateMutation = useMutation({
    mutationFn: ({ id, payload }: { id: string; payload: CreateNewsPostPayload }) =>
      updateNewsPost(id, payload),
    onSuccess: () => {
      invalidate()
      setEditingPost(null)
    },
  })

  const deleteMutation = useMutation({
    mutationFn: deleteNewsPost,
    onSuccess: () => {
      invalidate()
      setConfirmDelete(null)
    },
  })

  return (
    <div style={{ minHeight: '100vh', color: '#F0DDB4' }}>
      <NavBar />

      <Hero />

      <main style={{ maxWidth: 760, margin: '0 auto', padding: '0 24px 80px' }}>
        {isAdmin && (
          <div style={{ display: 'flex', justifyContent: 'flex-end', marginBottom: 28 }}>
            <button
              onClick={() => setShowCreatePanel(true)}
              style={{
                fontFamily: 'Cinzel, serif',
                fontSize: 12,
                letterSpacing: '0.2em',
                color: '#07030A',
                background: 'linear-gradient(135deg, #C9A84C, #E8C870)',
                border: 'none',
                padding: '12px 26px',
                borderRadius: 2,
                cursor: 'pointer',
                boxShadow: '0 4px 20px rgba(201,168,76,0.3)',
              }}
            >
              {t('home.newPost')}
            </button>
          </div>
        )}

        {isLoading && (
          <div style={{ display: 'flex', flexDirection: 'column', gap: 28 }}>
            <SkeletonCard />
            <SkeletonCard />
            <SkeletonCard />
          </div>
        )}

        {isError && !isLoading && (
          <div style={{ textAlign: 'center', padding: '60px 0' }}>
            <div style={{ fontFamily: 'Cinzel, serif', fontSize: 13, letterSpacing: '0.3em', color: '#C04040', marginBottom: 12 }}>
              {t('home.errorLoading')}
            </div>
          </div>
        )}

        {!isLoading && !isError && feed.length === 0 && (
          <div style={{ textAlign: 'center', padding: '80px 0', animation: 'fadeInUp 0.6s ease-out' }}>
            <div style={{ fontSize: 44, marginBottom: 16, opacity: 0.5 }}>&#9998;</div>
            <p
              style={{
                fontFamily: 'Cormorant Garamond, serif',
                fontSize: 20,
                fontStyle: 'italic',
                color: '#C9A84C',
                maxWidth: 380,
                margin: '0 auto',
              }}
            >
              {t('home.noNews')}
            </p>
          </div>
        )}

        {!isLoading && !isError && feed.length > 0 && (
          <div style={{ display: 'flex', flexDirection: 'column', gap: 28 }}>
            {feed.map((item) => {
              if (item.type === 'News' && item.postId) {
                const post: NewsPost = {
                  id: item.postId,
                  title: item.postTitle ?? '',
                  content: item.postContent ?? '',
                  coverImageUrl: item.postCoverImageUrl,
                  authorId: '',
                  authorDisplayName: item.postAuthorDisplayName ?? '',
                  createdAt: item.timestamp,
                  updatedAt: item.timestamp,
                  translations: [],
                }
                return (
                  <NewsPostCard
                    key={`news-${item.postId}`}
                    post={post}
                    isAdmin={isAdmin}
                    onEdit={() => handleEditPost(item.postId!)}
                    onDelete={() => setConfirmDelete(item.postId!)}
                  />
                )
              }

              return (
                <BottleActivityCard
                  key={`${item.type}-${item.bottleId}`}
                  item={item}
                />
              )
            })}
          </div>
        )}
      </main>

      {showCreatePanel && (
        <PostFormPanel
          mode="create"
          pending={createMutation.isPending}
          error={createMutation.isError}
          onSubmit={(payload) => createMutation.mutate(payload)}
          onClose={() => setShowCreatePanel(false)}
        />
      )}

      {editingPost && (
        <PostFormPanel
          mode="edit"
          initial={editingPost}
          pending={updateMutation.isPending}
          error={updateMutation.isError}
          onSubmit={(payload) => updateMutation.mutate({ id: editingPost.id, payload })}
          onClose={() => setEditingPost(null)}
        />
      )}

      {confirmDelete && (
        <ConfirmDeleteDialog
          pending={deleteMutation.isPending}
          onConfirm={() => deleteMutation.mutate(confirmDelete)}
          onCancel={() => setConfirmDelete(null)}
        />
      )}
    </div>
  )
}
