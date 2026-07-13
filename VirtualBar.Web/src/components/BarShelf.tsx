import { useId, useRef, useState } from 'react'
import type { CSSProperties } from 'react'
import { createPortal } from 'react-dom'
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

type BottleShapeKey = 'shoulder' | 'decanter' | 'tall' | 'round' | 'crystal'

interface BottleShapeDef {
  viewW: number
  viewH: number
  path: string
  bodyTop: number
  bodyBottom: number
  cap: { x: number; y: number; w: number; h: number }
  label?: { x: number; y: number; w: number; h: number }
  baseW: number
}

const SHAPES: Record<BottleShapeKey, BottleShapeDef> = {
  // Square shoulders — classic single malt (Whisky, Tequila)
  shoulder: {
    viewW: 56,
    viewH: 150,
    path: 'M 23 6 L 33 6 L 33 28 L 44 38 L 44 136 C 44 145 39 148 28 148 C 17 148 12 145 12 136 L 12 38 L 23 28 Z',
    bodyTop: 40,
    bodyBottom: 148,
    cap: { x: 21, y: 0, w: 14, h: 8 },
    label: { x: 17, y: 76, w: 22, h: 40 },
    baseW: 54,
  },
  // Wide-bellied carafe (Cognac, Brandy)
  decanter: {
    viewW: 64,
    viewH: 132,
    path: 'M 28 6 L 36 6 L 36 30 C 51 38 58 51 58 66 L 58 112 C 58 123 48 128 32 128 C 16 128 6 123 6 112 L 6 66 C 6 51 13 38 28 30 Z',
    bodyTop: 58,
    bodyBottom: 128,
    cap: { x: 26, y: 0, w: 12, h: 8 },
    label: { x: 20, y: 78, w: 24, h: 30 },
    baseW: 62,
  },
  // Tall and slim (Gin, Vodka)
  tall: {
    viewW: 44,
    viewH: 170,
    path: 'M 17 6 L 27 6 L 27 24 L 33 36 L 33 156 C 33 163 29 166 22 166 C 15 166 11 163 11 156 L 11 36 L 17 24 Z',
    bodyTop: 38,
    bodyBottom: 166,
    cap: { x: 15, y: 0, w: 14, h: 8 },
    label: { x: 14, y: 72, w: 16, h: 50 },
    baseW: 42,
  },
  // Rounded shoulders (Rum, Other)
  round: {
    viewW: 50,
    viewH: 155,
    path: 'M 20 7 L 30 7 C 33 8 35 18 34 24 C 42 32 44 42 44 52 L 44 128 C 44 142 37 150 25 150 C 13 150 6 142 6 128 L 6 52 C 6 42 8 32 16 24 C 15 18 17 8 20 7 Z',
    bodyTop: 30,
    bodyBottom: 150,
    cap: { x: 16, y: 0, w: 18, h: 8 },
    label: { x: 13, y: 72, w: 24, h: 38 },
    baseW: 50,
  },
  // Faceted crystal decanter — limited editions, no paper label
  crystal: {
    viewW: 52,
    viewH: 178,
    path: 'M 21 4 L 31 4 L 34 20 L 30 32 C 40 40 44 52 44 66 L 44 158 C 44 169 36 174 26 174 C 16 174 8 169 8 158 L 8 66 C 8 52 12 40 22 32 L 18 20 Z',
    bodyTop: 68,
    bodyBottom: 174,
    cap: { x: 19, y: 0, w: 14, h: 10 },
    baseW: 48,
  },
}

const CATEGORY_SHAPE: Record<SpiritCategory, BottleShapeKey> = {
  Whisky: 'shoulder',
  Tequila: 'shoulder',
  Cognac: 'decanter',
  Brandy: 'decanter',
  Gin: 'tall',
  Vodka: 'tall',
  Rum: 'round',
  Other: 'round',
}

function bottleShape(category: SpiritCategory, isLimited?: boolean): BottleShapeDef {
  return isLimited ? SHAPES.crystal : SHAPES[CATEGORY_SHAPE[category]]
}

function volumeScale(volumeMl?: number | null): number {
  if (!volumeMl) return 1
  return Math.min(1.12, Math.max(0.82, 0.55 + 0.45 * (volumeMl / 700)))
}

