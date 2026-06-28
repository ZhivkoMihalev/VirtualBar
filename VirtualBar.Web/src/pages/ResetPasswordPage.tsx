import { useMemo } from 'react'
import { Link, useNavigate, useSearchParams } from 'react-router-dom'
import { useMutation } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { resetPassword } from '../api/authApi'
import AuthLayout from '../components/AuthLayout'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Form, FormControl, FormField, FormItem, FormLabel, FormMessage } from '@/components/ui/form'
import { PASSWORD_REGEX, type TFn } from '@/lib/validation'

const makeSchema = (t: TFn) =>
  z
    .object({
      newPassword: z
        .string()
        .min(1, t('resetPassword.errorRequired'))
        .min(8, t('resetPassword.errorPasswordFormat'))
        .regex(PASSWORD_REGEX, t('resetPassword.errorPasswordFormat')),
      confirmPassword: z.string().min(1, t('resetPassword.errorRequired')),
    })
    .refine((data) => data.newPassword === data.confirmPassword, {
      path: ['confirmPassword'],
      message: t('resetPassword.errorPasswordMatch'),
    })

type ResetValues = z.infer<ReturnType<typeof makeSchema>>

export default function ResetPasswordPage() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const [searchParams] = useSearchParams()
  const email = searchParams.get('email')
  const token = searchParams.get('token')
  const schema = useMemo(() => makeSchema(t), [t])
  const form = useForm<ResetValues>({
    resolver: zodResolver(schema),
    defaultValues: { newPassword: '', confirmPassword: '' },
  })

  const resetMutation = useMutation({
    mutationFn: (values: ResetValues) =>
      resetPassword(email ?? '', token ?? '', values.newPassword),
    onSuccess: () => navigate('/login'),
    onError: () => {
      form.setError('root', { message: t('resetPassword.errorFailed') })
    },
  })

  const onSubmit = (values: ResetValues) => resetMutation.mutate(values)

  return (
    <AuthLayout
      title={t('resetPassword.title')}
      subtitle={t('resetPassword.subtitle')}
      footer={
        <Link to="/login" className="font-medium text-primary hover:underline">
          {t('resetPassword.backToLogin')}
        </Link>
      }
    >
      {!email || !token ? (
        <div className="rounded-md border border-destructive/40 bg-destructive/10 px-3 py-2 text-sm text-destructive">
          {t('resetPassword.invalidLink')}
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
              name="newPassword"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>{t('resetPassword.newPasswordLabel')}</FormLabel>
                  <FormControl>
                    <Input
                      type="password"
                      autoComplete="new-password"
                      className="h-9"
                      placeholder={t('resetPassword.newPasswordPlaceholder')}
                      disabled={resetMutation.isPending}
                      {...field}
                    />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />

            <FormField
              control={form.control}
              name="confirmPassword"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>{t('resetPassword.confirmPasswordLabel')}</FormLabel>
                  <FormControl>
                    <Input
                      type="password"
                      autoComplete="new-password"
                      className="h-9"
                      placeholder={t('resetPassword.confirmPasswordPlaceholder')}
                      disabled={resetMutation.isPending}
                      {...field}
                    />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />

            <Button type="submit" size="lg" className="h-10 w-full" disabled={resetMutation.isPending}>
              {resetMutation.isPending
                ? t('resetPassword.submittingBtn')
                : t('resetPassword.submitBtn')}
            </Button>
          </form>
        </Form>
      )}
    </AuthLayout>
  )
}
