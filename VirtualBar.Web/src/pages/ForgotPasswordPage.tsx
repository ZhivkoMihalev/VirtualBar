import { useMemo } from 'react'
import { Link } from 'react-router-dom'
import { useMutation } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { forgotPassword } from '../api/authApi'
import AuthLayout from '../components/AuthLayout'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Form, FormControl, FormField, FormItem, FormLabel, FormMessage } from '@/components/ui/form'
import { EMAIL_REGEX, type TFn } from '@/lib/validation'

const makeSchema = (t: TFn) =>
  z.object({
    email: z
      .string()
      .min(1, t('forgotPassword.errorInvalidEmail'))
      .regex(EMAIL_REGEX, t('forgotPassword.errorInvalidEmail')),
  })

type ForgotValues = z.infer<ReturnType<typeof makeSchema>>

export default function ForgotPasswordPage() {
  const { t, i18n } = useTranslation()
  const schema = useMemo(() => makeSchema(t), [t])
  const form = useForm<ForgotValues>({
    resolver: zodResolver(schema),
    defaultValues: { email: '' },
  })

  const forgotMutation = useMutation({
    mutationFn: (values: ForgotValues) =>
      forgotPassword(values.email, i18n.language.startsWith('en') ? 'en' : 'bg'),
    onError: () => {
      form.setError('root', { message: t('forgotPassword.errorFailed') })
    },
  })

  const onSubmit = (values: ForgotValues) => forgotMutation.mutate(values)

  return (
    <AuthLayout
      title={t('forgotPassword.title')}
      subtitle={t('forgotPassword.subtitle')}
      footer={
        <Link to="/login" className="font-medium text-primary hover:underline">
          {t('forgotPassword.backToLogin')}
        </Link>
      }
    >
      {forgotMutation.isSuccess ? (
        <div className="rounded-md border border-success/40 bg-success/10 px-3 py-2 text-sm text-success">
          {t('forgotPassword.successMessage')}
        </div>
      ) : (
        <Form {...form}>
          <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-4" noValidate>
            {form.formState.errors.root && (
              <p className="rounded-md border border-destructive/40 bg-destructive/10 px-3 py-2 text-sm text-destructive">
                {form.formState.errors.root.message}
              </p>
            )}

            <FormField
              control={form.control}
              name="email"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>{t('forgotPassword.emailLabel')}</FormLabel>
                  <FormControl>
                    <Input
                      type="email"
                      autoComplete="email"
                      className="h-9"
                      placeholder={t('forgotPassword.emailPlaceholder')}
                      disabled={forgotMutation.isPending}
                      {...field}
                    />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />

            <Button type="submit" size="lg" className="h-10 w-full" disabled={forgotMutation.isPending}>
              {forgotMutation.isPending
                ? t('forgotPassword.submittingBtn')
                : t('forgotPassword.submitBtn')}
            </Button>
          </form>
        </Form>
      )}
    </AuthLayout>
  )
}
