import { useState } from 'react'
import type { CSSProperties } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { useAuth } from '../contexts/AuthContext'
import { getBottlesByUser, addBottle, uploadBottleImage, linkBottleImage, lookupBarcode } from '../api/bottlesApi'
import type { AddBottlePayload } from '../api/bottlesApi'
import type { Bottle, SpiritCategory, BottleCondition } from '../types'

/* ------------------------------------------------------------------ */
/*  Category palette                                                   */
/* ------------------------------------------------------------------ */

const CATEGORY_COLORS: Record<SpiritCategory, { body: string; glass: string; glow: string; label: string }> = {
  Whisky:  { body: '#5C3A00', glass: '#C8820A', glow: '#C8820A', label: 'Whisky' },
  Rum:     { body: '#5A1008', glass: '#D42020', glow: '#E83020', label: 'Rum' },
  Cognac:  { body: '#4A2008', glass: '#C86030', glow: '#D87040', label: 'Cognac' },
  Vodka:   { body: '#1A2A3A', glass: '#90B8D8', glow: '#A0C8E8', label: 'Vodka' },
  Gin:     { body: '#102818', glass: '#4A9A6A', glow: '#5AAA7A', label: 'Gin' },
  Tequila: { body: '#5A4A00', glass: '#D4A820', glow: '#E4B830', label: 'Tequila' },
  Brandy:  { body: '#4A2008', glass: '#C84818', glow: '#D85828', label: 'Brandy' },
  Other:   { body: '#201040', glass: '#8060C8', glow: '#9070D8', label: 'Other' },
}

const CATEGORIES = Object.keys(CATEGORY_COLORS) as SpiritCategory[]
const CONDITIONS: BottleCondition[] = ['Sealed', 'Opened', 'Empty']

const LIQUID_LEVEL: Record<BottleCondition, number> = {
  Sealed: 0.88,
  Opened: 0.45,
  Empty: 0,
}

const CAP_COLOR: Record<BottleCondition, string> = {
  Sealed: '#C9A84C',
  Opened: '#5A3A1A',
  Empty: '#1A0C06',
}

/* ------------------------------------------------------------------ */
/*  BottleSvg                                                          */
/* ------------------------------------------------------------------ */

const BOTTLE_PATH =
  'M 20 7 L 30 7 C 33 8 35 18 34 24 C 42 32 44 42 44 52 L 44 128 C 44 142 37 150 25 150 C 13 150 6 142 6 128 L 6 52 C 6 42 8 32 16 24 C 15 18 17 8 20 7 Z'

function BottleSvg({ category, condition }: { category: SpiritCategory; condition: BottleCondition }) {
  const col = CATEGORY_COLORS[category]
  const level = LIQUID_LEVEL[condition]

  // Liquid fills inside the body from bottom (y=150) up. Body spans ~y=24..150.
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

      {/* glass body fill */}
      <path d={BOTTLE_PATH} fill={`url(#${bodyId})`} stroke="rgba(0,0,0,0.4)" strokeWidth="0.6" />

      {/* liquid */}
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

      {/* glass sheen overlay */}
      <path d={BOTTLE_PATH} fill={`url(#${glassId})`} />

      {/* left-edge shimmer highlight */}
      <rect x="11" y="30" width="3" height="110" rx="1.5" fill={`url(#${highlightId})`} clipPath={`url(#${clipId})`} />

      {/* cap */}
      <rect x="16" y="0" width="18" height="8" rx="2" fill={CAP_COLOR[condition]} stroke="rgba(0,0,0,0.4)" strokeWidth="0.5" />
    </svg>
  )
}

/* ------------------------------------------------------------------ */
/*  BottleCard                                                         */
/* ------------------------------------------------------------------ */

function BottleCard({ bottle, index, onSelect }: { bottle: Bottle; index: number; onSelect: (b: Bottle) => void }) {
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

/* ------------------------------------------------------------------ */
/*  EmptySlot                                                          */
/* ------------------------------------------------------------------ */

function EmptySlot({ onClick }: { onClick: () => void }) {
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
          Add bottle
        </span>
      )}
    </div>
  )
}

/* ------------------------------------------------------------------ */
/*  Shelf                                                              */
/* ------------------------------------------------------------------ */

