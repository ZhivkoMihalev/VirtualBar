import { useState, useEffect } from 'react'
import type { CSSProperties } from 'react'
import { Link } from 'react-router-dom'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { useAuth } from '../contexts/AuthContext'
import { getMarketplace } from '../api/bottlesApi'
import { getAllWishListItems, addWishListItem, removeWishListItem, uploadWishListImage } from '../api/wishListApi'
import type { AddWishListItemRequest } from '../api/wishListApi'
import type { Bottle, SpiritCategory, PublicWishListItem } from '../types'
import { CATEGORY_COLORS, BottleSvg } from '../components/BarShelf'
import BottleDetailPanel from '../components/BottleDetailPanel'
import DistillerySelect from '../components/DistillerySelect'
import NavBar from '../components/NavBar'

const CATEGORIES = Object.keys(CATEGORY_COLORS) as SpiritCategory[]

type SortOption = 'price_asc' | 'price_desc' | 'newest'
type MarketTab = 'sale' | 'search'

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

const tabBarStyle: CSSProperties = {
  display: 'flex',
  gap: 4,
  borderBottom: '1px solid rgba(201,168,76,0.12)',
  marginBottom: 32,
}

const tabBaseStyle: CSSProperties = {
  fontFamily: 'Cinzel, serif',
  fontSize: 13,
  letterSpacing: '0.25em',
  textTransform: 'uppercase',
  padding: '12px 22px',
  cursor: 'pointer',
  background: 'transparent',
  borderTop: 'none',
  borderLeft: 'none',
  borderRight: 'none',
  outline: 'none',
  transition: 'all 0.2s ease',
}

const tabActiveStyle: CSSProperties = {
  ...tabBaseStyle,
  color: '#C9A84C',
  borderBottom: '2px solid #C9A84C',
}

const tabInactiveStyle: CSSProperties = {
  ...tabBaseStyle,
  color: '#B09868',
  borderBottom: '2px solid transparent',
}

const publishSearchBarStyle: CSSProperties = {
  marginBottom: 32,
}

const publishSearchButtonStyle: CSSProperties = {
  fontFamily: 'Cinzel, serif',
  fontSize: 12,
  letterSpacing: '0.25em',
  textTransform: 'uppercase',
  color: '#07030A',
  background: 'linear-gradient(135deg, #C9A84C, #E8C870)',
  border: 'none',
  padding: '13px 30px',
  borderRadius: 2,
  cursor: 'pointer',
  boxShadow: '0 4px 20px rgba(201,168,76,0.3)',
}

const modalBackdropStyle: CSSProperties = {
  position: 'fixed',
  inset: 0,
  zIndex: 1000,
  background: 'rgba(0,0,0,0.75)',
  display: 'flex',
  alignItems: 'center',
  justifyContent: 'center',
  padding: '2rem',
}

const modalPanelStyle: CSSProperties = {
  position: 'relative',
  background: '#0D0804',
  border: '1px solid rgba(201,168,76,0.25)',
  borderRadius: 8,
  padding: 40,
  maxWidth: 640,
  width: '90%',
}

const modalCloseStyle: CSSProperties = {
  position: 'absolute',
  top: 16,
  right: 20,
  width: 32,
  height: 32,
  borderRadius: '50%',
  border: '1px solid rgba(201,168,76,0.35)',
  background: 'transparent',
  color: '#E8C870',
  fontFamily: 'Cormorant Garamond, serif',
  fontSize: 22,
  lineHeight: '28px',
  textAlign: 'center',
  cursor: 'pointer',
  padding: 0,
}

const modalTitleStyle: CSSProperties = {
  fontFamily: 'Cinzel, serif',
  fontSize: 18,
  letterSpacing: '0.2em',
  textTransform: 'uppercase',
  color: '#E8C870',
  margin: '0 0 28px',
}

const wishLabelStyle: CSSProperties = {
  fontFamily: 'Cinzel, serif',
  fontSize: 11,
  letterSpacing: '0.2em',
  color: '#B09868',
  textTransform: 'uppercase',
  marginBottom: 6,
  display: 'block',
}

const wishFieldGridStyle: CSSProperties = {
  display: 'grid',
  gridTemplateColumns: 'repeat(3, 1fr)',
  gap: 14,
  marginBottom: 18,
}

const wishImageSectionStyle: CSSProperties = {
  marginBottom: 18,
}

