import { useState } from 'react'
import type { CSSProperties } from 'react'
import { Link } from 'react-router-dom'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { useAuth } from '../contexts/AuthContext'
import {
  createNewsPost,
  updateNewsPost,
  deleteNewsPost,
  getNewsPost,
} from '../api/newsApi'
import { getFeed } from '../api/feedApi'
import type { CreateNewsPostPayload } from '../api/newsApi'
import type { FeedItem, NewsPost } from '../types'

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

const navLinkStyle: CSSProperties = {
  fontFamily: 'Cinzel, serif',
  fontSize: 11,
  letterSpacing: '0.15em',
  color: '#B09868',
  textDecoration: 'none',
}

function focusOn(e: React.FocusEvent<HTMLInputElement | HTMLTextAreaElement>) {
  e.currentTarget.style.border = '1px solid rgba(201,168,76,0.5)'
}

function focusOff(e: React.FocusEvent<HTMLInputElement | HTMLTextAreaElement>) {
  e.currentTarget.style.border = '1px solid rgba(201,168,76,0.2)'
}

function NavBar() {
  const { user, isAuthenticated, logout } = useAuth()

  return (
    <nav
      style={{
        borderBottom: '1px solid rgba(201,168,76,0.12)',
        background: 'rgba(7,3,10,0.95)',
        backdropFilter: 'blur(8px)',
        position: 'sticky',
        top: 0,
        zIndex: 40,
        padding: '0 40px',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'space-between',
        height: 64,
      }}
    >
      <Link to="/" style={{ display: 'flex', alignItems: 'center', gap: 12, textDecoration: 'none' }}>
        <div
          style={{
            width: 36,
            height: 36,
            borderRadius: '50%',
            border: '1.5px solid #C9A84C',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            fontFamily: 'Cinzel, serif',
            fontSize: 12,
            color: '#C9A84C',
            letterSpacing: '0.05em',
          }}
        >
          VB
        </div>
        <span style={{ fontFamily: 'Playfair Display, serif', fontSize: 18, color: '#E8C870', letterSpacing: '0.1em' }}>
          VIRTUALBAR
        </span>
      </Link>

      <div style={{ display: 'flex', alignItems: 'center', gap: 22 }}>
        <Link to="/" style={navLinkStyle}>HOME</Link>
        <Link to="/browse" style={navLinkStyle}>BROWSE</Link>
        <Link to="/marketplace" style={navLinkStyle}>MARKETPLACE</Link>

        {isAuthenticated ? (
          <>
            <span style={{ fontFamily: 'Cormorant Garamond, serif', fontSize: 15, color: '#C9A84C' }}>
              {user?.displayName}
            </span>
            <Link to="/dashboard" style={navLinkStyle}>MY BAR</Link>
            <button
              onClick={logout}
              style={{
                fontFamily: 'Cinzel, serif',
                fontSize: 11,
                letterSpacing: '0.15em',
                color: '#B09868',
                border: '1px solid rgba(201,168,76,0.2)',
                background: 'transparent',
                padding: '6px 16px',
                borderRadius: 2,
                cursor: 'pointer',
              }}
            >
              LOGOUT
            </button>
          </>
        ) : (
          <>
            <Link to="/login" style={navLinkStyle}>LOGIN</Link>
            <Link
              to="/register"
              style={{
                fontFamily: 'Cinzel, serif',
                fontSize: 11,
                letterSpacing: '0.15em',
                color: '#07030A',
                background: 'linear-gradient(135deg, #C9A84C, #E8C870)',
                padding: '7px 18px',
                borderRadius: 2,
                textDecoration: 'none',
              }}
            >
              REGISTER
            </Link>
          </>
        )}
      </div>
    </nav>
  )
}

