import { useState, useEffect } from 'react'
import type { CSSProperties } from 'react'
import { Link } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { useAuth } from '../contexts/AuthContext'
import { searchUsers } from '../api/usersApi'
import type { UserSearchResult } from '../types'

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
        {collector.bio || 'A collector of fine spirits.'}
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
            {collector.bottleCount} {collector.bottleCount === 1 ? 'bottle' : 'bottles'}
          </span>
          <span>
            {collector.followerCount} {collector.followerCount === 1 ? 'follower' : 'followers'}
          </span>
        </div>
        <span style={{ fontFamily: 'Cinzel, serif', fontSize: 11, letterSpacing: '0.15em', color: hover ? '#E8C870' : '#C9A84C' }}>
          VIEW BAR →
        </span>
      </div>
    </Link>
  )
}

export default function BrowsePage() {
  const { isAuthenticated, user, logout } = useAuth()
  const [search, setSearch] = useState('')
  const query = useDebounced(search.trim(), 300)

  const { data: collectors = [], isLoading, isError } = useQuery({
    queryKey: ['users', query],
    queryFn: () => searchUsers(query || undefined),
  })

  return (
    <div style={{ minHeight: '100vh', background: '#07030A', color: '#F0DDB4' }}>
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
          <span style={{ fontFamily: 'Playfair Display, serif', fontSize: 18, color: '#E8C870', letterSpacing: '0.05em' }}>
            VirtualBar
          </span>
        </Link>

        <div style={{ display: 'flex', alignItems: 'center', gap: 20 }}>
          {isAuthenticated ? (
            <>
              <Link to="/dashboard" style={{ fontFamily: 'Cinzel, serif', fontSize: 11, letterSpacing: '0.15em', color: '#B09868', textDecoration: 'none' }}>
                ← BACK TO MY BAR
              </Link>
              <span style={{ fontFamily: 'Cormorant Garamond, serif', fontSize: 15, color: '#C9A84C' }}>
                {user?.displayName}
              </span>
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
                SIGN OUT
              </button>
            </>
          ) : (
            <Link
              to="/login"
              style={{
                fontFamily: 'Cinzel, serif',
                fontSize: 11,
                letterSpacing: '0.15em',
                color: '#B09868',
                border: '1px solid rgba(201,168,76,0.2)',
                background: 'transparent',
                padding: '6px 16px',
                borderRadius: 2,
                textDecoration: 'none',
              }}
            >
              SIGN IN
            </Link>
          )}
        </div>
      </nav>

      <main style={{ maxWidth: 1100, margin: '0 auto', padding: '40px 40px' }}>
        <div style={{ marginBottom: 28 }}>
          <div style={{ fontFamily: 'Cinzel, serif', fontSize: 13, letterSpacing: '0.4em', color: '#B09868', marginBottom: 8 }}>
            DISCOVER
          </div>
          <h1 style={{ fontFamily: 'Playfair Display, serif', fontSize: 38, fontWeight: 700, color: '#E8C870', margin: 0, lineHeight: 1.1 }}>
            Browse Collectors
          </h1>
          <div style={{ fontFamily: 'Cormorant Garamond, serif', fontSize: 18, fontStyle: 'italic', color: '#C9A84C', marginTop: 6 }}>
            Explore the virtual bars of fellow connoisseurs
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
            placeholder="Search collectors by name…"
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
            SEARCHING…
          </div>
        )}

        {isError && !isLoading && (
          <div style={{ textAlign: 'center', padding: '60px 0', fontFamily: 'Cormorant Garamond, serif', fontSize: 18, fontStyle: 'italic', color: '#C04040' }}>
            Could not load collectors. Please try again.
          </div>
        )}

        {!isLoading && !isError && collectors.length === 0 && (
          <div style={{ textAlign: 'center', padding: '60px 0', fontFamily: 'Cormorant Garamond, serif', fontSize: 20, fontStyle: 'italic', color: '#B09868' }}>
            No collectors found.
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