const wishImageToggleRowStyle: CSSProperties = {
  display: 'flex',
  gap: 4,
  marginBottom: 12,
}

const wishImageToggleBaseStyle: CSSProperties = {
  fontFamily: 'Cinzel, serif',
  fontSize: 10,
  letterSpacing: '0.2em',
  textTransform: 'uppercase',
  padding: '8px 18px',
  borderRadius: 2,
  cursor: 'pointer',
  background: 'transparent',
  transition: 'all 0.2s ease',
}

const wishImageToggleActiveStyle: CSSProperties = {
  ...wishImageToggleBaseStyle,
  color: '#C9A84C',
  border: '1px solid rgba(201,168,76,0.5)',
}

const wishImageToggleInactiveStyle: CSSProperties = {
  ...wishImageToggleBaseStyle,
  color: '#B09868',
  border: '1px solid rgba(201,168,76,0.15)',
}

const wishChooseFileButtonStyle: CSSProperties = {
  fontFamily: 'Cinzel, serif',
  fontSize: 11,
  letterSpacing: '0.2em',
  textTransform: 'uppercase',
  color: '#C9A84C',
  background: 'transparent',
  border: '1px solid rgba(201,168,76,0.35)',
  padding: '10px 22px',
  borderRadius: 2,
  cursor: 'pointer',
  display: 'inline-block',
}

const wishImagePreviewWrapStyle: CSSProperties = {
  position: 'relative',
  display: 'inline-block',
  marginTop: 12,
}

const wishImagePreviewStyle: CSSProperties = {
  width: 80,
  height: 80,
  objectFit: 'cover',
  borderRadius: 4,
  border: '1px solid rgba(201,168,76,0.4)',
  display: 'block',
}

const wishImageRemoveStyle: CSSProperties = {
  position: 'absolute',
  top: -8,
  right: -8,
  width: 22,
  height: 22,
  borderRadius: '50%',
  border: '1px solid rgba(201,168,76,0.5)',
  background: '#07030A',
  color: '#E8C870',
  fontFamily: 'Cormorant Garamond, serif',
  fontSize: 16,
  lineHeight: '20px',
  textAlign: 'center',
  cursor: 'pointer',
  padding: 0,
}

const wishUploadingStyle: CSSProperties = {
  fontFamily: 'Cinzel, serif',
  fontSize: 11,
  letterSpacing: '0.2em',
  color: '#C9A84C',
  marginTop: 10,
}

const wishCardContentStyle: CSSProperties = {
  flex: 1,
  minWidth: 0,
}

const wishCardImageStyle: CSSProperties = {
  width: 64,
  height: 64,
  objectFit: 'cover',
  borderRadius: 4,
  border: '1px solid rgba(201,168,76,0.35)',
  flexShrink: 0,
}

const wishAddButtonStyle: CSSProperties = {
  fontFamily: 'Cinzel, serif',
  fontSize: 12,
  letterSpacing: '0.2em',
  color: '#07030A',
  background: 'linear-gradient(135deg, #C9A84C, #E8C870)',
  border: 'none',
  padding: '12px 28px',
  borderRadius: 2,
  cursor: 'pointer',
  boxShadow: '0 4px 20px rgba(201,168,76,0.3)',
}

const wishErrorStyle: CSSProperties = {
  fontFamily: 'Cormorant Garamond, serif',
  fontSize: 15,
  color: '#D42020',
  marginBottom: 14,
}

const wishListStyle: CSSProperties = {
  display: 'flex',
  flexDirection: 'column',
  gap: 14,
}

const wishCardStyle: CSSProperties = {
  display: 'flex',
  alignItems: 'center',
  justifyContent: 'space-between',
  gap: 16,
  padding: '18px 22px',
  background: 'rgba(15,6,4,0.6)',
  border: '1px solid rgba(201,168,76,0.15)',
  borderRadius: 6,
}

const wishUserStyle: CSSProperties = {
  fontFamily: 'Cinzel, serif',
  fontSize: 10,
  letterSpacing: '0.2em',
  textTransform: 'uppercase',
  color: '#B09868',
  marginBottom: 8,
}

const wishCardTitleStyle: CSSProperties = {
  fontFamily: 'Playfair Display, serif',
  fontSize: 20,
  color: '#E8C870',
  marginBottom: 8,
}

