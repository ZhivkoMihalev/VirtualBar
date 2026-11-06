import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { getNotifications } from '@/api/notificationsApi'
import { getProductEstimate } from '@/api/pricesApi'
import { getProduct, getProductReviews } from '@/api/productsApi'
import { addWishListItem, getWishList } from '@/api/wishListApi'
import i18n from '@/i18n'
import bg from '@/i18n/bg.json'
import ProductPage from '@/pages/ProductPage'
import type { ProductDetail } from '@/types'
import { listing, owner, paged, productDetail, review } from '../fixtures/products'
import { wishListItem } from '../fixtures/wishList'
import { apiError, deferred, renderWithProviders, signIn } from '../testUtils'

vi.mock('@/api/productsApi')
vi.mock('@/api/pricesApi')
vi.mock('@/api/notificationsApi')
vi.mock('@/api/bottlesApi')
vi.mock('@/api/wishListApi')

function renderProduct(detail: ProductDetail) {
  vi.mocked(getProduct).mockResolvedValue(detail)
  return renderWithProviders(<ProductPage />, { route: `/product/${detail.id}`, path: '/product/:id' })
}

/** Logs a collector in, with the NavBar's notifications, the price estimate and their own wish list answered. */
function signInCollector() {
  signIn()
  vi.mocked(getNotifications).mockResolvedValue({ notifications: [], unreadCount: 0 })
  vi.mocked(getProductEstimate).mockResolvedValue(null)
  vi.mocked(getWishList).mockResolvedValue([])
}

/** A section of the page, found by its heading once the product has loaded. */
const findSection = async (title: string) =>
  (await screen.findByRole('heading', { level: 2, name: title })).closest('section')!

/** Reviews labelled `prefix1` … `prefixN`, newest (highest number) first, as the API returns them. */
const reviewsNewestFirst = (prefix: string, from: number, to: number) =>
  Array.from({ length: from - to + 1 }, (_, i) => review(`${prefix}${from - i}`))

