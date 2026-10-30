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
import { authErrorMessage } from '@/lib/authErrors'
import { emailLanguage } from '@/lib/emailLanguage'

const makeSchema = (t: TFn) =>
  z
    .object({
      displayName: z
        .string()
        .trim()
        .min(1, t('register.errorRequired'))
        .min(2, t('register.errorDisplayNameLength')),
      email: z
        .string()
        .min(1, t('register.errorRequired'))
        .regex(EMAIL_REGEX, t('register.errorInvalidEmail')),
      password: z
        .string()
        .min(1, t('register.errorRequired'))
        .min(8, t('register.errorPasswordFormat'))
        .regex(/[A-Z]/, t('register.errorPasswordFormat'))
        .regex(/\d/, t('register.errorPasswordFormat')),
      confirmPassword: z.string().min(1, t('register.errorRequired')),
    })
    .refine((data) => data.password === data.confirmPassword, {
      path: ['confirmPassword'],
      message: t('register.errorPasswordMatch'),
    })

type RegisterValues = z.infer<ReturnType<typeof makeSchema>>

export default function RegisterPage() {
  const navigate = useNavigate()
  const { t, i18n } = useTranslation()
  const { register } = useAuth()
  // Set when the account was created but must confirm its email before it can sign in.
  const [pendingEmail, setPendingEmail] = useState<string | null>(null)
  const schema = useMemo(() => makeSchema(t), [t])
  const form = useForm<RegisterValues>({
    resolver: zodResolver(schema),
    defaultValues: { displayName: '', email: '', password: '', confirmPassword: '' },
  })

  const registerMutation = useMutation({
    mutationFn: (values: RegisterValues) =>
      register(values.email, values.password, values.displayName, emailLanguage(i18n.language)),
    onSuccess: (outcome, values) => {
      if (outcome === 'emailConfirmationRequired') setPendingEmail(values.email)
      else navigate('/dashboard')
    },
    onError: (err: unknown) => {
      form.setError('root', { message: authErrorMessage(err, t, 'register.errorFailed') })
    },
  })

  const onSubmit = (values: RegisterValues) => registerMutation.mutate(values)

  if (pendingEmail) {
    return (
      <AuthLayout
        title={t('confirmEmail.checkTitle')}
        subtitle={t('confirmEmail.checkSubtitle')}
        footer={
          <Link to="/login" className="font-medium text-primary hover:underline">
            {t('confirmEmail.backToLogin')}
          </Link>
        }
      >
        <div className="space-y-4">
          <p className="text-sm">{t('confirmEmail.checkMessage', { email: pendingEmail })}</p>
          <p className="text-sm text-muted-foreground">{t('confirmEmail.checkHint')}</p>
          <ResendConfirmation key={pendingEmail} email={pendingEmail} />
        </div>
      </AuthLayout>
    )
  }

  return (
    <AuthLayout
      title="VirtualBar"
      subtitle={t('register.subtitle')}
      footer={
        <>
          {t('register.haveAccount')}{' '}
          <Link to="/login" className="font-medium text-primary hover:underline">
            {t('register.loginLink')}
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

          <FormField
            control={form.control}
            name="displayName"
            render={({ field }) => (
              <FormItem>
                <FormLabel>{t('register.displayNameLabel')}</FormLabel>
                <FormControl>
                  <Input
                    autoComplete="name"
                    className="h-9"
                    placeholder={t('register.displayNamePlaceholder')}
                    disabled={registerMutation.isPending}
                    {...field}
                  />
                </FormControl>
                <FormMessage />
              </FormItem>
            )}
          />

          <FormField
            control={form.control}
            name="email"
            render={({ field }) => (
              <FormItem>
                <FormLabel>{t('register.emailLabel')}</FormLabel>
                <FormControl>
                  <Input
                    type="email"
                    autoComplete="email"
                    className="h-9"
                    placeholder={t('register.emailPlaceholder')}
                    disabled={registerMutation.isPending}
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
                <FormLabel>{t('register.passwordLabel')}</FormLabel>
                <FormControl>
                  <Input
                    type="password"
                    autoComplete="new-password"
                    className="h-9"
                    placeholder={t('register.passwordPlaceholder')}
                    disabled={registerMutation.isPending}
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
                <FormLabel>{t('register.confirmPasswordLabel')}</FormLabel>
                <FormControl>
                  <Input
                    type="password"
                    autoComplete="new-password"
                    className="h-9"
                    placeholder={t('register.confirmPasswordPlaceholder')}
                    disabled={registerMutation.isPending}
                    {...field}
                  />
                </FormControl>
                <FormMessage />
              </FormItem>
            )}
          />

          <Button type="submit" size="lg" className="h-10 w-full" disabled={registerMutation.isPending}>
            {registerMutation.isPending ? t('register.submittingBtn') : t('register.submitBtn')}
          </Button>
        </form>
      </Form>
    </AuthLayout>
  )
}
