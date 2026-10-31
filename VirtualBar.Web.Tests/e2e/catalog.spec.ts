import { test, expect, type Page } from '@playwright/test'

/**
 * E2E coverage for the public catalog (/catalog) and the product page (/product/:id), issue #50.
 * Plan: docs/features/product-page/07-tests-and-docs.md.
 *
 * Real app: Vite dev server (:5173, started by Playwright's webServer config) against the running
 * backend API (:5000) and its seeded catalog. Throwaway users are registered through the API and the
 * session is seeded into localStorage before navigation — the pattern from reviews.spec.ts.
 *
 * The local database keeps whatever earlier runs left behind (owners, reviews, listings on the same
 * product), so nothing here asserts an exact count. Each run instead writes data with a unique marker
 * and asserts that marker shows up.
 */

const API = 'http://localhost:5000/api'

// ---- Exact expected UI strings (from src/i18n/bg.json) ----
const BG = {
  catalogTitle: 'Каталог на напитките',
  searchLabel: 'Търсене в каталога',
  minChars: 'Въведете поне 2 символа',
  backToCatalog: '← Каталог',
  estimateLabel: 'Ориентировъчна пазарна стойност',
  forSale: 'За продан',
  open: 'Отвори',
  viewProduct: 'Виж продукта в каталога →',
  makeOffer: 'Направи оферта',
  notFound: 'Продуктът не е намерен',
}

/** A brand with a few dozen seeded Whisky products, so the search has something real to narrow. */
const BRAND = 'Lagavulin'

const sleep = (ms: number) => new Promise(r => setTimeout(r, ms))

type Session = { id: string; token: string; user: unknown; displayName: string }

type CatalogCard = { id: string; name: string; brand: string | null; ownersCount: number }

/** The product page lists the top 20 owners; below this, a new owner is always among them. */
const VISIBLE_OWNERS = 20

// The /api/auth policy is a fixed window of 10 req/min per IP (Program.cs). Registrations can 429 if
// the suite is run repeatedly within a minute — wait out the window and retry.
async function registerUser(tag: string): Promise<Session> {
  for (let attempt = 0; ; attempt++) {
    const suffix = `${Date.now()}-${Math.random().toString(36).slice(2, 7)}`
    const email = `cat-${tag}-${suffix}@test.com`
    const displayName = `Cat-${tag}-${Math.random().toString(36).slice(2, 6)}`
    const res = await fetch(`${API}/auth/register`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ email, password: 'TestPass123', displayName }),
    })
    if (res.ok) {
      const body = await res.json()
      return { id: body.user.id, token: body.token, user: body.user, displayName }
    }
    if (res.status === 429 && attempt < 6) {
      await sleep(11_000)
      continue
    }
    throw new Error(`register(${tag}) failed: ${res.status} ${await res.text()}`)
  }
}

/**
 * A seeded product of {@link BRAND}, read from the same endpoint the page calls. With
 * `roomForOwner`, one whose owner list a new collector is sure to appear in, even on a local database
 * that keeps the owners earlier runs added.
 */
async function pickProduct(roomForOwner = false): Promise<CatalogCard> {
  const res = await fetch(`${API}/products/browse?search=${BRAND}&category=Whisky&pageSize=48`)
  if (!res.ok) throw new Error(`browse failed: ${res.status} ${await res.text()}`)
  const page: { items: CatalogCard[] } = await res.json()
  const product = page.items.find(p => !roomForOwner || p.ownersCount < VISIBLE_OWNERS - 1)
  if (!product) throw new Error(`no usable seeded ${BRAND} product — is the catalog seeded?`)
  return product
}

