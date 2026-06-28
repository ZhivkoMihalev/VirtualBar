import { useRef, useState } from 'react'
import type { FormEvent } from 'react'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { toast } from 'sonner'
import { ImagePlus } from 'lucide-react'
import { useAuth } from '../contexts/AuthContext'
import NavBar from '../components/NavBar'
import Avatar from '../components/Avatar'
import { updateProfile, uploadAvatar } from '../api/usersApi'
import type { UpdatedProfile } from '../types'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Textarea } from '@/components/ui/textarea'
import { Label } from '@/components/ui/label'
import { Separator } from '@/components/ui/separator'

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

  const avatarMutation = useMutation({
    mutationFn: (file: File) => uploadAvatar(file),
    onSuccess: (data: UpdatedProfile) => {
      setAvatarUrl(data.avatarUrl)
      updateUser({ avatarUrl: data.avatarUrl })
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
      </main>
    </div>
  )
}