export function BottleSvg({
  category,
  condition,
  isLimited,
  plain,
}: {
  category: SpiritCategory
  condition: BottleCondition
  isLimited?: boolean
  plain?: boolean
}) {
  const uid = useId().replace(/:/g, '')
  const col = CATEGORY_COLORS[category]
  const shape = bottleShape(category, isLimited)
  const level = LIQUID_LEVEL[condition]

  const liquidHeight = (shape.bodyBottom - shape.bodyTop) * level
  const liquidY = shape.bodyBottom - liquidHeight

  const bodyId = `grad-body-${uid}`
  const glassId = `grad-glass-${uid}`
  const highlightId = `grad-highlight-${uid}`
  const clipId = `clip-${uid}`

  return (
    <svg
      viewBox={`0 0 ${shape.viewW} ${shape.viewH}`}
      style={{
        display: 'block',
        width: '100%',
        height: 'auto',
        filter: plain ? undefined : `drop-shadow(0 4px 12px ${col.glow}50)`,
      }}
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
          <path d={shape.path} />
        </clipPath>
      </defs>

      <path
        d={shape.path}
        fill={`url(#${bodyId})`}
        stroke={shape === SHAPES.crystal ? 'rgba(201,168,76,0.4)' : 'rgba(0,0,0,0.4)'}
        strokeWidth="0.6"
      />

      {level > 0 && (
        <rect
          x="0"
          y={liquidY}
          width={shape.viewW}
          height={liquidHeight + 2}
          fill={col.glass}
          opacity="0.9"
          clipPath={`url(#${clipId})`}
        />
      )}

      <path d={shape.path} fill={`url(#${glassId})`} />

      <rect
        x={Math.round(shape.viewW * 0.2)}
        y={shape.bodyTop - 8}
        width="3"
        height={shape.bodyBottom - shape.bodyTop}
        rx="1.5"
        fill={`url(#${highlightId})`}
        clipPath={`url(#${clipId})`}
      />

      {shape === SHAPES.crystal && (
        <rect
          x={Math.round(shape.viewW * 0.48)}
          y={shape.bodyTop - 14}
          width="3"
          height={shape.bodyBottom - shape.bodyTop}
          rx="1.5"
          fill="#FFFFFF"
          opacity="0.12"
          clipPath={`url(#${clipId})`}
        />
      )}

      {shape.label && (
        <rect
          x={shape.label.x}
          y={shape.label.y}
          width={shape.label.w}
          height={shape.label.h}
          rx="2"
          fill="#EAE0C8"
          opacity="0.92"
        />
      )}

      <rect
        x={shape.cap.x}
        y={shape.cap.y}
        width={shape.cap.w}
        height={shape.cap.h}
        rx="2"
        fill={CAP_COLOR[condition]}
        stroke="rgba(0,0,0,0.4)"
        strokeWidth="0.5"
      />
    </svg>
  )
}

const plaqueStyle: CSSProperties = {
  position: 'relative',
  zIndex: 2,
  marginTop: 6,
  maxWidth: 90,
  padding: '2px 8px',
  fontSize: 11,
  letterSpacing: '0.04em',
  whiteSpace: 'nowrap',
  overflow: 'hidden',
  textOverflow: 'ellipsis',
  color: '#F0DCA8',
  background: 'linear-gradient(180deg, #8A6A28, #5C431A)',
  border: '0.5px solid rgba(232,200,112,0.5)',
  borderRadius: 3,
}

const hoverCardStyle: CSSProperties = {
  position: 'fixed',
  transform: 'translateX(-50%)',
  width: 150,
  padding: 8,
  background: 'rgba(16,8,4,0.97)',
  border: '1px solid rgba(201,168,76,0.45)',
  borderRadius: 6,
  zIndex: 900,
  pointerEvents: 'none',
}

const reflectionStyle: CSSProperties = {
  position: 'absolute',
  top: '100%',
  left: 0,
  right: 0,
  marginTop: 1,
  transform: 'scaleY(-1)',
  opacity: 0.35,
  maskImage: 'linear-gradient(to top, rgba(0,0,0,0.5) 0%, transparent 40%)',
  WebkitMaskImage: 'linear-gradient(to top, rgba(0,0,0,0.5) 0%, transparent 40%)',
  pointerEvents: 'none',
}

const hoverFactsStyle: CSSProperties = {
  marginTop: 2,
  fontSize: 11,
  color: 'rgba(232,200,112,0.6)',
}

const hoverPhotoStyle: CSSProperties = {
  display: 'block',
  width: '100%',
  height: 72,
  objectFit: 'cover',
  borderRadius: 4,
  background: '#1C0E06',
}