function Shelf({
  bottles,
  startIndex,
  onAdd,
  onSelect,
}: {
  bottles: Bottle[]
  startIndex: number
  onAdd: () => void
  onSelect: (b: Bottle) => void
}) {
  const emptyCount = Math.min(3, Math.max(1, 8 - bottles.length))

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
        {Array.from({ length: emptyCount }).map((_, i) => (
          <EmptySlot key={`empty-${i}`} onClick={onAdd} />
        ))}
      </div>

      {/* shelf plank */}
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

/* ------------------------------------------------------------------ */
/*  VirtualBarScene                                                    */
/* ------------------------------------------------------------------ */

function VirtualBarScene({ bottles, onAdd, onSelect }: { bottles: Bottle[]; onAdd: () => void; onSelect: (b: Bottle) => void }) {
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
      {/* back wall - vertical wood panels */}
      <div
        style={{
          position: 'absolute',
          inset: 0,
          backgroundImage:
            'repeating-linear-gradient(90deg, transparent, transparent 119px, rgba(25,10,4,0.5) 120px), repeating-linear-gradient(90deg, transparent, transparent 59px, rgba(15,6,2,0.2) 60px)',
          pointerEvents: 'none',
        }}
      />

      {/* crown molding */}
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

      {/* engraved title */}
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
        — Virtual Bar —
      </div>

      <div style={{ position: 'absolute', bottom: 265, left: 0, right: 0 }}>
        <Shelf bottles={shelf1Bottles} startIndex={0} onAdd={onAdd} onSelect={onSelect} />
      </div>

      <div style={{ position: 'absolute', bottom: 60, left: 0, right: 0 }}>
        <Shelf bottles={shelf2Bottles} startIndex={8} onAdd={onAdd} onSelect={onSelect} />
      </div>

      {/* bar counter */}
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

      {/* vignette */}
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

/* ------------------------------------------------------------------ */
/*  CategoryPill                                                       */
/* ------------------------------------------------------------------ */

function CategoryPill({
  label,
  active,
  color,
  count,
  onClick,
}: {
  label: string
  active: boolean
  color: string
  count: number
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
      }}
    >
      {label}
      <span style={{ marginLeft: 6, opacity: 0.7 }}>{count}</span>
    </button>
  )
}

/* ------------------------------------------------------------------ */
/*  StatItem                                                           */
/* ------------------------------------------------------------------ */

function StatItem({ value, label }: { value: number; label: string }) {
  return (
    <div>
      <div style={{ fontFamily: 'Playfair Display, serif', fontSize: 24, fontWeight: 700, color: '#C9A84C' }}>
        {value}
      </div>
      <div
        style={{
          fontFamily: 'Cormorant Garamond, serif',
          fontSize: 14,
          color: '#B09868',
          letterSpacing: '0.1em',
          textTransform: 'uppercase',
        }}
      >
        {label}
      </div>
    </div>
  )
}

/* ------------------------------------------------------------------ */
/*  AddBottlePanel                                                     */
/* ------------------------------------------------------------------ */

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

const labelStyle: CSSProperties = {
  fontFamily: 'Cinzel, serif',
  fontSize: 11,
  letterSpacing: '0.2em',
  color: '#B09868',
  textTransform: 'uppercase',
  marginBottom: 6,
  display: 'block',
}

function focusOn(e: React.FocusEvent<HTMLInputElement | HTMLTextAreaElement>) {
  e.currentTarget.style.border = '1px solid rgba(201,168,76,0.5)'
}

function focusOff(e: React.FocusEvent<HTMLInputElement | HTMLTextAreaElement>) {
  e.currentTarget.style.border = '1px solid rgba(201,168,76,0.2)'
}

