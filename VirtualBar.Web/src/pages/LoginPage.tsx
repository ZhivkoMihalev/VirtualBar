import { useMemo, useState } from 'react'
import { useNavigate, Link } from 'react-router-dom'
import { useMutation } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { useAuth } from '../contexts/useAuth'
import AuthLayout from '../components/AuthLayout'
import ResendConfirmation from '../components/ResendConfirmation'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Form, FormControl, FormField, FormItem, FormLabel, FormMessage } from '@/components/ui/form'
import { EMAIL_REGEX, type TFn } from '@/lib/validation'
import { authErrorMessage, authErrorReason } from '@/lib/authErrors'

const makeSchema = (t: TFn) =>
  z.object({
    email: z
      .string()
      .min(1, t('login.errorRequired'))
      .regex(EMAIL_REGEX, t('login.errorInvalidEmail')),
    password: z
      .string()
      .min(1, t('login.errorRequired'))
      .min(8, t('login.errorPasswordLength')),
  })

type LoginValues = z.infer<ReturnType<typeof makeSchema>>

export default function LoginPage() {
  const navigate = useNavigate()
  const { t } = useTranslation()
  const { login } = useAuth()
  // Set when the login was refused because the account has not confirmed its email yet.
  const [unconfirmedEmail, setUnconfirmedEmail] = useState<string | null>(null)
  const schema = useMemo(() => makeSchema(t), [t])
  const form = useForm<LoginValues>({
    resolver: zodResolver(schema),
    defaultValues: { email: '', password: '' },
  })

  const loginMutation = useMutation({
    mutationFn: (values: LoginValues) => login(values.email, values.password),
    onMutate: () => setUnconfirmedEmail(null),
    onSuccess: () => navigate('/dashboard'),
    onError: (err: unknown, values) => {
      form.setError('root', { message: authErrorMessage(err, t, 'login.errorFailed') })
      if (authErrorReason(err) === 'EmailNotConfirmed') setUnconfirmedEmail(values.email)
    },
  })

  const onSubmit = (values: LoginValues) => loginMutation.mutate(values)

  return (
    <AuthLayout
      title="VirtualBar"
      subtitle={t('login.subtitle')}
      footer={
        <>
          {t('login.noAccount')}{' '}
          <Link to="/register" className="font-medium text-primary hover:underline">
            {t('login.registerLink')}
          </Link>
        </>
      }
    >
      <Form {...form}>
        <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-4" noValidate>
          {form.formState.errors.root && (
            <p className="rounded-md border border-destructive/40 bg-destructive/10 px-3 py-2 text-sm text-destructive">
              {form.formState.errors.root.message}
            </p>
          )}

          {unconfirmedEmail && <ResendConfirmation key={unconfirmedEmail} email={unconfirmedEmail} />}

          <FormField
            control={form.control}
            name="email"
            render={({ field }) => (
              <FormItem>
                <FormLabel>{t('login.emailLabel')}</FormLabel>
                <FormControl>
                  <Input
                    type="email"
                    autoComplete="email"
                    className="h-9"
                    placeholder={t('login.emailPlaceholder')}
                    disabled={loginMutation.isPending}
                    {...field}
                  />
                </FormControl>
                <FormMessage />
              </FormItem>
            )}
          />

          <FormField
            control={form.control}
            name="password"
            render={({ field }) => (
              <FormItem>
                <div className="flex items-center justify-between">
                  <FormLabel>{t('login.passwordLabel')}</FormLabel>
                  <Link
                    to="/forgot-password"
                    className="text-xs font-medium text-primary hover:underline"
                  >
                    {t('login.forgotPassword')}
                  </Link>
                </div>
                <FormControl>
                  <Input
                    type="password"
                    autoComplete="current-password"
                    className="h-9"
                    placeholder={t('login.passwordPlaceholder')}
                    disabled={loginMutation.isPending}
                    {...field}
                  />
                </FormControl>
                <FormMessage />
              </FormItem>
            )}
          />

          <Button type="submit" size="lg" className="h-10 w-full" disabled={loginMutation.isPending}>
            {loginMutation.isPending ? t('login.submittingBtn') : t('login.submitBtn')}
          </Button>
        </form>
      </Form>
    </AuthLayout>
  )
}
