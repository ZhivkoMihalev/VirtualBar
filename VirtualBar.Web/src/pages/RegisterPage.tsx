import { useState } from 'react'
import { useNavigate, Link } from 'react-router-dom'
import { useMutation } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import type { AxiosError } from 'axios'
import { useAuth } from '../contexts/AuthContext'
import LanguageSwitcher from '../components/LanguageSwitcher'

export default function RegisterPage() {
  const navigate = useNavigate()
  const { t } = useTranslation()
  const { register } = useAuth()
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [confirmPassword, setConfirmPassword] = useState('')
  const [displayName, setDisplayName] = useState('')
  const [error, setError] = useState('')

  const registerMutation = useMutation({
    mutationFn: async () => {
      await register(email, password, displayName)
    },
    onSuccess: () => {
      navigate('/dashboard')
    },
    onError: (err: unknown) => {
      const axiosErr = err as AxiosError<{ message?: string }>
      const message = axiosErr.response?.data?.message || t('register.errorFailed')
      setError(message)
    },
  })

  const isPasswordValid = (pwd: string): boolean => {
    return pwd.length >= 8 && /[A-Z]/.test(pwd) && /\d/.test(pwd)
  }

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault()
    setError('')

    // Client-side validation
    if (!email || !password || !confirmPassword || !displayName) {
      setError(t('register.errorRequired'))
      return
    }

    if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email)) {
      setError(t('register.errorInvalidEmail'))
      return
    }

    if (!isPasswordValid(password)) {
      setError(t('register.errorPasswordFormat'))
      return
    }

    if (password !== confirmPassword) {
      setError(t('register.errorPasswordMatch'))
      return
    }

    if (displayName.trim().length < 2) {
      setError(t('register.errorDisplayNameLength'))
      return
    }

    registerMutation.mutate()
  }

  return (
    <div className="min-h-screen flex items-center justify-center bg-stone-900 px-4">
      <div className="w-full max-w-md">
        <div className="flex justify-end mb-4">
          <LanguageSwitcher />
        </div>
        <div className="bg-stone-800 rounded-lg shadow-xl p-8 border border-amber-500/20">
          <h1 className="text-3xl font-bold text-amber-500 text-center mb-2">VirtualBar</h1>
          <p className="text-stone-400 text-center mb-8">{t('register.subtitle')}</p>

          {error && (
            <div className="mb-6 p-4 bg-red-900/20 border border-red-700 rounded text-red-200 text-sm">
              {error}
            </div>
          )}

          <form onSubmit={handleSubmit} className="space-y-4">
            <div>
              <label htmlFor="displayName" className="block text-stone-300 text-sm font-medium mb-2">
                {t('register.displayNameLabel')}
              </label>
              <input
                id="displayName"
                type="text"
                value={displayName}
                onChange={(e) => setDisplayName(e.target.value)}
                placeholder={t('register.displayNamePlaceholder')}
                required
                disabled={registerMutation.isPending}
                className="w-full px-4 py-2 bg-stone-700 border border-stone-600 rounded text-stone-100 placeholder-stone-500 focus:outline-none focus:border-amber-500 focus:ring-1 focus:ring-amber-500 disabled:opacity-50 disabled:cursor-not-allowed"
              />
            </div>

            <div>
              <label htmlFor="email" className="block text-stone-300 text-sm font-medium mb-2">
                {t('register.emailLabel')}
              </label>
              <input
                id="email"
                type="email"
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                placeholder={t('register.emailPlaceholder')}
                required
                disabled={registerMutation.isPending}
                className="w-full px-4 py-2 bg-stone-700 border border-stone-600 rounded text-stone-100 placeholder-stone-500 focus:outline-none focus:border-amber-500 focus:ring-1 focus:ring-amber-500 disabled:opacity-50 disabled:cursor-not-allowed"
              />
            </div>

            <div>
              <label htmlFor="password" className="block text-stone-300 text-sm font-medium mb-2">
                {t('register.passwordLabel')}
              </label>
              <input
                id="password"
                type="password"
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                placeholder={t('register.passwordPlaceholder')}
                required
                disabled={registerMutation.isPending}
                className="w-full px-4 py-2 bg-stone-700 border border-stone-600 rounded text-stone-100 placeholder-stone-500 focus:outline-none focus:border-amber-500 focus:ring-1 focus:ring-amber-500 disabled:opacity-50 disabled:cursor-not-allowed"
              />
            </div>

            <div>
              <label htmlFor="confirmPassword" className="block text-stone-300 text-sm font-medium mb-2">
                {t('register.confirmPasswordLabel')}
              </label>
              <input
                id="confirmPassword"
                type="password"
                value={confirmPassword}
                onChange={(e) => setConfirmPassword(e.target.value)}
                placeholder={t('register.confirmPasswordPlaceholder')}
                required
                disabled={registerMutation.isPending}
                className="w-full px-4 py-2 bg-stone-700 border border-stone-600 rounded text-stone-100 placeholder-stone-500 focus:outline-none focus:border-amber-500 focus:ring-1 focus:ring-amber-500 disabled:opacity-50 disabled:cursor-not-allowed"
              />
            </div>

            <button
              type="submit"
              disabled={registerMutation.isPending}
              className="w-full py-2 mt-6 bg-amber-600 hover:bg-amber-500 text-white font-semibold rounded transition-colors disabled:opacity-50 disabled:cursor-not-allowed"
            >
              {registerMutation.isPending ? t('register.submittingBtn') : t('register.submitBtn')}
            </button>
          </form>

          <p className="text-center text-stone-400 mt-6 text-sm">
            {t('register.haveAccount')}{' '}
            <Link to="/login" className="text-amber-500 hover:text-amber-400 font-medium">
              {t('register.loginLink')}
            </Link>
          </p>
        </div>
      </div>
    </div>
  )
}
