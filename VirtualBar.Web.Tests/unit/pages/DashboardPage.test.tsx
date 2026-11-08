import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { addBottle, getBottlesByUser, lookupBarcode } from '@/api/bottlesApi'
import { getDistilleries } from '@/api/distilleriesApi'
import { getNotifications } from '@/api/notificationsApi'
import { getCollectionValue } from '@/api/pricesApi'
import i18n from '@/i18n'
import bg from '@/i18n/bg.json'
import DashboardPage from '@/pages/DashboardPage'
import type { BarcodeLookup, BarcodeSuggestion, Product } from '@/types'
import { cameraReads, setCameraSupported } from '../fakeCamera'
import { bottle } from '../fixtures/bottles'
import { catalogProduct } from '../fixtures/products'
import { apiError, renderWithProviders, signedInUser, signIn } from '../testUtils'

vi.mock('@/api/bottlesApi')
vi.mock('@/api/distilleriesApi')
vi.mock('@/api/messagesApi')
vi.mock('@/api/notificationsApi')
vi.mock('@/api/pricesApi')
vi.mock('@/api/productsApi')
vi.mock('@zxing/browser', async () => (await import('../fakeCamera')).zxingBrowserMock())

const BARCODE = '5000267014203'

const DISTILLERY = { id: '00000000-0000-4000-a000-000000000001', name: 'Foursquare', country: 'Barbados', region: null, categories: ['Rum'] }

// The product picker and the sheet are Radix components. jsdom has neither layout nor pointer capture, so give
// both what they call; the components themselves behave as in the browser.
beforeEach(() => {
  globalThis.ResizeObserver ??= class {
    observe() {}
    unobserve() {}
    disconnect() {}
  }
  Element.prototype.scrollIntoView ??= () => {}
  Element.prototype.hasPointerCapture ??= () => false
})

/** Opens the own bar of a logged-in collector with an empty shelf, and the add-bottle form on it. */
async function openAddForm() {
  signIn()
  vi.mocked(getNotifications).mockResolvedValue({ notifications: [], unreadCount: 0 })
  vi.mocked(getBottlesByUser).mockResolvedValue([])
  vi.mocked(getCollectionValue).mockResolvedValue({ totalValue: 0, currency: 'EUR', pricedCount: 0, totalCount: 0, items: [] })
  vi.mocked(getDistilleries).mockResolvedValue([DISTILLERY])
  vi.mocked(addBottle).mockImplementation(async payload => bottle({ ...payload, userId: signedInUser.id }))
  renderWithProviders(<DashboardPage />, { route: '/dashboard' })
  await userEvent.click(await screen.findByRole('button', { name: bg.dashboard.addBottle }))
}

/** The lookup's answer when the catalog product itself has the barcode. */
const fromCatalog = (product: Product): BarcodeLookup => ({ source: 'Catalog', product, suggestion: null, collectors: 0 })

async function findByBarcode(barcode: string) {
  await userEvent.type(screen.getByLabelText(bg.addBottle.barcodeLookup), barcode)
  await userEvent.click(screen.getByRole('button', { name: bg.addBottle.barcodeFind }))
}

