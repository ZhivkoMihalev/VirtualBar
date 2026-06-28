import { Link } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { Menu } from 'lucide-react'
import { useAuth } from '../contexts/AuthContext'
import { useChat } from '../contexts/ChatContext'
import LanguageSwitcher from './LanguageSwitcher'
import NotificationBell from './NotificationBell'
import Avatar from './Avatar'
import { Button } from '@/components/ui/button'
import { Separator } from '@/components/ui/separator'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import {
  Sheet,
  SheetClose,
  SheetContent,
  SheetHeader,
  SheetTitle,
  SheetTrigger,
} from '@/components/ui/sheet'

export default function NavBar() {
  const { t } = useTranslation()
  const { user, isAuthenticated, logout } = useAuth()
  const { toggleInbox } = useChat()

  return (
    <div className="sticky top-0 z-40">
      <nav className="flex h-16 items-center justify-between gap-4 border-b border-border bg-background/95 px-6 backdrop-blur">
        {/* Logo */}
        <Link to="/" className="flex shrink-0 items-center gap-3">
          <div className="flex h-9 w-9 items-center justify-center rounded-full border border-primary text-xs text-primary">
            VB
          </div>
          <span className="text-lg font-semibold tracking-[0.1em] text-primary">VIRTUALBAR</span>
        </Link>

        {/* Desktop center links */}
        <div className="hidden flex-1 items-center justify-center gap-2 md:flex">
          <Button asChild variant="ghost" size="sm">
            <Link to="/">{t('nav.home')}</Link>
          </Button>
          <Button asChild variant="ghost" size="sm">
            <Link to="/browse">{t('nav.browse')}</Link>
          </Button>
          <Button asChild variant="ghost" size="sm">
            <Link to="/marketplace">{t('nav.marketplace')}</Link>
          </Button>
          {isAuthenticated && (
            <Button asChild variant="ghost" size="sm">
              <Link to="/dashboard">{t('nav.myBar')}</Link>
            </Button>
          )}
          {isAuthenticated && (
            <Button asChild variant="ghost" size="sm">
              <Link to="/offers">{t('nav.offers')}</Link>
            </Button>
          )}
          {isAuthenticated && (
            <Button variant="ghost" size="sm" onClick={toggleInbox}>
              {t('nav.messages')}
            </Button>
          )}
        </div>

        {/* Desktop right slot */}
        <div className="hidden items-center gap-2 md:flex">
          {isAuthenticated ? (
            <>
              <NotificationBell />
              <LanguageSwitcher />
              <DropdownMenu>
                <DropdownMenuTrigger asChild>
                  <Button variant="ghost" size="sm">
                    {user && (
                      <Avatar displayName={user.displayName} avatarUrl={user.avatarUrl} size={32} />
                    )}
                    <span className="text-sm text-primary">{user?.displayName}</span>
                  </Button>
                </DropdownMenuTrigger>
                <DropdownMenuContent align="end">
                  <DropdownMenuItem asChild>
                    <Link to="/profile">{user?.displayName}</Link>
                  </DropdownMenuItem>
                  <DropdownMenuItem onSelect={logout}>{t('nav.logout')}</DropdownMenuItem>
                </DropdownMenuContent>
              </DropdownMenu>
            </>
          ) : (
            <>
              <Button asChild variant="ghost" size="sm">
                <Link to="/login">{t('nav.login')}</Link>
              </Button>
              <LanguageSwitcher />
              <Button asChild>
                <Link to="/register">{t('nav.register')}</Link>
              </Button>
            </>
          )}
        </div>

        {/* Mobile menu */}
        <Sheet>
          <SheetTrigger asChild>
            <Button variant="ghost" size="icon" className="md:hidden" aria-label="Toggle menu">
              <Menu className="size-5" />
            </Button>
          </SheetTrigger>
          <SheetContent side="right" className="w-72">
            <SheetHeader>
              <SheetTitle className="sr-only">{t('nav.brand')}</SheetTitle>
            </SheetHeader>
            <div className="flex flex-col gap-1 px-4">
              <SheetClose asChild>
                <Button asChild variant="ghost" className="justify-start">
                  <Link to="/">{t('nav.home')}</Link>
                </Button>
              </SheetClose>
              <SheetClose asChild>
                <Button asChild variant="ghost" className="justify-start">
                  <Link to="/browse">{t('nav.browse')}</Link>
                </Button>
              </SheetClose>
              <SheetClose asChild>
                <Button asChild variant="ghost" className="justify-start">
                  <Link to="/marketplace">{t('nav.marketplace')}</Link>
                </Button>
              </SheetClose>
              {isAuthenticated && (
                <SheetClose asChild>
                  <Button asChild variant="ghost" className="justify-start">
                    <Link to="/dashboard">{t('nav.myBar')}</Link>
                  </Button>
                </SheetClose>
              )}
              {isAuthenticated && (
                <SheetClose asChild>
                  <Button asChild variant="ghost" className="justify-start">
                    <Link to="/offers">{t('nav.offers')}</Link>
                  </Button>
                </SheetClose>
              )}
              {isAuthenticated && (
                <SheetClose asChild>
                  <Button variant="ghost" className="justify-start" onClick={toggleInbox}>
                    {t('nav.messages')}
                  </Button>
                </SheetClose>
              )}

              <Separator className="my-3" />

              {isAuthenticated ? (
                <div className="flex flex-col gap-3">
                  <SheetClose asChild>
                    <Link to="/profile" className="flex items-center gap-3">
                      {user && (
                        <Avatar
                          displayName={user.displayName}
                          avatarUrl={user.avatarUrl}
                          size={32}
                        />
                      )}
                      <span className="text-sm text-primary">{user?.displayName}</span>
                    </Link>
                  </SheetClose>
                  <div className="flex items-center gap-2">
                    <NotificationBell />
                    <LanguageSwitcher />
                  </div>
                  <SheetClose asChild>
                    <Button
                      variant="outline"
                      className="justify-start"
                      onClick={logout}
                    >
                      {t('nav.logout')}
                    </Button>
                  </SheetClose>
                </div>
              ) : (
                <div className="flex flex-col gap-3">
                  <SheetClose asChild>
                    <Button asChild variant="ghost" className="justify-start">
                      <Link to="/login">{t('nav.login')}</Link>
                    </Button>
                  </SheetClose>
                  <SheetClose asChild>
                    <Button asChild className="justify-start">
                      <Link to="/register">{t('nav.register')}</Link>
                    </Button>
                  </SheetClose>
                  <LanguageSwitcher />
                </div>
              )}
            </div>
          </SheetContent>
        </Sheet>
      </nav>
    </div>
  )
}
