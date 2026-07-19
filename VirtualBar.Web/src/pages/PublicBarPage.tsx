import { useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { MessageCircle, Loader2 } from 'lucide-react'
import { useAuth } from '../contexts/AuthContext'
import { useChat } from '../contexts/ChatContext'
import { getBottlesByUser } from '../api/bottlesApi'
import { getUserProfile, followUser, unfollowUser } from '../api/usersApi'
import { getUserBadges } from '../api/badgesApi'
import type { Bottle, UserProfile } from '../types'
import { VirtualBarScene } from '../components/BarShelf'
import BottleDetailPanel from '../components/BottleDetailPanel'
import Avatar from '../components/Avatar'
import BadgeChip from '../components/BadgeChip'
import NavBar from '../components/NavBar'
import { Button } from '@/components/ui/button'
import { Card, CardContent } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'

function FollowButton({ profile, userId }: { profile: UserProfile; userId: string }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()

  const mutation = useMutation({
    mutationFn: () => (profile.isFollowedByMe ? unfollowUser(userId) : followUser(userId)),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['profile', userId] })
    },
  })

  const following = profile.isFollowedByMe

  return (
    <Button
      variant={following ? 'outline' : 'default'}
      onClick={() => mutation.mutate()}
      disabled={mutation.isPending}
      className="group/follow w-full min-w-28"
    >
      {mutation.isPending && <Loader2 className="size-3.5 animate-spin" />}
      {following ? (
        <>
          <span className="group-hover/follow:hidden">{t('publicBar.following')}</span>
          <span className="hidden group-hover/follow:inline">{t('publicBar.unfollow')}</span>
        </>
      ) : (
        t('publicBar.follow')
      )}
    </Button>
  )
}

function EarnedBadgesStrip({ userId }: { userId: string }) {
  const { data: badges } = useQuery({
    queryKey: ['badges', userId],
    queryFn: () => getUserBadges(userId),
  })

  if (!badges || badges.length === 0) return null

  return (
    <div className="mb-9 flex flex-wrap justify-center gap-5 sm:justify-start">
      {badges.map(b => (
        <BadgeChip key={b.badge} badge={b.badge} earned awardedAt={b.awardedAt} size={64} />
      ))}
    </div>
  )
}

export default function PublicBarPage() {
  const { t } = useTranslation()
  const { userId } = useParams<{ userId: string }>()
  const { user } = useAuth()
  const { openChat } = useChat()
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
    <div className="min-h-screen text-foreground">
      <NavBar />

      <main className="mx-auto max-w-6xl px-6 py-10">
        {isLoading && (
          <div className="space-y-6">
            <Card>
              <CardContent className="flex items-start gap-6">
                <Skeleton className="size-[72px] rounded-full" />
                <div className="flex-1 space-y-3">
                  <Skeleton className="h-7 w-48" />
                  <Skeleton className="h-4 w-32" />
                  <Skeleton className="h-4 w-64" />
                </div>
              </CardContent>
            </Card>
            <Skeleton className="h-[480px] w-full rounded-lg" />
          </div>
        )}

        {isError && !isLoading && (
          <div className="mx-auto max-w-md rounded-md border border-destructive/40 bg-destructive/10 px-3 py-2 text-center text-sm text-destructive">
            {t('publicBar.error')}
          </div>
        )}

        {!isLoading && !isError && profile && (
          <>
            <div className="mb-6">
              <Link
                to="/browse"
                className="text-sm text-muted-foreground transition-colors hover:text-foreground"
              >
                {t('publicBar.backToBrowse')}
              </Link>
            </div>

            <Card className="mb-9">
              <CardContent className="flex flex-col items-start gap-6 sm:flex-row">
                <Avatar displayName={profile.displayName} avatarUrl={profile.avatarUrl} size={72} />

                <div className="min-w-0 flex-1">
                  <h1 className="font-heading text-2xl font-bold text-primary sm:text-3xl">
                    {profile.displayName}
                  </h1>

                  {(profile.country || profile.city) && (
                    <div className="mt-1 text-base italic text-muted-foreground">
                      {[profile.city, profile.country].filter(Boolean).join(', ')}
                    </div>
                  )}

                  {profile.bio && (
                    <p className="mt-2.5 max-w-xl text-base italic leading-relaxed text-muted-foreground">
                      {profile.bio}
                    </p>
                  )}

                  <div className="mt-3.5 text-sm text-primary">
                    {t('publicBar.stats', {
                      bottles: t('publicBar.bottles', { count: profile.bottleCount }),
                      followers: t('publicBar.followers', { count: profile.followerCount }),
                      following: t('publicBar.following', { count: profile.followingCount }),
                    })}
                  </div>
                </div>

                {canFollow && (
                  <div className="flex w-full shrink-0 flex-col gap-2.5 sm:w-auto sm:min-w-32">
                    <FollowButton profile={profile} userId={profile.id} />
                    <Button variant="outline" className="w-full" onClick={() => openChat(profile.id)}>
                      <MessageCircle className="size-3.5" />
                      {t('messages.sendMessage')}
                    </Button>
                  </div>
                )}
              </CardContent>
            </Card>

            {userId && <EarnedBadgesStrip userId={userId} />}

            {bottles.length > 0 ? (
              <VirtualBarScene bottles={bottles} onSelect={setSelectedBottle} />
            ) : (
              <div className="py-20 text-center text-xl italic text-muted-foreground">
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