const wishChipRowStyle: CSSProperties = {
  display: 'flex',
  alignItems: 'center',
  gap: 8,
  flexWrap: 'wrap',
}

const wishDistilleryChipStyle: CSSProperties = {
  fontFamily: 'Cormorant Garamond, serif',
  fontSize: 14,
  padding: '3px 12px',
  borderRadius: 14,
  background: 'rgba(201,168,76,0.12)',
  border: '1px solid rgba(201,168,76,0.35)',
  color: '#E8C870',
}

const wishCategoryChipStyle: CSSProperties = {
  fontFamily: 'Cormorant Garamond, serif',
  fontSize: 14,
  padding: '3px 12px',
  borderRadius: 14,
  background: 'rgba(255,255,255,0.04)',
  border: '1px solid rgba(168,168,168,0.25)',
  color: '#C9C3B4',
}

const wishDateStyle: CSSProperties = {
  fontFamily: 'Cinzel, serif',
  fontSize: 10,
  letterSpacing: '0.15em',
  color: '#6A5C44',
  marginTop: 10,
}

const wishRemoveButtonStyle: CSSProperties = {
  fontFamily: 'Cinzel, serif',
  fontSize: 10,
  letterSpacing: '0.15em',
  color: '#B09868',
  background: 'transparent',
  border: '1px solid rgba(201,168,76,0.25)',
  padding: '8px 16px',
  borderRadius: 2,
  cursor: 'pointer',
  whiteSpace: 'nowrap',
  flexShrink: 0,
}

const wishConfirmButtonStyle: CSSProperties = {
  ...wishRemoveButtonStyle,
  color: '#07030A',
  background: '#D42020',
  border: '1px solid #D42020',
}

const wishEmptyStyle: CSSProperties = {
  textAlign: 'center',
  padding: '60px 0',
  fontFamily: 'Cormorant Garamond, serif',
  fontSize: 20,
  fontStyle: 'italic',
  color: '#C9A84C',
}

const wishLoadingStyle: CSSProperties = {
  textAlign: 'center',
  padding: '60px 0',
  fontFamily: 'Cinzel, serif',
  fontSize: 13,
  letterSpacing: '0.4em',
  color: '#C9A84C',
}

function focusOn(e: React.FocusEvent<HTMLInputElement | HTMLSelectElement>) {
  e.currentTarget.style.border = '1px solid rgba(201,168,76,0.5)'
}

function focusOff(e: React.FocusEvent<HTMLInputElement | HTMLSelectElement>) {
  e.currentTarget.style.border = '1px solid rgba(201,168,76,0.2)'
}

