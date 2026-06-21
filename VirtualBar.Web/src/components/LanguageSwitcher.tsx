import { useState, useEffect, useRef } from 'react'
import { useTranslation } from 'react-i18next'

export default function LanguageSwitcher() {
  const { i18n, t } = useTranslation()
  const [open, setOpen] = useState(false)
  const ref = useRef<HTMLDivElement>(null)

  useEffect(() => {
    function handleClickOutside(e: MouseEvent) {
      if (ref.current && !ref.current.contains(e.target as Node)) {
        setOpen(false)
      }
    }
    document.addEventListener('mousedown', handleClickOutside)
    return () => document.removeEventListener('mousedown', handleClickOutside)
  }, [])

  const languages = [
    { code: 'bg', label: t('lang.bg') },
    { code: 'en', label: t('lang.en') },
  ]

  const currentCode = i18n.language?.startsWith('bg') ? 'bg' : 'en'
  const currentLabel = currentCode === 'bg' ? 'БГ' : 'EN'

  return (
    <div ref={ref} style={{ position: 'relative' }}>
      <button
        onClick={() => setOpen(o => !o)}
        style={{
          fontFamily: 'Cinzel, serif',
          fontSize: 11,
          letterSpacing: '0.15em',
          color: '#C9A84C',
          background: 'transparent',
          border: '1px solid rgba(201,168,76,0.3)',
          borderRadius: 3,
          padding: '4px 10px',
          cursor: 'pointer',
          display: 'flex',
          alignItems: 'center',
          gap: 5,
        }}
      >
        {currentLabel}
        <span style={{ fontSize: 8, opacity: 0.7 }}>▼</span>
      </button>

      {open && (
        <div
          style={{
            position: 'absolute',
            top: 'calc(100% + 6px)',
            right: 0,
            background: '#0A0502',
            border: '1px solid rgba(201,168,76,0.2)',
            borderRadius: 4,
            minWidth: 130,
            zIndex: 100,
            overflow: 'hidden',
          }}
        >
          {languages.map(lang => (
            <button
              key={lang.code}
              onClick={() => {
                i18n.changeLanguage(lang.code)
                setOpen(false)
              }}
              style={{
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'space-between',
                width: '100%',
                padding: '9px 14px',
                fontFamily: 'Cinzel, serif',
                fontSize: 11,
                letterSpacing: '0.1em',
                color: currentCode === lang.code ? '#C9A84C' : '#8A7A5A',
                background: 'transparent',
                border: 'none',
                borderBottom: '1px solid rgba(201,168,76,0.08)',
                cursor: 'pointer',
                textAlign: 'left',
              }}
            >
              {lang.label}
              {currentCode === lang.code && (
                <span style={{ color: '#C9A84C', fontSize: 10 }}>✓</span>
              )}
            </button>
          ))}
        </div>
      )}
    </div>
  )
}
