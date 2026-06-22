import { useState, useEffect } from 'react'
import type { CSSProperties } from 'react'
import { Link } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { useAuth } from '../contexts/AuthContext'
import { getMarketplace } from '../api/bottlesApi'
import type { Bottle, SpiritCategory } from '../types'
import { CATEGORY_COLORS, BottleSvg } from '../components/BarShelf'
import BottleDetailPanel from '../components/BottleDetailPanel'
import NavBar from '../components/NavBar'

const CATEGORIES = Object.keys(CATEGORY_COLORS) as SpiritCategory[]

type SortOption = 'price_asc' | 'price_desc' | 'newest'

function useDebounced<T>(value: T, delay: number): T {
  const [debounced, setDebounced] = useState(value)

  useEffect(() => {
    const handle = setTimeout(() => setDebounced(value), delay)
    return () => clearTimeout(handle)
  }, [value, delay])

  return debounced
}

const inputStyle: CSSProperties = {
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

const selectStyle: CSSProperties = {
  background: '#0A0502',
  border: '1px solid rgba(201,168,76,0.2)',
  color: '#F0DDB4',
  fontFamily: 'Cormorant Garamond, serif',
  fontSize: 15,
  padding: '10px 14px',
  borderRadius: 4,
  outline: 'none',
  cursor: 'pointer',
}

function focusOn(e: React.FocusEvent<HTMLInputElement>) {
  e.currentTarget.style.border = '1px solid rgba(201,168,76,0.5)'
}

function focusOff(e: React.FocusEvent<HTMLInputElement>) {
  e.currentTarget.style.border = '1px solid rgba(201,168,76,0.2)'
}

function CategoryPill({
  label,
  active,
  color,
  onClick,
}: {
  label: string
  active: boolean
  color: string
  onClick: () => void
}) {
  const [hover, setHover] = useState(false)

  return (
    <button
      onClick={onClick}
      onMouseEnter={() => setHover(true)}
      onMouseLeave={() => setHover(false)}
      style={{
        fontFamily: 'Cormorant Garamond, serif',
        fontSize: 15,
        padding: '6px 18px',
        borderRadius: 20,
        cursor: 'pointer',
        transition: 'all 0.2s ease',
        background: active ? `${color}26` : 'transparent',
        border: active ? `1px solid ${color}` : `1px solid rgba(201,168,76,${hover ? 0.35 : 0.15})`,
        color: active ? color : '#B09868',
        letterSpacing: '0.03em',
        whiteSpace: 'nowrap',
      }}
    >
      {label}
    </button>
  )
}

const conditionColor: Record<string, string> = {
  Sealed: '#4A9A6A',
  Opened: '#C8820A',
  Empty: '#7A6040',
}

function MarketplaceCard({ bottle, onView }: { bottle: Bottle; onView: () => void }) {
  const { t } = useTranslation()
  const [hover, setHover] = useState(false)
  const cat = CATEGORY_COLORS[bottle.category]
  const primaryImage = bottle.images.find((i) => i.isPrimary) ?? bottle.images[0]

  return (
    <div
      onMouseEnter={() => setHover(true)}
      onMouseLeave={() => setHover(false)}
      style={{
        background: 'linear-gradient(180deg, #0F0604, #130805)',
        border: `1px solid ${hover ? 'rgba(201,168,76,0.3)' : 'rgba(201,168,76,0.12)'}`,
        borderRadius: 4,
        overflow: 'hidden',
        transition: 'all 0.2s ease',
        transform: hover ? 'translateY(-2px)' : 'translateY(0)',
        display: 'flex',
        flexDirection: 'column',
      }}
    >
      <div
        style={{
          height: 160,
          position: 'relative',
          overflow: 'hidden',
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
          background: `radial-gradient(ellipse at 50% 40%, ${cat.glow}1A, #0A0502)`,
        }}
      >
        {primaryImage ? (
          <img
            src={primaryImage.url}
            alt={bottle.name}
            style={{ width: '100%', height: '100%', objectFit: 'cover', display: 'block' }}
          />
        ) : (
          <div style={{ width: 50 }}>
            <BottleSvg category={bottle.category} condition={bottle.condition} />
          </div>
        )}
      </div>

      <div style={{ padding: 16, display: 'flex', flexDirection: 'column', flex: 1 }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginBottom: 10, flexWrap: 'wrap' }}>
          <span
            style={{
              fontFamily: 'Cinzel, serif',
              fontSize: 9,
              letterSpacing: '0.15em',
              textTransform: 'uppercase',
              padding: '3px 8px',
              borderRadius: 3,
              background: `${cat.glass}22`,
              color: cat.glass,
            }}
          >
            {cat.label}
          </span>
          <span
            style={{
              fontFamily: 'Cinzel, serif',
              fontSize: 9,
              letterSpacing: '0.15em',
              textTransform: 'uppercase',
              padding: '3px 8px',
              borderRadius: 3,
              background: `${conditionColor[bottle.condition]}1A`,
              color: conditionColor[bottle.condition],
            }}
          >
            {t(`addBottle.condition${bottle.condition}`)}
          </span>
          {bottle.isLimited && (
            <span style={{ fontFamily: 'Cinzel, serif', fontSize: 11, color: '#E8C870' }}>◆</span>
          )}
        </div>

        <div
          style={{
            fontFamily: 'Playfair Display, serif',
            fontSize: 16,
            color: '#E8C870',
            lineHeight: 1.2,
            marginBottom: 2,
          }}
        >
          {bottle.name}
        </div>

        {bottle.distillery && (
          <div
            style={{
              fontFamily: 'Cormorant Garamond, serif',
              fontStyle: 'italic',
              fontSize: 15,
              color: '#B09868',
              marginBottom: 8,
            }}
          >
            {bottle.distillery}
          </div>
        )}

        <div
          style={{
            display: 'flex',
            gap: 14,
            flexWrap: 'wrap',
            fontFamily: 'Cormorant Garamond, serif',
            fontSize: 14,
            color: '#8A7350',
            marginBottom: 12,
          }}
        >
          {bottle.age != null && <span>{bottle.age} yr</span>}
          {bottle.abvPercent != null && <span>{bottle.abvPercent}% ABV</span>}
          {bottle.volumeMl != null && <span>{bottle.volumeMl} ml</span>}
        </div>

        <div style={{ marginTop: 'auto' }}>
          <div
            style={{
              fontFamily: 'Playfair Display, serif',
              fontSize: 20,
              color: '#E8C870',
              marginBottom: 8,
            }}
          >
            {bottle.askingPrice != null
              ? `${bottle.currency ?? ''} ${bottle.askingPrice.toLocaleString()}`.trim()
              : t('marketplace.priceOnRequest')}
          </div>

          <Link
            to={`/bar/${bottle.userId}`}
            style={{
              fontFamily: 'Cormorant Garamond, serif',
              fontStyle: 'italic',
              fontSize: 14,
              color: '#B09868',
              textDecoration: 'none',
              display: 'inline-block',
              marginBottom: 12,
            }}
          >
            {t('marketplace.by', { name: bottle.userDisplayName })}
          </Link>

          <button
            onClick={onView}
            style={{
              width: '100%',
              fontFamily: 'Cinzel, serif',
              fontSize: 11,
              letterSpacing: '0.2em',
              textTransform: 'uppercase',
              color: '#C9A84C',
              background: 'transparent',
              border: '1px solid rgba(201,168,76,0.35)',
              padding: '10px',
              borderRadius: 2,
              cursor: 'pointer',
              transition: 'all 0.2s ease',
            }}
          >
            {t('marketplace.viewBottle')}
          </button>
        </div>
      </div>
    </div>
  )
}

