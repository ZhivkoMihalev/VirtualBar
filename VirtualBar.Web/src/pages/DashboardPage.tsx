import { useState, useMemo } from 'react'
import type { CSSProperties } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { useAuth } from '../contexts/AuthContext'
import NavBar from '../components/NavBar'
import { getBottlesByUser, addBottle, uploadBottleImage, linkBottleImage, lookupBarcode } from '../api/bottlesApi'
import type { Bottle, SpiritCategory, BottleCondition, AddBottlePayload } from '../types'
import { CATEGORY_COLORS, BottleSvg, VirtualBarScene } from '../components/BarShelf'
import BottleDetailPanel from '../components/BottleDetailPanel'
import DistillerySelect from '../components/DistillerySelect'

const CATEGORIES = Object.keys(CATEGORY_COLORS) as SpiritCategory[]
const CONDITIONS: BottleCondition[] = ['Sealed', 'Opened', 'Empty']


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


const statValueStyle: CSSProperties = {
  fontFamily: 'Playfair Display, serif',
  fontSize: 24,
  fontWeight: 700,
  color: '#C9A84C',
}

const statLabelStyle: CSSProperties = {
  fontFamily: 'Cormorant Garamond, serif',
  fontSize: 14,
  color: '#B09868',
  letterSpacing: '0.1em',
  textTransform: 'uppercase',
}

