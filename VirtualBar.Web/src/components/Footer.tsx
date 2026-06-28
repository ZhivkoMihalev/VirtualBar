import { useTranslation } from 'react-i18next'
import { Link } from 'react-router-dom'
import { Diamond } from 'lucide-react'
import { useAuth } from '../contexts/AuthContext'
import { Separator } from '@/components/ui/separator'

export default function Footer() {
  const { t } = useTranslation()
  const { isAuthenticated } = useAuth()

  const navLinks = [
    { to: '/', label: t('nav.home') },
    { to: '/browse', label: t('nav.browse') },
    { to: '/marketplace', label: t('nav.marketplace') },
    ...(isAuthenticated ? [{ to: '/dashboard', label: t('nav.myBar') }] : []),
  ]

  const legalItems = [t('footer.about'), t('footer.privacy'), t('footer.terms')]

  return (
    <footer className="relative z-[1] border-t border-border bg-background px-10 pt-13 pb-7 text-muted-foreground">
      <div className="mx-auto max-w-[1100px]">
        <div className="grid grid-cols-1 gap-12 md:grid-cols-[2fr_1fr_1fr]">
          {/* Brand */}
          <div>
            <div className="mb-5 flex items-center gap-3">
              <div className="flex h-9 w-9 shrink-0 items-center justify-center rounded-full border border-primary text-xs font-semibold text-primary">
                VB
              </div>
              <span className="text-base font-semibold tracking-[0.25em] text-primary">
                VIRTUALBAR
              </span>
            </div>
            <p className="max-w-[260px] text-sm leading-relaxed italic text-muted-foreground">
              {t('footer.tagline')}
            </p>
          </div>

          {/* Navigation */}
          <div>
            <h4 className="mb-5 text-xs font-medium tracking-wide uppercase text-primary">
              {t('footer.explore')}
            </h4>
            <ul className="flex flex-col gap-3">
              {navLinks.map((link) => (
                <li key={link.to}>
                  <Link
                    to={link.to}
                    className="text-xs text-muted-foreground transition-colors hover:text-primary"
                  >
                    {link.label}
                  </Link>
                </li>
              ))}
            </ul>
          </div>

          {/* Legal */}
          <div>
            <h4 className="mb-5 text-xs font-medium tracking-wide uppercase text-primary">
              {t('footer.legal')}
            </h4>
            <ul className="flex flex-col gap-3">
              {legalItems.map((item) => (
                <li key={item}>
                  <span className="text-xs text-muted-foreground">{item}</span>
                </li>
              ))}
            </ul>
          </div>
        </div>

        {/* Decorative divider */}
        <div className="my-6 flex items-center gap-4">
          <Separator className="flex-1" />
          <Diamond className="size-3 text-primary" />
          <Separator className="flex-1" />
        </div>

        {/* Copyright */}
        <p className="text-center text-xs tracking-wide text-muted-foreground">
          {t('footer.rights')}
        </p>
      </div>
    </footer>
  )
}
