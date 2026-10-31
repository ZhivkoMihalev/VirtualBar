import type { Page } from '@playwright/test'
import {
  api,
  bg,
  createBottle,
  en,
  expect,
  fill,
  FLOW_TIMEOUT,
  registerCollector,
  switchToEnglish,
  test,
  uniqueName,
  untouchedProduct,
} from './support'

/**
 * The product page adds up every bottle linked to a catalog product: the community score and top flavors, the
 * collectors, the listings and the reviews, and its catalog card shows the same numbers. Each test links bottles to
 * a product nobody had linked before, so every number on the page comes from the bottles it created.
 */

/** A section of the product page, by its heading. */
const section = (page: Page, title: string) =>
  page.locator('section').filter({ has: page.getByRole('heading', { level: 2, name: title }) })

test.describe('Product page', () => {
  test.describe.configure({ timeout: FLOW_TIMEOUT })

  test('the page and its catalog card add up every bottle linked to the product, in both languages', async ({
    openAs,
  }) => {
    const product = await untouchedProduct()
    const [stacker, single] = await Promise.all([registerCollector('stacker'), registerCollector('single')])
    const marker = uniqueName('Product')

    // Two collectors, one with two bottles of it (one listed for sale), both reviewing: (90 + 81) / 2 = 85.5.
    const stacked = await createBottle(stacker, {
      name: `${product.name} ${marker}`,
      category: product.category,
      quantity: 2,
      productId: product.id,
    })
    await api(stacker, 'POST', `/bottles/${stacked.id}/reviews`, {
      score: 90,
      summary: `${marker} A`,
      flavors: ['Smoky', 'Peaty', 'Vanilla'],
    })
    await api(stacker, 'POST', `/bottles/${stacked.id}/list-for-sale`, { askingPrice: 150, currency: 'EUR', quantity: 1 })
    const other = await createBottle(single, {
      name: `${product.name} ${marker}`,
      category: product.category,
      productId: product.id,
    })
    await api(single, 'POST', `/bottles/${other.id}/reviews`, { score: 81, summary: `${marker} B`, flavors: ['Smoky', 'Vanilla'] })

    const page = await openAs(null)
    await page.goto(`/product/${product.id}`)
    await expect(page.getByRole('heading', { level: 1, name: product.name })).toBeVisible()

    // Smoky and Vanilla appear twice and tie on count, so the enum order breaks the tie; Peaty appears once.
    const community = section(page, bg.product.community)
    await expect(community).toContainText('85.5')
    await expect(community).toContainText(fill(bg.reviews.count_other, { count: 2 }))
    await expect(community.locator('[data-slot="badge"]')).toHaveText([
      bg.flavors.Smoky,
      bg.flavors.Vanilla,
      bg.flavors.Peaty,
    ])

    const owners = section(page, bg.product.owners)
    await expect(owners.getByRole('link', { name: new RegExp(stacker.displayName) })).toContainText('×2')
    await expect(owners.getByRole('link', { name: new RegExp(single.displayName) })).not.toContainText('×')

    const listings = section(page, bg.product.forSale).getByRole('listitem')
    await expect(listings).toHaveCount(1)
    await expect(listings).toContainText(stacker.displayName)
    await expect(listings).toContainText('EUR 150')

    const reviews = section(page, bg.product.reviews)
    await expect(reviews.getByText(`${marker} A`, { exact: true })).toBeVisible()
    await expect(reviews.getByText(`${marker} B`, { exact: true })).toBeVisible()

    await page.goto(`/catalog?${new URLSearchParams({ q: product.name, category: product.category })}`)
    const card = page.locator(`main a[href="/product/${product.id}"]`)
    await expect(card).toContainText('★ 85.5')
    await expect(card).toContainText(`(${fill(bg.catalog.reviews_other, { count: 2 })})`)
    await expect(card).toContainText(fill(bg.catalog.owners_other, { count: 2 }))
    await expect(card).toContainText(fill(bg.catalog.forSale_one, { count: 1 }))

    await switchToEnglish(page)
    await expect(card).toContainText(`(${fill(en.catalog.reviews_other, { count: 2 })})`)
    await expect(card).toContainText(fill(en.catalog.owners_other, { count: 2 }))
    await expect(card).toContainText(fill(en.catalog.forSale_one, { count: 1 }))

    await card.click()
    const communityInEnglish = section(page, en.product.community)
    await expect(communityInEnglish).toContainText(fill(en.reviews.count_other, { count: 2 }))
    await expect(communityInEnglish.locator('[data-slot="badge"]')).toHaveText([
      en.flavors.Smoky,
      en.flavors.Vanilla,
      en.flavors.Peaty,
    ])
    await expect(section(page, en.product.owners)).toBeVisible()
    await expect(section(page, en.product.forSale)).toBeVisible()
    await expect(section(page, en.product.reviews)).toBeVisible()
  })

  test('"show more reviews" goes from the newest five to every review, newest first', async ({ openAs }) => {
    const product = await untouchedProduct()
    const taster = await registerCollector('taster')
    const marker = uniqueName('Tasting')

    // One collector, seven bottles of the product, one review on each: #7 is the newest.
    for (let i = 1; i <= 7; i++) {
      const bottle = await createBottle(taster, {
        name: `${product.name} ${marker} ${i}`,
        category: product.category,
        productId: product.id,
      })
      await api(taster, 'POST', `/bottles/${bottle.id}/reviews`, { score: 80 + i, summary: `${marker} #${i}` })
    }

    const page = await openAs(null)
    await page.goto(`/product/${product.id}`)
    const reviews = section(page, bg.product.reviews)
    const summaries = reviews.getByText(new RegExp(`^${marker} #\\d$`))
    const newestFirst = (from: number) => Array.from({ length: from }, (_, i) => `${marker} #${from - i}`)

    const showMore = reviews.getByRole('button', { name: bg.product.showMore })

    await expect(summaries).toHaveText(newestFirst(7).slice(0, 5))
    await expect(showMore).toBeVisible()

    await showMore.click()

    await expect(summaries).toHaveText(newestFirst(7))
    await expect(showMore).toHaveCount(0)
  })
})