function formatDate(iso: string): string {
  return new Date(iso).toLocaleDateString(undefined, { year: 'numeric', month: 'short', day: 'numeric' })
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

        {bottle.distilleryName && (
          <div
            style={{
              fontFamily: 'Cormorant Garamond, serif',
              fontStyle: 'italic',
              fontSize: 15,
              color: '#B09868',
              marginBottom: 8,
            }}
          >
            {bottle.distilleryName}
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

function WishCard({ item, isOwn }: { item: PublicWishListItem; isOwn: boolean }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const [confirming, setConfirming] = useState(false)

  const removeMutation = useMutation({
    mutationFn: () => removeWishListItem(item.id),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['wishlist', 'all'] }),
  })

  function handleRemoveClick() {
    if (!confirming) {
      setConfirming(true)
      return
    }
    removeMutation.mutate()
  }

  return (
    <div style={wishCardStyle}>
      <div style={wishCardContentStyle}>
        <div style={wishUserStyle}>{item.userDisplayName}</div>
        {item.bottleName && <div style={wishCardTitleStyle}>{item.bottleName}</div>}
        <div style={wishChipRowStyle}>
          {item.distilleryName && <span style={wishDistilleryChipStyle}>{item.distilleryName}</span>}
          {item.category && <span style={wishCategoryChipStyle}>{CATEGORY_COLORS[item.category].label}</span>}
        </div>
        <div style={wishDateStyle}>{formatDate(item.createdAt)}</div>
      </div>
      {item.imageUrl && <img src={item.imageUrl} alt={item.bottleName ?? ''} style={wishCardImageStyle} />}
      {isOwn && (
        <button
          onClick={handleRemoveClick}
          disabled={removeMutation.isPending}
          style={confirming ? wishConfirmButtonStyle : wishRemoveButtonStyle}
        >
          {confirming ? t('wishList.removeConfirm') : t('wishList.remove')}
        </button>
      )}
    </div>
  )
}

function SearchTab() {
  const { t } = useTranslation()
  const { user, isAuthenticated } = useAuth()
  const queryClient = useQueryClient()

  const [bottleName, setBottleName] = useState('')
  const [distilleryId, setDistilleryId] = useState<string | null>(null)
  const [category, setCategory] = useState<SpiritCategory | ''>('')
  const [imageUrl, setImageUrl] = useState('')
  const [imageTab, setImageTab] = useState<'url' | 'upload'>('url')
  const [validationError, setValidationError] = useState<string | null>(null)
  const [modalOpen, setModalOpen] = useState(false)

  const { data: items = [], isLoading } = useQuery({
    queryKey: ['wishlist', 'all'],
    queryFn: getAllWishListItems,
  })

  const uploadMutation = useMutation({
    mutationFn: (file: File) => uploadWishListImage(file),
    onSuccess: (result) => setImageUrl(result.url),
  })

  const addMutation = useMutation({
    mutationFn: (payload: AddWishListItemRequest) => addWishListItem(payload),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['wishlist', 'all'] })
      setBottleName('')
      setDistilleryId(null)
      setCategory('')
      setImageUrl('')
      setImageTab('url')
      uploadMutation.reset()
      setValidationError(null)
      setModalOpen(false)
    },
  })

  function handleFileChange(e: React.ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0]
    if (file) uploadMutation.mutate(file)
    e.target.value = ''
  }

  function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    if (!distilleryId && !category) {
      setValidationError(t('wishList.atLeastOne'))
      return
    }
    setValidationError(null)
    addMutation.mutate({
      bottleName: bottleName.trim() || undefined,
      distilleryId: distilleryId,
      category: category || undefined,
      imageUrl: imageUrl || undefined,
    })
  }

  return (
    <>
      {isAuthenticated && (
        <div style={publishSearchBarStyle}>
          <button type="button" onClick={() => setModalOpen(true)} style={publishSearchButtonStyle}>
            {t('wishList.publishSearch')}
          </button>
        </div>
      )}

      {isAuthenticated && modalOpen && (
        <div style={modalBackdropStyle} onClick={() => setModalOpen(false)}>
          <div style={modalPanelStyle} onClick={(e) => e.stopPropagation()}>
            <button
              type="button"
              onClick={() => setModalOpen(false)}
              style={modalCloseStyle}
              aria-label={t('wishList.close')}
            >
              ×
            </button>
            <h2 style={modalTitleStyle}>{t('wishList.publishSearchTitle')}</h2>
            <form onSubmit={handleSubmit}>
              <div style={wishFieldGridStyle}>
                <div>
                  <label style={wishLabelStyle}>{t('wishList.bottleName')}</label>
                  <input
                    value={bottleName}
                    onChange={(e) => setBottleName(e.target.value)}
                    onFocus={focusOn}
                    onBlur={focusOff}
                    placeholder={t('wishList.bottleNamePlaceholder')}
                    style={inputStyle}
                  />
                </div>
                <div>
                  <label style={wishLabelStyle}>{t('wishList.distillery')}</label>
                  <DistillerySelect
                    value={distilleryId}
                    onChange={(id) => setDistilleryId(id)}
                    category={category || undefined}
                    placeholder={t('wishList.distilleryPlaceholder')}
                  />
                </div>
                <div>
                  <label style={wishLabelStyle}>{t('wishList.category')}</label>
                  <select
                    value={category}
                    onChange={(e) => {
                      setCategory(e.target.value as SpiritCategory | '')
                      setDistilleryId(null)
                    }}
                    onFocus={focusOn}
                    onBlur={focusOff}
                    style={selectStyle}
                  >
                    <option value="">{t('wishList.categoryAny')}</option>
                    {CATEGORIES.map((cat) => (
                      <option key={cat} value={cat}>{CATEGORY_COLORS[cat].label}</option>
                    ))}
                  </select>
                </div>
              </div>

              <div style={wishImageSectionStyle}>
                <div style={wishImageToggleRowStyle}>
                  <button
                    type="button"
                    onClick={() => setImageTab('url')}
                    style={imageTab === 'url' ? wishImageToggleActiveStyle : wishImageToggleInactiveStyle}
                  >
                    {t('wishList.imageTabUrl')}
                  </button>
                  <button
                    type="button"
                    onClick={() => setImageTab('upload')}
                    style={imageTab === 'upload' ? wishImageToggleActiveStyle : wishImageToggleInactiveStyle}
                  >
                    {t('wishList.imageTabUpload')}
                  </button>
                </div>

                {imageTab === 'url' && (
                  <div>
                    <label style={wishLabelStyle}>{t('wishList.imageUrl')}</label>
                    <input
                      value={imageUrl}
                      onChange={(e) => setImageUrl(e.target.value)}
                      onFocus={focusOn}
                      onBlur={focusOff}
                      placeholder={t('wishList.imageUrlPlaceholder')}
                      style={inputStyle}
                    />
                  </div>
                )}

                {imageTab === 'upload' && (
                  <div>
                    <label style={wishLabelStyle}>{t('wishList.uploadImage')}</label>
                    <label style={wishChooseFileButtonStyle}>
                      {t('wishList.chooseFile')}
                      <input
                        type="file"
                        accept="image/jpeg,image/png,image/webp"
                        style={{ display: 'none' }}
                        onChange={handleFileChange}
                      />
                    </label>
                    {uploadMutation.isPending && <div style={wishUploadingStyle}>{t('wishList.uploading')}</div>}
                    {uploadMutation.isError && <div style={wishErrorStyle}>{t('wishList.uploadError')}</div>}
                  </div>
                )}

                {imageUrl && (
                  <div style={wishImagePreviewWrapStyle}>
                    <img src={imageUrl} alt="" style={wishImagePreviewStyle} />
                    <button type="button" onClick={() => setImageUrl('')} style={wishImageRemoveStyle}>
                      ×
                    </button>
                  </div>
                )}
              </div>

              {validationError && <div style={wishErrorStyle}>{validationError}</div>}
              {addMutation.isError && <div style={wishErrorStyle}>{t('wishList.addFailed')}</div>}

              <button type="submit" disabled={addMutation.isPending} style={wishAddButtonStyle}>
                {addMutation.isPending ? t('wishList.adding') : t('wishList.addBtn')}
              </button>
            </form>
          </div>
        </div>
      )}

      {isLoading && <div style={wishLoadingStyle}>···</div>}

      {!isLoading && items.length === 0 && (
        <div style={wishEmptyStyle}>{t('marketplace.searchEmpty')}</div>
      )}

      {!isLoading && items.length > 0 && (
        <div style={wishListStyle}>
          {items.map((item) => (
            <WishCard key={item.id} item={item} isOwn={item.userId === user?.id} />
          ))}
        </div>
      )}
    </>
  )
}