describe('ProductPage', () => {
  it('shows the product with its facts and description', async () => {
    renderProduct(
      productDetail({
        name: 'Caorunn Small Batch',
        brand: 'Caorunn',
        distilleryName: 'Balmenach',
        category: 'Gin',
        age: 12,
        abvPercent: 41.8,
        volumeMl: 700,
        region: 'Speyside',
        country: 'Scotland',
        description: 'Infused with five Celtic botanicals.',
      }),
    )

    expect(await screen.findByRole('heading', { level: 1, name: 'Caorunn Small Batch' })).toBeInTheDocument()
    expect(screen.getByText('Caorunn · Balmenach')).toBeInTheDocument()
    expect(screen.getByText(i18n.t('products.ageSuffix', { age: 12 }))).toBeInTheDocument()
    expect(screen.getByText('41.8%')).toBeInTheDocument()
    expect(screen.getByText(i18n.t('products.volumeSuffix', { volume: 700 }))).toBeInTheDocument()
    expect(screen.getByText('Speyside, Scotland')).toBeInTheDocument()
    expect(screen.getByText('Infused with five Celtic botanicals.')).toBeInTheDocument()
    expect(screen.getAllByRole('link', { name: bg.product.backToCatalog })[0]).toHaveAttribute('href', '/catalog')
  })

  it("sums up the community's average score, review count and top flavors", async () => {
    renderProduct(productDetail({ averageScore: 85.5, reviewsCount: 2, topFlavors: ['Smoky', 'Vanilla', 'Peaty'] }))

    const community = await findSection(bg.product.community)
    expect(community).toHaveTextContent('85.5')
    expect(community).toHaveTextContent(i18n.t('reviews.count', { count: 2 }))
    const flavors = within(community).getAllByText(/./, { selector: '[data-slot="badge"]' })
    expect(flavors.map(f => f.textContent)).toEqual([bg.flavors.Smoky, bg.flavors.Vanilla, bg.flavors.Peaty])
  })

  it('says when nobody has reviewed, sold or added the product yet', async () => {
    renderProduct(productDetail())

    expect(await screen.findByText(bg.product.noReviews)).toBeInTheDocument()
    expect(screen.getByText(bg.product.nobodySelling)).toBeInTheDocument()
    expect(screen.getByText(bg.product.noOwners)).toBeInTheDocument()
    expect(screen.queryByRole('heading', { level: 2, name: bg.product.reviews })).not.toBeInTheDocument()
  })

  it('lists the collectors with their bottle counts, and how many more own it', async () => {
    const stacked = owner({ displayName: 'Stacked Collector', bottleCount: 2 })
    const single = owner({ displayName: 'Single Collector', bottleCount: 1 })
    renderProduct(productDetail({ ownersCount: 25, owners: [stacked, single] }))

    const stackedLink = await screen.findByRole('link', { name: /Stacked Collector/ })
    expect(stackedLink).toHaveAttribute('href', `/bar/${stacked.userId}`)
    expect(stackedLink).toHaveTextContent('×2')
    expect(screen.getByRole('link', { name: /Single Collector/ })).not.toHaveTextContent('×')
    expect(screen.getByText(i18n.t('product.andMore', { count: 23 }))).toBeInTheDocument()
  })

  it('lists the bottles for sale with their price, per bottle when there are several', async () => {
    signInCollector()
    renderProduct(
      productDetail({
        listings: [
          listing({ sellerDisplayName: 'Stack Seller', askingPrice: 321, currency: 'EUR', forSaleQuantity: 2 }),
          listing({ sellerDisplayName: 'Quiet Seller', askingPrice: null, currency: null }),
        ],
      }),
    )

    const forSale = await findSection(bg.product.forSale)
    const [stack, quiet] = within(forSale).getAllByRole('listitem')
    expect(stack).toHaveTextContent('Stack Seller')
    expect(stack).toHaveTextContent('EUR 321')
    expect(stack).toHaveTextContent('×2')
    expect(stack).toHaveTextContent(bg.bottle.perBottle)
    expect(within(stack).getByRole('button', { name: bg.product.open })).toBeEnabled()
    expect(quiet).toHaveTextContent(bg.marketplace.priceOnRequest)
    expect(quiet).not.toHaveTextContent(bg.bottle.perBottle)
    expect(within(forSale).queryByRole('link', { name: bg.product.listingsLogin })).not.toBeInTheDocument()
  })

  it('shows a visitor only how many bottles are for sale, with a login button (#102)', async () => {
    // The API sends a visitor the count and no listings.
    renderProduct(productDetail({ listingsCount: 3, listings: [] }))

    const forSale = await findSection(bg.product.forSale)
    expect(forSale).toHaveTextContent(i18n.t('product.listingsCount', { count: 3 }))
    expect(within(forSale).getByRole('link', { name: bg.product.listingsLogin })).toHaveAttribute('href', '/login')
    expect(within(forSale).queryByRole('listitem')).not.toBeInTheDocument()
    expect(within(forSale).queryByRole('button', { name: bg.product.open })).not.toBeInTheDocument()
    expect(forSale).not.toHaveTextContent(bg.product.nobodySelling)
  })

  it('uses the singular for a visitor when one bottle is for sale', async () => {
    renderProduct(productDetail({ listingsCount: 1, listings: [] }))

    const forSale = await findSection(bg.product.forSale)
    expect(forSale).toHaveTextContent(bg.product.listingsCount_one.replace('{{count}}', '1'))
  })

  it('shows a visitor the empty text and no login button when nothing is for sale', async () => {
    renderProduct(productDetail())

    const forSale = await findSection(bg.product.forSale)
    expect(forSale).toHaveTextContent(bg.product.nobodySelling)
    expect(within(forSale).queryByRole('link', { name: bg.product.listingsLogin })).not.toBeInTheDocument()
  })

  it('shows the latest reviews, and "show more" switches to the full list without repeating any', async () => {
    renderProduct(productDetail({ reviewsCount: 7, latestReviews: reviewsNewestFirst('R', 7, 3) }))
    vi.mocked(getProductReviews).mockResolvedValue(paged(reviewsNewestFirst('R', 7, 1), false))

    expect(await screen.findByText('R7')).toBeInTheDocument()
    expect(screen.getByText('R3')).toBeInTheDocument()
    expect(screen.queryByText('R2')).not.toBeInTheDocument()
    expect(getProductReviews).not.toHaveBeenCalled()

    await userEvent.click(screen.getByRole('button', { name: bg.product.showMore }))

    expect(await screen.findByText('R1')).toBeInTheDocument()
    for (let i = 1; i <= 7; i++) expect(screen.getAllByText(`R${i}`)).toHaveLength(1)
    expect(getProductReviews).toHaveBeenCalledExactlyOnceWith(expect.any(String), 1, 10)
    expect(screen.queryByRole('button', { name: bg.product.showMore })).not.toBeInTheDocument()
  })

  it('keeps paging through the reviews while there are more', async () => {
    const detail = productDetail({ reviewsCount: 25, latestReviews: reviewsNewestFirst('R', 25, 21) })
    renderProduct(detail)
    vi.mocked(getProductReviews)
      .mockResolvedValueOnce(paged(reviewsNewestFirst('R', 25, 16), true))
      .mockResolvedValueOnce(paged(reviewsNewestFirst('R', 15, 6), true))

    await userEvent.click(await screen.findByRole('button', { name: bg.product.showMore }))
    expect(await screen.findByText('R16')).toBeInTheDocument()

    await userEvent.click(screen.getByRole('button', { name: bg.product.showMore }))

    expect(await screen.findByText('R6')).toBeInTheDocument()
    expect(screen.getAllByText(/^R\d+$/)).toHaveLength(20)
    expect(getProductReviews).toHaveBeenLastCalledWith(detail.id, 2, 10)
    expect(screen.getByRole('button', { name: bg.product.showMore })).toBeInTheDocument()
  })

  it('shows no price estimate to a visitor and never asks for one', async () => {
    renderProduct(productDetail({ name: 'Anonymous view' }))

    await screen.findByRole('heading', { level: 1, name: 'Anonymous view' })
    expect(screen.queryByText(bg.collectionValue.estimateLabel)).not.toBeInTheDocument()
    expect(getProductEstimate).not.toHaveBeenCalled()
  })

  it('shows the cached price estimate to a logged-in collector', async () => {
    signInCollector()
    const detail = productDetail({ name: 'Logged-in view' })

    renderProduct(detail)

    expect(await screen.findByText(bg.collectionValue.estimateLabel)).toBeInTheDocument()
    expect(getProductEstimate).toHaveBeenCalledExactlyOnceWith(detail.id)
  })

  describe('years (#59)', () => {
    const yearBoxes = (section: HTMLElement) =>
      within(section)
        .getAllByRole('listitem')
        .map(item => item.querySelector('.font-heading')?.textContent)

    it("shows a vintage's badge and the other vintages of its line, oldest first, with this one in its place", async () => {
      renderProduct(
        productDetail({
          name: 'Millésime 1988',
          vintageYear: 1988,
          averageScore: 90,
          otherYears: [
            { id: 'vintage-1976', name: 'Millésime 1976', year: 1976, averageScore: 88.5, listingsCount: 2 },
            { id: 'vintage-1990', name: 'Millésime 1990', year: 1990, averageScore: null, listingsCount: 0 },
          ],
        }),
      )

      expect(await screen.findByText(i18n.t('product.vintageBadge', { year: 1988 }))).toBeInTheDocument()
      const line = await findSection(bg.product.otherVintages)
      expect(yearBoxes(line)).toEqual(['1976', '1988', '1990'])

      const older = within(line).getByRole('link', { name: 'Millésime 1976' })
      expect(older).toHaveAttribute('href', '/product/vintage-1976')
      expect(older).toHaveTextContent('★ 88.5')
      expect(older).toHaveTextContent(i18n.t('catalog.forSale', { count: 2 }))
      expect(within(line).getByRole('link', { name: 'Millésime 1990' })).not.toHaveTextContent('★')

      const current = line.querySelector('[aria-current="page"]')!
      expect(current).toHaveTextContent('1988')
      expect(current).toHaveTextContent(bg.product.thisYear)
      expect(within(line).queryByRole('link', { name: 'Millésime 1988' })).not.toBeInTheDocument()
    })

    it('calls the line of an edition its other editions', async () => {
      renderProduct(
        productDetail({
          name: 'Classic Cut 2020 Edition',
          releaseYear: 2020,
          otherYears: [{ id: 'cut-2021', name: 'Classic Cut 2021 Edition', year: 2021, averageScore: null, listingsCount: 0 }],
        }),
      )

      expect(await screen.findByText(i18n.t('product.releaseBadge', { year: 2020 }))).toBeInTheDocument()
      expect(await findSection(bg.product.otherReleases)).toBeInTheDocument()
      expect(screen.queryByText(i18n.t('product.vintageBadge', { year: 2020 }))).not.toBeInTheDocument()
    })

    it('shows the year but no line for a dated product alone in its line', async () => {
      renderProduct(productDetail({ name: 'Enmore 1988', vintageYear: 1988 }))

      expect(await screen.findByText(i18n.t('product.vintageBadge', { year: 1988 }))).toBeInTheDocument()
      expect(screen.queryByRole('heading', { name: bg.product.otherVintages })).not.toBeInTheDocument()
    })

    it('shows no year and no line for an undated product', async () => {
      renderProduct(productDetail({ name: 'VSOP' }))

      await screen.findByRole('heading', { level: 1, name: 'VSOP' })
      expect(screen.queryByText(/^(Реколта|Издание) \d{4}$/)).not.toBeInTheDocument()
      expect(screen.queryByRole('heading', { name: bg.product.otherVintages })).not.toBeInTheDocument()
      expect(screen.queryByRole('heading', { name: bg.product.otherReleases })).not.toBeInTheDocument()
    })
  })

  describe('"want it" (#51)', () => {
    /** Renders the page for a logged-in collector whose own wish list holds `wishes`. */
    function renderLoggedIn(detail: ProductDetail, wishes = [wishListItem()]) {
      signInCollector()
      vi.mocked(getWishList).mockResolvedValue(wishes)
      return renderProduct(detail)
    }

    /** The button, once the wish list has loaded: until then it is disabled, so a duplicate cannot slip in. */
    async function findWantIt() {
      const button = await screen.findByRole('button', { name: bg.product.wantIt })
      await waitFor(() => expect(button).toBeEnabled())
      return button
    }

    it('is not offered to a visitor, and no wish list is read', async () => {
      renderProduct(productDetail({ name: 'Anonymous view' }))

      await screen.findByRole('heading', { level: 1, name: 'Anonymous view' })
      expect(screen.queryByRole('button', { name: bg.product.wantIt })).not.toBeInTheDocument()
      expect(getWishList).not.toHaveBeenCalled()
    })

    it('adds the product to the wish list, then says it is there', async () => {
      const detail = productDetail()
      const adding = deferred<Awaited<ReturnType<typeof addWishListItem>>>()
      vi.mocked(addWishListItem).mockReturnValue(adding.promise)
      renderLoggedIn(detail)

      await userEvent.click(await findWantIt())

      expect(addWishListItem).toHaveBeenCalledExactlyOnceWith({ productId: detail.id })
      expect(screen.getByRole('button', { name: bg.product.wantItAdding })).toBeDisabled()

      vi.mocked(getWishList).mockResolvedValue([wishListItem({ productId: detail.id, category: null })])
      adding.resolve(wishListItem({ productId: detail.id, category: null }))

      expect(await screen.findByText(bg.product.wanted)).toBeInTheDocument()
      expect(screen.queryByRole('button', { name: bg.product.wantIt })).not.toBeInTheDocument()
    })

    it('says the product is already on the wish list instead of offering it again', async () => {
      const detail = productDetail()
      renderLoggedIn(detail, [wishListItem(), wishListItem({ productId: detail.id, category: null })])

      expect(await screen.findByText(bg.product.wanted)).toBeInTheDocument()
      expect(screen.queryByRole('button', { name: bg.product.wantIt })).not.toBeInTheDocument()
    })

    it('still offers it when only a wish for another product is on the list', async () => {
      renderLoggedIn(productDetail(), [wishListItem({ productId: 'another-product', category: null })])

      await findWantIt()
      expect(screen.queryByText(bg.product.wanted)).not.toBeInTheDocument()
    })

    it('says when adding fails, and lets the collector try again', async () => {
      vi.mocked(addWishListItem).mockRejectedValue(apiError(500))
      renderLoggedIn(productDetail())

      await userEvent.click(await findWantIt())

      expect(await screen.findByText(bg.product.wantItError)).toBeInTheDocument()
      expect(screen.getByRole('button', { name: bg.product.wantIt })).toBeEnabled()
    })
  })

  it('says the product is not found, and does not retry', async () => {
    vi.mocked(getProduct).mockRejectedValue(apiError(404))

    renderWithProviders(<ProductPage />, { route: '/product/missing', path: '/product/:id' })

    expect(await screen.findByRole('heading', { level: 1, name: bg.product.notFound })).toBeInTheDocument()
    expect(screen.getByText(bg.product.notFoundHint)).toBeInTheDocument()
    expect(getProduct).toHaveBeenCalledExactlyOnceWith('missing')
  })

  it('retries any other error three times before saying the product could not load', async () => {
    vi.mocked(getProduct).mockRejectedValue(apiError(500))

    renderWithProviders(<ProductPage />, { route: '/product/broken', path: '/product/:id' })

    expect(await screen.findByText(bg.product.error)).toBeInTheDocument()
    expect(screen.queryByRole('heading', { level: 1, name: bg.product.notFound })).not.toBeInTheDocument()
    expect(getProduct).toHaveBeenCalledTimes(4)
  })
})
