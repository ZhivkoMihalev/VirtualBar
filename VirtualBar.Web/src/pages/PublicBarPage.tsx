import { useState } from 'react'
import { Link, useParams, useNavigate } from 'react-router-dom'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { useAuth } from '../contexts/AuthContext'
import { getBottlesByUser } from '../api/bottlesApi'
import { getUserProfile, followUser, unfollowUser } from '../api/usersApi'
import type { Bottle, UserProfile } from '../types'
import { VirtualBarScene } from '../components/BarShelf'
import BottleDetailPanel from '../components/BottleDetailPanel'
import NavBar from '../components/NavBar'

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
          border: '2px solid rgba(201,168,76,0.4)',
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
        border: '2px solid rgba(201,168,76,0.4)',
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

function FollowButton({ profile, userId }: { profile: UserProfile; userId: string }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const [hover, setHover] = useState(false)

  const mutation = useMutation({
    mutationFn: () => (profile.isFollowedByMe ? unfollowUser(userId) : followUser(userId)),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['profile', userId] })
    },
  })

  const following = profile.isFollowedByMe

  return (
    <button
      onClick={() => mutation.mutate()}
      disabled={mutation.isPending}
      onMouseEnter={() => setHover(true)}
      onMouseLeave={() => setHover(false)}
      style={{
        fontFamily: 'Cinzel, serif',
        fontSize: 11,
        letterSpacing: '0.2em',
        textTransform: 'uppercase',
        padding: '10px 24px',
        borderRadius: 2,
        cursor: mutation.isPending ? 'wait' : 'pointer',
        opacity: mutation.isPending ? 0.6 : 1,
        transition: 'all 0.2s ease',
        color: following ? '#C9A84C' : '#07030A',
        background: following ? 'transparent' : 'linear-gradient(135deg, #C9A84C, #E8C870)',
        border: following ? '1px solid rgba(201,168,76,0.4)' : 'none',
        boxShadow: following ? 'none' : '0 4px 20px rgba(201,168,76,0.3)',
      }}
    >
      {following ? (hover ? t('publicBar.unfollow') : t('publicBar.following')) : t('publicBar.follow')}
    </button>
  )
}

export default function PublicBarPage() {
  const { t } = useTranslation()
  const { userId } = useParams<{ userId: string }>()
  const navigate = useNavigate()
  const { user } = useAuth()
  const [selectedBottle, setSelectedBottle] = useState<Bottle | null>(null)

  const {
    data: profile,
    isLoading: profileLoading,
    isError: profileError,
  } = useQuery({
    queryKey: ['profile', userId],
    queryFn: () => getUserProfile(userId!),
    enabled: !!userId,
  })

  const {
    data: bottles = [],
    isLoading: bottlesLoading,
    isError: bottlesError,
  } = useQuery({
    queryKey: ['bottles', userId],
    queryFn: () => getBottlesByUser(userId!),
    enabled: !!userId,
  })

  const isLoading = profileLoading || bottlesLoading
  const isError = profileError || bottlesError
  const canFollow = !!user && !!profile && user.id !== profile.id

  return (
    <div style={{ minHeight: '100vh', color: '#F0DDB4' }}>
      <NavBar />

      <main style={{ maxWidth: 1200, margin: '0 auto', padding: '40px 40px' }}>
        {isLoading && (
          <div
            style={{
              textAlign: 'center',
              padding: '120px 0',
              fontFamily: 'Cinzel, serif',
              fontSize: 13,
              letterSpacing: '0.4em',
              color: '#C9A84C',
              animation: 'shimmer 1.6s ease-in-out infinite',
            }}
          >
            {t('publicBar.loading')}
          </div>
        )}

        {isError && !isLoading && (
          <div style={{ textAlign: 'center', padding: '100px 0', fontFamily: 'Cormorant Garamond, serif', fontSize: 20, fontStyle: 'italic', color: '#C04040' }}>
            {t('publicBar.error')}
          </div>
        )}

        {!isLoading && !isError && profile && (
          <>
            <div style={{ marginBottom: 24 }}>
              <Link to="/browse" style={{ fontFamily: 'Cinzel, serif', fontSize: 11, letterSpacing: '0.15em', color: '#B09868', textDecoration: 'none' }}>
                {t('publicBar.backToBrowse')}
              </Link>
            </div>

            <div
              style={{
                display: 'flex',
                alignItems: 'flex-start',
                gap: 24,
                marginBottom: 36,
                padding: '24px 28px',
                background: 'rgba(201,168,76,0.04)',
                border: '1px solid rgba(201,168,76,0.1)',
                borderRadius: 6,
              }}
            >
              <Avatar name={profile.displayName} url={profile.avatarUrl} size={72} />

              <div style={{ flex: 1, minWidth: 0 }}>
                <h1 style={{ fontFamily: 'Playfair Display, serif', fontSize: 32, fontWeight: 700, color: '#E8C870', margin: 0, lineHeight: 1.15 }}>
                  {profile.displayName}
                </h1>

                {(profile.country || profile.city) && (
                  <div style={{ fontFamily: 'Cormorant Garamond, serif', fontSize: 16, fontStyle: 'italic', color: '#C9A84C', marginTop: 4 }}>
                    {[profile.city, profile.country].filter(Boolean).join(', ')}
                  </div>
                )}

                {profile.bio && (
                  <p style={{ fontFamily: 'Cormorant Garamond, serif', fontSize: 17, fontStyle: 'italic', color: '#B09868', lineHeight: 1.5, margin: '10px 0 0', maxWidth: 560 }}>
                    {profile.bio}
                  </p>
                )}

                <div style={{ fontFamily: 'Cormorant Garamond, serif', fontSize: 15, color: '#C9A84C', marginTop: 14, letterSpacing: '0.02em' }}>
                  {t('publicBar.stats', {
                    bottles: t('publicBar.bottles', { count: profile.bottleCount }),
                    followers: t('publicBar.followers', { count: profile.followerCount }),
                    following: t('publicBar.following', { count: profile.followingCount }),
                  })}
                </div>
              </div>

              {canFollow && (
                <div style={{ flexShrink: 0, display: 'flex', flexDirection: 'column', gap: 10 }}>
                  <FollowButton profile={profile} userId={profile.id} />
                  <button
                    onClick={() => navigate(`/messages?with=${profile.id}`)}
                    style={{
                      fontFamily: 'Cinzel, serif',
                      fontSize: 11,
                      letterSpacing: '0.2em',
                      textTransform: 'uppercase',
                      padding: '10px 24px',
                      borderRadius: 2,
                      cursor: 'pointer',
                      color: '#C9A84C',
                      background: 'transparent',
                      border: '1px solid rgba(201,168,76,0.4)',
                    }}
                  >
                    {t('messages.sendMessage')}
                  </button>
                </div>
              )}
            </div>

            {bottles.length > 0 ? (
              <VirtualBarScene bottles={bottles} onSelect={setSelectedBottle} />
            ) : (
              <div style={{ textAlign: 'center', padding: '80px 0', fontFamily: 'Cormorant Garamond, serif', fontSize: 20, fontStyle: 'italic', color: '#B09868' }}>
                {t('publicBar.empty')}
              </div>
            )}
          </>
        )}
      </main>

      {selectedBottle && userId && (
        <BottleDetailPanel
          bottle={bottles.find(b => b.id === selectedBottle.id) ?? selectedBottle}
          userId={userId}
          currentUserId={user?.id ?? ''}
          onClose={() => setSelectedBottle(null)}
        />
      )}
    </div>
  )
}
