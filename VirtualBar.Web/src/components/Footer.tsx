import { useTranslation } from 'react-i18next'
import { Link } from 'react-router-dom'
import { useAuth } from '../contexts/AuthContext'

export default function Footer() {
  const { t } = useTranslation()
  const { isAuthenticated } = useAuth()

  const navLinks = [
    { to: '/', label: t('nav.home') },
    { to: '/browse', label: t('nav.browse') },
    { to: '/marketplace', label: t('nav.marketplace') },
    ...(isAuthenticated ? [{ to: '/dashboard', label: t('nav.myBar') }] : []),
  ]

  const legalItems = [
    t('footer.about'),
    t('footer.privacy'),
    t('footer.terms'),
  ]

  return (
    <footer style={{
      background: '#07030A',
      borderTop: '1px solid rgba(201, 168, 76, 0.25)',
      padding: '52px 40px 28px',
      color: '#B8A88A',
      fontFamily: "'Cormorant Garamond', Georgia, serif",
      position: 'relative',
      zIndex: 1,
    }}>
      <div style={{ maxWidth: 1100, margin: '0 auto' }}>

        <div style={{
          display: 'grid',
          gridTemplateColumns: '2fr 1fr 1fr',
          gap: 48,
          marginBottom: 44,
        }}>
          {/* Brand */}
          <div>
            <div style={{ display: 'flex', alignItems: 'center', gap: 12, marginBottom: 18 }}>
              <div style={{
                width: 38, height: 38,
                borderRadius: '50%',
                border: '1.5px solid #C9A84C',
                display: 'flex', alignItems: 'center', justifyContent: 'center',
                fontFamily: "'Cinzel', serif",
                fontSize: 11,
                color: '#C9A84C',
                fontWeight: 600,
                flexShrink: 0,
              }}>
                VB
              </div>
              <span style={{
                fontFamily: "'Cinzel', serif",
                fontSize: 15,
                color: '#E8C870',
                letterSpacing: 4,
                fontWeight: 600,
              }}>
                VIRTUALBAR
              </span>
            </div>
            <p style={{
              fontSize: 15,
              lineHeight: 1.75,
              color: '#7A6A52',
              maxWidth: 260,
              margin: 0,
              fontStyle: 'italic',
            }}>
              {t('footer.tagline')}
            </p>
          </div>

          {/* Navigation */}
          <div>
            <h4 style={{
              fontFamily: "'Cinzel', serif",
              fontSize: 10,
              letterSpacing: 3,
              color: '#C9A84C',
              margin: '0 0 20px 0',
              fontWeight: 600,
            }}>
              {t('footer.explore')}
            </h4>
            <ul style={{ listStyle: 'none', padding: 0, margin: 0, display: 'flex', flexDirection: 'column', gap: 12 }}>
              {navLinks.map(link => (
                <li key={link.to}>
                  <Link
                    to={link.to}
                    style={{
                      color: '#7A6A52',
                      textDecoration: 'none',
                      fontSize: 12,
                      letterSpacing: 2,
                      fontFamily: "'Cinzel', serif",
                      transition: 'color 0.2s',
                      display: 'inline-block',
                    }}
                    onMouseEnter={e => (e.currentTarget.style.color = '#C9A84C')}
                    onMouseLeave={e => (e.currentTarget.style.color = '#7A6A52')}
                  >
                    {link.label}
                  </Link>
                </li>
              ))}
            </ul>
          </div>

          {/* Legal */}
          <div>
            <h4 style={{
              fontFamily: "'Cinzel', serif",
              fontSize: 10,
              letterSpacing: 3,
              color: '#C9A84C',
              margin: '0 0 20px 0',
              fontWeight: 600,
            }}>
              {t('footer.legal')}
            </h4>
            <ul style={{ listStyle: 'none', padding: 0, margin: 0, display: 'flex', flexDirection: 'column', gap: 12 }}>
              {legalItems.map(item => (
                <li key={item}>
                  <span style={{
                    color: '#7A6A52',
                    fontSize: 12,
                    letterSpacing: 2,
                    fontFamily: "'Cinzel', serif",
                  }}>
                    {item}
                  </span>
                </li>
              ))}
            </ul>
          </div>
        </div>

        {/* Decorative divider */}
        <div style={{ display: 'flex', alignItems: 'center', gap: 16, marginBottom: 24 }}>
          <div style={{ flex: 1, height: 1, background: 'rgba(201, 168, 76, 0.2)' }} />
          <div style={{ color: '#C9A84C', fontSize: 10, letterSpacing: 4 }}>◆</div>
          <div style={{ flex: 1, height: 1, background: 'rgba(201, 168, 76, 0.2)' }} />
        </div>

        {/* Copyright */}
        <p style={{
          textAlign: 'center',
          fontSize: 11,
          color: '#4A3A22',
          letterSpacing: 2.5,
          fontFamily: "'Cinzel', serif",
          margin: 0,
        }}>
          {t('footer.rights')}
        </p>
      </div>
    </footer>
  )
}
