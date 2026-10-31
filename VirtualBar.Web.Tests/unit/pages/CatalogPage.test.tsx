import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { browseProducts } from '@/api/productsApi'
import i18n from '@/i18n'
import bg from '@/i18n/bg.json'
import CatalogPage from '@/pages/CatalogPage'
import { paged, productCard } from '../fixtures/products'
import { networkError, renderWithProviders } from '../testUtils'

vi.mock('@/api/productsApi')

const PAGE_SIZE = 24

function renderCatalog(route = '/catalog') {
  return renderWithProviders(<CatalogPage />, { route })
}

const cards = () => screen.queryAllByRole('link').filter(link => link.getAttribute('href')?.startsWith('/product/'))

const pill = (label: string) =>
  within(screen.getByRole('group', { name: bg.catalog.categoryFilter })).getByRole('button', { name: label })

/** The search box debounces for 300 ms; wait past that before saying a term was never sent. */
const pastTheDebounce = () => new Promise(resolve => setTimeout(resolve, 400))

describe('CatalogPage', () => {
  it('lists the whole catalog when nothing is typed', async () => {
    const lagavulin = productCard({ name: 'Lagavulin 16' })
    vi.mocked(browseProducts).mockResolvedValue(paged([lagavulin, productCard({ name: 'Talisker 10' })]))

    renderCatalog()

    expect(await screen.findByRole('link', { name: /Lagavulin 16/ })).toHaveAttribute('href', `/product/${lagavulin.id}`)
    expect(cards()).toHaveLength(2)
    expect(browseProducts).toHaveBeenCalledExactlyOnceWith({
      search: undefined,
      category: undefined,
      page: 1,
      pageSize: PAGE_SIZE,
    })
  })

  it('names the distillery after the brand only when it is a different name', async () => {
    vi.mocked(browseProducts).mockResolvedValue(
      paged([
        productCard({ name: 'Caorunn Gin', brand: 'Caorunn', distilleryName: 'Balmenach', category: 'Gin' }),
        productCard({ name: 'Lagavulin 16', brand: 'Lagavulin', distilleryName: 'Lagavulin' }),
      ]),
    )

    renderCatalog()

    expect(await screen.findByRole('link', { name: /Caorunn Gin/ })).toHaveTextContent('Caorunn · Balmenach')
    expect(screen.getByRole('link', { name: /Lagavulin 16/ })).not.toHaveTextContent('·')
  })

  it('shows the community numbers on a card, and nothing on a product nobody has touched', async () => {
    vi.mocked(browseProducts).mockResolvedValue(
      paged([
        productCard({ name: 'Popular', averageScore: 91.5, reviewsCount: 3, ownersCount: 2, listingsCount: 1 }),
        productCard({ name: 'Untouched' }),
      ]),
    )

    renderCatalog()

    const popular = await screen.findByRole('link', { name: /Popular/ })
    expect(popular).toHaveTextContent('★ 91.5')
    expect(popular).toHaveTextContent(`(${i18n.t('catalog.reviews', { count: 3 })})`)
    expect(popular).toHaveTextContent(i18n.t('catalog.owners', { count: 2 }))
    expect(popular).toHaveTextContent(i18n.t('catalog.forSale', { count: 1 }))

    const untouched = screen.getByRole('link', { name: /Untouched/ })
    expect(untouched).not.toHaveTextContent('★')
    expect(untouched).not.toHaveTextContent(i18n.t('catalog.owners', { count: 0 }))
  })

  it('never sends a single character, and says why', async () => {
    vi.mocked(browseProducts).mockResolvedValue(paged([productCard()]))
    renderCatalog()
    await screen.findByRole('link', { name: /Lagavulin/ })

    await userEvent.type(screen.getByRole('searchbox', { name: bg.catalog.searchLabel }), 'L')
    await pastTheDebounce()

    expect(screen.getByText(bg.catalog.minChars)).toBeInTheDocument()
    expect(browseProducts).toHaveBeenCalledTimes(1)
    expect(browseProducts).not.toHaveBeenCalledWith(expect.objectContaining({ search: 'L' }))
  })

  it('searches once the typed term settles, and keeps the term in the URL', async () => {
    vi.mocked(browseProducts).mockResolvedValue(paged([productCard()]))
    const { location } = renderCatalog()
    await screen.findByRole('link', { name: /Lagavulin/ })

    await userEvent.type(screen.getByRole('searchbox', { name: bg.catalog.searchLabel }), 'La')

    await waitFor(() =>
      expect(browseProducts).toHaveBeenLastCalledWith({ search: 'La', category: undefined, page: 1, pageSize: PAGE_SIZE }),
    )
    expect(location()).toBe('/catalog?q=La')
    expect(screen.queryByText(bg.catalog.minChars)).not.toBeInTheDocument()
  })

  it('opens a shared link with its search and category', async () => {
    vi.mocked(browseProducts).mockResolvedValue(paged([productCard({ name: 'Plantation Rum', category: 'Rum' })]))

    renderCatalog('/catalog?q=plan&category=Rum')

    expect(await screen.findByRole('link', { name: /Plantation Rum/ })).toBeInTheDocument()
    expect(screen.getByRole('searchbox', { name: bg.catalog.searchLabel })).toHaveValue('plan')
    expect(pill('Rum')).toHaveAttribute('aria-pressed', 'true')
    expect(pill(bg.catalog.allCategories)).toHaveAttribute('aria-pressed', 'false')
    expect(browseProducts).toHaveBeenCalledWith({ search: 'plan', category: 'Rum', page: 1, pageSize: PAGE_SIZE })
  })

  it('ignores a category the app does not know', async () => {
    vi.mocked(browseProducts).mockResolvedValue(paged([productCard()]))

    renderCatalog('/catalog?category=Beer')

    await screen.findByRole('link', { name: /Lagavulin/ })
    expect(pill(bg.catalog.allCategories)).toHaveAttribute('aria-pressed', 'true')
    expect(browseProducts).toHaveBeenCalledWith(expect.objectContaining({ category: undefined }))
  })

  it('filters by a category, and a second click on it shows every category again', async () => {
    vi.mocked(browseProducts).mockResolvedValue(paged([productCard()]))
    const { location } = renderCatalog()
    await screen.findByRole('link', { name: /Lagavulin/ })

    await userEvent.click(pill('Rum'))

    await waitFor(() => expect(browseProducts).toHaveBeenLastCalledWith(expect.objectContaining({ category: 'Rum' })))
    expect(location()).toBe('/catalog?category=Rum')
    expect(pill('Rum')).toHaveAttribute('aria-pressed', 'true')

    await userEvent.click(pill('Rum'))

    expect(location()).toBe('/catalog')
    expect(pill('Rum')).toHaveAttribute('aria-pressed', 'false')
    expect(pill(bg.catalog.allCategories)).toHaveAttribute('aria-pressed', 'true')
  })

  it('appends the next page with "show more" until the last one', async () => {
    vi.mocked(browseProducts)
      .mockResolvedValueOnce(paged([productCard({ name: 'First' }), productCard({ name: 'Second' })], true))
      .mockResolvedValueOnce(paged([productCard({ name: 'Third' })], false))
    renderCatalog()
    await screen.findByRole('link', { name: /First/ })

    await userEvent.click(screen.getByRole('button', { name: bg.catalog.loadMore }))

    expect(await screen.findByRole('link', { name: /Third/ })).toBeInTheDocument()
    expect(cards()).toHaveLength(3)
    expect(browseProducts).toHaveBeenLastCalledWith({
      search: undefined,
      category: undefined,
      page: 2,
      pageSize: PAGE_SIZE,
    })
    expect(screen.queryByRole('button', { name: bg.catalog.loadMore })).not.toBeInTheDocument()
  })

  it('says so when nothing matches', async () => {
    vi.mocked(browseProducts).mockResolvedValue(paged([]))

    renderCatalog('/catalog?q=zzzz')

    expect(await screen.findByText(bg.catalog.empty)).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: bg.catalog.loadMore })).not.toBeInTheDocument()
  })

  it('says so when the catalog cannot load', async () => {
    vi.mocked(browseProducts).mockRejectedValue(networkError())

    renderCatalog()

    expect(await screen.findByText(bg.catalog.error)).toBeInTheDocument()
    expect(cards()).toHaveLength(0)
  })
})
