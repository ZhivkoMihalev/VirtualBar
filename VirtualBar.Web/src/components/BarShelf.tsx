import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import type { Bottle, SpiritCategory, BottleCondition } from '../types'

export const CATEGORY_COLORS: Record<SpiritCategory, { body: string; glass: string; glow: string; label: string }> = {
  Whisky:  { body: '#5C3A00', glass: '#C8820A', glow: '#C8820A', label: 'Whisky' },
  Rum:     { body: '#5A1008', glass: '#D42020', glow: '#E83020', label: 'Rum' },
  Cognac:  { body: '#4A2008', glass: '#C86030', glow: '#D87040', label: 'Cognac' },
  Vodka:   { body: '#1A2A3A', glass: '#90B8D8', glow: '#A0C8E8', label: 'Vodka' },
  Gin:     { body: '#102818', glass: '#4A9A6A', glow: '#5AAA7A', label: 'Gin' },
  Tequila: { body: '#5A4A00', glass: '#D4A820', glow: '#E4B830', label: 'Tequila' },
  Brandy:  { body: '#4A2008', glass: '#C84818', glow: '#D85828', label: 'Brandy' },
  Other:   { body: '#201040', glass: '#8060C8', glow: '#9070D8', label: 'Other' },
}

export const LIQUID_LEVEL: Record<BottleCondition, number> = {
  Sealed: 0.88,
  Opened: 0.45,
  Empty: 0,
}

export const CAP_COLOR: Record<BottleCondition, string> = {
  Sealed: '#C9A84C',
  Opened: '#5A3A1A',
  Empty: '#1A0C06',
}

export const BOTTLE_PATH =
  'M 20 7 L 30 7 C 33 8 35 18 34 24 C 42 32 44 42 44 52 L 44 128 C 44 142 37 150 25 150 C 13 150 6 142 6 128 L 6 52 C 6 42 8 32 16 24 C 15 18 17 8 20 7 Z'

export function BottleSvg({ category, condition }: { category: SpiritCategory; condition: BottleCondition }) {
  const col = CATEGORY_COLORS[category]
  const level = LIQUID_LEVEL[condition]

  const bodyTop = 24
  const bodyBottom = 150
  const liquidHeight = (bodyBottom - bodyTop) * level
  const liquidY = bodyBottom - liquidHeight

  const bodyId = `grad-body-${category}`
  const glassId = `grad-glass-${category}`
  const highlightId = `grad-highlight-${category}`
  const clipId = `clip-${category}`

  return (
    <svg
      viewBox="0 0 50 155"
      width="50"
      height="155"
      style={{ display: 'block', filter: `drop-shadow(0 4px 12px ${col.glow}50)` }}
    >
      <defs>
        <linearGradient id={bodyId} x1="0" y1="0" x2="1" y2="0">
          <stop offset="0%" stopColor={col.body} stopOpacity="0.95" />
          <stop offset="45%" stopColor={col.glass} stopOpacity="0.85" />
          <stop offset="100%" stopColor={col.body} stopOpacity="0.95" />
        </linearGradient>
        <linearGradient id={glassId} x1="0" y1="0" x2="1" y2="0">
          <stop offset="0%" stopColor="#FFFFFF" stopOpacity="0.2" />
          <stop offset="50%" stopColor="#FFFFFF" stopOpacity="0" />
          <stop offset="100%" stopColor="#FFFFFF" stopOpacity="0.1" />
        </linearGradient>
        <linearGradient id={highlightId} x1="0" y1="0" x2="0" y2="1">
          <stop offset="0%" stopColor="#FFFFFF" stopOpacity="0.35" />
          <stop offset="100%" stopColor="#FFFFFF" stopOpacity="0.04" />
        </linearGradient>
        <clipPath id={clipId}>
          <path d={BOTTLE_PATH} />
        </clipPath>
      </defs>

      <path d={BOTTLE_PATH} fill={`url(#${bodyId})`} stroke="rgba(0,0,0,0.4)" strokeWidth="0.6" />

      {level > 0 && (
        <rect
          x="0"
          y={liquidY}
          width="50"
          height={liquidHeight + 2}
          fill={col.glass}
          opacity="0.9"
          clipPath={`url(#${clipId})`}
        />
      )}

      <path d={BOTTLE_PATH} fill={`url(#${glassId})`} />

      <rect x="11" y="30" width="3" height="110" rx="1.5" fill={`url(#${highlightId})`} clipPath={`url(#${clipId})`} />

      <rect x="16" y="0" width="18" height="8" rx="2" fill={CAP_COLOR[condition]} stroke="rgba(0,0,0,0.4)" strokeWidth="0.5" />
    </svg>
  )
}

