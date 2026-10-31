import type { Page } from '@playwright/test'
import { bg, expect, FLOW_TIMEOUT, test } from './support'

/**
 * Browsing /catalog: the NavBar link, the category filter, the URL as the page's state (back/forward, shared links,
 * typing that does not pile up history entries) and "show more". Reads the seeded catalog only, so it creates no data.
 */

const PAGE_SIZE = 24

const productCards = (page: Page) => page.locator('main a[href^="/product/"]')

const categoryPill = (page: Page, label: string) =>
  page.getByRole('group', { name: bg.catalog.categoryFilter }).getByRole('button', { name: label, exact: true })

const searchBox = (page: Page) => page.getByRole('searchbox', { name: bg.catalog.searchLabel })

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
