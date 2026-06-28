import type { ReactNode } from 'react'
import LanguageSwitcher from '@/components/LanguageSwitcher'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'

interface AuthLayoutProps {
  title: string
  subtitle: string
  children: ReactNode
  footer: ReactNode
}

export default function AuthLayout({ title, subtitle, children, footer }: AuthLayoutProps) {
  return (
    <div className="flex min-h-screen items-center justify-center px-4 py-10">
      <div className="w-full max-w-md">
        <div className="mb-4 flex justify-end">
          <LanguageSwitcher />
        </div>
        <Card className="[--card-spacing:--spacing(6)]">
          <CardHeader className="text-center">
            <CardTitle className="text-2xl font-semibold">{title}</CardTitle>
            <CardDescription className="text-sm">{subtitle}</CardDescription>
          </CardHeader>
          <CardContent>
            {children}
            <div className="mt-6 text-center text-sm text-muted-foreground">{footer}</div>
          </CardContent>
        </Card>
      </div>
    </div>
  )
}
