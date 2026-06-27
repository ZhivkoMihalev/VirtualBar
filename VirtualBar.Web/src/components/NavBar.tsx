import { useState, useEffect, useRef } from 'react'
import { Link } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { useAuth } from '../contexts/AuthContext'
import { useChat } from '../contexts/ChatContext'
import LanguageSwitcher from './LanguageSwitcher'
import NotificationBell from './NotificationBell'
import Avatar from './Avatar'
import type { CSSProperties } from 'react'

const navLinkStyle: CSSProperties = {
  fontFamily: 'Cinzel, serif',
  fontSize: 11,
  letterSpacing: '0.15em',
  color: '#B09868',
  textDecoration: 'none',
}

const mobileLinkStyle: CSSProperties = {
  ...navLinkStyle,
  padding: '14px 24px',
  borderBottom: '1px solid rgba(201,168,76,0.07)',
  display: 'block',
}

export default function NavBar() {
  const { t } = useTranslation()
  const { user, isAuthenticated, logout } = useAuth()
  const { toggleInbox } = useChat()
  const [menuOpen, setMenuOpen] = useState(false)
  const wrapperRef = useRef<HTMLDivElement>(null)

  useEffect(() => {
    function handleClickOutside(e: MouseEvent) {
      if (wrapperRef.current && !wrapperRef.current.contains(e.target as Node)) {
        setMenuOpen(false)
      }
    }
    if (menuOpen) {
      document.addEventListener('mousedown', handleClickOutside)
    }
    return () => document.removeEventListener('mousedown', handleClickOutside)
  }, [menuOpen])

  const close = () => setMenuOpen(false)

  return (
    <div ref={wrapperRef} style={{ position: 'sticky', top: 0, zIndex: 40 }}>
      {/* ── Main bar ── */}
      <nav
        style={{
          borderBottom: '1px solid rgba(201,168,76,0.12)',
          background: 'rgba(7,3,10,0.95)',
          backdropFilter: 'blur(8px)',
          padding: '0 24px',
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'space-between',
          height: 64,
          gap: 16,
        }}
      >
        {/* Logo */}
        <Link
          to="/"
          onClick={close}
          style={{ display: 'flex', alignItems: 'center', gap: 12, textDecoration: 'none', flexShrink: 0 }}
        >
          <div
            style={{
              width: 36,
              height: 36,
              borderRadius: '50%',
              border: '1.5px solid #C9A84C',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              fontFamily: 'Cinzel, serif',
              fontSize: 12,
              color: '#C9A84C',
              letterSpacing: '0.05em',
            }}
          >
            VB
          </div>
          <span style={{ fontFamily: 'Playfair Display, serif', fontSize: 18, color: '#E8C870', letterSpacing: '0.1em' }}>
            VIRTUALBAR
          </span>
        </Link>

        {/* Desktop center links */}
        <div
          className="hidden md:flex"
          style={{ flex: 1, alignItems: 'center', justifyContent: 'center', gap: 28 }}
        >
          <Link to="/" style={navLinkStyle}>{t('nav.home')}</Link>
          <Link to="/browse" style={navLinkStyle}>{t('nav.browse')}</Link>
          <Link to="/marketplace" style={navLinkStyle}>{t('nav.marketplace')}</Link>
          {isAuthenticated && <Link to="/dashboard" style={navLinkStyle}>{t('nav.myBar')}</Link>}
          {isAuthenticated && <Link to="/offers" style={navLinkStyle}>{t('nav.offers')}</Link>}
          {isAuthenticated && (
            <button
              onClick={toggleInbox}
              style={{ ...navLinkStyle, background: 'transparent', border: 'none', cursor: 'pointer', padding: 0 }}
            >
              {t('nav.messages')}
            </button>
          )}
        </div>

        {/* Desktop right auth */}
        <div
          className="hidden md:flex"
          style={{ alignItems: 'center', gap: 16, flexShrink: 0 }}
        >
          {isAuthenticated ? (
            <>
              <Link to="/profile" style={{ display: 'flex', alignItems: 'center', gap: 10, textDecoration: 'none' }}>
                {user && <Avatar displayName={user.displayName} avatarUrl={user.avatarUrl} size={32} />}
                <span style={{ fontFamily: 'Cormorant Garamond, serif', fontSize: 15, color: '#C9A84C' }}>
                  {user?.displayName}
                </span>
              </Link>
              <NotificationBell />
              <LanguageSwitcher />
              <button
                onClick={logout}
                style={{
                  fontFamily: 'Cinzel, serif',
                  fontSize: 11,
                  letterSpacing: '0.15em',
                  color: '#B09868',
                  border: '1px solid rgba(201,168,76,0.2)',
                  background: 'transparent',
                  padding: '6px 16px',
                  borderRadius: 2,
                  cursor: 'pointer',
                }}
              >
                {t('nav.logout')}
              </button>
            </>
          ) : (
            <>
              <Link to="/login" style={navLinkStyle}>{t('nav.login')}</Link>
              <LanguageSwitcher />
              <Link
                to="/register"
                style={{
                  fontFamily: 'Cinzel, serif',
                  fontSize: 11,
                  letterSpacing: '0.15em',
                  color: '#07030A',
                  background: 'linear-gradient(135deg, #C9A84C, #E8C870)',
                  padding: '7px 18px',
                  borderRadius: 2,
                  textDecoration: 'none',
                }}
              >
                {t('nav.register')}
              </Link>
            </>
          )}
        </div>

        {/* Mobile hamburger */}
        <button
          className="flex md:hidden"
          onClick={() => setMenuOpen(o => !o)}
          aria-label="Toggle menu"
          style={{
            background: 'transparent',
            border: 'none',
            cursor: 'pointer',
            fontSize: 22,
            color: '#C9A84C',
            padding: '4px 8px',
            lineHeight: 1,
            flexShrink: 0,
          }}
        >
          {menuOpen ? '✕' : '☰'}
        </button>
      </nav>

      {/* ── Mobile dropdown ── */}
      {menuOpen && (
        <div
          className="flex md:hidden flex-col"
          style={{
            background: 'rgba(7,3,10,0.97)',
            borderBottom: '1px solid rgba(201,168,76,0.2)',
          }}
        >
          <Link to="/" onClick={close} style={mobileLinkStyle}>{t('nav.home')}</Link>
          <Link to="/browse" onClick={close} style={mobileLinkStyle}>{t('nav.browse')}</Link>
          <Link to="/marketplace" onClick={close} style={mobileLinkStyle}>{t('nav.marketplace')}</Link>
          {isAuthenticated && <Link to="/dashboard" onClick={close} style={mobileLinkStyle}>{t('nav.myBar')}</Link>}
          {isAuthenticated && <Link to="/offers" onClick={close} style={mobileLinkStyle}>{t('nav.offers')}</Link>}
          {isAuthenticated && (
            <button
              onClick={() => { toggleInbox(); close() }}
              style={{ ...mobileLinkStyle, background: 'transparent', border: 'none', cursor: 'pointer', textAlign: 'left', width: '100%' }}
            >
              {t('nav.messages')}
            </button>
          )}

          <div style={{ height: 1, background: 'rgba(201,168,76,0.15)', margin: '4px 0' }} />

          {isAuthenticated ? (
            <div style={{ padding: '12px 24px', display: 'flex', flexDirection: 'column', gap: 14 }}>
              <Link
                to="/profile"
                onClick={close}
                style={{ display: 'flex', alignItems: 'center', gap: 10, textDecoration: 'none' }}
              >
                {user && <Avatar displayName={user.displayName} avatarUrl={user.avatarUrl} size={32} />}
                <span style={{ fontFamily: 'Cormorant Garamond, serif', fontSize: 15, color: '#C9A84C' }}>
                  {user?.displayName}
                </span>
              </Link>
              <div style={{ display: 'flex', alignItems: 'center', gap: 12 }}>
                <NotificationBell />
                <LanguageSwitcher />
              </div>
              <button
                onClick={() => { logout(); close() }}
                style={{
                  fontFamily: 'Cinzel, serif',
                  fontSize: 11,
                  letterSpacing: '0.15em',
                  color: '#B09868',
                  border: '1px solid rgba(201,168,76,0.2)',
                  background: 'transparent',
                  padding: '8px 16px',
                  borderRadius: 2,
                  cursor: 'pointer',
                  alignSelf: 'flex-start',
                }}
              >
                {t('nav.logout')}
              </button>
            </div>
          ) : (
            <div style={{ padding: '12px 24px', display: 'flex', flexDirection: 'column', gap: 12 }}>
              <Link to="/login" onClick={close} style={{ ...navLinkStyle, padding: '4px 0' }}>{t('nav.login')}</Link>
              <Link
                to="/register"
                onClick={close}
                style={{
                  fontFamily: 'Cinzel, serif',
                  fontSize: 11,
                  letterSpacing: '0.15em',
                  color: '#07030A',
                  background: 'linear-gradient(135deg, #C9A84C, #E8C870)',
                  padding: '8px 18px',
                  borderRadius: 2,
                  textDecoration: 'none',
                  alignSelf: 'flex-start',
                }}
              >
                {t('nav.register')}
              </Link>
              <div style={{ paddingTop: 4 }}>
                <LanguageSwitcher />
              </div>
            </div>
          )}
        </div>
      )}
    </div>
  )
}
