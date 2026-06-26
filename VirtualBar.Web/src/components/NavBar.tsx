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

export default function NavBar() {
  const { t } = useTranslation()
  const { user, isAuthenticated, logout } = useAuth()
  const { toggleInbox } = useChat()

  return (
    <nav
      style={{
        borderBottom: '1px solid rgba(201,168,76,0.12)',
        background: 'rgba(7,3,10,0.95)',
        backdropFilter: 'blur(8px)',
        position: 'sticky',
        top: 0,
        zIndex: 40,
        padding: '0 40px',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'space-between',
        height: 64,
      }}
    >
      {/* Logo — left */}
      <Link to="/" style={{ display: 'flex', alignItems: 'center', gap: 12, textDecoration: 'none', flex: '0 0 auto' }}>
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

      {/* Nav links — center */}
      <div style={{ display: 'flex', alignItems: 'center', gap: 28, position: 'absolute', left: '50%', transform: 'translateX(-50%)' }}>
        <Link to="/" style={navLinkStyle}>{t('nav.home')}</Link>
        <Link to="/browse" style={navLinkStyle}>{t('nav.browse')}</Link>
        <Link to="/marketplace" style={navLinkStyle}>{t('nav.marketplace')}</Link>
        {isAuthenticated && (
          <Link to="/dashboard" style={navLinkStyle}>{t('nav.myBar')}</Link>
        )}
        {isAuthenticated && (
          <Link to="/offers" style={navLinkStyle}>{t('nav.offers')}</Link>
        )}
        {isAuthenticated && (
          <button
            onClick={toggleInbox}
            style={{ ...navLinkStyle, background: 'transparent', border: 'none', cursor: 'pointer', padding: 0 }}
          >
            {t('nav.messages')}
          </button>
        )}
      </div>

      {/* Auth — right */}
      <div style={{ display: 'flex', alignItems: 'center', gap: 16, flex: '0 0 auto' }}>
        {isAuthenticated ? (
          <>
            <Link
              to="/profile"
              style={{ display: 'flex', alignItems: 'center', gap: 10, textDecoration: 'none' }}
            >
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
    </nav>
  )
}
