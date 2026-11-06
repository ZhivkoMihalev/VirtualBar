import { api, bg, expect, fill, FLOW_TIMEOUT, registerCollector, test } from './support'

/**
 * The vintage in the add-bottle form (#59): picking a vintage-dated catalog product fills the bottle's vintage, and a
 * different vintage offers to request the missing one in one step. The request is filed by the real API when the
 * bottle is saved, and the test withdraws it at the end, so no pending request is left for the admin.
 */

type ProductRequestBody = { id: string; name: string; vintageYear: number | null; sourceBottleId: string | null }

test.describe('Add bottle: vintage', () => {
  test.describe.configure({ timeout: FLOW_TIMEOUT })

  test('picking a vintage fills it, and a vintage the catalog lacks is requested when the bottle is saved', async ({
    openAs,
  }) => {
    const collector = await registerCollector('vintage')
    // A year the seeded Frapin line does not have; random, so runs against the same database do not collide.
    const year = 1801 + Math.floor(Math.random() * 98)
    const missing = `Millésime ${year}`

    const page = await openAs(collector)
    await page.goto('/dashboard')
    await page.getByRole('button', { name: bg.dashboard.addBottle, exact: true }).click()
    await expect(page.getByRole('heading', { name: bg.addBottle.title })).toBeVisible()

    await page.getByRole('radio', { name: 'Cognac' }).click()
    await page.getByRole('combobox', { name: bg.products.searchLabel, exact: true }).click()
    await page.getByPlaceholder(bg.products.searchPlaceholder).fill('Millésime 1988')
    await page.getByRole('option').filter({ hasText: 'Millésime 1988' }).filter({ hasText: 'Frapin' }).click()

    const vintage = page.getByLabel(bg.addBottle.vintage, { exact: true })
    await expect(vintage).toHaveValue('1988')
    await expect(page.getByText(bg.products.linkedChip)).toBeVisible()

    await vintage.fill(String(year))
    await expect(
      page.getByText(fill(bg.addBottle.vintageMismatch, { productYear: 1988, year }), { exact: true }),
    ).toBeVisible()
    await page.getByRole('button', { name: fill(bg.addBottle.requestVintage, { year }) }).click()

    await expect(page.getByLabel(bg.addBottle.name, { exact: true })).toHaveValue(missing)
    await expect(page.getByText(bg.products.linkedChip)).toHaveCount(0)
    await expect(page.getByText(fill(bg.addBottle.vintageRequested, { name: missing }), { exact: true })).toBeVisible()

    await page.getByRole('button', { name: bg.addBottle.submit }).click()
    await expect(page.getByRole('heading', { name: bg.addBottle.title })).toHaveCount(0)

    // Saving the unlinked bottle filed the request for the missing vintage, with its year and the bottle.
    const mine = await api<ProductRequestBody[]>(collector, 'GET', '/product-requests/mine')
    const request = mine.find(r => r.name === missing)
    try {
      expect(request).toBeDefined()
      expect(request!.vintageYear).toBe(year)
      expect(request!.sourceBottleId).not.toBeNull()
    } finally {
      if (request) await api(collector, 'DELETE', `/product-requests/${request.id}`)
    }
  })
})
