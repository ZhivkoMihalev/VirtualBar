import type { Page } from '@playwright/test'
import {
  bg,
  createBottle,
  expect,
  FLOW_TIMEOUT,
  registerCollector,
  test,
  uniqueName,
  untouchedProduct,
} from './support'

/**
 * Browsing /catalog: the NavBar link, the category filter, the sort control (#56), the URL as the page's state
 * (back/forward, shared links, typing that does not pile up history entries) and "show more". Reads the seeded
 * catalog; only the sort test links one bottle to a product, so the "most collected" order has a product to put first.
 */

const PAGE_SIZE = 24

const productCards = (page: Page) => page.locator('main a[href^="/product/"]')

const categoryPill = (page: Page, label: string) =>
  page.getByRole('group', { name: bg.catalog.categoryFilter }).getByRole('button', { name: label, exact: true })

const searchBox = (page: Page) => page.getByRole('searchbox', { name: bg.catalog.searchLabel })

const sortControl = (page: Page) => page.getByRole('combobox', { name: bg.catalog.sortLabel })

/** The collectors count on every card, in list order; a card with no collectors shows none, which is 0. */
async function ownersCounts(page: Page): Promise<number[]> {
  const texts = await productCards(page).allInnerTexts()
  return texts.map(text => Number(/(\d+) колекционер/.exec(text)?.[1] ?? 0))
}

/** At least one card is shown, and every card carries the badge of `category`. */
async function expectOnly(page: Page, category: string) {
  await expect(productCards(page).first()).toBeVisible()
  await expect(productCards(page).filter({ hasNotText: category })).toHaveCount(0)
}

async function openCatalogFromNavBar(page: Page) {
  await page.getByRole('link', { name: bg.nav.catalog, exact: true }).first().click()
  await expect(page).toHaveURL(/\/catalog$/)
}

test.describe('Catalog browsing', () => {
  test.describe.configure({ timeout: FLOW_TIMEOUT })

  test('the NavBar opens the catalog, and back and forward step through the category filters', async ({ openAs }) => {
    const page = await openAs(null)
    await page.goto('/')
    await openCatalogFromNavBar(page)
    await expect(page.getByRole('heading', { level: 1, name: bg.catalog.title })).toBeVisible()
    await expect(productCards(page)).toHaveCount(PAGE_SIZE)
    await expect(categoryPill(page, bg.catalog.allCategories)).toHaveAttribute('aria-pressed', 'true')

    await categoryPill(page, 'Rum').click()
    await expect(page).toHaveURL(/\/catalog\?category=Rum$/)
    await expectOnly(page, 'Rum')

    await categoryPill(page, 'Gin').click()
    await expect(page).toHaveURL(/\/catalog\?category=Gin$/)
    await expectOnly(page, 'Gin')
    await expect(categoryPill(page, 'Rum')).toHaveAttribute('aria-pressed', 'false')

    await page.goBack()
    await expect(page).toHaveURL(/\/catalog\?category=Rum$/)
    await expect(categoryPill(page, 'Rum')).toHaveAttribute('aria-pressed', 'true')
    await expectOnly(page, 'Rum')

    await page.goBack()
    await expect(page).toHaveURL(/\/catalog$/)
    await expect(categoryPill(page, bg.catalog.allCategories)).toHaveAttribute('aria-pressed', 'true')

    await page.goForward()
    await expect(page).toHaveURL(/\/catalog\?category=Rum$/)
    await expect(categoryPill(page, 'Rum')).toHaveAttribute('aria-pressed', 'true')
  })

  test('a shared link opens the same search; typing at full speed keeps every letter and adds no history entries', async ({ openAs }) => {
    const page = await openAs(null)
    await page.goto('/catalog?q=glen&category=Whisky')
    await expect(searchBox(page)).toHaveValue('glen')
    await expect(categoryPill(page, 'Whisky')).toHaveAttribute('aria-pressed', 'true')
    await expectOnly(page, 'Whisky')
    // The search matches the name, the brand or the distillery, and the card shows all three.
    await expect(productCards(page).filter({ hasNotText: /glen/i })).toHaveCount(0)

    await page.goto('/')
    await openCatalogFromNavBar(page)
    // Letter by letter, as fast as Playwright types: the box must keep every letter while the URL catches up (it used
    // to keep only the last one), and each keystroke replaces the entry, so Back leaves the catalog.
    await searchBox(page).pressSequentially('Lagavulin')
    await expect(page).toHaveURL(/\/catalog\?q=Lagavulin$/)

    await page.goBack()
    await expect(page).toHaveURL(/:5173\/$/)

    await page.goForward()
    await expect(page).toHaveURL(/\/catalog\?q=Lagavulin$/)
    await expect(searchBox(page)).toHaveValue('Lagavulin')
  })

  test('choosing an order sorts the list, keeps it in the URL, and back and forward restore it (#56)', async ({
    openAs,
  }) => {
    // At least one product with a collector, so "most collected" has something to put first.
    const [product, collector] = await Promise.all([untouchedProduct(), registerCollector('sorter')])
    await createBottle(collector, {
      name: `${product.name} ${uniqueName('Sort')}`,
      category: product.category,
      productId: product.id,
    })

    const page = await openAs(null)
    await page.goto('/catalog')
    await expect(productCards(page)).toHaveCount(PAGE_SIZE)
    await expect(sortControl(page)).toHaveText(bg.catalog.sortName)

    await sortControl(page).click()
    await page.getByRole('option', { name: bg.catalog.sortPopular }).click()

    await expect(page).toHaveURL(/\/catalog\?sort=Popular$/)
    await expect(sortControl(page)).toHaveText(bg.catalog.sortPopular)
    await expect(productCards(page).first()).toContainText(/\d+ колекционер/)
    const counts = await ownersCounts(page)
    expect(counts).toEqual([...counts].sort((a, b) => b - a))

    await page.goBack()
    await expect(page).toHaveURL(/\/catalog$/)
    await expect(sortControl(page)).toHaveText(bg.catalog.sortName)

    await page.goForward()
    await expect(page).toHaveURL(/\/catalog\?sort=Popular$/)
    await expect(sortControl(page)).toHaveText(bg.catalog.sortPopular)
  })

  test('"show more" appends the next page of the catalog', async ({ openAs }) => {
    const page = await openAs(null)
    await page.goto('/catalog')
    await expect(productCards(page)).toHaveCount(PAGE_SIZE)
    const firstCard = await productCards(page).first().getAttribute('href')

    await page.getByRole('button', { name: bg.catalog.loadMore }).click()

    await expect(productCards(page)).toHaveCount(2 * PAGE_SIZE)
    await expect(productCards(page).first()).toHaveAttribute('href', firstCard!)
    const links = await productCards(page).evaluateAll(cards => cards.map(card => card.getAttribute('href')))
    expect(new Set(links).size).toBe(2 * PAGE_SIZE)
  })
})
