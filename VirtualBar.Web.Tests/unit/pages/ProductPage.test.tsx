import { screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { getNotifications } from '@/api/notificationsApi'
import { getProductEstimate } from '@/api/pricesApi'
import { getProduct, getProductReviews } from '@/api/productsApi'
import i18n from '@/i18n'
import bg from '@/i18n/bg.json'
import ProductPage from '@/pages/ProductPage'
import type { ProductDetail } from '@/types'
import { listing, owner, paged, productDetail, review } from '../fixtures/products'
import { apiError, renderWithProviders, signIn } from '../testUtils'

vi.mock('@/api/productsApi')
vi.mock('@/api/pricesApi')
vi.mock('@/api/notificationsApi')
vi.mock('@/api/bottlesApi')

function renderProduct(detail: ProductDetail) {
  vi.mocked(getProduct).mockResolvedValue(detail)
  return renderWithProviders(<ProductPage />, { route: `/product/${detail.id}`, path: '/product/:id' })
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
    signIn()
    vi.mocked(getNotifications).mockResolvedValue({ notifications: [], unreadCount: 0 })
    vi.mocked(getProductEstimate).mockResolvedValue(null)
    const detail = productDetail({ name: 'Logged-in view' })

    renderProduct(detail)

    expect(await screen.findByText(bg.collectionValue.estimateLabel)).toBeInTheDocument()
    expect(getProductEstimate).toHaveBeenCalledExactlyOnceWith(detail.id)
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