function Hero() {
  return (
    <header style={{ textAlign: 'center', padding: '64px 24px 48px', position: 'relative' }}>
      <div
        style={{
          fontFamily: 'Cinzel, serif',
          fontSize: 12,
          letterSpacing: '0.5em',
          color: '#B09868',
          marginBottom: 18,
        }}
      >
        VOL. I &middot; THE SPIRITS DISPATCH
      </div>
      <h1
        style={{
          fontFamily: 'Playfair Display, serif',
          fontSize: 64,
          fontWeight: 700,
          color: '#E8C870',
          margin: 0,
          lineHeight: 1,
          letterSpacing: '0.04em',
        }}
      >
        THE CHRONICLE
      </h1>
      <div
        style={{
          width: 180,
          height: 2,
          margin: '24px auto',
          background: 'linear-gradient(90deg, transparent, #C9A84C, transparent)',
        }}
      />
      <p
        style={{
          fontFamily: 'Cormorant Garamond, serif',
          fontSize: 21,
          fontStyle: 'italic',
          color: '#C9A84C',
          maxWidth: 620,
          margin: '0 auto',
          lineHeight: 1.5,
        }}
      >
        Dispatches from the world of rare whisky, aged rum, and fine cognac &mdash;
        releases, auctions, and the stories behind the bottle.
      </p>
    </header>
  )
}

function SkeletonCard() {
  return (
    <div
      style={{
        background: 'rgba(201,168,76,0.04)',
        borderLeft: '3px solid rgba(201,168,76,0.2)',
        borderRadius: 6,
        padding: 28,
        animation: 'shimmer 1.6s ease-in-out infinite',
      }}
    >
      <div style={{ height: 12, width: 110, background: 'rgba(201,168,76,0.18)', borderRadius: 2, marginBottom: 18 }} />
      <div style={{ height: 28, width: '70%', background: 'rgba(201,168,76,0.14)', borderRadius: 2, marginBottom: 16 }} />
      <div style={{ height: 14, width: '100%', background: 'rgba(201,168,76,0.08)', borderRadius: 2, marginBottom: 8 }} />
      <div style={{ height: 14, width: '85%', background: 'rgba(201,168,76,0.08)', borderRadius: 2, marginBottom: 8 }} />
      <div style={{ height: 14, width: '60%', background: 'rgba(201,168,76,0.08)', borderRadius: 2 }} />
    </div>
  )
}

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
  const [hover, setHover] = useState(false)

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
          THE CHRONICLE
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
            margin: '0 0 20px',
          }}
        >
          {post.excerpt}
        </p>

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
            By {post.authorDisplayName} &middot; {formatDate(post.createdAt)}
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
                EDIT
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
                DELETE
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
  const [hover, setHover] = useState(false)
  const forSale = item.type === 'ForSale'
  const accentColor = forSale ? '#E8C870' : '#C9A84C'

  const headerText = forSale ? 'listed for sale' : 'added to their collection'
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
              FOR SALE
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
  const [title, setTitle] = useState(initial?.title ?? '')
  const [excerpt, setExcerpt] = useState(initial?.excerpt ?? '')
  const [content, setContent] = useState(initial?.content ?? '')
  const [coverImageUrl, setCoverImageUrl] = useState(initial?.coverImageUrl ?? '')

  const valid = title.trim() && excerpt.trim() && content.trim()

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault()
    if (!valid) return
    onSubmit({
      title: title.trim(),
      excerpt: excerpt.trim(),
      content: content.trim(),
      coverImageUrl: coverImageUrl.trim() || undefined,
    })
  }

  return (
    <div style={{ position: 'fixed', inset: 0, zIndex: 50 }}>
      <div onClick={onClose} style={{ position: 'absolute', inset: 0, background: 'rgba(4,2,1,0.85)' }} />

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
            {mode === 'create' ? 'NEW DISPATCH' : 'EDIT DISPATCH'}
          </div>
          <button
            onClick={onClose}
            aria-label="Close panel"
            style={{ background: 'transparent', border: 'none', color: '#B09868', fontSize: 24, cursor: 'pointer', lineHeight: 1 }}
          >
            &times;
          </button>
        </div>

        <form onSubmit={handleSubmit}>
          <label style={labelStyle}>Title *</label>
          <input
            value={title}
            onChange={(e) => setTitle(e.target.value)}
            onFocus={focusOn}
            onBlur={focusOff}
            required
            placeholder="A rare cask emerges"
            style={{ ...inputStyle, marginBottom: 18 }}
          />

          <label style={labelStyle}>Excerpt *</label>
          <textarea
            value={excerpt}
            onChange={(e) => setExcerpt(e.target.value)}
            onFocus={focusOn}
            onBlur={focusOff}
            required
            rows={2}
            placeholder="A short teaser for the feed&hellip;"
            style={{ ...inputStyle, marginBottom: 18, resize: 'vertical' }}
          />

          <label style={labelStyle}>Content *</label>
          <textarea
            value={content}
            onChange={(e) => setContent(e.target.value)}
            onFocus={focusOn}
            onBlur={focusOff}
            required
            rows={10}
            placeholder="The full story&hellip;"
            style={{ ...inputStyle, marginBottom: 18, resize: 'vertical', lineHeight: 1.5 }}
          />

          <label style={labelStyle}>Cover Image URL</label>
          <input
            value={coverImageUrl}
            onChange={(e) => setCoverImageUrl(e.target.value)}
            onFocus={focusOn}
            onBlur={focusOff}
            placeholder="https://&hellip;"
            style={{ ...inputStyle, marginBottom: 24 }}
          />

          {error && (
            <div
              style={{
                fontFamily: 'Cormorant Garamond, serif',
                fontSize: 15,
                color: '#D42020',
                marginBottom: 16,
              }}
            >
              Something went wrong. Please try again.
            </div>
          )}

          <button
            type="submit"
            disabled={pending || !valid}
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
              cursor: pending || !valid ? 'not-allowed' : 'pointer',
              opacity: pending || !valid ? 0.6 : 1,
              boxShadow: '0 4px 20px rgba(201,168,76,0.3)',
              marginBottom: 12,
            }}
          >
            {pending ? 'Saving&hellip;' : mode === 'create' ? 'Publish' : 'Save Changes'}
          </button>

          <button
            type="button"
            onClick={onClose}
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
            CANCEL
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
        <div style={{ fontFamily: 'Cinzel, serif', fontSize: 13, letterSpacing: '0.3em', color: '#C04040', marginBottom: 16 }}>
          REMOVE DISPATCH
        </div>
        <p
          style={{
            fontFamily: 'Cormorant Garamond, serif',
            fontSize: 18,
            fontStyle: 'italic',
            color: '#C9A84C',
            margin: '0 0 28px',
          }}
        >
          This dispatch will be permanently removed from The Chronicle.
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
            CANCEL
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
            {pending ? '...' : 'DELETE'}
          </button>
        </div>
      </div>
    </div>
  )
}