export default function MarketplacePage() {
  const { t } = useTranslation()
  const { user } = useAuth()
  const [search, setSearch] = useState('')
  const [category, setCategory] = useState<string>('')
  const [sort, setSort] = useState<SortOption>('newest')
  const [selectedBottle, setSelectedBottle] = useState<Bottle | null>(null)
  const debouncedSearch = useDebounced(search, 300)

  const { data: bottles = [], isLoading } = useQuery({
    queryKey: ['marketplace', debouncedSearch, category, sort],
    queryFn: () =>
      getMarketplace({
        search: debouncedSearch || undefined,
        category: category || undefined,
        sort,
      }),
  })

  return (
    <div style={{ minHeight: '100vh', color: '#F0DDB4' }}>
      <NavBar />

      <div
        style={{
          borderBottom: '1px solid rgba(201,168,76,0.08)',
          background: 'rgba(7,3,10,0.92)',
          backdropFilter: 'blur(8px)',
          position: 'sticky',
          top: 64,
          zIndex: 30,
          padding: '16px 40px',
        }}
      >
        <div
          style={{
            maxWidth: 1200,
            margin: '0 auto',
            display: 'flex',
            alignItems: 'center',
            gap: 16,
            flexWrap: 'wrap',
          }}
        >
          <div style={{ position: 'relative', width: '100%', maxWidth: 320 }}>
            <span
              style={{
                position: 'absolute',
                left: 14,
                top: '50%',
                transform: 'translateY(-50%)',
                color: 'rgba(201,168,76,0.5)',
                fontSize: 14,
                pointerEvents: 'none',
              }}
            >
              🔍
            </span>
            <input
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              onFocus={focusOn}
              onBlur={focusOff}
              placeholder={t('marketplace.searchPlaceholder')}
              style={{ ...inputStyle, paddingLeft: 40 }}
            />
          </div>

          <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap', flex: 1 }}>
            <CategoryPill label={t('marketplace.allCategories')} active={category === ''} color="#C9A84C" onClick={() => setCategory('')} />
            {CATEGORIES.map((cat) => (
              <CategoryPill
                key={cat}
                label={CATEGORY_COLORS[cat].label}
                active={category === cat}
                color={CATEGORY_COLORS[cat].glass}
                onClick={() => setCategory(category === cat ? '' : cat)}
              />
            ))}
          </div>

          <select
            value={sort}
            onChange={(e) => setSort(e.target.value as SortOption)}
            style={selectStyle}
          >
            <option value="newest">{t('marketplace.sortNewest')}</option>
            <option value="price_asc">{t('marketplace.sortPriceAsc')}</option>
            <option value="price_desc">{t('marketplace.sortPriceDesc')}</option>
          </select>
        </div>
      </div>

      <main style={{ maxWidth: 1200, margin: '0 auto', padding: '40px' }}>
        <div style={{ marginBottom: 32 }}>
          <div
            style={{
              fontFamily: 'Cinzel, serif',
              fontSize: 13,
              letterSpacing: '0.4em',
              color: '#B09868',
              marginBottom: 8,
            }}
          >
            {t('marketplace.label')}
          </div>
          <h1
            style={{
              fontFamily: 'Playfair Display, serif',
              fontSize: 38,
              fontWeight: 700,
              color: '#E8C870',
              margin: 0,
              lineHeight: 1.1,
            }}
          >
            {t('marketplace.title')}
          </h1>
        </div>

        {isLoading && (
          <div
            style={{
              textAlign: 'center',
              padding: '80px 0',
              fontFamily: 'Cinzel, serif',
              fontSize: 13,
              letterSpacing: '0.4em',
              color: '#C9A84C',
              animation: 'shimmer 1.6s ease-in-out infinite',
            }}
          >
            {t('marketplace.loading')}
          </div>
        )}

        {!isLoading && bottles.length === 0 && (
          <div style={{ textAlign: 'center', padding: '60px 0' }}>
            <p
              style={{
                fontFamily: 'Cormorant Garamond, serif',
                fontSize: 20,
                fontStyle: 'italic',
                color: '#C9A84C',
              }}
            >
              {t('marketplace.empty')}
            </p>
          </div>
        )}

        {!isLoading && bottles.length > 0 && (
          <div
            style={{
              display: 'grid',
              gridTemplateColumns: 'repeat(auto-fill, minmax(280px, 1fr))',
              gap: 20,
            }}
          >
            {bottles.map((bottle) => (
              <MarketplaceCard key={bottle.id} bottle={bottle} onView={() => setSelectedBottle(bottle)} />
            ))}
          </div>
        )}
      </main>

      {selectedBottle && (
        <BottleDetailPanel
          bottle={selectedBottle}
          userId={selectedBottle.userId}
          currentUserId={user?.id ?? ''}
          onClose={() => setSelectedBottle(null)}
        />
      )}
    </div>
  )
}