export interface ShelfDnd {
  dragIndex: number | null
  overIndex: number | null
  start: (index: number) => void
  over: (index: number) => void
  end: () => void
  drop: (index: number) => void
  dropAtEnd: () => void
}

export function BottleCard({
  bottle,
  index,
  onSelect,
  dnd,
}: {
  bottle: Bottle
  index: number
  onSelect: (b: Bottle) => void
  dnd?: ShelfDnd
}) {
  const { t } = useTranslation()
  const rootRef = useRef<HTMLDivElement>(null)
  const [hoverPos, setHoverPos] = useState<{ x: number; y: number } | null>(null)
  const hover = hoverPos !== null
  const col = CATEGORY_COLORS[bottle.category]
  const primaryImage = bottle.images.find(i => i.isPrimary) ?? bottle.images[0]

  const isDragging = dnd?.dragIndex === index
  const isDropTarget = dnd != null && dnd.dragIndex !== null && dnd.dragIndex !== index && dnd.overIndex === index
  const lifted = hover || isDropTarget

  const handleEnter = () => {
    const rect = rootRef.current?.getBoundingClientRect()
    if (!rect) return
    // Clamp the card centre so a 150px-wide card never leaves the viewport.
    const halfCard = 83
    const x = Math.min(Math.max(rect.left + rect.width / 2, halfCard), window.innerWidth - halfCard)
    setHoverPos({ x, y: rect.top + 28 })
  }

  const shape = bottleShape(bottle.category, bottle.isLimited)
  const width = Math.round(shape.baseW * volumeScale(bottle.volumeMl))

  const facts = [
    col.label,
    bottle.volumeMl ? `${bottle.volumeMl} ml` : null,
    t(`addBottle.condition${bottle.condition}`),
  ]
    .filter(Boolean)
    .join(' · ')

  return (
    <div
      ref={rootRef}
      onClick={() => onSelect(bottle)}
      onMouseEnter={handleEnter}
      onMouseLeave={() => setHoverPos(null)}
      draggable={dnd != null}
      onDragStart={dnd ? e => {
        e.dataTransfer.effectAllowed = 'move'
        e.dataTransfer.setData('text/plain', bottle.id)
        setHoverPos(null)
        dnd.start(index)
      } : undefined}
      onDragEnd={dnd ? () => dnd.end() : undefined}
      onDragOver={dnd ? e => {
        e.preventDefault()
        e.dataTransfer.dropEffect = 'move'
        dnd.over(index)
      } : undefined}
      onDrop={dnd ? e => {
        e.preventDefault()
        e.stopPropagation()
        dnd.drop(index)
      } : undefined}
      style={{
        position: 'relative',
        width: 94,
        flexShrink: 0,
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        cursor: dnd ? 'grab' : 'pointer',
        opacity: isDragging ? 0.35 : 1,
        animation: 'bottleIn 0.4s ease-out both',
        animationDelay: `${index * 0.06}s`,
        transition: 'transform 0.25s cubic-bezier(0.34, 1.56, 0.64, 1), opacity 0.2s ease',
        transform: lifted ? 'translateY(-10px)' : 'translateY(0)',
        zIndex: hover ? 30 : 1,
      }}
    >
      {bottle.isLimited && (
        <span style={{ position: 'absolute', top: -2, right: 10, fontSize: 10, color: '#E8C870', textShadow: '0 0 6px rgba(201,168,76,0.8)', zIndex: 5 }}>◆</span>
      )}
      {bottle.isForSale && (
        <span style={{ position: 'absolute', top: 2, left: 14, width: 7, height: 7, borderRadius: '50%', background: '#4A9A6A', boxShadow: '0 0 6px rgba(74,154,106,0.9)', zIndex: 5 }} />
      )}

      <div style={{
        position: 'absolute',
        bottom: 26,
        left: '50%',
        transform: 'translateX(-50%)',
        width: 84,
        height: 130,
        background: `radial-gradient(ellipse at 50% 100%, ${col.glow}${lifted ? '45' : '22'} 0%, transparent 70%)`,
        transition: 'all 0.3s ease',
        pointerEvents: 'none',
        zIndex: 0,
      }} />

      <div style={{
        position: 'relative',
        width: '100%',
        height: 192,
        display: 'flex',
        alignItems: 'flex-end',
        justifyContent: 'center',
        filter: `drop-shadow(0 8px 18px ${col.glow}${lifted ? '55' : '22'})`,
        transition: 'filter 0.3s ease',
        zIndex: 1,
      }}>
        {hoverPos && createPortal(
          <div style={{ ...hoverCardStyle, left: hoverPos.x, top: hoverPos.y }}>
            {primaryImage && <img src={primaryImage.url} alt={bottle.name} style={hoverPhotoStyle} />}
            <div style={{ marginTop: primaryImage ? 6 : 0, fontSize: 12, color: '#E8C870', lineHeight: 1.3, overflow: 'hidden', display: '-webkit-box', WebkitLineClamp: 2, WebkitBoxOrient: 'vertical' }}>
              {bottle.name}
            </div>
            <div style={hoverFactsStyle}>{facts}</div>
          </div>,
          document.body,
        )}

        <div style={{ position: 'relative', width }}>
          <BottleSvg category={bottle.category} condition={bottle.condition} isLimited={bottle.isLimited} />
          <div aria-hidden="true" style={reflectionStyle}>
            <BottleSvg category={bottle.category} condition={bottle.condition} isLimited={bottle.isLimited} plain />
          </div>
        </div>
      </div>

      <div style={plaqueStyle}>{bottle.name}</div>
    </div>
  )
}

