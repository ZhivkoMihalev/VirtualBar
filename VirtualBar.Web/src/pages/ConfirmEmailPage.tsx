import type { ReactNode } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { isAxiosError } from 'axios'
import { confirmEmail } from '../api/authApi'
import AuthLayout from '../components/AuthLayout'
import ResendConfirmation from '../components/ResendConfirmation'
import { Button } from '@/components/ui/button'

const ERROR_CLASS = 'rounded-md border border-destructive/40 bg-destructive/10 px-3 py-2 text-sm text-destructive'
const SUCCESS_CLASS = 'rounded-md border border-success/40 bg-success/10 px-3 py-2 text-sm text-success'

/**
 * The page the link in the confirmation email opens: /confirm-email?email=…&token=… (backend:
 * AuthService.SendConfirmationEmailAsync). It confirms the address as soon as it loads.
 */
export default function ConfirmEmailPage() {
  const { t } = useTranslation()
  const [searchParams] = useSearchParams()
  const email = searchParams.get('email')
  const token = searchParams.get('token')
  const link = email && token ? { email, token } : null

  // A query rather than a mutation fired from an effect: StrictMode runs effects twice in development, while a
  // query sends one request per key. The answer for a link does not change, so it is never refetched or retried.
  const confirmQuery = useQuery({
    queryKey: ['confirmEmail', email, token],
    queryFn: async () => {
      await confirmEmail(link?.email ?? '', link?.token ?? '')
      return true
    },
    enabled: link !== null,
    retry: false,
    staleTime: Infinity,
    refetchOnWindowFocus: false,
    refetchOnReconnect: false,
  })

  let content: ReactNode
  if (!link) {
    content = (
      <div className="space-y-4">
        <p className={ERROR_CLASS}>{t('confirmEmail.invalidLink')}</p>
        {email && <ResendConfirmation key={email} email={email} />}
      </div>
    )
  } else if (confirmQuery.isPending) {
    content = (
      <p role="status" className="text-center text-sm text-muted-foreground">
        {t('confirmEmail.confirming')}
      </p>
    )
  } else if (confirmQuery.isSuccess) {
    content = (
      <div className="space-y-4">
        <p role="status" className={SUCCESS_CLASS}>
          {t('confirmEmail.success')}
        </p>
        <Button asChild size="lg" className="h-10 w-full">
          <Link to="/login">{t('confirmEmail.loginBtn')}</Link>
        </Button>
      </div>
    )
  } else if (isAxiosError(confirmQuery.error) && confirmQuery.error.response?.status === 400) {
    // The API answers 400 to a wrong, expired or already replaced token: only a new link helps.
    content = (
      <div className="space-y-4">
        <p className={ERROR_CLASS}>{t('confirmEmail.invalidLink')}</p>
        <ResendConfirmation key={link.email} email={link.email} />
      </div>
    )
  } else {
    // No answer, or one that says nothing about the link (rate limit, server error): the link may still be good.
    content = (
      <div className="space-y-4">
        <p className={ERROR_CLASS}>{t('confirmEmail.errorFailed')}</p>
        <Button
          type="button"
          variant="outline"
          size="lg"
          className="h-10 w-full"
          disabled={confirmQuery.isFetching}
          onClick={() => confirmQuery.refetch()}
        >
          {t('confirmEmail.retryBtn')}
        </Button>
      </div>
    )
  }

  return (
    <AuthLayout
      title={t('confirmEmail.title')}
      subtitle={t('confirmEmail.subtitle')}
      footer={
        <Link to="/login" className="font-medium text-primary hover:underline">
          {t('confirmEmail.backToLogin')}
        </Link>
      }
    >
      {content}
    </AuthLayout>
  )
}
