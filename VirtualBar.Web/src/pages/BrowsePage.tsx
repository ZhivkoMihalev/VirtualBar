import { useState, useEffect } from 'react'
import type { CSSProperties } from 'react'
import { Link } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { searchUsers } from '../api/usersApi'
import type { UserSearchResult } from '../types'
import NavBar from '../components/NavBar'

function useDebounced<T>(value: T, delay: number): T {
  const [debounced, setDebounced] = useState(value)

  useEffect(() => {
    const handle = setTimeout(() => setDebounced(value), delay)
    return () => clearTimeout(handle)
  }, [value, delay])

  return debounced
}

const inputStyle: CSSProperties = {
  background: '#0A0502',
  border: '1px solid rgba(201,168,76,0.2)',
  color: '#F0DDB4',
  fontFamily: 'Cormorant Garamond, serif',
  fontSize: 17,
  padding: '12px 16px 12px 44px',
  borderRadius: 4,
  outline: 'none',
  width: '100%',
}

function Avatar({ name, url, size }: { name: string; url?: string; size: number }) {
  const initial = name.trim().charAt(0).toUpperCase() || '?'

  if (url) {
    return (
      <img
        src={url}
        alt={name}
        style={{
          width: size,
          height: size,
          borderRadius: '50%',
          objectFit: 'cover',
          border: '1.5px solid rgba(201,168,76,0.4)',
          flexShrink: 0,
        }}
      />
    )
  }

  return (
    <div
      style={{
        width: size,
        height: size,
        borderRadius: '50%',
        border: '1.5px solid rgba(201,168,76,0.4)',
        background: 'radial-gradient(ellipse at 50% 30%, rgba(201,168,76,0.15), rgba(10,5,2,0.6))',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        fontFamily: 'Playfair Display, serif',
        fontSize: size * 0.42,
        color: '#E8C870',
        flexShrink: 0,
      }}
    >
      {initial}
    </div>
  )
}

function CollectorCard({ collector }: { collector: UserSearchResult }) {
  const { t } = useTranslation()
  const [hover, setHover] = useState(false)

  return (
    <Link
      to={`/bar/${collector.id}`}
      onMouseEnter={() => setHover(true)}
      onMouseLeave={() => setHover(false)}
      style={{
        display: 'flex',
        flexDirection: 'column',
        gap: 14,
        padding: '24px 22px',
        background: 'rgba(201,168,76,0.04)',
        border: hover ? '1px solid rgba(201,168,76,0.4)' : '1px solid rgba(201,168,76,0.12)',
        borderRadius: 6,
        textDecoration: 'none',
        transition: 'all 0.2s ease',
        transform: hover ? 'translateY(-3px)' : 'translateY(0)',
        boxShadow: hover ? '0 10px 30px rgba(0,0,0,0.5)' : 'none',
      }}
    >
      <div style={{ display: 'flex', alignItems: 'center', gap: 14 }}>
        <Avatar name={collector.displayName} url={collector.avatarUrl} size={56} />
        <div style={{ minWidth: 0 }}>
          <div
            style={{
              fontFamily: 'Playfair Display, serif',
              fontSize: 20,
              fontWeight: 700,
              color: '#E8C870',
              lineHeight: 1.2,
              overflow: 'hidden',
              textOverflow: 'ellipsis',
              whiteSpace: 'nowrap',
            }}
          >
            {collector.displayName}
          </div>
          {collector.country && (
            <div style={{ fontFamily: 'Cormorant Garamond, serif', fontSize: 14, color: '#B09868' }}>
              {collector.country}
            </div>
          )}
        </div>
      </div>

      <p
        style={{
          fontFamily: 'Cormorant Garamond, serif',
          fontSize: 16,
          fontStyle: 'italic',
          color: '#C9A84C',
          lineHeight: 1.45,
          margin: 0,
          minHeight: 46,
          display: '-webkit-box',
          WebkitLineClamp: 2,
          WebkitBoxOrient: 'vertical',
          overflow: 'hidden',
        }}
      >
        {collector.bio || t('browse.defaultBio')}
      </p>

      <div
        style={{
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'space-between',
          paddingTop: 12,
          borderTop: '1px solid rgba(201,168,76,0.1)',
        }}
      >
        <div style={{ display: 'flex', gap: 16, fontFamily: 'Cormorant Garamond, serif', fontSize: 14, color: '#B09868' }}>
          <span>
            {t('browse.bottles', { count: collector.bottleCount })}
          </span>
          <span>
            {t('browse.followers', { count: collector.followerCount })}
          </span>
        </div>
        <span style={{ fontFamily: 'Cinzel, serif', fontSize: 11, letterSpacing: '0.15em', color: hover ? '#E8C870' : '#C9A84C' }}>
          {t('browse.viewBar')}
        </span>
      </div>
    </Link>
  )
}