export function Shelf({
  bottles,
  startIndex,
  onSelect,
  dnd,
}: {
  bottles: Bottle[]
  startIndex: number
  onSelect: (b: Bottle) => void
  dnd?: ShelfDnd
}) {
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
        onDragOver={dnd ? e => e.preventDefault() : undefined}
        onDrop={dnd ? e => {
          e.preventDefault()
          dnd.dropAtEnd()
        } : undefined}
        style={{
          position: 'relative',
          display: 'flex',
          alignItems: 'flex-end',
          justifyContent: 'safe center',
          gap: 22,
          padding: '0 40px',
          overflowX: 'auto',
          scrollbarWidth: 'none',
          msOverflowStyle: 'none',
        }}
      >
        {bottles.map((bottle, i) => (
          <BottleCard key={bottle.id} bottle={bottle} index={startIndex + i} onSelect={onSelect} dnd={dnd} />
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

export function VirtualBarScene({
  bottles,
  onSelect,
  onReorder,
}: {
  bottles: Bottle[]
  onSelect: (b: Bottle) => void
  onReorder?: (orderedIds: string[]) => void
}) {
  const { t } = useTranslation()
  const [dragIndex, setDragIndex] = useState<number | null>(null)
  const [overIndex, setOverIndex] = useState<number | null>(null)
  const shelf1Bottles = bottles.slice(0, 8)
  const shelf2Bottles = bottles.slice(8, 16)

  const resetDrag = () => {
    setDragIndex(null)
    setOverIndex(null)
  }

  // "Take the target's position": remove the dragged id, then insert so it ends up
  // exactly at the target index ('end' appends to the back of the whole collection).
  const moveTo = (target: number | 'end') => {
    if (!onReorder || dragIndex === null) {
      resetDrag()
      return
    }

    const ids = bottles.map(b => b.id)
    const [moved] = ids.splice(dragIndex, 1)
    if (target === 'end') ids.push(moved)
    else ids.splice(target, 0, moved)

    const changed = target === 'end' ? dragIndex !== bottles.length - 1 : target !== dragIndex
    resetDrag()
    if (changed) onReorder(ids)
  }

  const dnd: ShelfDnd | undefined = onReorder
    ? {
        dragIndex,
        overIndex,
        start: setDragIndex,
        over: setOverIndex,
        end: resetDrag,
        drop: moveTo,
        dropAtEnd: () => moveTo('end'),
      }
    : undefined

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
          fontSize: 11,
          letterSpacing: '0.4em',
          color: 'rgba(201,168,76,0.25)',
          textTransform: 'uppercase',
        }}
      >
        — {t('barShelf.virtualBar')} —
      </div>

      <div style={{ position: 'absolute', bottom: 265, left: 0, right: 0 }}>
        <Shelf bottles={shelf1Bottles} startIndex={0} onSelect={onSelect} dnd={dnd} />
      </div>

      <div style={{ position: 'absolute', bottom: 60, left: 0, right: 0 }}>
        <Shelf bottles={shelf2Bottles} startIndex={8} onSelect={onSelect} dnd={dnd} />
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
