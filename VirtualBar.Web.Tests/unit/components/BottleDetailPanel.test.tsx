import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { getBottleComments, updateBottle } from '@/api/bottlesApi'
import { getNotifications } from '@/api/notificationsApi'
import { getBottleEstimate } from '@/api/pricesApi'
import { getReviews } from '@/api/reviewsApi'
import BottleDetailPanel from '@/components/BottleDetailPanel'
import i18n from '@/i18n'
import bg from '@/i18n/bg.json'
import type { Bottle } from '@/types'
import { cameraReads, setCameraSupported } from '../fakeCamera'
import { bottle, noReviews } from '../fixtures/bottles'
import { renderWithProviders, signedInUser, signIn } from '../testUtils'

vi.mock('@/api/bottlesApi')
vi.mock('@/api/notificationsApi')
vi.mock('@/api/offersApi')
vi.mock('@/api/pricesApi')
vi.mock('@/api/reviewsApi')
vi.mock('@zxing/browser', async () => (await import('../fakeCamera')).zxingBrowserMock())

/** Opens the panel on another collector's bottle, as a public bar does. */
function renderPanel(shown: Bottle, currentUserId = '') {
  return renderWithProviders(
    <BottleDetailPanel bottle={shown} userId={shown.userId} currentUserId={currentUserId} onClose={() => {}} />,
  )
}

/** Logs a collector in, with the NavBar's notifications and the price estimate answered. */
function signInCollector() {
  signIn()
  vi.mocked(getNotifications).mockResolvedValue({ notifications: [], unreadCount: 0 })
  vi.mocked(getBottleEstimate).mockResolvedValue(null)
}

describe('BottleDetailPanel', () => {
  beforeEach(() => {
    vi.mocked(getBottleComments).mockResolvedValue([])
    vi.mocked(getReviews).mockResolvedValue(noReviews())
  })

  /** A visitor sees that the bottle is for sale and how many, but a login link instead of the price (#103). */
  it('shows a visitor a listed bottle without its price, with a link to log in', async () => {
    // The API sends a visitor no price.
    renderPanel(bottle({ isForSale: true, forSaleQuantity: 2, quantity: 3 }))

    expect(await screen.findByText(bg.bottle.forSale)).toBeInTheDocument()
    expect(screen.getByRole('link', { name: bg.bottle.loginToSeePrice })).toHaveAttribute('href', '/login')
    expect(screen.getByText(i18n.t('bottle.availableForSale', { count: 2 }))).toBeInTheDocument()
    expect(screen.queryByText(bg.bottle.perBottle)).not.toBeInTheDocument()
  })

  it('shows a logged-in collector the price of a listed bottle, and no login link', async () => {
    signInCollector()
    renderPanel(
      bottle({ isForSale: true, forSaleQuantity: 2, quantity: 3, askingPrice: 150, currency: 'GBP' }),
      signedInUser.id,
    )

    expect(await screen.findByText(/GBP 150/)).toBeInTheDocument()
    expect(screen.getByText(bg.bottle.perBottle)).toBeInTheDocument()
    expect(screen.getByText(i18n.t('bottle.availableForSale', { count: 2 }))).toBeInTheDocument()
    expect(screen.queryByRole('link', { name: bg.bottle.loginToSeePrice })).not.toBeInTheDocument()
  })

  it('shows a visitor no sale box for a bottle that is not for sale', async () => {
    renderPanel(bottle())

    expect(await screen.findByRole('heading', { name: 'Lagavulin 16' })).toBeInTheDocument()
    expect(screen.queryByText(bg.bottle.askingPrice)).not.toBeInTheDocument()
    expect(screen.queryByRole('link', { name: bg.bottle.loginToSeePrice })).not.toBeInTheDocument()
  })

  /** A quantity-only change echoes the whole bottle, its barcode too, or the update would clear it (#49). */
  it('keeps the barcode when the owner changes the quantity', async () => {
    signInCollector()
    const owned = bottle({ userId: signedInUser.id, quantity: 2, barcode: '5000267014203' })
    vi.mocked(updateBottle).mockResolvedValue({ ...owned, quantity: 3 })
    renderPanel(owned, signedInUser.id)

    await userEvent.click(await screen.findByRole('button', { name: bg.addBottle.quantityIncrease }))

    expect(updateBottle).toHaveBeenCalledWith(
      owned.id,
      expect.objectContaining({ quantity: 3, barcode: '5000267014203' }),
    )
  })

  describe('barcode (#49)', () => {
    const BARCODE = '5000267014203'

    /** Opens the panel on the collector's own bottle, with the update answered. */
    function openOwnBottle(overrides: Partial<Bottle> = {}) {
      signInCollector()
      const owned = bottle({ userId: signedInUser.id, quantity: 2, productId: 'product-1', ...overrides })
      vi.mocked(updateBottle).mockImplementation(async (_id, payload) => ({ ...owned, ...payload }) as Bottle)
      renderPanel(owned, signedInUser.id)
      return owned
    }

    it('saves a typed barcode and echoes the rest of the bottle', async () => {
      const owned = openOwnBottle()

      expect(await screen.findByText(bg.bottle.noBarcode)).toBeInTheDocument()
      await userEvent.type(screen.getByLabelText(bg.bottle.barcode), ` ${BARCODE} `)
      await userEvent.click(screen.getByRole('button', { name: bg.bottle.saveBarcode }))

      expect(await screen.findByText(bg.bottle.barcodeSaved)).toBeInTheDocument()
      expect(updateBottle).toHaveBeenCalledWith(
        owned.id,
        expect.objectContaining({ barcode: BARCODE, quantity: 2, productId: 'product-1', name: owned.name }),
      )
    })

    it('saves the barcode the camera reads', async () => {
      setCameraSupported(true)
      cameraReads(BARCODE)
      const owned = openOwnBottle()

      await userEvent.click(await screen.findByRole('button', { name: bg.addBottle.scanBarcode }))

      expect(await screen.findByText(bg.bottle.barcodeSaved)).toBeInTheDocument()
      expect(updateBottle).toHaveBeenCalledWith(owned.id, expect.objectContaining({ barcode: BARCODE }))
      expect(screen.getByLabelText(bg.bottle.barcode)).toHaveValue(BARCODE)
    })

    it('refuses a barcode that is not 8 to 14 digits without sending it', async () => {
      openOwnBottle()

      await userEvent.type(await screen.findByLabelText(bg.bottle.barcode), '50002670142AB')
      await userEvent.click(screen.getByRole('button', { name: bg.bottle.saveBarcode }))

      expect(await screen.findByText(bg.addBottle.barcodeInvalid)).toBeInTheDocument()
      expect(updateBottle).not.toHaveBeenCalled()
    })

    it('clears the barcode when the field is emptied', async () => {
      const owned = openOwnBottle({ barcode: BARCODE })

      await userEvent.clear(await screen.findByLabelText(bg.bottle.barcode))
      await userEvent.click(screen.getByRole('button', { name: bg.bottle.saveBarcode }))

      await vi.waitFor(() => expect(updateBottle).toHaveBeenCalledWith(owned.id, expect.objectContaining({ barcode: null })))
    })

    it('shows the barcode section only to the owner', async () => {
      renderPanel(bottle({ barcode: BARCODE }))

      expect(await screen.findByRole('heading', { name: 'Lagavulin 16' })).toBeInTheDocument()
      expect(screen.queryByLabelText(bg.bottle.barcode)).not.toBeInTheDocument()
    })
  })
})