async function api(token: string, method: string, path: string, body: unknown) {
  const res = await fetch(`${API}${path}`, {
    method,
    headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${token}` },
    body: JSON.stringify(body),
  })
  if (!res.ok) throw new Error(`${method} ${path} failed: ${res.status} ${await res.text()}`)
  return res.status === 204 ? null : res.json().catch(() => null)
}

async function seedSession(page: Page, auth: Session | null) {
  await page.addInitScript(
    ({ auth }) => {
      localStorage.setItem('vbar_lang', 'bg')
      if (auth) {
        localStorage.setItem('vbar_token', auth.token)
        localStorage.setItem('vbar_user', JSON.stringify(auth.user))
      } else {
        localStorage.removeItem('vbar_token')
        localStorage.removeItem('vbar_user')
      }
    },
    { auth },
  )
}

/** Collect console errors + uncaught page errors. Registered BEFORE navigation. */
function captureConsole(page: Page) {
  const issues: string[] = []
  page.on('console', msg => {
    if (msg.type() === 'error') issues.push(`[console.error] ${msg.text()}`)
  })
  page.on('pageerror', err => issues.push(`[pageerror] ${err.message}`))
  return issues
}

/** Infra noise unrelated to the feature (favicon), and the 404s a not-found test triggers on purpose. */
function appIssues(issues: string[], allow404 = false): string[] {
  return issues.filter(i => !/favicon/i.test(i) && !(allow404 && /404/.test(i)))
}

const productLinks = (page: Page) => page.locator('main a[href^="/product/"]')

// =====================================================================================

test.describe('Public catalog and product page', () => {
  test('anonymous · browse → search → open product → no price estimate', async ({ page }) => {
    const product = await pickProduct()
    const issues = captureConsole(page)
    await seedSession(page, null)

    await page.goto('/catalog')
    await expect(page.getByRole('heading', { name: BG.catalogTitle })).toBeVisible()

    // The whole catalog is listed without a search term.
    await expect(productLinks(page).first()).toBeVisible()

    // One character is never sent: the hint shows and the list stays.
    const search = page.getByRole('searchbox', { name: BG.searchLabel })
    await search.fill(BRAND.slice(0, 1))
    await expect(page.getByText(BG.minChars)).toBeVisible()
    await expect(productLinks(page).first()).toBeVisible()

    // A real term narrows to the brand, and the URL carries it so the view can be shared.
    await search.fill(BRAND)
    await expect(page).toHaveURL(/[?&]q=Lagavulin/)
    const card = page.locator(`main a[href="/product/${product.id}"]`)
    await expect(card).toBeVisible({ timeout: 10_000 })

    await card.click()
    await expect(page).toHaveURL(new RegExp(`/product/${product.id}$`))
    await expect(page.getByRole('heading', { level: 1, name: product.name })).toBeVisible()

    // /api/prices is [Authorize]; an anonymous visitor must not see (or trigger) the estimate.
    await expect(page.getByText(BG.estimateLabel)).toHaveCount(0)
    await expect(page).toHaveURL(new RegExp(`/product/${product.id}$`))

    await page.getByRole('link', { name: BG.backToCatalog }).first().click()
    await expect(page).toHaveURL(/\/catalog/)

    expect(appIssues(issues)).toEqual([])
  })

  test('anonymous · unknown product shows "not found" instead of redirecting', async ({ page }) => {
    const issues = captureConsole(page)
    await seedSession(page, null)

    await page.goto('/product/00000000-0000-0000-0000-000000000001')
    await expect(page.getByRole('heading', { name: BG.notFound })).toBeVisible()
    await expect(page).toHaveURL(/\/product\/00000000-0000-0000-0000-000000000001$/)

    expect(appIssues(issues, true)).toEqual([])
  })

  test('logged in · a linked, reviewed, listed bottle shows on the product page', async ({ page }) => {
    test.setTimeout(120_000)
    const product = await pickProduct(true)
    const owner = await registerUser('owner')
    const visitor = await registerUser('visitor')
    const marker = `E2E catalog ${Date.now().toString(36)}`

    // The owner adds a bottle linked to the product, reviews it and lists it for sale.
    const bottle = await api(owner.token, 'POST', '/bottles', {
      name: `${product.name} (${marker})`,
      category: 'Whisky',
      condition: 'Sealed',
      isLimited: false,
      quantity: 2,
      productId: product.id,
    })
    await api(owner.token, 'POST', `/bottles/${bottle.id}/reviews`, {
      score: 93,
      summary: marker,
      flavors: ['Smoky', 'Maritime'],
    })
    await api(owner.token, 'POST', `/bottles/${bottle.id}/list-for-sale`, {
      askingPrice: 321,
      currency: 'EUR',
      quantity: 1,
    })

    const issues = captureConsole(page)
    await seedSession(page, visitor)
    await page.goto(`/product/${product.id}`)
    await expect(page.getByRole('heading', { level: 1, name: product.name })).toBeVisible()

    // Community: the new review is the newest, so it is among the latest five.
    await expect(page.getByText(marker, { exact: true })).toBeVisible()

    // Logged in: the cache-only estimate block is shown ("—" when nothing is cached).
    await expect(page.getByText(BG.estimateLabel)).toBeVisible()

    // Collectors: the owner is listed with their bottle count (quantity 2).
    const ownerChip = page.locator(`main a[href="/bar/${owner.id}"]`).filter({ hasText: owner.displayName })
    await expect(ownerChip.filter({ hasText: '×2' })).toBeVisible()

    // For sale: the listing opens the bottle panel, where a visitor can make an offer.
    const listing = page.locator('main li').filter({ hasText: owner.displayName })
    await expect(listing).toContainText('321')
    await listing.getByRole('button', { name: BG.open }).click()
    const dialog = page.getByRole('dialog')
    await expect(dialog).toBeVisible()
    await expect(dialog.getByRole('heading', { name: `${product.name} (${marker})` })).toBeVisible()
    await expect(dialog.getByRole('button', { name: BG.makeOffer })).toBeVisible()

    // The panel links back to the product, closing itself.
    await dialog.getByRole('link', { name: BG.viewProduct }).click()
    await expect(page.getByRole('dialog')).toHaveCount(0)
    await expect(page).toHaveURL(new RegExp(`/product/${product.id}$`))

    expect(appIssues(issues)).toEqual([])
  })
})
