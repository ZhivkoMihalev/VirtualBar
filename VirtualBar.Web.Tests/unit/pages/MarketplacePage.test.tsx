import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { getMarketplace } from '@/api/bottlesApi'
import { getDistilleries } from '@/api/distilleriesApi'
import { getNotifications } from '@/api/notificationsApi'
import { searchProducts } from '@/api/productsApi'
import { addWishListItem, getAllWishListItems } from '@/api/wishListApi'
import bg from '@/i18n/bg.json'
import MarketplacePage from '@/pages/MarketplacePage'
import type { PublicWishListItem } from '@/types'
import { productCard } from '../fixtures/products'
import { publicWishListItem, wishListItem } from '../fixtures/wishList'
import { renderWithProviders, signIn } from '../testUtils'

vi.mock('@/api/bottlesApi')
vi.mock('@/api/distilleriesApi')
vi.mock('@/api/messagesApi')
vi.mock('@/api/notificationsApi')
vi.mock('@/api/productsApi')
vi.mock('@/api/wishListApi')

// The product picker is a Radix popover around a cmdk list. jsdom has neither layout nor pointer capture, so give
// both what they call; the picker itself behaves as in the browser.
beforeEach(() => {
  globalThis.ResizeObserver ??= class {
    observe() {}
    unobserve() {}
    disconnect() {}
  }
  Element.prototype.scrollIntoView ??= () => {}
  Element.prototype.hasPointerCapture ??= () => false
})

/** Opens the marketplace on its "Looking for" tab, listing `wishes`. */
async function openLookingFor(wishes: PublicWishListItem[]) {
  vi.mocked(getMarketplace).mockResolvedValue([])
  vi.mocked(getAllWishListItems).mockResolvedValue(wishes)
  renderWithProviders(<MarketplacePage />, { route: '/marketplace' })
  await userEvent.click(screen.getByRole('tab', { name: bg.marketplace.tabSearch }))
}

describe('MarketplacePage — wish list by product (#51)', () => {
  describe('the "Looking for" tab', () => {
    it('titles a product wish with no label of its own by the product, linked to its page', async () => {
      await openLookingFor([
        publicWishListItem({ productId: 'lagavulin-16', productName: 'Lagavulin 16', category: null }),
      ])

      expect(await screen.findByRole('link', { name: 'Lagavulin 16' })).toHaveAttribute(
        'href',
        '/product/lagavulin-16',
      )
    })

    it("keeps the collector's own label as the title and links the product next to it", async () => {
      await openLookingFor([
        publicWishListItem({
          bottleName: 'A birthday dram',
          productId: 'lagavulin-16',
          productName: 'Lagavulin 16',
          category: null,
        }),
      ])

      expect(await screen.findByText('A birthday dram')).toBeInTheDocument()
      expect(screen.queryByRole('link', { name: 'A birthday dram' })).not.toBeInTheDocument()
      expect(screen.getByRole('link', { name: 'Lagavulin 16' })).toHaveAttribute('href', '/product/lagavulin-16')
    })

    it('links no product once it has left the catalog', async () => {
      await openLookingFor([
        publicWishListItem({ bottleName: 'An old wish', productId: 'removed', productName: null, category: null }),
      ])

      expect(await screen.findByText('An old wish')).toBeInTheDocument()
      const hrefs = screen.getAllByRole('link').map(link => link.getAttribute('href'))
      expect(hrefs).not.toContain('/product/removed')
    })
  })

  describe('posting a search', () => {
    const lagavulin = productCard({ id: 'lagavulin-16', name: 'Lagavulin 16' })

    /** Opens the "post a search" dialog as a logged-in collector and returns it. */
    async function openPublishDialog() {
      signIn()
      vi.mocked(getNotifications).mockResolvedValue({ notifications: [], unreadCount: 0 })
      vi.mocked(getDistilleries).mockResolvedValue([])
      vi.mocked(searchProducts).mockResolvedValue([lagavulin])
      vi.mocked(addWishListItem).mockResolvedValue(wishListItem({ productId: lagavulin.id }))
      await openLookingFor([])
      await userEvent.click(screen.getByRole('button', { name: bg.wishList.publishSearch }))
      return screen.findByRole('dialog')
    }

    async function pickProduct(dialog: HTMLElement) {
      await userEvent.click(within(dialog).getByRole('combobox', { name: bg.products.searchLabel }))
      await userEvent.type(await screen.findByPlaceholderText(bg.products.searchPlaceholder), 'Laga')
      await userEvent.click(await screen.findByRole('option', { name: /Lagavulin 16/ }))
    }

    it('posts a wish for the picked product alone, without a distillery or a category', async () => {
      const dialog = await openPublishDialog()

      await pickProduct(dialog)

      expect(within(dialog).getByText(bg.wishList.productChip)).toBeInTheDocument()
      expect(within(dialog).getByText(bg.wishList.productHint)).toBeInTheDocument()
      expect(within(dialog).queryByText(bg.wishList.distillery)).not.toBeInTheDocument()
      expect(within(dialog).queryByText(bg.wishList.category)).not.toBeInTheDocument()

      await userEvent.click(within(dialog).getByRole('button', { name: bg.wishList.addBtn }))

      await waitFor(() =>
        expect(addWishListItem).toHaveBeenCalledExactlyOnceWith({
          bottleName: undefined,
          productId: lagavulin.id,
          distilleryId: null,
          category: undefined,
          imageUrl: undefined,
        }),
      )
    })

    it('brings the distillery and the category back when the product is removed', async () => {
      const dialog = await openPublishDialog()
      await pickProduct(dialog)

      await userEvent.click(within(dialog).getByRole('button', { name: bg.wishList.removeProduct }))

      expect(within(dialog).queryByText(bg.wishList.productChip)).not.toBeInTheDocument()
      expect(within(dialog).getByText(bg.wishList.distillery)).toBeInTheDocument()
      expect(within(dialog).getByText(bg.wishList.category)).toBeInTheDocument()
    })

    it('refuses a wish with no product, distillery or category', async () => {
      const dialog = await openPublishDialog()

      await userEvent.click(within(dialog).getByRole('button', { name: bg.wishList.addBtn }))

      expect(await within(dialog).findByText(bg.wishList.atLeastOne)).toBeInTheDocument()
      expect(addWishListItem).not.toHaveBeenCalled()
    })
  })
})
