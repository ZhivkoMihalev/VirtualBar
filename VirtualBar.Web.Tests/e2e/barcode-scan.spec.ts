import { rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import type { Page, PlaywrightWorkerArgs } from '@playwright/test'
import { testBarcode, writeBarcodeVideo } from './barcodeVideo'
import {
  api,
  bg,
  createBottle,
  expect,
  FLOW_TIMEOUT,
  registerCollector,
  signInContext,
  test,
  untouchedProduct,
} from './support'
import type { Collector } from './support'

/**
 * Scanning a bottle's barcode with the camera (#49). Chromium plays a video of a real EAN-13 instead of a camera, so
 * the app's own scanner reads it, and the barcode goes through the real API. The barcodes are in the GS1 range shops
 * use for their own goods (200–299), random per run, so no real product and no earlier run has them.
 */

type BottleBody = { id: string; productId: string | null; barcode: string | null }

/**
 * Runs `steps` on a page signed in as `who`, in a Chromium of its own whose camera shows `barcode`: the fake camera is
 * a launch option, so the shared browser cannot have it. The browser and the video are removed afterwards.
 */
async function withCameraShowing(
  playwright: PlaywrightWorkerArgs['playwright'],
  baseURL: string | undefined,
  who: Collector,
  barcode: string,
  steps: (page: Page) => Promise<void>,
) {
  const video = join(tmpdir(), `virtualbar-barcode-${barcode}.y4m`)
  writeBarcodeVideo(barcode, video)
  const browser = await playwright.chromium.launch({
    args: ['--use-fake-ui-for-media-stream', '--use-fake-device-for-media-stream', `--use-file-for-fake-video-capture=${video}`],
  })
  try {
    const context = await browser.newContext({ baseURL })
    await signInContext(context, who)
    await steps(await context.newPage())
  } finally {
    await browser.close()
    rmSync(video, { force: true })
  }
}

async function openAddFormAndScan(page: Page) {
  await page.goto('/dashboard')
  await page.getByRole('button', { name: bg.dashboard.addBottle, exact: true }).click()
  await expect(page.getByRole('heading', { name: bg.addBottle.title })).toBeVisible()
  await page.getByRole('button', { name: bg.addBottle.scanBarcode, exact: true }).click()
}

/** Saves the form and returns the bottle the API stored: the collector is new, so it is their only one. */
async function saveBottle(page: Page, who: Collector): Promise<BottleBody> {
  await page.getByRole('button', { name: bg.addBottle.submit }).click()
  await expect(page.getByRole('heading', { name: bg.addBottle.title })).toHaveCount(0)
  const bottles = await api<BottleBody[]>(who, 'GET', `/bottles?userId=${who.id}`)
  expect(bottles).toHaveLength(1)
  return bottles[0]
}

test.describe('Barcode scan', () => {
  test.skip(({ browserName }) => browserName !== 'chromium', 'Only Chromium can play a video file as its camera.')
  test.describe.configure({ timeout: FLOW_TIMEOUT })

  test('a barcode another collector saved fills the form from its product, and the bottle keeps it', async ({
    playwright,
    baseURL,
  }) => {
    const barcode = testBarcode()
    const product = await untouchedProduct()
    const owner = await registerCollector('barcode-owner')
    await createBottle(owner, { name: product.name, category: product.category, productId: product.id, barcode })
    const scanner = await registerCollector('barcode-scan')

    await withCameraShowing(playwright, baseURL, scanner, barcode, async page => {
      await openAddFormAndScan(page)

      await expect(page.getByText(bg.addBottle.barcodeSuccess)).toBeVisible()
      await expect(page.getByLabel(bg.addBottle.barcodeLookup)).toHaveValue(barcode)
      await expect(page.getByText(bg.products.linkedChip)).toBeVisible()
      await expect(page.getByLabel(bg.addBottle.name, { exact: true })).toHaveValue(product.name)
      // The product's category, not the form's default (#120).
      await expect(page.getByRole('radio', { name: product.category, exact: true })).toBeChecked()

      const saved = await saveBottle(page, scanner)
      expect(saved.productId).toBe(product.id)
      expect(saved.barcode).toBe(barcode)
    })
  })

  test('a barcode nobody has asks to pick the bottle from the catalog, and the next lookup finds it', async ({
    playwright,
    baseURL,
  }) => {
    const barcode = testBarcode()
    const product = await untouchedProduct()
    const scanner = await registerCollector('barcode-first')

    await withCameraShowing(playwright, baseURL, scanner, barcode, async page => {
      await openAddFormAndScan(page)

      await expect(page.getByText(bg.addBottle.barcodeNotFound)).toBeVisible()
      await expect(page.getByLabel(bg.addBottle.barcodeLookup)).toHaveValue(barcode)

      await page.getByRole('radio', { name: product.category, exact: true }).click()
      await page.getByRole('combobox', { name: bg.products.searchLabel, exact: true }).click()
      await page.getByPlaceholder(bg.products.searchPlaceholder).fill(product.name)
      await page.getByRole('option').filter({ hasText: product.name }).first().click()
      await expect(page.getByText(bg.products.linkedChip)).toBeVisible()

      const saved = await saveBottle(page, scanner)
      expect(saved.barcode).toBe(barcode)
      expect(saved.productId).not.toBeNull()

      // The platform learned the barcode: the next collector's lookup finds the product the bottle was linked to.
      const next = await registerCollector('barcode-next')
      const found = await api<{ source: string; product: { id: string } | null }>(next, 'GET', `/products/barcode/${barcode}`)
      expect(found.source).toBe('Collectors')
      expect(found.product?.id).toBe(saved.productId)
    })
  })
})