describe('DashboardPage: add a bottle by barcode', () => {
  /**
   * A barcode hit fills the form from the catalog product, like a catalog pick: the bottle used to be saved linked to
   * a rum while keeping the form's default category, and no distillery or age (#120).
   */
  it('fills the category, distillery and age from the product the barcode belongs to', async () => {
    const rum = catalogProduct({
      name: 'Foursquare 2005 Vintage',
      brand: 'Foursquare',
      distilleryId: DISTILLERY.id,
      distilleryName: DISTILLERY.name,
      category: 'Rum',
      country: 'Barbados',
      region: 'Caribbean',
      age: 12,
      abvPercent: 62,
      volumeMl: 700,
      barcode: BARCODE,
    })
    vi.mocked(lookupBarcode).mockResolvedValue(fromCatalog(rum))
    await openAddForm()

    await findByBarcode(BARCODE)
    expect(await screen.findByText(bg.addBottle.barcodeSuccess)).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: bg.addBottle.submit }))

    await vi.waitFor(() => expect(addBottle).toHaveBeenCalled())
    expect(addBottle).toHaveBeenCalledWith(
      expect.objectContaining({
        name: 'Foursquare 2005 Vintage',
        productId: rum.id,
        category: 'Rum',
        distilleryId: DISTILLERY.id,
        age: 12,
        abvPercent: 62,
        volumeMl: 700,
        country: 'Barbados',
        region: 'Caribbean',
        barcode: BARCODE,
      }),
    )
  })

  it('asks to pick the bottle from the catalog when nobody has the barcode yet, and keeps the barcode', async () => {
    vi.mocked(lookupBarcode).mockRejectedValue(apiError(404, { error: 'Product not found.' }))
    await openAddForm()

    await findByBarcode(BARCODE)

    expect(await screen.findByText(bg.addBottle.barcodeNotFound)).toBeInTheDocument()
    await userEvent.type(screen.getByLabelText(bg.addBottle.name), 'Unknown Single Cask')
    await userEvent.click(screen.getByRole('button', { name: bg.addBottle.submit }))

    await vi.waitFor(() => expect(addBottle).toHaveBeenCalled())
    expect(addBottle).toHaveBeenCalledWith(expect.objectContaining({ productId: null, barcode: BARCODE }))
  })

  it('looks up a barcode read by the camera and saves it with the bottle (#49)', async () => {
    setCameraSupported(true)
    cameraReads(BARCODE)
    const rum = catalogProduct({ name: 'Foursquare 2005 Vintage', category: 'Rum' })
    vi.mocked(lookupBarcode).mockResolvedValue(fromCatalog(rum))
    await openAddForm()

    await userEvent.click(screen.getByRole('button', { name: bg.addBottle.scanBarcode }))

    expect(await screen.findByText(bg.addBottle.barcodeSuccess)).toBeInTheDocument()
    expect(lookupBarcode).toHaveBeenCalledWith(BARCODE)
    expect(screen.getByLabelText(bg.addBottle.barcodeLookup)).toHaveValue(BARCODE)
    await userEvent.click(screen.getByRole('button', { name: bg.addBottle.submit }))

    await vi.waitFor(() => expect(addBottle).toHaveBeenCalled())
    expect(addBottle).toHaveBeenCalledWith(expect.objectContaining({ productId: rum.id, barcode: BARCODE }))
  })

  it('drops the link a barcode made when another barcode is not found, so it is not given to that product', async () => {
    vi.mocked(lookupBarcode)
      .mockResolvedValueOnce(fromCatalog(catalogProduct({ name: 'Foursquare 2005 Vintage', category: 'Rum' })))
      .mockRejectedValueOnce(apiError(404, { error: 'Product not found.' }))
    await openAddForm()
    await findByBarcode(BARCODE)
    expect(await screen.findByText(bg.products.linkedChip)).toBeInTheDocument()

    await userEvent.clear(screen.getByLabelText(bg.addBottle.barcodeLookup))
    await findByBarcode('5000267014210')

    expect(await screen.findByText(bg.addBottle.barcodeNotFound)).toBeInTheDocument()
    expect(screen.queryByText(bg.products.linkedChip)).not.toBeInTheDocument()
  })

  it('drops the link a barcode made when the barcode is changed', async () => {
    vi.mocked(lookupBarcode).mockResolvedValue(fromCatalog(catalogProduct({ name: 'Foursquare 2005 Vintage', category: 'Rum' })))
    await openAddForm()
    await findByBarcode(BARCODE)
    expect(await screen.findByText(bg.products.linkedChip)).toBeInTheDocument()

    await userEvent.type(screen.getByLabelText(bg.addBottle.barcodeLookup), '9')

    expect(screen.queryByText(bg.products.linkedChip)).not.toBeInTheDocument()
    expect(screen.queryByText(bg.addBottle.barcodeSuccess)).not.toBeInTheDocument()
  })

  it.each([
    [400, bg.addBottle.barcodeInvalid],
    [500, bg.addBottle.barcodeError],
  ])('tells a refused lookup (%i) apart from a barcode nobody has', async (status, message) => {
    vi.mocked(lookupBarcode).mockRejectedValue(apiError(status, { error: 'refused' }))
    await openAddForm()

    await findByBarcode('1234567')

    expect(await screen.findByText(message)).toBeInTheDocument()
    expect(screen.queryByText(bg.addBottle.barcodeNotFound)).not.toBeInTheDocument()
  })

  it.each([1, 3])('says how many collectors gave the barcode to the product (%i)', async collectors => {
    const rum = catalogProduct({ name: 'Foursquare 2005 Vintage', category: 'Rum' })
    vi.mocked(lookupBarcode).mockResolvedValue({ source: 'Collectors', product: rum, suggestion: null, collectors })
    await openAddForm()

    await findByBarcode(BARCODE)

    expect(await screen.findByText(i18n.t('addBottle.barcodeFromCollectors', { count: collectors }))).toBeInTheDocument()
    expect(screen.getByText(bg.products.linkedChip)).toBeInTheDocument()
  })

  it('fills the form from other collectors\' bottles of a barcode the catalog lacks, without linking anything', async () => {
    const suggestion: BarcodeSuggestion = {
      name: 'Foursquare Exceptional Cask',
      category: 'Rum',
      distilleryId: DISTILLERY.id,
      distilleryName: DISTILLERY.name,
      age: 12,
      vintageYear: 2008,
      abvPercent: 62,
      volumeMl: 700,
      country: 'Barbados',
      region: 'Caribbean',
    }
    vi.mocked(lookupBarcode).mockResolvedValue({ source: 'Bottles', product: null, suggestion, collectors: 2 })
    await openAddForm()

    await findByBarcode(BARCODE)

    expect(await screen.findByText(i18n.t('addBottle.barcodeSuggested', { count: 2 }))).toBeInTheDocument()
    expect(screen.queryByText(bg.products.linkedChip)).not.toBeInTheDocument()
    expect(screen.getByLabelText(bg.addBottle.name)).toHaveValue('Foursquare Exceptional Cask')
    await userEvent.click(screen.getByRole('button', { name: bg.addBottle.submit }))

    await vi.waitFor(() => expect(addBottle).toHaveBeenCalled())
    expect(addBottle).toHaveBeenCalledWith(
      expect.objectContaining({
        name: 'Foursquare Exceptional Cask',
        productId: null,
        category: 'Rum',
        distilleryId: DISTILLERY.id,
        age: 12,
        vintageYear: 2008,
        abvPercent: 62,
        volumeMl: 700,
        country: 'Barbados',
        region: 'Caribbean',
        barcode: BARCODE,
      }),
    )
  })

  it('drops the link of an earlier barcode when the next one only has a suggestion', async () => {
    vi.mocked(lookupBarcode)
      .mockResolvedValueOnce(fromCatalog(catalogProduct({ name: 'Foursquare 2005 Vintage', category: 'Rum' })))
      .mockResolvedValueOnce({
        source: 'Bottles',
        product: null,
        suggestion: {
          name: 'Unknown Cask',
          category: 'Rum',
          distilleryId: null,
          distilleryName: null,
          age: null,
          vintageYear: null,
          abvPercent: null,
          volumeMl: null,
          country: null,
          region: null,
        },
        collectors: 1,
      })
    await openAddForm()
    await findByBarcode(BARCODE)
    expect(await screen.findByText(bg.products.linkedChip)).toBeInTheDocument()

    await userEvent.clear(screen.getByLabelText(bg.addBottle.barcodeLookup))
    await findByBarcode('5000267014210')

    expect(await screen.findByText(i18n.t('addBottle.barcodeSuggested', { count: 1 }))).toBeInTheDocument()
    expect(screen.queryByText(bg.products.linkedChip)).not.toBeInTheDocument()
    expect(screen.getByLabelText(bg.addBottle.name)).toHaveValue('Unknown Cask')
  })
})