export default function BrowsePage() {
  const { t } = useTranslation()
  const [search, setSearch] = useState('')
  const query = useDebounced(search.trim(), 300)

  const { data: collectors = [], isLoading, isError } = useQuery({
    queryKey: ['users', query],
    queryFn: () => searchUsers(query || undefined),
  })

  return (
    <div style={{ minHeight: '100vh', color: '#F0DDB4' }}>
      <NavBar />

      <main style={{ maxWidth: 1100, margin: '0 auto', padding: '40px 40px' }}>
        <div style={{ marginBottom: 28 }}>
          <div style={{ fontFamily: 'Cinzel, serif', fontSize: 13, letterSpacing: '0.4em', color: '#B09868', marginBottom: 8 }}>
            {t('browse.discover')}
          </div>
          <h1 style={{ fontFamily: 'Playfair Display, serif', fontSize: 38, fontWeight: 700, color: '#E8C870', margin: 0, lineHeight: 1.1 }}>
            {t('browse.title')}
          </h1>
          <div style={{ fontFamily: 'Cormorant Garamond, serif', fontSize: 18, fontStyle: 'italic', color: '#C9A84C', marginTop: 6 }}>
            {t('browse.subtitle')}
          </div>
        </div>

        <div style={{ position: 'relative', maxWidth: 480, marginBottom: 36 }}>
          <span
            style={{
              position: 'absolute',
              left: 16,
              top: '50%',
              transform: 'translateY(-50%)',
              fontSize: 16,
              color: '#C9A84C',
              pointerEvents: 'none',
            }}
          >
            ⌕
          </span>
          <input
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            placeholder={t('browse.searchPlaceholder')}
            onFocus={(e) => { e.currentTarget.style.border = '1px solid rgba(201,168,76,0.5)' }}
            onBlur={(e) => { e.currentTarget.style.border = '1px solid rgba(201,168,76,0.2)' }}
            style={inputStyle}
          />
        </div>

        {isLoading && (
          <div
            style={{
              textAlign: 'center',
              padding: '80px 0',
              fontFamily: 'Cinzel, serif',
              fontSize: 13,
              letterSpacing: '0.4em',
              color: '#C9A84C',
              animation: 'shimmer 1.6s ease-in-out infinite',
            }}
          >
            {t('browse.loading')}
          </div>
        )}

        {isError && !isLoading && (
          <div style={{ textAlign: 'center', padding: '60px 0', fontFamily: 'Cormorant Garamond, serif', fontSize: 18, fontStyle: 'italic', color: '#C04040' }}>
            {t('browse.error')}
          </div>
        )}

        {!isLoading && !isError && collectors.length === 0 && (
          <div style={{ textAlign: 'center', padding: '60px 0', fontFamily: 'Cormorant Garamond, serif', fontSize: 20, fontStyle: 'italic', color: '#B09868' }}>
            {t('browse.noResults')}
          </div>
        )}

        {!isLoading && !isError && collectors.length > 0 && (
          <div
            style={{
              display: 'grid',
              gridTemplateColumns: 'repeat(auto-fill, minmax(300px, 1fr))',
              gap: 20,
            }}
          >
            {collectors.map((collector) => (
              <CollectorCard key={collector.id} collector={collector} />
            ))}
          </div>
        )}
      </main>
    </div>
  )
}