function AddBottlePanel({ onClose, onSuccess }: { onClose: () => void; onSuccess: () => void }) {
  const [category, setCategory] = useState<SpiritCategory>('Whisky')
  const [condition, setCondition] = useState<BottleCondition>('Sealed')
  const [name, setName] = useState('')
  const [distillery, setDistillery] = useState('')
  const [age, setAge] = useState('')
  const [abv, setAbv] = useState('')
  const [volume, setVolume] = useState('')
  const [isLimited, setIsLimited] = useState(false)
  const [description, setDescription] = useState('')
  const [imageFile, setImageFile] = useState<File | null>(null)
  const [imagePreview, setImagePreview] = useState<string | null>(null)
  const [barcodeImageUrl, setBarcodeImageUrl] = useState<string | null>(null)
  const [dropHover, setDropHover] = useState(false)
  const [barcode, setBarcode] = useState('')
  const [barcodeLoading, setBarcodeLoading] = useState(false)
  const [barcodeStatus, setBarcodeStatus] = useState<'idle' | 'found' | 'error'>('idle')

  const handleImageChange = (file: File | null) => {
    if (!file) return
    setImageFile(file)
    setBarcodeImageUrl(null)
    const url = URL.createObjectURL(file)
    setImagePreview(url)
  }

  const handleBarcodeSearch = async () => {
    const code = barcode.trim()
    if (!code) return
    setBarcodeLoading(true)
    setBarcodeStatus('idle')
    try {
      const product = await lookupBarcode(code)
      if (product.name) setName(product.name)
      if (product.brand) setDistillery(product.brand)
      if (product.volumeMl) setVolume(String(product.volumeMl))
      if (product.abvPercent) setAbv(String(product.abvPercent))
      if (product.imageUrl) {
        setImagePreview(product.imageUrl)
        setImageFile(null)
        setBarcodeImageUrl(product.imageUrl)
      }
      setBarcodeStatus('found')
    } catch {
      setBarcodeStatus('error')
    } finally {
      setBarcodeLoading(false)
    }
  }

  const mutation = useMutation({
    mutationFn: async (payload: AddBottlePayload) => {
      const bottle = await addBottle(payload)
      if (imageFile) {
        await uploadBottleImage(bottle.id, imageFile)
      } else if (barcodeImageUrl) {
        await linkBottleImage(bottle.id, barcodeImageUrl)
      }
      return bottle
    },
    onSuccess: () => onSuccess(),
  })

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault()
    if (!name.trim()) return
    const payload: AddBottlePayload = {
      name: name.trim(),
      distillery: distillery.trim() || undefined,
      category,
      condition,
      isLimited,
      age: age ? Number(age) : undefined,
      abvPercent: abv ? Number(abv) : undefined,
      volumeMl: volume ? Number(volume) : undefined,
      description: description.trim() || undefined,
    }
    mutation.mutate(payload)
  }

  return (
    <div style={{ position: 'fixed', inset: 0, zIndex: 50 }}>
      {/* backdrop */}
      <div onClick={onClose} style={{ position: 'absolute', inset: 0, background: 'rgba(4,2,1,0.85)' }} />

      {/* panel */}
      <div
        style={{
          position: 'absolute',
          right: 0,
          top: 0,
          width: 480,
          maxWidth: '100%',
          height: '100%',
          background: 'linear-gradient(180deg, #0F0604, #130805)',
          borderLeft: '1px solid rgba(201,168,76,0.2)',
          overflowY: 'auto',
          padding: 32,
          animation: 'fadeInUp 0.3s ease-out',
        }}
      >
        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 24 }}>
          <div
            style={{
              fontFamily: 'Cinzel, serif',
              fontSize: 14,
              letterSpacing: '0.3em',
              color: '#C9A84C',
            }}
          >
            ADD TO YOUR BAR
          </div>
          <button
            onClick={onClose}
            style={{
              background: 'transparent',
              border: 'none',
              color: '#B09868',
              fontSize: 24,
              cursor: 'pointer',
              lineHeight: 1,
            }}
          >
            ×
          </button>
        </div>

        {/* image upload + live preview */}
        <div style={{ marginBottom: 24 }}>
          <div
            onDragOver={(e) => { e.preventDefault(); setDropHover(true) }}
            onDragLeave={() => setDropHover(false)}
            onDrop={(e) => {
              e.preventDefault()
              setDropHover(false)
              const file = e.dataTransfer.files[0]
              if (file && file.type.startsWith('image/')) handleImageChange(file)
            }}
            onClick={() => document.getElementById('bottle-image-input')?.click()}
            style={{
              position: 'relative',
              height: 180,
              borderRadius: 6,
              border: dropHover
                ? '2px solid rgba(201,168,76,0.7)'
                : '2px dashed rgba(201,168,76,0.25)',
              background: dropHover ? 'rgba(201,168,76,0.06)' : 'rgba(201,168,76,0.02)',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              cursor: 'pointer',
              overflow: 'hidden',
              transition: 'all 0.2s ease',
            }}
          >
            {imagePreview ? (
              <>
                <img
                  src={imagePreview}
                  alt="preview"
                  style={{ width: '100%', height: '100%', objectFit: 'cover', display: 'block' }}
                />
                <div style={{
                  position: 'absolute', inset: 0, background: 'rgba(0,0,0,0.45)',
                  display: 'flex', alignItems: 'center', justifyContent: 'center',
                  opacity: 0,
                  transition: 'opacity 0.2s',
                }}
                  onMouseEnter={e => (e.currentTarget as HTMLElement).style.opacity = '1'}
                  onMouseLeave={e => (e.currentTarget as HTMLElement).style.opacity = '0'}
                >
                  <span style={{ fontFamily: 'Cinzel, serif', fontSize: 11, letterSpacing: '0.2em', color: '#E8C870' }}>
                    CHANGE PHOTO
                  </span>
                </div>
              </>
            ) : (
              <div style={{ textAlign: 'center', pointerEvents: 'none' }}>
                <div style={{ fontSize: 32, marginBottom: 8, color: 'rgba(201,168,76,0.4)' }}>📷</div>
                <div style={{ fontFamily: 'Cinzel, serif', fontSize: 10, letterSpacing: '0.2em', color: 'rgba(201,168,76,0.5)', marginBottom: 4 }}>
                  UPLOAD PHOTO
                </div>
                <div style={{ fontFamily: 'Cormorant Garamond, serif', fontSize: 13, color: 'rgba(201,168,76,0.3)' }}>
                  Click or drag &amp; drop
                </div>
              </div>
            )}
          </div>

          {/* Hidden file input */}
          <input
            id="bottle-image-input"
            type="file"
            accept="image/jpeg,image/png,image/webp,image/gif"
            style={{ display: 'none' }}
            onChange={(e) => handleImageChange(e.target.files?.[0] ?? null)}
          />

          {/* Bottle SVG preview below drop zone */}
          <div style={{ display: 'flex', justifyContent: 'center', marginTop: 16 }}>
            <div style={{ width: 50 }}>
              <BottleSvg category={category} condition={condition} />
            </div>
          </div>
        </div>

        <form onSubmit={handleSubmit}>
          {/* barcode lookup */}
          <label style={labelStyle}>Barcode Lookup</label>
          <div style={{ display: 'flex', gap: 8, marginBottom: 6 }}>
            <input
              value={barcode}
              onChange={e => setBarcode(e.target.value)}
              onKeyDown={e => { if (e.key === 'Enter') { e.preventDefault(); handleBarcodeSearch() } }}
              placeholder="EAN / UPC barcode…"
              style={{ ...inputStyle, flex: 1 }}
            />
            <button
              type="button"
              onClick={handleBarcodeSearch}
              disabled={!barcode.trim() || barcodeLoading}
              style={{
                padding: '10px 16px',
                background: 'rgba(201,168,76,0.12)',
                border: '1px solid rgba(201,168,76,0.35)',
                color: '#C9A84C',
                borderRadius: 4,
                cursor: barcode.trim() && !barcodeLoading ? 'pointer' : 'not-allowed',
                fontFamily: 'Cinzel, serif',
                fontSize: 11,
                letterSpacing: '0.15em',
                whiteSpace: 'nowrap',
                opacity: barcode.trim() && !barcodeLoading ? 1 : 0.5,
                transition: 'all 0.2s',
              }}
            >
              {barcodeLoading ? '···' : 'FIND'}
            </button>
          </div>
          {barcodeStatus === 'found' && (
            <div style={{ fontFamily: 'Cormorant Garamond, serif', fontSize: 14, color: '#4A9A6A', marginBottom: 14 }}>
              ✓ Product found — fields auto-filled
            </div>
          )}
          {barcodeStatus === 'error' && (
            <div style={{ fontFamily: 'Cormorant Garamond, serif', fontSize: 14, color: '#C04040', marginBottom: 14 }}>
              Product not found. Try a different barcode or fill in manually.
            </div>
          )}
          {barcodeStatus === 'idle' && <div style={{ marginBottom: 18 }} />}

          {/* category selector */}
          <label style={labelStyle}>Category</label>
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: 8, marginBottom: 18 }}>
            {CATEGORIES.map((cat) => {
              const c = CATEGORY_COLORS[cat]
              const selected = category === cat
              return (
                <button
                  key={cat}
                  type="button"
                  onClick={() => setCategory(cat)}
                  style={{
                    display: 'flex',
                    flexDirection: 'column',
                    alignItems: 'center',
                    gap: 4,
                    padding: '8px 4px',
                    borderRadius: 4,
                    cursor: 'pointer',
                    background: selected ? 'rgba(201,168,76,0.1)' : '#0A0502',
                    border: selected ? '1px solid #C9A84C' : '1px solid rgba(201,168,76,0.15)',
                    color: selected ? '#E8C870' : '#C9A84C',
                    fontFamily: 'Cormorant Garamond, serif',
                    fontSize: 16,
                    transition: 'all 0.15s ease',
                  }}
                >
                  <span style={{ width: 10, height: 10, borderRadius: '50%', background: c.glass }} />
                  {c.label}
                </button>
              )
            })}
          </div>

          {/* name */}
          <label style={labelStyle}>Name *</label>
          <input
            value={name}
            onChange={(e) => setName(e.target.value)}
            onFocus={focusOn}
            onBlur={focusOff}
            required
            placeholder="e.g. Glenfiddich 18"
            style={{ ...inputStyle, marginBottom: 18 }}
          />

          {/* distillery */}
          <label style={labelStyle}>Distillery</label>
          <input
            value={distillery}
            onChange={(e) => setDistillery(e.target.value)}
            onFocus={focusOn}
            onBlur={focusOff}
            placeholder="e.g. Glenfiddich"
            style={{ ...inputStyle, marginBottom: 18 }}
          />

          {/* age / abv / volume */}
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: 12, marginBottom: 18 }}>
            <div>
              <label style={labelStyle}>Age</label>
              <input
                type="number"
                min={0}
                max={100}
                value={age}
                onChange={(e) => setAge(e.target.value)}
                onFocus={focusOn}
                onBlur={focusOff}
                style={inputStyle}
              />
            </div>
            <div>
              <label style={labelStyle}>ABV %</label>
              <input
                type="number"
                min={0}
                max={100}
                step={0.1}
                value={abv}
                onChange={(e) => setAbv(e.target.value)}
                onFocus={focusOn}
                onBlur={focusOff}
                style={inputStyle}
              />
            </div>
            <div>
              <label style={labelStyle}>Volume ml</label>
              <input
                type="number"
                min={0}
                value={volume}
                onChange={(e) => setVolume(e.target.value)}
                onFocus={focusOn}
                onBlur={focusOff}
                style={inputStyle}
              />
            </div>
          </div>

          {/* condition */}
          <label style={labelStyle}>Condition</label>
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: 8, marginBottom: 18 }}>
            {CONDITIONS.map((cond) => {
              const selected = condition === cond
              return (
                <button
                  key={cond}
                  type="button"
                  onClick={() => setCondition(cond)}
                  style={{
                    padding: '10px 4px',
                    borderRadius: 4,
                    cursor: 'pointer',
                    background: selected ? 'rgba(201,168,76,0.1)' : '#0A0502',
                    border: selected ? '1px solid #C9A84C' : '1px solid rgba(201,168,76,0.15)',
                    color: selected ? '#E8C870' : '#C9A84C',
                    fontFamily: 'Cormorant Garamond, serif',
                    fontSize: 15,
                    transition: 'all 0.15s ease',
                  }}
                >
                  {cond}
                </button>
              )
            })}
          </div>

          {/* limited toggle */}
          <div
            onClick={() => setIsLimited((v) => !v)}
            style={{
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'space-between',
              marginBottom: 18,
              cursor: 'pointer',
              padding: '10px 14px',
              background: '#0A0502',
              border: '1px solid rgba(201,168,76,0.15)',
              borderRadius: 4,
            }}
          >
            <span style={{ fontFamily: 'Cormorant Garamond, serif', fontSize: 15, color: '#C9A84C' }}>
              Limited Edition ◆
            </span>
            <span
              style={{
                width: 40,
                height: 22,
                borderRadius: 12,
                background: isLimited ? '#C9A84C' : 'rgba(201,168,76,0.15)',
                position: 'relative',
                transition: 'background 0.2s ease',
                flexShrink: 0,
              }}
            >
              <span
                style={{
                  position: 'absolute',
                  top: 2,
                  left: isLimited ? 20 : 2,
                  width: 18,
                  height: 18,
                  borderRadius: '50%',
                  background: isLimited ? '#07030A' : '#B09868',
                  transition: 'left 0.2s ease',
                }}
              />
            </span>
          </div>

          {/* description */}
          <label style={labelStyle}>Description</label>
          <textarea
            value={description}
            onChange={(e) => setDescription(e.target.value)}
            onFocus={focusOn}
            onBlur={focusOff}
            rows={3}
            placeholder="Tasting notes, provenance…"
            style={{ ...inputStyle, marginBottom: 24, resize: 'vertical' }}
          />

          {mutation.isError && (
            <div
              style={{
                fontFamily: 'Cormorant Garamond, serif',
                fontSize: 14,
                color: '#D42020',
                marginBottom: 16,
              }}
            >
              Something went wrong. Please try again.
            </div>
          )}

          <button
            type="submit"
            disabled={mutation.isPending || !name.trim()}
            style={{
              width: '100%',
              fontFamily: 'Cinzel, serif',
              fontSize: 14,
              letterSpacing: '0.2em',
              textTransform: 'uppercase',
              color: '#07030A',
              background: 'linear-gradient(135deg, #C9A84C, #E8C870)',
              border: 'none',
              padding: '14px',
              borderRadius: 2,
              cursor: mutation.isPending || !name.trim() ? 'not-allowed' : 'pointer',
              opacity: mutation.isPending || !name.trim() ? 0.6 : 1,
              boxShadow: '0 4px 20px rgba(201,168,76,0.3)',
            }}
          >
            {mutation.isPending ? 'Adding…' : 'Add to Collection'}
          </button>
        </form>
      </div>
    </div>
  )
}