function SaleTab({ onView }: { onView: (bottle: Bottle) => void }) {
  const { t } = useTranslation()
  const [search, setSearch] = useState('')
  const [category, setCategory] = useState<string>('')
  const [sort, setSort] = useState<SortOption>('newest')
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
    <>
      <div
        style={{
          display: 'flex',
          alignItems: 'center',
          gap: 16,
          flexWrap: 'wrap',
          marginBottom: 32,
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
            <MarketplaceCard key={bottle.id} bottle={bottle} onView={() => onView(bottle)} />
          ))}
        </div>
      )}
    </>
  )
}

export default function MarketplacePage() {
  const { t } = useTranslation()
  const { user } = useAuth()
  const [tab, setTab] = useState<MarketTab>('sale')
  const [selectedBottle, setSelectedBottle] = useState<Bottle | null>(null)

  return (
    <div style={{ minHeight: '100vh', color: '#F0DDB4' }}>
      <NavBar />

      <main style={{ maxWidth: 1200, margin: '0 auto', padding: '40px' }}>
        <div style={{ marginBottom: 24 }}>
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
            {tab === 'sale' ? t('marketplace.title') : t('marketplace.titleSearch')}
          </h1>
        </div>

        <div style={tabBarStyle}>
          <button
            onClick={() => setTab('sale')}
            style={tab === 'sale' ? tabActiveStyle : tabInactiveStyle}
          >
            {t('marketplace.tabSale')}
          </button>
          <button
            onClick={() => setTab('search')}
            style={tab === 'search' ? tabActiveStyle : tabInactiveStyle}
          >
            {t('marketplace.tabSearch')}
          </button>
        </div>

        {tab === 'sale' ? (
          <SaleTab onView={setSelectedBottle} />
        ) : (
          <SearchTab />
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
