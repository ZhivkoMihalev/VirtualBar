import { useMutation } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { resendConfirmation } from '../api/authApi'
import { Button } from '@/components/ui/button'
import { emailLanguage } from '@/lib/emailLanguage'

interface ResendConfirmationProps {
  email: string
}

/**
 * "Send a new confirmation link" for an account that has not confirmed its email. The API answers the same whether
 * or not the account exists, so the success message says "if". Render it with key={email}, so a different email
 * starts from the button again rather than from the previous email's message.
 */
export default function ResendConfirmation({ email }: ResendConfirmationProps) {
  const { t, i18n } = useTranslation()
  const resendMutation = useMutation({
    mutationFn: () => resendConfirmation(email, emailLanguage(i18n.language)),
  })

  if (resendMutation.isSuccess) {
    return (
      <p role="status" className="rounded-md border border-success/40 bg-success/10 px-3 py-2 text-sm text-success">
        {t('confirmEmail.resendSuccess', { email })}
      </p>
    )
  }

  return (
    <div className="space-y-2">
      {resendMutation.isError && (
        <p className="rounded-md border border-destructive/40 bg-destructive/10 px-3 py-2 text-sm text-destructive">
          {t('confirmEmail.resendFailed')}
        </p>
      )}
      <Button
        type="button"
        variant="outline"
        size="lg"
        className="h-10 w-full"
        disabled={resendMutation.isPending}
        onClick={() => resendMutation.mutate()}
      >
        {resendMutation.isPending ? t('confirmEmail.resendingBtn') : t('confirmEmail.resendBtn')}
      </Button>
    </div>
  )
}
