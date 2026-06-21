import { useState } from 'react'
import { useNavigate, Link } from 'react-router-dom'
import { useMutation } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import type { AxiosError } from 'axios'
import { useAuth } from '../contexts/AuthContext'
import LanguageSwitcher from '../components/LanguageSwitcher'

export default function LoginPage() {
  const navigate = useNavigate()
  const { t } = useTranslation()
  const { login } = useAuth()
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState('')

  const loginMutation = useMutation({
    mutationFn: async () => {
      await login(email, password)
    },
    onSuccess: () => {
      navigate('/dashboard')
    },
    onError: (err: unknown) => {
      const axiosErr = err as AxiosError<{ message?: string }>
      const message = axiosErr.response?.data?.message || t('login.errorFailed')
      setError(message)
    },
  })

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault()
    setError('')

    // Client-side validation
    if (!email || !password) {
      setError(t('login.errorRequired'))
      return
    }

    if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email)) {
      setError(t('login.errorInvalidEmail'))
      return
    }

    if (password.length < 8) {
      setError(t('login.errorPasswordLength'))
      return
    }

    loginMutation.mutate()
  }

  return (
    <div className="min-h-screen flex items-center justify-center bg-stone-900 px-4">
      <div className="w-full max-w-md">
        <div className="flex justify-end mb-4">
          <LanguageSwitcher />
        </div>
        <div className="bg-stone-800 rounded-lg shadow-xl p-8 border border-amber-500/20">
          <h1 className="text-3xl font-bold text-amber-500 text-center mb-2">VirtualBar</h1>
          <p className="text-stone-400 text-center mb-8">{t('login.subtitle')}</p>

          {error && (
            <div className="mb-6 p-4 bg-red-900/20 border border-red-700 rounded text-red-200 text-sm">
              {error}
            </div>
          )}

          <form onSubmit={handleSubmit} className="space-y-4">
            <div>
              <label htmlFor="email" className="block text-stone-300 text-sm font-medium mb-2">
                {t('login.emailLabel')}
              </label>
              <input
                id="email"
                type="email"
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                placeholder={t('login.emailPlaceholder')}
                required
                disabled={loginMutation.isPending}
                className="w-full px-4 py-2 bg-stone-700 border border-stone-600 rounded text-stone-100 placeholder-stone-500 focus:outline-none focus:border-amber-500 focus:ring-1 focus:ring-amber-500 disabled:opacity-50 disabled:cursor-not-allowed"
              />
            </div>

            <div>
              <label htmlFor="password" className="block text-stone-300 text-sm font-medium mb-2">
                {t('login.passwordLabel')}
              </label>
              <input
                id="password"
                type="password"
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                placeholder={t('login.passwordPlaceholder')}
                required
                minLength={8}
                disabled={loginMutation.isPending}
                className="w-full px-4 py-2 bg-stone-700 border border-stone-600 rounded text-stone-100 placeholder-stone-500 focus:outline-none focus:border-amber-500 focus:ring-1 focus:ring-amber-500 disabled:opacity-50 disabled:cursor-not-allowed"
              />
            </div>

            <button
              type="submit"
              disabled={loginMutation.isPending}
              className="w-full py-2 mt-6 bg-amber-600 hover:bg-amber-500 text-white font-semibold rounded transition-colors disabled:opacity-50 disabled:cursor-not-allowed"
            >
              {loginMutation.isPending ? t('login.submittingBtn') : t('login.submitBtn')}
            </button>
          </form>

          <p className="text-center text-stone-400 mt-6 text-sm">
            {t('login.noAccount')}{' '}
            <Link to="/register" className="text-amber-500 hover:text-amber-400 font-medium">
              {t('login.registerLink')}
            </Link>
          </p>
        </div>
      </div>
    </div>
  )
}