export function BottleCard({ bottle, index, onSelect }: { bottle: Bottle; index: number; onSelect: (b: Bottle) => void }) {
  const [hover, setHover] = useState(false)
  const col = CATEGORY_COLORS[bottle.category]
  const primaryImage = bottle.images.find(i => i.isPrimary) ?? bottle.images[0]

  return (
    <div
      onClick={() => onSelect(bottle)}
      onMouseEnter={() => setHover(true)}
      onMouseLeave={() => setHover(false)}
      style={{
        position: 'relative',
        width: 84,
        flexShrink: 0,
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        cursor: 'pointer',
        animation: 'bottleIn 0.4s ease-out both',
        animationDelay: `${index * 0.06}s`,
        transition: 'transform 0.25s cubic-bezier(0.34, 1.56, 0.64, 1)',
        transform: hover ? 'translateY(-14px) scale(1.06)' : 'translateY(0) scale(1)',
        zIndex: hover ? 30 : 1,
      }}
    >
      {bottle.isLimited && (
        <span style={{ position: 'absolute', top: -2, right: 8, fontSize: 10, color: '#E8C870', textShadow: '0 0 6px rgba(201,168,76,0.8)', zIndex: 5 }}>◆</span>
      )}
      {bottle.isForSale && (
        <span style={{ position: 'absolute', top: 2, left: 12, width: 7, height: 7, borderRadius: '50%', background: '#4A9A6A', boxShadow: '0 0 6px rgba(74,154,106,0.9)', zIndex: 5 }} />
      )}

      <div style={{
        position: 'absolute',
        bottom: 30,
        left: '50%',
        transform: 'translateX(-50%)',
        width: 80,
        height: 130,
        background: `radial-gradient(ellipse at 50% 100%, ${col.glow}${hover ? '45' : '22'} 0%, transparent 70%)`,
        transition: 'all 0.3s ease',
        pointerEvents: 'none',
        zIndex: 0,
      }} />

      <div style={{
        position: 'relative',
        width: 66,
        height: 190,
        display: 'flex',
        alignItems: 'flex-end',
        justifyContent: 'center',
        filter: `drop-shadow(0 8px 18px ${col.glow}${hover ? '55' : '22'})`,
        transition: 'filter 0.3s ease',
        zIndex: 1,
      }}>
        {primaryImage ? (
          <img
            src={primaryImage.url}
            alt={bottle.name}
            style={{ maxWidth: '100%', maxHeight: '100%', objectFit: 'contain', objectPosition: 'bottom', borderRadius: '2px 2px 0 0' }}
          />
        ) : (
          <div style={{ width: 50, alignSelf: 'flex-end' }}>
            <BottleSvg category={bottle.category} condition={bottle.condition} />
          </div>
        )}
      </div>

      <div style={{
        fontFamily: 'Cormorant Garamond, serif',
        fontSize: 13,
        color: '#C9A84C',
        maxWidth: 84,
        textAlign: 'center',
        marginTop: 5,
        lineHeight: 1.15,
        display: '-webkit-box',
        WebkitLineClamp: 2,
        WebkitBoxOrient: 'vertical',
        overflow: 'hidden',
        zIndex: 1,
      }}>
        {bottle.name}
      </div>
    </div>
  )
}

export function EmptySlot({ onClick }: { onClick: () => void }) {
  const { t } = useTranslation()
  const [hover, setHover] = useState(false)

  return (
    <div
      onClick={onClick}
      onMouseEnter={() => setHover(true)}
      onMouseLeave={() => setHover(false)}
      style={{
        width: 66,
        height: 190,
        flexShrink: 0,
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        justifyContent: 'center',
        gap: 6,
        border: hover ? '1.5px dashed rgba(201,168,76,0.4)' : '1.5px dashed rgba(201,168,76,0.15)',
        borderRadius: 6,
        background: hover ? 'rgba(201,168,76,0.03)' : 'transparent',
        cursor: 'pointer',
        transition: 'all 0.2s ease',
      }}
    >
      <span style={{ fontSize: 20, color: hover ? 'rgba(201,168,76,0.5)' : 'rgba(201,168,76,0.2)', lineHeight: 1 }}>
        +
      </span>
      {hover && (
        <span
          style={{
            fontFamily: 'Cormorant Garamond, serif',
            fontSize: 10,
            color: 'rgba(201,168,76,0.5)',
            letterSpacing: '0.05em',
          }}
        >
          {t('barShelf.addBottle')}
        </span>
      )}
    </div>
  )
}

