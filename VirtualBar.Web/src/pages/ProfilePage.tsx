import { useRef, useState } from 'react'
import type { FormEvent } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { toast } from 'sonner'
import { ImagePlus } from 'lucide-react'
import { useAuth } from '../contexts/AuthContext'
import NavBar from '../components/NavBar'
import Avatar from '../components/Avatar'
import BadgeChip from '../components/BadgeChip'
import { updateProfile, uploadAvatar } from '../api/usersApi'
import { getMyProgress } from '../api/badgesApi'
import type { UpdatedProfile } from '../types'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Textarea } from '@/components/ui/textarea'
import { Label } from '@/components/ui/label'
import { Separator } from '@/components/ui/separator'
import { Skeleton } from '@/components/ui/skeleton'

export default function ProfilePage() {
  const { t } = useTranslation()
  const { user, updateUser } = useAuth()
  const queryClient = useQueryClient()

  const [displayName, setDisplayName] = useState(user?.displayName ?? '')
  const [bio, setBio] = useState(user?.bio ?? '')
  const [country, setCountry] = useState(user?.country ?? '')
  const [city, setCity] = useState(user?.city ?? '')
  const [avatarUrl, setAvatarUrl] = useState(user?.avatarUrl)
  const fileRef = useRef<HTMLInputElement>(null)

  const progressQuery = useQuery({ queryKey: ['badges', 'progress'], queryFn: getMyProgress })
  const earnedBadges = (progressQuery.data ?? []).filter(p => p.earned)
  const unearnedBadges = (progressQuery.data ?? []).filter(p => !p.earned)

  const avatarMutation = useMutation({
    mutationFn: (file: File) => uploadAvatar(file),
    onSuccess: (data: UpdatedProfile) => {
      setAvatarUrl(data.avatarUrl)
      updateUser({ avatarUrl: data.avatarUrl })
      if (user?.id) {
        queryClient.invalidateQueries({ queryKey: ['profile', user.id] })
      }
      queryClient.invalidateQueries({ queryKey: ['users'] })
    },
    onError: () => toast.error(t('profile.avatarError')),
  })

  const saveMutation = useMutation({
    mutationFn: () =>
      updateProfile({
        displayName: displayName.trim(),
        bio: bio.trim() || undefined,
        country: country.trim() || undefined,
        city: city.trim() || undefined,
      }),
    onSuccess: (data: UpdatedProfile) => {
      updateUser({
        displayName: data.displayName,
        bio: data.bio,
        country: data.country,
        city: data.city,
        avatarUrl: data.avatarUrl,
      })
      if (user?.id) {
        queryClient.invalidateQueries({ queryKey: ['profile', user.id] })
      }
      queryClient.invalidateQueries({ queryKey: ['users'] })
      toast.success(t('profile.successMessage'))
    },
    onError: () => toast.error(t('profile.errorMessage')),
  })

  const handleAvatarChange = (file: File | null) => {
    if (!file) return
    avatarMutation.mutate(file)
  }

  const handleSubmit = (e: FormEvent) => {
    e.preventDefault()
    if (!displayName.trim()) return
    saveMutation.mutate()
  }

  return (
    <div className="min-h-screen text-foreground">
      <NavBar />

      <main className="mx-auto max-w-[560px] px-6 py-12">
        <h1 className="font-heading text-2xl font-semibold text-primary sm:text-3xl">
          {t('profile.editTitle')}
        </h1>

        <Separator className="my-8" />

        <div className="mb-6 space-y-2">
          <Label>{t('profile.avatar')}</Label>
          <div className="flex items-center gap-4">
            <Avatar displayName={displayName} avatarUrl={avatarUrl ?? undefined} size={80} />
            <Button
              type="button"
              variant="outline"
              size="sm"
              onClick={() => fileRef.current?.click()}
              disabled={avatarMutation.isPending}
            >
              <ImagePlus className="size-3.5" />
              {t('profile.uploadAvatar')}
            </Button>
            <input
              ref={fileRef}
              type="file"
              accept="image/*"
              className="hidden"
              onChange={e => handleAvatarChange(e.target.files?.[0] ?? null)}
            />
          </div>
        </div>

        <form onSubmit={handleSubmit} className="space-y-5">
          <div className="space-y-2">
            <Label htmlFor="displayName">{t('profile.displayName')}</Label>
            <Input
              id="displayName"
              value={displayName}
              onChange={e => setDisplayName(e.target.value)}
              required
              className="h-9"
            />
          </div>

          <div className="space-y-2">
            <Label htmlFor="bio">{t('profile.bio')}</Label>
            <Textarea id="bio" value={bio} onChange={e => setBio(e.target.value)} rows={4} />
          </div>

          <div className="space-y-2">
            <Label htmlFor="country">{t('profile.country')}</Label>
            <Input id="country" value={country} onChange={e => setCountry(e.target.value)} className="h-9" />
          </div>

          <div className="space-y-2">
            <Label htmlFor="city">{t('profile.city')}</Label>
            <Input id="city" value={city} onChange={e => setCity(e.target.value)} className="h-9" />
          </div>

          <Button
            type="submit"
            size="lg"
            className="h-10 w-full"
            disabled={saveMutation.isPending || !displayName.trim()}
          >
            {saveMutation.isPending ? t('profile.saving') : t('profile.save')}
          </Button>
        </form>

        {!progressQuery.isError && (
          <>
            <Separator className="my-8" />

            <section>
              <h2 className="font-heading text-2xl font-semibold text-primary sm:text-3xl">
                {t('badges.title')}
              </h2>

              {progressQuery.isLoading ? (
                <div className="mt-6 flex flex-wrap gap-4">
                  {Array.from({ length: 8 }).map((_, i) => (
                    <Skeleton key={i} className="size-[72px] rounded-full" />
                  ))}
                </div>
              ) : progressQuery.data ? (
                <>
                  {earnedBadges.length > 0 && (
                    <div className="mt-6 grid grid-cols-[repeat(auto-fill,minmax(84px,1fr))] gap-x-4 gap-y-6">
                      {earnedBadges.map(p => (
                        <BadgeChip key={p.badge} badge={p.badge} earned awardedAt={p.awardedAt} size={72} />
                      ))}
                    </div>
                  )}

                  {unearnedBadges.length > 0 && (
                    <>
                      <h3 className="mt-9 font-heading text-lg font-medium text-primary/80">
                        {t('badges.progressTitle')}
                      </h3>
                      <div className="mt-4 grid grid-cols-[repeat(auto-fill,minmax(84px,1fr))] gap-x-4 gap-y-6">
                        {unearnedBadges.map(p => (
                          <div key={p.badge} className="flex flex-col items-center gap-2">
                            <BadgeChip badge={p.badge} earned={false} size={72} />
                            <div className="w-full">
                              <div className="h-1 overflow-hidden rounded-full bg-primary/20">
                                <div
                                  className="h-full rounded-full bg-primary"
                                  style={{ width: `${(Math.min(p.current, p.threshold) / p.threshold) * 100}%` }}
                                />
                              </div>
                              <div className="mt-1 text-center text-[11px] text-foreground/75">
                                {Math.min(p.current, p.threshold)}/{p.threshold}
                              </div>
                            </div>
                          </div>
                        ))}
                      </div>
                    </>
                  )}
                </>
              ) : null}
            </section>
          </>
        )}
      </main>
    </div>
  )
}
