import { useState } from 'react'
import { Link } from 'react-router-dom'
import { useMutation } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { forgotPassword } from '../api/authApi'
import LanguageSwitcher from '../components/LanguageSwitcher'

export default function ForgotPasswordPage() {
  const { t } = useTranslation()
  const [email, setEmail] = useState('')
  const [error, setError] = useState('')

  const forgotMutation = useMutation({
    mutationFn: async () => {
      await forgotPassword(email)
    },
    onError: () => {
      setError(t('forgotPassword.errorFailed'))
    },
  })

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault()
    setError('')

    if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email)) {
      setError(t('forgotPassword.errorInvalidEmail'))
      return
    }

    forgotMutation.mutate()
  }

  return (
    <div className="min-h-screen flex items-center justify-center bg-stone-900 px-4">
      <div className="w-full max-w-md">
        <div className="flex justify-end mb-4">
          <LanguageSwitcher />
        </div>
        <div className="bg-stone-800 rounded-lg shadow-xl p-8 border border-amber-500/20">
          <h1 className="text-3xl font-bold text-amber-500 text-center mb-2">
            {t('forgotPassword.title')}
          </h1>
          <p className="text-stone-400 text-center mb-8">{t('forgotPassword.subtitle')}</p>

          {forgotMutation.isSuccess ? (
            <div className="mb-6 p-4 bg-green-900/20 border border-green-700 rounded text-green-200 text-sm">
              {t('forgotPassword.successMessage')}
            </div>
          ) : (
            <>
              {error && (
                <div className="mb-6 p-4 bg-red-900/20 border border-red-700 rounded text-red-200 text-sm">
                  {error}
                </div>
              )}

              <form onSubmit={handleSubmit} className="space-y-4">
                <div>
                  <label htmlFor="email" className="block text-stone-300 text-sm font-medium mb-2">
                    {t('forgotPassword.emailLabel')}
                  </label>
                  <input
                    id="email"
                    type="email"
                    value={email}
                    onChange={(e) => setEmail(e.target.value)}
                    placeholder={t('forgotPassword.emailPlaceholder')}
                    required
                    disabled={forgotMutation.isPending}
                    className="w-full px-4 py-2 bg-stone-700 border border-stone-600 rounded text-stone-100 placeholder-stone-500 focus:outline-none focus:border-amber-500 focus:ring-1 focus:ring-amber-500 disabled:opacity-50 disabled:cursor-not-allowed"
                  />
                </div>

                <button
                  type="submit"
                  disabled={forgotMutation.isPending}
                  className="w-full py-2 mt-6 bg-amber-600 hover:bg-amber-500 text-white font-semibold rounded transition-colors disabled:opacity-50 disabled:cursor-not-allowed"
                >
                  {forgotMutation.isPending
                    ? t('forgotPassword.submittingBtn')
                    : t('forgotPassword.submitBtn')}
                </button>
              </form>
            </>
          )}

          <p className="text-center mt-6 text-sm">
            <Link to="/login" className="text-amber-500 hover:text-amber-400 font-medium">
              {t('forgotPassword.backToLogin')}
            </Link>
          </p>
        </div>
      </div>
    </div>
  )
}