function StatItem({ value, label }: { value: number; label: string }) {
  return (
    <div>
      <div style={statValueStyle}>{value}</div>
      <div style={statLabelStyle}>{label}</div>
    </div>
  )
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
  const { t } = useTranslation()
  const [category, setCategory] = useState<SpiritCategory>('Whisky')
  const [condition, setCondition] = useState<BottleCondition>('Sealed')
  const [name, setName] = useState('')
  const [distilleryId, setDistilleryId] = useState<string | null>(null)
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
      distilleryId: distilleryId,
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
            {t('addBottle.title')}
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
                    {t('addBottle.changePhoto')}
                  </span>
                </div>
              </>
            ) : (
              <div style={{ textAlign: 'center', pointerEvents: 'none' }}>
                <div style={{ fontSize: 32, marginBottom: 8, color: 'rgba(201,168,76,0.4)' }}>📷</div>
                <div style={{ fontFamily: 'Cinzel, serif', fontSize: 10, letterSpacing: '0.2em', color: 'rgba(201,168,76,0.5)', marginBottom: 4 }}>
                  {t('addBottle.uploadPhoto')}
                </div>
                <div style={{ fontFamily: 'Cormorant Garamond, serif', fontSize: 13, color: 'rgba(201,168,76,0.3)' }}>
                  {t('addBottle.clickOrDrag')}
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
          <label style={labelStyle}>{t('addBottle.barcodeLookup')}</label>
          <div style={{ display: 'flex', gap: 8, marginBottom: 6 }}>
            <input
              value={barcode}
              onChange={e => setBarcode(e.target.value)}
              onKeyDown={e => { if (e.key === 'Enter') { e.preventDefault(); handleBarcodeSearch() } }}
              placeholder={t('addBottle.barcodePlaceholder')}
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
              {barcodeLoading ? '···' : t('addBottle.barcodeFind')}
            </button>
          </div>
          {barcodeStatus === 'found' && (
            <div style={{ fontFamily: 'Cormorant Garamond, serif', fontSize: 14, color: '#4A9A6A', marginBottom: 14 }}>
              {t('addBottle.barcodeSuccess')}
            </div>
          )}
          {barcodeStatus === 'error' && (
            <div style={{ fontFamily: 'Cormorant Garamond, serif', fontSize: 14, color: '#C04040', marginBottom: 14 }}>
              {t('addBottle.barcodeNotFound')}
            </div>
          )}
          {barcodeStatus === 'idle' && <div style={{ marginBottom: 18 }} />}

          {/* category selector */}
          <label style={labelStyle}>{t('addBottle.category')}</label>
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: 8, marginBottom: 18 }}>
            {CATEGORIES.map((cat) => {
              const c = CATEGORY_COLORS[cat]
              const selected = category === cat
              return (
                <button
                  key={cat}
                  type="button"
                  onClick={() => {
                    setCategory(cat)
                    setDistilleryId(null)
                  }}
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
          <label style={labelStyle}>{t('addBottle.name')}</label>
          <input
            value={name}
            onChange={(e) => setName(e.target.value)}
            onFocus={focusOn}
            onBlur={focusOff}
            required
            placeholder={t('addBottle.namePlaceholder')}
            style={{ ...inputStyle, marginBottom: 18 }}
          />

          {/* distillery */}
          <label style={labelStyle}>{t('addBottle.distillery')}</label>
          <DistillerySelect
            value={distilleryId}
            onChange={(id) => setDistilleryId(id)}
            category={category || undefined}
            placeholder={t('addBottle.distilleryPlaceholder')}
            style={{ marginBottom: 18 }}
          />

          {/* age / abv / volume */}
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: 12, marginBottom: 18 }}>
            <div>
              <label style={labelStyle}>{t('addBottle.age')}</label>
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
              <label style={labelStyle}>{t('addBottle.abv')}</label>
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
              <label style={labelStyle}>{t('addBottle.volume')}</label>
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
          <label style={labelStyle}>{t('addBottle.condition')}</label>
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: 8, marginBottom: 18 }}>
            {CONDITIONS.map((cond) => {
              const selected = condition === cond
              const conditionLabel = t(`addBottle.condition${cond}`)
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
                  {conditionLabel}
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
              {t('addBottle.limited')}
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
          <label style={labelStyle}>{t('addBottle.description')}</label>
          <textarea
            value={description}
            onChange={(e) => setDescription(e.target.value)}
            onFocus={focusOn}
            onBlur={focusOff}
            rows={3}
            placeholder={t('addBottle.descriptionPlaceholder')}
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
              {t('addBottle.error')}
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
            {mutation.isPending ? t('addBottle.submitting') : t('addBottle.submit')}
          </button>
        </form>
      </div>
    </div>
  )
}


export default function DashboardPage() {
  const { t } = useTranslation()
  const { user } = useAuth()
  const queryClient = useQueryClient()
  const [addOpen, setAddOpen] = useState(false)
  const [selectedBottle, setSelectedBottle] = useState<Bottle | null>(null)
  const [activeCategory, setActiveCategory] = useState<SpiritCategory | null>(null)

  const { data: bottles = [], isLoading } = useQuery({
    queryKey: ['bottles', user?.id],
    queryFn: () => getBottlesByUser(user!.id),
    enabled: !!user?.id,
  })

  const displayedBottles = useMemo(
    () => activeCategory ? bottles.filter(b => b.category === activeCategory) : bottles,
    [bottles, activeCategory],
  )

  const categoryCounts = useMemo(
    () => CATEGORIES.reduce((acc, cat) => {
      acc[cat] = bottles.filter(b => b.category === cat).length
      return acc
    }, {} as Record<SpiritCategory, number>),
    [bottles],
  )

  return (
    <div style={{ minHeight: '100vh', color: '#F0DDB4' }}>
      <NavBar />

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
              {t('dashboard.yourCollection')}
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
              {t('dashboard.virtualBar')}
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
                ? t('dashboard.collectionAwaits')
                : t('dashboard.bottlesInCollection', { count: bottles.length })}
            </div>
          </div>

          <div style={{ display: 'flex', alignItems: 'center', gap: 12 }}>
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
              {t('dashboard.addBottle')}
            </button>
          </div>
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
            <StatItem value={bottles.length} label={t('dashboard.totalBottles')} />
            <div style={{ width: 1, background: 'rgba(201,168,76,0.15)' }} />
            <StatItem value={bottles.filter((b) => b.condition === 'Sealed').length} label={t('dashboard.sealed')} />
            <div style={{ width: 1, background: 'rgba(201,168,76,0.15)' }} />
            <StatItem value={bottles.filter((b) => b.isForSale).length} label={t('dashboard.forSale')} />
            <div style={{ width: 1, background: 'rgba(201,168,76,0.15)' }} />
            <StatItem value={bottles.filter((b) => b.isLimited).length} label={t('dashboard.limited')} />
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
            {t('dashboard.loading')}
          </div>
        )}

        {!isLoading && bottles.length > 0 && (
          <VirtualBarScene bottles={displayedBottles} onAdd={() => setAddOpen(true)} onSelect={setSelectedBottle} />
        )}

        {/* Category filter */}
        {bottles.length > 0 && (
          <div style={{ display: 'flex', gap: 10, flexWrap: 'wrap', marginTop: 28 }}>
            <CategoryPill
              label={t('marketplace.allCategories')}
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
              {t('dashboard.emptyTitle')}
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
              {t('dashboard.emptySubtitle')}
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
              {t('dashboard.emptyButton')}
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

      {selectedBottle && user && (
        <BottleDetailPanel
          bottle={bottles.find(b => b.id === selectedBottle.id) ?? selectedBottle}
          userId={user.id}
          currentUserId={user.id}
          onClose={() => setSelectedBottle(null)}
          onDelete={() => {
            queryClient.invalidateQueries({ queryKey: ['bottles', user.id] })
            setSelectedBottle(null)
          }}
        />
      )}
    </div>
  )
}
