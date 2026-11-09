import { screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { getNotifications } from '@/api/notificationsApi'
import { getProductBarcodes, removeProductBarcode } from '@/api/productBarcodesApi'
import i18n from '@/i18n'
import bg from '@/i18n/bg.json'
import ProductBarcodesAdminPage from '@/pages/ProductBarcodesAdminPage'
import type { ProductBarcode } from '@/types'
import { apiError, renderWithProviders, signedInUser, signIn } from '../testUtils'

vi.mock('@/api/messagesApi')
vi.mock('@/api/notificationsApi')
vi.mock('@/api/productBarcodesApi')

let sequence = 0

function pair(overrides: Partial<ProductBarcode> = {}): ProductBarcode {
  sequence++
  return {
    id: `00000000-0000-4000-b000-${String(sequence).padStart(12, '0')}`,
    barcode: '5000267014203',
    productId: `00000000-0000-4000-c000-${String(sequence).padStart(12, '0')}`,
    productName: 'Red Label',
    productBrand: 'Johnnie Walker',
    addedByUserId: 'collector-1',
    addedByDisplayName: 'Ivan',
    collectors: 1,
    conflict: false,
    createdAt: '2026-11-08T10:00:00Z',
    ...overrides,
  }
}

function openAsAdmin() {
  signIn({ ...signedInUser, isAdmin: true })
  vi.mocked(getNotifications).mockResolvedValue({ notifications: [], unreadCount: 0 })
  renderWithProviders(<ProductBarcodesAdminPage />, { route: '/admin/barcodes' })
}

describe('ProductBarcodesAdminPage (#49)', () => {
  beforeEach(() => {
    vi.mocked(removeProductBarcode).mockResolvedValue()
  })

  it('lists each pair with its product, who gave it, how many collectors back it and a conflict', async () => {
    vi.mocked(getProductBarcodes).mockResolvedValue({
      items: [pair({ collectors: 3, conflict: true }), pair({ barcode: '082184090466', productName: 'Old No. 7', productBrand: null })],
      hasMore: false,
    })

    openAsAdmin()

    const first = (await screen.findByText('5000267014203')).closest('[data-slot="card"]') as HTMLElement
    expect(within(first).getByRole('link', { name: 'Johnnie Walker · Red Label' })).toHaveAttribute(
      'href',
      expect.stringMatching(/^\/product\//),
    )
    expect(within(first).getByText(bg.productBarcodes.conflict)).toBeInTheDocument()
    expect(within(first).getByText(i18n.t('productBarcodes.collectors', { count: 3 }), { exact: false })).toBeInTheDocument()
    const second = screen.getByText('082184090466').closest('[data-slot="card"]') as HTMLElement
    expect(within(second).getByRole('link', { name: 'Old No. 7' })).toBeInTheDocument()
    expect(within(second).queryByText(bg.productBarcodes.conflict)).not.toBeInTheDocument()
    expect(getProductBarcodes).toHaveBeenCalledWith('', 1)
  })

  it('searches by the term the admin submits', async () => {
    vi.mocked(getProductBarcodes).mockResolvedValue({ items: [], hasMore: false })
    openAsAdmin()
    expect(await screen.findByText(bg.productBarcodes.empty)).toBeInTheDocument()

    await userEvent.type(screen.getByRole('textbox', { name: bg.productBarcodes.searchLabel }), ' 70142 ')
    await userEvent.click(screen.getByRole('button', { name: bg.productBarcodes.search }))

    await vi.waitFor(() => expect(getProductBarcodes).toHaveBeenCalledWith('70142', 1))
  })

  it('removes a pair after the admin confirms, and reloads the list', async () => {
    const wrong = pair()
    vi.mocked(getProductBarcodes).mockResolvedValueOnce({ items: [wrong], hasMore: false }).mockResolvedValue({ items: [], hasMore: false })
    openAsAdmin()

    await userEvent.click(await screen.findByRole('button', { name: bg.productBarcodes.remove }))
    const dialog = await screen.findByRole('alertdialog')
    expect(within(dialog).getByText(i18n.t('productBarcodes.removeText', { product: 'Red Label' }))).toBeInTheDocument()
    await userEvent.click(within(dialog).getByRole('button', { name: bg.productBarcodes.remove }))

    await vi.waitFor(() => expect(removeProductBarcode).toHaveBeenCalledWith(wrong.id))
    expect(await screen.findByText(bg.productBarcodes.empty)).toBeInTheDocument()
  })

  it('loads the next page on "show more"', async () => {
    vi.mocked(getProductBarcodes)
      .mockResolvedValueOnce({ items: [pair()], hasMore: true })
      .mockResolvedValueOnce({ items: [pair({ barcode: '082184090466' })], hasMore: false })
    openAsAdmin()

    await userEvent.click(await screen.findByRole('button', { name: bg.productBarcodes.showMore }))

    expect(await screen.findByText('082184090466')).toBeInTheDocument()
    expect(getProductBarcodes).toHaveBeenLastCalledWith('', 2)
    expect(screen.queryByRole('button', { name: bg.productBarcodes.showMore })).not.toBeInTheDocument()
  })

  it('says so when the list cannot be loaded', async () => {
    vi.mocked(getProductBarcodes).mockRejectedValue(apiError(500))

    openAsAdmin()

    expect(await screen.findByRole('alert')).toHaveTextContent(bg.productBarcodes.error)
  })
})
