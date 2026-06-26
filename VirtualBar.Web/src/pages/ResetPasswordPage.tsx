import { useState } from 'react'
import { Link, useNavigate, useSearchParams } from 'react-router-dom'
import { useMutation } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { resetPassword } from '../api/authApi'
import LanguageSwitcher from '../components/LanguageSwitcher'

const PASSWORD_REGEX = /^(?=.*[A-Z])(?=.*\d).+$/

export default function ResetPasswordPage() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const [searchParams] = useSearchParams()
  const email = searchParams.get('email')
  const token = searchParams.get('token')

  const [newPassword, setNewPassword] = useState('')
  const [confirmPassword, setConfirmPassword] = useState('')
  const [error, setError] = useState('')

  const resetMutation = useMutation({
    mutationFn: async () => {
      await resetPassword(email ?? '', token ?? '', newPassword)
    },
    onSuccess: () => {
      navigate('/login')
    },
    onError: () => {
      setError(t('resetPassword.errorFailed'))
    },
  })

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault()
    setError('')

    if (!newPassword || !confirmPassword) {
      setError(t('resetPassword.errorRequired'))
      return
    }

    if (newPassword.length < 8 || !PASSWORD_REGEX.test(newPassword)) {
      setError(t('resetPassword.errorPasswordFormat'))
      return
    }

    if (newPassword !== confirmPassword) {
      setError(t('resetPassword.errorPasswordMatch'))
      return
    }

    resetMutation.mutate()
  }

  return (
    <div className="min-h-screen flex items-center justify-center bg-stone-900 px-4">
      <div className="w-full max-w-md">
        <div className="flex justify-end mb-4">
          <LanguageSwitcher />
        </div>
        <div className="bg-stone-800 rounded-lg shadow-xl p-8 border border-amber-500/20">
          <h1 className="text-3xl font-bold text-amber-500 text-center mb-2">
            {t('resetPassword.title')}
          </h1>
          <p className="text-stone-400 text-center mb-8">{t('resetPassword.subtitle')}</p>

          {!email || !token ? (
            <div className="mb-6 p-4 bg-red-900/20 border border-red-700 rounded text-red-200 text-sm">
              {t('resetPassword.invalidLink')}
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
                  <label
                    htmlFor="newPassword"
                    className="block text-stone-300 text-sm font-medium mb-2"
                  >
                    {t('resetPassword.newPasswordLabel')}
                  </label>
                  <input
                    id="newPassword"
                    type="password"
                    value={newPassword}
                    onChange={(e) => setNewPassword(e.target.value)}
                    placeholder={t('resetPassword.newPasswordPlaceholder')}
                    required
                    minLength={8}
                    disabled={resetMutation.isPending}
                    className="w-full px-4 py-2 bg-stone-700 border border-stone-600 rounded text-stone-100 placeholder-stone-500 focus:outline-none focus:border-amber-500 focus:ring-1 focus:ring-amber-500 disabled:opacity-50 disabled:cursor-not-allowed"
                  />
                </div>

                <div>
                  <label
                    htmlFor="confirmPassword"
                    className="block text-stone-300 text-sm font-medium mb-2"
                  >
                    {t('resetPassword.confirmPasswordLabel')}
                  </label>
                  <input
                    id="confirmPassword"
                    type="password"
                    value={confirmPassword}
                    onChange={(e) => setConfirmPassword(e.target.value)}
                    placeholder={t('resetPassword.confirmPasswordPlaceholder')}
                    required
                    minLength={8}
                    disabled={resetMutation.isPending}
                    className="w-full px-4 py-2 bg-stone-700 border border-stone-600 rounded text-stone-100 placeholder-stone-500 focus:outline-none focus:border-amber-500 focus:ring-1 focus:ring-amber-500 disabled:opacity-50 disabled:cursor-not-allowed"
                  />
                </div>

                <button
                  type="submit"
                  disabled={resetMutation.isPending}
                  className="w-full py-2 mt-6 bg-amber-600 hover:bg-amber-500 text-white font-semibold rounded transition-colors disabled:opacity-50 disabled:cursor-not-allowed"
                >
                  {resetMutation.isPending
                    ? t('resetPassword.submittingBtn')
                    : t('resetPassword.submitBtn')}
                </button>
              </form>
            </>
          )}

          <p className="text-center mt-6 text-sm">
            <Link to="/login" className="text-amber-500 hover:text-amber-400 font-medium">
              {t('resetPassword.backToLogin')}
            </Link>
          </p>
        </div>
      </div>
    </div>
  )
}