/* ------------------------------------------------------------------ */
/*  DashboardPage                                                      */
/* ------------------------------------------------------------------ */

function DetailRow({ label, value }: { label: string; value: string | number }) {
  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
      <span style={{ fontFamily: 'Cinzel, serif', fontSize: 9, letterSpacing: '0.2em', color: '#7A6040', textTransform: 'uppercase' }}>
        {label}
      </span>
      <span style={{ fontFamily: 'Cormorant Garamond, serif', fontSize: 17, color: '#E8D4A0' }}>
        {value}
      </span>
    </div>
  )
}

function BottleDetailPanel({ bottle, onClose }: { bottle: Bottle; onClose: () => void }) {
  const col = CATEGORY_COLORS[bottle.category]
  const primaryImage = bottle.images.find(i => i.isPrimary) ?? bottle.images[0]
  const galleryImages = bottle.images.filter(i => !i.isPrimary).sort((a, b) => a.sortOrder - b.sortOrder)

  return (
    <div style={{ position: 'fixed', inset: 0, zIndex: 50 }}>
      <div onClick={onClose} style={{ position: 'absolute', inset: 0, background: 'rgba(4,2,1,0.85)' }} />

      <div
        style={{
          position: 'absolute',
          right: 0,
          top: 0,
          width: 520,
          maxWidth: '100%',
          height: '100%',
          background: 'linear-gradient(180deg, #0F0604, #130805)',
          borderLeft: '1px solid rgba(201,168,76,0.2)',
          overflowY: 'auto',
          animation: 'fadeInUp 0.28s ease-out',
        }}
      >
        {/* image header */}
        <div style={{ position: 'relative', width: '100%', height: 280, background: '#0A0402', overflow: 'hidden', flexShrink: 0 }}>
          {primaryImage ? (
            <img
              src={primaryImage.url}
              alt={bottle.name}
              style={{ width: '100%', height: '100%', objectFit: 'cover', objectPosition: 'center top' }}
            />
          ) : (
            <div style={{ width: '100%', height: '100%', display: 'flex', alignItems: 'center', justifyContent: 'center', background: `radial-gradient(ellipse at 50% 80%, ${col.glow}18 0%, transparent 65%)` }}>
              <div style={{ width: 90 }}>
                <BottleSvg category={bottle.category} condition={bottle.condition} />
              </div>
            </div>
          )}

          <div style={{ position: 'absolute', inset: 0, background: 'linear-gradient(to bottom, transparent 40%, rgba(15,6,4,0.95) 100%)' }} />

          <button
            onClick={onClose}
            style={{
              position: 'absolute',
              top: 16,
              right: 16,
              width: 34,
              height: 34,
              borderRadius: '50%',
              background: 'rgba(10,4,2,0.75)',
              border: '1px solid rgba(201,168,76,0.3)',
              color: '#C9A84C',
              fontSize: 20,
              lineHeight: 1,
              cursor: 'pointer',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
            }}
          >
            ×
          </button>

          <div style={{ position: 'absolute', bottom: 0, left: 0, right: 0, padding: '0 28px 20px' }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginBottom: 6, flexWrap: 'wrap' }}>
              <span style={{
                fontFamily: 'Cormorant Garamond, serif',
                fontSize: 12,
                padding: '3px 10px',
                borderRadius: 10,
                background: `${col.glass}22`,
                border: `1px solid ${col.glass}66`,
                color: col.glass,
                letterSpacing: '0.05em',
              }}>
                {col.label}
              </span>
              <span style={{
                fontFamily: 'Cormorant Garamond, serif',
                fontSize: 12,
                padding: '3px 10px',
                borderRadius: 10,
                background: 'rgba(201,168,76,0.08)',
                border: '1px solid rgba(201,168,76,0.25)',
                color: '#C9A84C',
              }}>
                {bottle.condition}
              </span>
              {bottle.isLimited && (
                <span style={{ fontFamily: 'Cormorant Garamond, serif', fontSize: 12, color: '#E8C870', letterSpacing: '0.05em' }}>◆ Limited</span>
              )}
              {bottle.isForSale && (
                <span style={{ fontFamily: 'Cormorant Garamond, serif', fontSize: 12, color: '#4A9A6A' }}>● For Sale</span>
              )}
            </div>

            <h2 style={{ fontFamily: 'Playfair Display, serif', fontSize: 26, fontWeight: 700, color: '#E8C870', margin: '0 0 4px', lineHeight: 1.15 }}>
              {bottle.name}
            </h2>
            {bottle.distillery && (
              <div style={{ fontFamily: 'Cormorant Garamond, serif', fontSize: 16, fontStyle: 'italic', color: '#C9A84C' }}>
                {bottle.distillery}
              </div>
            )}
          </div>
        </div>

        {/* body */}
        <div style={{ padding: '24px 28px 40px' }}>

          {/* numeric details grid */}
          {(bottle.age != null || bottle.abvPercent != null || bottle.volumeMl != null || bottle.vintageYear != null) && (
            <div style={{
              display: 'grid',
              gridTemplateColumns: 'repeat(auto-fit, minmax(90px, 1fr))',
              gap: '16px 24px',
              padding: '18px 20px',
              background: 'rgba(201,168,76,0.04)',
              border: '1px solid rgba(201,168,76,0.1)',
              borderRadius: 4,
              marginBottom: 20,
            }}>
              {bottle.age != null && <DetailRow label="Age" value={`${bottle.age} yr`} />}
              {bottle.abvPercent != null && <DetailRow label="ABV" value={`${bottle.abvPercent}%`} />}
              {bottle.volumeMl != null && <DetailRow label="Volume" value={`${bottle.volumeMl} ml`} />}
              {bottle.vintageYear != null && <DetailRow label="Vintage" value={bottle.vintageYear} />}
            </div>
          )}

          {/* origin */}
          {(bottle.region || bottle.country) && (
            <div style={{ marginBottom: 20 }}>
              <div style={{ fontFamily: 'Cinzel, serif', fontSize: 9, letterSpacing: '0.2em', color: '#7A6040', textTransform: 'uppercase', marginBottom: 6 }}>
                Origin
              </div>
              <div style={{ fontFamily: 'Cormorant Garamond, serif', fontSize: 16, color: '#E8D4A0' }}>
                {[bottle.region, bottle.country].filter(Boolean).join(', ')}
              </div>
            </div>
          )}

          {/* for sale */}
          {bottle.isForSale && bottle.askingPrice != null && (
            <div style={{
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'space-between',
              padding: '14px 20px',
              background: 'rgba(74,154,106,0.06)',
              border: '1px solid rgba(74,154,106,0.25)',
              borderRadius: 4,
              marginBottom: 20,
            }}>
              <span style={{ fontFamily: 'Cinzel, serif', fontSize: 10, letterSpacing: '0.2em', color: '#4A9A6A', textTransform: 'uppercase' }}>
                Asking Price
              </span>
              <span style={{ fontFamily: 'Playfair Display, serif', fontSize: 22, color: '#6ABF8A', fontWeight: 700 }}>
                {bottle.currency ?? 'USD'} {bottle.askingPrice.toLocaleString()}
              </span>
            </div>
          )}

          {/* description */}
          {bottle.description && (
            <div style={{ marginBottom: 20 }}>
              <div style={{ fontFamily: 'Cinzel, serif', fontSize: 9, letterSpacing: '0.2em', color: '#7A6040', textTransform: 'uppercase', marginBottom: 8 }}>
                Notes
              </div>
              <p style={{ fontFamily: 'Cormorant Garamond, serif', fontSize: 17, color: '#C9A84C', lineHeight: 1.65, margin: 0, fontStyle: 'italic' }}>
                {bottle.description}
              </p>
            </div>
          )}

          {/* gallery strip */}
          {galleryImages.length > 0 && (
            <div>
              <div style={{ fontFamily: 'Cinzel, serif', fontSize: 9, letterSpacing: '0.2em', color: '#7A6040', textTransform: 'uppercase', marginBottom: 8 }}>
                Gallery
              </div>
              <div style={{ display: 'flex', gap: 8, overflowX: 'auto', scrollbarWidth: 'none' }}>
                {galleryImages.map(img => (
                  <img
                    key={img.id}
                    src={img.url}
                    alt=""
                    style={{ width: 80, height: 80, objectFit: 'cover', borderRadius: 3, border: '1px solid rgba(201,168,76,0.15)', flexShrink: 0 }}
                  />
                ))}
              </div>
            </div>
          )}
        </div>
      </div>
    </div>
  )
}