export function Shelf({
  bottles,
  startIndex,
  onAdd,
  onSelect,
}: {
  bottles: Bottle[]
  startIndex: number
  onAdd?: () => void
  onSelect: (b: Bottle) => void
}) {
  const emptyCount = onAdd ? Math.min(3, Math.max(1, 8 - bottles.length)) : 0

  return (
    <div>
      <div
        style={{
          position: 'absolute',
          left: 0,
          right: 0,
          bottom: 18,
          height: 100,
          background:
            'radial-gradient(ellipse at 50% 100%, rgba(212,120,32,0.18) 0%, rgba(212,120,32,0.05) 45%, transparent 75%)',
          pointerEvents: 'none',
        }}
      />

      <div
        className="vb-shelf-row"
        style={{
          position: 'relative',
          display: 'flex',
          alignItems: 'flex-end',
          gap: 18,
          padding: '0 40px',
          overflowX: 'auto',
          scrollbarWidth: 'none',
          msOverflowStyle: 'none',
        }}
      >
        {bottles.map((bottle, i) => (
          <BottleCard key={bottle.id} bottle={bottle} index={startIndex + i} onSelect={onSelect} />
        ))}
        {onAdd && Array.from({ length: emptyCount }).map((_, i) => (
          <EmptySlot key={`empty-${i}`} onClick={onAdd} />
        ))}
      </div>

      <div
        style={{
          height: 18,
          background: 'linear-gradient(180deg, #3D1A08, #2A1008, #1C0904)',
          boxShadow:
            '0 6px 16px rgba(0,0,0,0.6), inset 0 1px 0 rgba(201,168,76,0.25), inset 0 -1px 0 rgba(0,0,0,0.5)',
        }}
      />
    </div>
  )
}

export function VirtualBarScene({ bottles, onAdd, onSelect }: { bottles: Bottle[]; onAdd?: () => void; onSelect: (b: Bottle) => void }) {
  const { t } = useTranslation()
  const shelf1Bottles = bottles.slice(0, 8)
  const shelf2Bottles = bottles.slice(8, 16)

  return (
    <div
      style={{
        position: 'relative',
        height: 560,
        overflow: 'hidden',
        background: 'linear-gradient(180deg, #0A0503 0%, #100704 50%, #080402 100%)',
        border: '1px solid rgba(201,168,76,0.12)',
        borderRadius: 4,
      }}
    >
      <div
        style={{
          position: 'absolute',
          inset: 0,
          backgroundImage:
            'repeating-linear-gradient(90deg, transparent, transparent 119px, rgba(25,10,4,0.5) 120px), repeating-linear-gradient(90deg, transparent, transparent 59px, rgba(15,6,2,0.2) 60px)',
          pointerEvents: 'none',
        }}
      />

      <div
        style={{
          position: 'absolute',
          top: 0,
          left: 0,
          right: 0,
          height: 4,
          background: 'linear-gradient(90deg, transparent, rgba(201,168,76,0.3), transparent)',
        }}
      />

      <div
        style={{
          position: 'absolute',
          top: 24,
          left: 0,
          right: 0,
          textAlign: 'center',
          fontFamily: 'Cinzel, serif',
          fontSize: 11,
          letterSpacing: '0.4em',
          color: 'rgba(201,168,76,0.25)',
          textTransform: 'uppercase',
        }}
      >
        — {t('barShelf.virtualBar')} —
      </div>

      <div style={{ position: 'absolute', bottom: 265, left: 0, right: 0 }}>
        <Shelf bottles={shelf1Bottles} startIndex={0} onAdd={onAdd} onSelect={onSelect} />
      </div>

      <div style={{ position: 'absolute', bottom: 60, left: 0, right: 0 }}>
        <Shelf bottles={shelf2Bottles} startIndex={8} onAdd={onAdd} onSelect={onSelect} />
      </div>

      <div
        style={{
          position: 'absolute',
          bottom: 0,
          left: 0,
          right: 0,
          height: 60,
          background: 'linear-gradient(180deg, #160806, #0E0502)',
          borderTop: '2px solid rgba(201,168,76,0.3)',
          boxShadow: 'inset 0 3px 12px rgba(0,0,0,0.6)',
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
        }}
      >
        <span
          style={{
            fontFamily: 'Cinzel, serif',
            fontSize: 9,
            color: 'rgba(201,168,76,0.7)',
            letterSpacing: '0.3em',
          }}
        >
          EST. MMXXVI
        </span>
      </div>

      <div
        style={{
          position: 'absolute',
          inset: 0,
          background: 'radial-gradient(ellipse at 50% 50%, transparent 35%, rgba(0,0,0,0.55) 100%)',
          pointerEvents: 'none',
        }}
      />
    </div>
  )
}
