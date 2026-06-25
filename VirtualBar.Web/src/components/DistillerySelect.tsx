import { useEffect, useMemo, useRef, useState } from 'react'
import type { CSSProperties, KeyboardEvent } from 'react'
import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { getDistilleries } from '../api/distilleriesApi'
import type { Distillery } from '../types'

interface Props {
  value: string | null
  onChange: (id: string | null, name: string | null) => void
  placeholder?: string
  category?: string
  style?: CSSProperties
  inputStyle?: CSSProperties
}

const wrapperStyle: CSSProperties = {
  position: 'relative',
  width: '100%',
}

const defaultInputStyle: CSSProperties = {
  background: '#0A0502',
  border: '1px solid rgba(201,168,76,0.2)',
  color: '#F0DDB4',
  fontFamily: 'Cormorant Garamond, serif',
  fontSize: 16,
  padding: '10px 14px',
  borderRadius: 4,
  outline: 'none',
  width: '100%',
}

const dropdownStyle: CSSProperties = {
  position: 'absolute',
  top: 'calc(100% + 4px)',
  left: 0,
  right: 0,
  zIndex: 60,
  background: '#0D0804',
  border: '1px solid rgba(201,168,76,0.35)',
  borderRadius: 4,
  maxHeight: 220,
  overflowY: 'auto',
  boxShadow: '0 16px 40px rgba(0,0,0,0.6)',
}

const optionBaseStyle: CSSProperties = {
  padding: '9px 14px',
  cursor: 'pointer',
  fontFamily: 'Cormorant Garamond, serif',
  fontSize: 16,
  color: '#E8D4A0',
  display: 'flex',
  flexDirection: 'column',
  gap: 2,
}

const optionActiveStyle: CSSProperties = {
  ...optionBaseStyle,
  background: 'rgba(201,168,76,0.12)',
}

const optionMetaStyle: CSSProperties = {
  fontFamily: 'Cinzel, serif',
  fontSize: 10,
  letterSpacing: '0.15em',
  color: '#7A6040',
  textTransform: 'uppercase',
}

const noOptionsStyle: CSSProperties = {
  padding: '12px 14px',
  fontFamily: 'Cormorant Garamond, serif',
  fontStyle: 'italic',
  fontSize: 15,
  color: '#B09868',
}

export default function DistillerySelect({ value, onChange, placeholder, category, style, inputStyle }: Props) {
  const { t } = useTranslation()
  const wrapperRef = useRef<HTMLDivElement>(null)
  const highlightedRef = useRef<HTMLDivElement>(null)
  const [query, setQuery] = useState('')
  const [open, setOpen] = useState(false)
  const [highlight, setHighlight] = useState(0)

  const { data: distilleries = [] } = useQuery({
    queryKey: ['distilleries', category ?? 'all'],
    queryFn: () => getDistilleries(category),
    staleTime: 5 * 60_000,
  })

  useEffect(() => {
    if (value === null) {
      setQuery('')
      return
    }
    const match = distilleries.find(d => d.id === value)
    if (match) setQuery(match.name)
  }, [value, distilleries])

  useEffect(() => {
    highlightedRef.current?.scrollIntoView({ block: 'nearest' })
  }, [highlight])

  const suggestions = useMemo(() => {
    const q = query.trim().toLowerCase()
    if (!q) return distilleries
    return distilleries.filter(d => d.name.toLowerCase().includes(q))
  }, [query, distilleries])

  useEffect(() => {
    if (!open) return

    function handleClick(e: MouseEvent) {
      if (wrapperRef.current && !wrapperRef.current.contains(e.target as Node)) {
        setOpen(false)
      }
    }

    document.addEventListener('mousedown', handleClick)
    return () => document.removeEventListener('mousedown', handleClick)
  }, [open])

  function select(d: Distillery) {
    setQuery(d.name)
    onChange(d.id, d.name)
    setOpen(false)
  }

  function handleInput(text: string) {
    setQuery(text)
    setOpen(true)
    setHighlight(0)
    if (text.trim() === '') onChange(null, null)
  }

  function handleKeyDown(e: KeyboardEvent<HTMLInputElement>) {
    if (e.key === 'Escape') {
      setOpen(false)
      return
    }
    if (e.key === 'ArrowDown') {
      e.preventDefault()
      setOpen(true)
      setHighlight(h => Math.min(h + 1, suggestions.length - 1))
      return
    }
    if (e.key === 'ArrowUp') {
      e.preventDefault()
      setHighlight(h => Math.max(h - 1, 0))
      return
    }
    if (e.key === 'Enter') {
      if (open && suggestions[highlight]) {
        e.preventDefault()
        select(suggestions[highlight])
      }
    }
  }

  const mergedInputStyle: CSSProperties = inputStyle
    ? { ...defaultInputStyle, ...inputStyle }
    : defaultInputStyle

  return (
    <div ref={wrapperRef} style={style ? { ...wrapperStyle, ...style } : wrapperStyle}>
      <input
        value={query}
        onChange={e => handleInput(e.target.value)}
        onFocus={() => setOpen(true)}
        onKeyDown={handleKeyDown}
        placeholder={placeholder ?? t('distillerySelect.placeholder')}
        style={mergedInputStyle}
        autoComplete="off"
        role="combobox"
        aria-expanded={open}
        aria-controls="distillery-listbox"
        aria-activedescendant={open && highlight >= 0 ? `distillery-opt-${highlight}` : undefined}
        aria-autocomplete="list"
      />
      {open && (
        <div id="distillery-listbox" role="listbox" style={dropdownStyle}>
          {suggestions.length === 0 ? (
            <div style={noOptionsStyle}>{t('distillerySelect.noOptions')}</div>
          ) : (
            suggestions.map((d, i) => (
              <div
                key={d.id}
                ref={i === highlight ? highlightedRef : undefined}
                role="option"
                id={`distillery-opt-${i}`}
                aria-selected={i === highlight}
                onMouseDown={() => select(d)}
                onMouseEnter={() => setHighlight(i)}
                style={i === highlight ? optionActiveStyle : optionBaseStyle}
              >
                <span>{d.name}</span>
                {(d.country || d.region) && (
                  <span style={optionMetaStyle}>
                    {[d.region, d.country].filter(Boolean).join(', ')}
                  </span>
                )}
              </div>
            ))
          )}
        </div>
      )}
    </div>
  )
}