export default function DashboardPage() {
  const { user, logout } = useAuth()
  const queryClient = useQueryClient()
  const [addOpen, setAddOpen] = useState(false)
  const [selectedBottle, setSelectedBottle] = useState<Bottle | null>(null)
  const [activeCategory, setActiveCategory] = useState<SpiritCategory | null>(null)

  const { data: bottles = [], isLoading } = useQuery({
    queryKey: ['bottles', user?.id],
    queryFn: () => getBottlesByUser(user!.id),
    enabled: !!user?.id,
  })

  const displayedBottles = activeCategory
    ? bottles.filter((b) => b.category === activeCategory)
    : bottles

  const categoryCounts = CATEGORIES.reduce((acc, cat) => {
    acc[cat] = bottles.filter((b) => b.category === cat).length
    return acc
  }, {} as Record<SpiritCategory, number>)

  return (
    <div style={{ minHeight: '100vh', background: '#07030A', color: '#F0DDB4' }}>
      {/* TOP NAVIGATION */}
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
        <div style={{ display: 'flex', alignItems: 'center', gap: 12 }}>
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
          <span style={{ fontFamily: 'Playfair Display, serif', fontSize: 18, color: '#E8C870', letterSpacing: '0.05em' }}>
            VirtualBar
          </span>
        </div>

        <div style={{ display: 'flex', alignItems: 'center', gap: 20 }}>
          <span style={{ fontFamily: 'Cormorant Garamond, serif', fontSize: 15, color: '#C9A84C' }}>
            {user?.displayName}
          </span>
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
            SIGN OUT
          </button>
        </div>
      </nav>

      {/* MAIN CONTENT */}
      <main style={{ maxWidth: 1200, margin: '0 auto', padding: '40px 40px' }}>
        {/* Page Header */}
        <div style={{ display: 'flex', alignItems: 'flex-end', justifyContent: 'space-between', marginBottom: 32 }}>
          <div>
            <div
              style={{
                fontFamily: 'Cinzel, serif',
                fontSize: 13,
                letterSpacing: '0.4em',
                color: '#B09868',
                marginBottom: 8,
              }}
            >
              YOUR COLLECTION
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
              Virtual Bar
            </h1>
            <div
              style={{
                fontFamily: 'Cormorant Garamond, serif',
                fontSize: 18,
                fontStyle: 'italic',
                color: '#C9A84C',
                marginTop: 6,
              }}
            >
              {bottles.length === 0
                ? 'Your collection awaits'
                : `${bottles.length} ${bottles.length === 1 ? 'bottle' : 'bottles'} in your collection`}
            </div>
          </div>

          <button
            onClick={() => setAddOpen(true)}
            style={{
              fontFamily: 'Cinzel, serif',
              fontSize: 12,
              letterSpacing: '0.2em',
              color: '#07030A',
              background: 'linear-gradient(135deg, #C9A84C, #E8C870)',
              border: 'none',
              padding: '14px 28px',
              borderRadius: 2,
              cursor: 'pointer',
              boxShadow: '0 4px 20px rgba(201,168,76,0.3)',
            }}
          >
            + ADD BOTTLE
          </button>
        </div>

        {/* Stats row */}
        {bottles.length > 0 && (
          <div
            style={{
              display: 'flex',
              gap: 24,
              marginBottom: 32,
              padding: '16px 24px',
              background: 'rgba(201,168,76,0.04)',
              border: '1px solid rgba(201,168,76,0.1)',
              borderRadius: 4,
            }}
          >
            <StatItem value={bottles.length} label="Total Bottles" />
            <div style={{ width: 1, background: 'rgba(201,168,76,0.15)' }} />
            <StatItem value={bottles.filter((b) => b.condition === 'Sealed').length} label="Sealed" />
            <div style={{ width: 1, background: 'rgba(201,168,76,0.15)' }} />
            <StatItem value={bottles.filter((b) => b.isForSale).length} label="For Sale" />
            <div style={{ width: 1, background: 'rgba(201,168,76,0.15)' }} />
            <StatItem value={bottles.filter((b) => b.isLimited).length} label="Limited" />
          </div>
        )}

        {/* Loading state */}
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
            POURING YOUR COLLECTION…
          </div>
        )}

        {!isLoading && bottles.length > 0 && (
          <VirtualBarScene bottles={displayedBottles} onAdd={() => setAddOpen(true)} onSelect={setSelectedBottle} />
        )}

        {/* Category filter */}
        {bottles.length > 0 && (
          <div style={{ display: 'flex', gap: 10, flexWrap: 'wrap', marginTop: 28 }}>
            <CategoryPill
              label="All"
              active={activeCategory === null}
              color="#C9A84C"
              count={bottles.length}
              onClick={() => setActiveCategory(null)}
            />
            {CATEGORIES.filter((c) => categoryCounts[c] > 0).map((cat) => (
              <CategoryPill
                key={cat}
                label={cat}
                active={activeCategory === cat}
                color={CATEGORY_COLORS[cat].glass}
                count={categoryCounts[cat]}
                onClick={() => setActiveCategory(activeCategory === cat ? null : cat)}
              />
            ))}
          </div>
        )}

        {/* Empty state */}
        {bottles.length === 0 && !isLoading && (
          <div style={{ textAlign: 'center', padding: '60px 0', animation: 'fadeInUp 0.6s ease-out' }}>
            <div
              style={{
                fontFamily: 'Cinzel, serif',
                fontSize: 13,
                letterSpacing: '0.4em',
                color: '#C9A84C',
                marginBottom: 16,
              }}
            >
              THE BAR IS EMPTY
            </div>
            <p
              style={{
                fontFamily: 'Cormorant Garamond, serif',
                fontSize: 20,
                fontStyle: 'italic',
                color: '#C9A84C',
                maxWidth: 380,
                margin: '0 auto 28px',
              }}
            >
              Begin your collection. Add your first bottle to the bar.
            </p>
            <button
              onClick={() => setAddOpen(true)}
              style={{
                fontFamily: 'Cinzel, serif',
                fontSize: 13,
                letterSpacing: '0.25em',
                color: '#C9A84C',
                background: 'transparent',
                border: '1px solid rgba(201,168,76,0.4)',
                padding: '12px 32px',
                borderRadius: 2,
                cursor: 'pointer',
              }}
            >
              ADD YOUR FIRST BOTTLE
            </button>
          </div>
        )}
      </main>

      {addOpen && (
        <AddBottlePanel
          onClose={() => setAddOpen(false)}
          onSuccess={() => {
            queryClient.invalidateQueries({ queryKey: ['bottles', user?.id] })
            setAddOpen(false)
          }}
        />
      )}

      {selectedBottle && (
        <BottleDetailPanel bottle={selectedBottle} onClose={() => setSelectedBottle(null)} />
      )}
    </div>
  )
}
