import { useState } from 'react'
import type { CSSProperties } from 'react'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { useAuth } from '../contexts/AuthContext'
import NavBar from '../components/NavBar'
import { updateProfile, uploadAvatar } from '../api/usersApi'
import type { UpdatedProfile } from '../types'

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

export default function ProfilePage() {
  const { t } = useTranslation()
  const { user, updateUser } = useAuth()
  const queryClient = useQueryClient()

  const [displayName, setDisplayName] = useState(user?.displayName ?? '')
  const [bio, setBio] = useState(user?.bio ?? '')
  const [country, setCountry] = useState(user?.country ?? '')
  const [city, setCity] = useState(user?.city ?? '')
  const [avatarUrl, setAvatarUrl] = useState(user?.avatarUrl)
  const [saved, setSaved] = useState(false)

  const avatarMutation = useMutation({
    mutationFn: (file: File) => uploadAvatar(file),
    onSuccess: (data: UpdatedProfile) => {
      setAvatarUrl(data.avatarUrl)
      updateUser({ avatarUrl: data.avatarUrl })
    },
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
      setSaved(true)
    },
  })

  const handleAvatarChange = (file: File | null) => {
    if (!file) return
    setSaved(false)
    avatarMutation.mutate(file)
  }

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault()
    if (!displayName.trim()) return
    setSaved(false)
    saveMutation.mutate()
  }

  const initial = displayName.trim().charAt(0).toUpperCase() || '?'

  return (
    <div style={{ minHeight: '100vh', color: '#F0DDB4' }}>
      <NavBar />

      <main style={{ maxWidth: 560, margin: '0 auto', padding: '48px 24px' }}>
        <div
          style={{
            fontFamily: 'Cinzel, serif',
            fontSize: 13,
            letterSpacing: '0.4em',
            color: '#B09868',
            marginBottom: 8,
          }}
        >
          {t('profile.editTitle')}
        </div>
        <h1
          style={{
            fontFamily: 'Playfair Display, serif',
            fontSize: 32,
            color: '#E8C870',
            margin: 0,
            lineHeight: 1.1,
          }}
        >
          {t('profile.editTitle')}
        </h1>

        <div style={{ borderTop: '1px solid rgba(201,168,76,0.15)', margin: '32px 0' }} />

        <label style={labelStyle}>{t('profile.avatar')}</label>
        <div style={{ display: 'flex', alignItems: 'center', gap: 16, marginBottom: 8 }}>
          {avatarUrl ? (
            <img
              src={avatarUrl}
              alt={displayName}
              style={{
                width: 80,
                height: 80,
                borderRadius: '50%',
                objectFit: 'cover',
                border: '2px solid rgba(201,168,76,0.4)',
                flexShrink: 0,
              }}
            />
          ) : (
            <div
              style={{
                width: 80,
                height: 80,
                borderRadius: '50%',
                border: '2px solid rgba(201,168,76,0.4)',
                background: 'radial-gradient(ellipse at 50% 30%, rgba(201,168,76,0.15), rgba(10,5,2,0.6))',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                fontFamily: 'Playfair Display, serif',
                fontSize: 34,
                color: '#E8C870',
                flexShrink: 0,
              }}
            >
              {initial}
            </div>
          )}

          <button
            type="button"
            onClick={() => document.getElementById('avatar-input')?.click()}
            disabled={avatarMutation.isPending}
            style={{
              fontFamily: 'Cinzel, serif',
              fontSize: 10,
              letterSpacing: '0.2em',
              color: '#C9A84C',
              background: 'transparent',
              border: '1px solid rgba(201,168,76,0.3)',
              padding: '8px 16px',
              borderRadius: 2,
              cursor: avatarMutation.isPending ? 'not-allowed' : 'pointer',
              opacity: avatarMutation.isPending ? 0.6 : 1,
            }}
          >
            {t('profile.uploadAvatar')}
          </button>

          <input
            id="avatar-input"
            type="file"
            accept="image/*"
            style={{ display: 'none' }}
            onChange={(e) => handleAvatarChange(e.target.files?.[0] ?? null)}
          />
        </div>

        {avatarMutation.isError && (
          <div style={{ fontFamily: 'Cormorant Garamond, serif', fontSize: 14, color: '#D42020', marginBottom: 12 }}>
            {t('profile.avatarError')}
          </div>
        )}

        <form onSubmit={handleSubmit} style={{ marginTop: 16 }}>
          <label style={labelStyle}>{t('profile.displayName')}</label>
          <input
            value={displayName}
            onChange={(e) => { setDisplayName(e.target.value); setSaved(false) }}
            onFocus={focusOn}
            onBlur={focusOff}
            required
            style={{ ...inputStyle, marginBottom: 18 }}
          />

          <label style={labelStyle}>{t('profile.bio')}</label>
          <textarea
            value={bio}
            onChange={(e) => { setBio(e.target.value); setSaved(false) }}
            onFocus={focusOn}
            onBlur={focusOff}
            rows={4}
            style={{ ...inputStyle, marginBottom: 18, resize: 'vertical' }}
          />

          <label style={labelStyle}>{t('profile.country')}</label>
          <input
            value={country}
            onChange={(e) => { setCountry(e.target.value); setSaved(false) }}
            onFocus={focusOn}
            onBlur={focusOff}
            style={{ ...inputStyle, marginBottom: 18 }}
          />

          <label style={labelStyle}>{t('profile.city')}</label>
          <input
            value={city}
            onChange={(e) => { setCity(e.target.value); setSaved(false) }}
            onFocus={focusOn}
            onBlur={focusOff}
            style={{ ...inputStyle, marginBottom: 24 }}
          />

          {saved && !saveMutation.isError && (
            <div style={{ fontFamily: 'Cormorant Garamond, serif', fontSize: 14, color: '#4CAF50', marginBottom: 16 }}>
              {t('profile.successMessage')}
            </div>
          )}

          {saveMutation.isError && (
            <div style={{ fontFamily: 'Cormorant Garamond, serif', fontSize: 14, color: '#D42020', marginBottom: 16 }}>
              {t('profile.errorMessage')}
            </div>
          )}

          <button
            type="submit"
            disabled={saveMutation.isPending || !displayName.trim()}
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
              cursor: saveMutation.isPending || !displayName.trim() ? 'not-allowed' : 'pointer',
              opacity: saveMutation.isPending || !displayName.trim() ? 0.6 : 1,
              boxShadow: '0 4px 20px rgba(201,168,76,0.3)',
            }}
          >
            {saveMutation.isPending ? t('profile.saving') : t('profile.save')}
          </button>
        </form>
      </main>
    </div>
  )
}