export default function HomePage() {
  const { user } = useAuth()
  const queryClient = useQueryClient()
  const isAdmin = user?.isAdmin === true

  const [showCreatePanel, setShowCreatePanel] = useState(false)
  const [editingPost, setEditingPost] = useState<NewsPost | null>(null)
  const [confirmDelete, setConfirmDelete] = useState<string | null>(null)

  async function handleEditPost(postId: string) {
    const full = await getNewsPost(postId)
    setEditingPost(full)
  }

  const { data: feed = [], isLoading, isError } = useQuery({
    queryKey: ['feed'],
    queryFn: () => getFeed(0, 50),
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
    <div style={{ minHeight: '100vh', background: '#07030A', color: '#F0DDB4' }}>
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
              + NEW POST
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
              THE PRESS HAS STALLED
            </div>
            <p style={{ fontFamily: 'Cormorant Garamond, serif', fontSize: 19, fontStyle: 'italic', color: '#C9A84C' }}>
              We could not load the latest dispatches. Please try again shortly.
            </p>
          </div>
        )}

        {!isLoading && !isError && feed.length === 0 && (
          <div style={{ textAlign: 'center', padding: '80px 0', animation: 'fadeInUp 0.6s ease-out' }}>
            <div style={{ fontSize: 44, marginBottom: 16, opacity: 0.5 }}>&#9998;</div>
            <div
              style={{
                fontFamily: 'Cinzel, serif',
                fontSize: 13,
                letterSpacing: '0.4em',
                color: '#C9A84C',
                marginBottom: 14,
              }}
            >
              THE PAGE IS BLANK
            </div>
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
              Nothing here yet. Follow fellow collectors and check back soon for the latest
              from the world of fine spirits.
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
                  excerpt: item.postExcerpt ?? '',
                  content: '',
                  coverImageUrl: item.postCoverImageUrl,
                  authorId: '',
                  authorDisplayName: item.postAuthorDisplayName ?? '',
                  createdAt: item.timestamp,
                  updatedAt: item.timestamp,
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
