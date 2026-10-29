import { test, expect, Page } from '@playwright/test'

/**
 * E2E coverage for ProductSelect — the catalog autocomplete in the add-bottle form.
 *
 * Real app: Vite dev server (:5173, started by Playwright's webServer config) against the running
 * backend API (:5000) and its seeded catalog. A throwaway user is registered per worker and the
 * session is seeded into localStorage before navigation — the established pattern from
 * badges.spec.ts / reviews.spec.ts.
 *
 * These pin the four things that were verified by hand and would otherwise silently regress:
 *  1. The +1 probe. The component asks for MAX_RESULTS + 1 (51) and renders at most 50, using the
 *     51st only to decide whether to show the "refine your search" hint. Asserting a hard-coded 50
 *     would tie the test to catalog size, so the expectation is derived from the API instead: the
 *     rendered count must be min(apiRows, 50), and the hint must appear exactly when apiRows > 50.
 *  2. products.volumeSuffix. "ml" used to be hard-coded while the age next to it was translated.
 *     The meta line is uppercased with CSS, which does not touch textContent, so the assertions are
 *     case-insensitive regexes rather than literal strings.
 *  3. Mouse-wheel scrolling. The parent Sheet mounts react-remove-scroll, which cancels wheel events
 *     outside its locked subtree; the popover portals outside it, so it needed Popover `modal`.
 *     page.mouse.wheel dispatches a trusted event, which is what that fix has to survive.
 *  4. Popover placement on a short viewport (issue #29). With a full page of results the popover is
 *     ~330px tall, so with the field mid-screen it fits on neither side and used to hang up to ~114px
 *     off screen. ProductSelect caps the list at the height Radix reports as free; the invariant is
 *     that the popover is fully inside the viewport wherever the field sits.
 */

const API = 'http://localhost:5000/api'

// ---- Exact expected UI strings (from src/i18n/{bg,en}.json) ----
const BG = {
  addBottle: '+ ДОБАВИ БУТИЛКА',
  sheetTitle: 'ДОБАВИ КЪМ БАРА',
  searchLabel: 'Намери в каталога',
  minChars: 'Въведи поне 2 знака',
  noResults: 'Няма съвпадение в каталога.',
  requestNotice: 'Няма го в каталога',
  linkedChip: 'Свързана с каталога:',
  truncated: (max: number) => `Показани са първите ${max}.`,
}
const EN = {
  addBottle: '+ ADD BOTTLE',
  sheetTitle: 'ADD TO YOUR BAR',
  searchLabel: 'Find in the catalog',
  noResults: 'No match in the catalog.',
  linkedChip: 'Linked to catalog:',
  truncated: (max: number) => `Showing the first ${max}.`,
}

/** What the component renders at most; the API is asked for one more than this. */
const MAX_RESULTS = 50

/** A brand with well over 50 catalog rows, so the truncation path is actually exercised. */
const BUSY_TERM = 'Macallan'

/** Whisky is the add-form's default category, so the component sends it alongside the term. */
const DEFAULT_CATEGORY = 'Whisky'

const sleep = (ms: number) => new Promise(r => setTimeout(r, ms))

type Session = { token: string; user: unknown; displayName: string }

// The /api/auth policy is a fixed window of 10 req/min per IP (Program.cs). Registrations can 429 if
// the suite is run repeatedly within a minute — wait out the window and retry.
async function registerUser(tag: string): Promise<Session> {
  for (let attempt = 0; ; attempt++) {
    const suffix = `${Date.now()}-${Math.random().toString(36).slice(2, 7)}`
    const email = `psel-${tag}-${suffix}@test.com`
    const displayName = `PSel-${tag}-${Math.random().toString(36).slice(2, 6)}`
    const res = await fetch(`${API}/auth/register`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ email, password: 'TestPass123', displayName }),
    })
    if (res.ok) {
      const body = await res.json()
      return { token: body.token, user: body.user, displayName }
    }
    if (res.status === 429 && attempt < 6) {
      await sleep(11_000)
      continue
    }
    throw new Error(`register(${tag}) failed: ${res.status} ${await res.text()}`)
  }
}

type CatalogProduct = { id: string; name: string; volumeMl: number | null }

/** The exact request the component makes, so the expectations can be derived from real data. */
async function searchCatalog(term: string, limit: number): Promise<CatalogProduct[]> {
  const url = `${API}/products?search=${encodeURIComponent(term)}&category=${DEFAULT_CATEGORY}&limit=${limit}`
  const res = await fetch(url)
  if (!res.ok) throw new Error(`catalog search failed: ${res.status} ${await res.text()}`)
  return res.json()
}

async function seedSession(page: Page, auth: Session) {
  await page.addInitScript(
    ({ auth }) => {
      localStorage.setItem('vbar_lang', 'bg') // app default; EN is switched via the UI
      localStorage.setItem('vbar_token', auth.token)
      localStorage.setItem('vbar_user', JSON.stringify(auth.user))
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

/** Infra noise unrelated to the feature (favicon, and the dev-only Anthropic key being absent). */
function appIssues(issues: string[]): string[] {
  return issues.filter(i => !/favicon/i.test(i))
}

const listbox = (page: Page) => page.locator('[data-slot="command-list"]')

async function openAddBottleSheet(page: Page) {
  await page.getByRole('button', { name: BG.addBottle, exact: true }).click()
  await expect(page.getByRole('heading', { name: BG.sheetTitle })).toBeVisible()
}

async function openCatalogSearch(page: Page, label: string) {
  await page.getByRole('combobox', { name: label, exact: true }).click()
  await expect(listbox(page)).toBeVisible()
}

/**
 * Waits for the debounce + request to settle. aria-busy alone is not enough: it is absent both when the
 * results are current AND before the group exists at all, so on its own it lets a test measure the
 * still-empty list. Waiting for the first option closes that window.
 *
 * The timeout is deliberately well above Playwright's 5s default. /api/products matches with three
 * OR'd `contains` predicates over the whole catalog, and with the suite's workers all searching at once
 * that runs past 5s on a cold API — a false failure about the component, which is already correct.
 */
const SETTLE_TIMEOUT = 30_000

async function searchFor(page: Page, term: string, expectResults = true) {
  await page.locator('[data-slot="command-input"]').fill(term)

  if (expectResults) {
    await expect(page.getByRole('option').first()).toBeVisible({ timeout: SETTLE_TIMEOUT })
  } else {
    await expect(page.getByText(BG.noResults).or(page.getByText(EN.noResults))).toBeVisible({
      timeout: SETTLE_TIMEOUT,
    })
  }

  await expect(page.locator('[data-slot="command-group"][aria-busy="true"]')).toHaveCount(0)
}

// =====================================================================================

// Serial, for two reasons. The /api/auth window is 10 requests/min per IP, and a worker per test would
// spend the whole budget on registrations before any of them got to the catalog. It also keeps the
// workers from searching the catalog concurrently, which is what pushed /api/products past its timeout.
test.describe.configure({ mode: 'serial' })

test.describe('ProductSelect — catalog autocomplete', () => {
  let session: Session

  // Generous, because registerUser waits out a full rate-limit window when it sees a 429 and the
  // default 30s hook timeout cannot accommodate even one such wait.
  test.beforeAll(async () => {
    test.setTimeout(120_000)
    session = await registerUser('cat')
  })

  test('А · bg · probe caps the list at 50, shows the truncation hint, and translates the volume', async ({
    page,
  }) => {
    test.setTimeout(120_000)

    const rows = await searchCatalog(BUSY_TERM, MAX_RESULTS + 1)
    const expectedCount = Math.min(rows.length, MAX_RESULTS)
    const expectTruncated = rows.length > MAX_RESULTS

    const issues = captureConsole(page)
    await seedSession(page, session)
    await page.goto('/dashboard')

    await openAddBottleSheet(page)
    await openCatalogSearch(page, BG.searchLabel)

    // Nothing typed yet: the minimum-length hint, and no options at all.
    await expect(page.getByText(BG.minChars)).toBeVisible()
    await expect(page.getByRole('option')).toHaveCount(0)

    await searchFor(page, BUSY_TERM)

    await expect(page.getByRole('option')).toHaveCount(expectedCount)

    // The hint must track the 51st row, not merely "the list looks full".
    const hint = page.getByText(BG.truncated(MAX_RESULTS), { exact: false })
    if (expectTruncated) await expect(hint).toBeVisible()
    else await expect(hint).toHaveCount(0)

    // products.volumeSuffix: pinned against a real row rather than a guessed number. CSS uppercases
    // the meta line, which leaves textContent alone, hence the case-insensitive match.
    const sized = rows.slice(0, expectedCount).find(r => r.volumeMl != null)
    expect(sized, 'no Macallan row carries a volume — pick a different term').toBeTruthy()
    await expect(
      page.getByRole('option').filter({ hasText: new RegExp(`${sized!.volumeMl}\\s*мл`, 'i') }).first(),
    ).toBeVisible()

    // No stray "700 ml" left anywhere in the list.
    await expect(listbox(page)).not.toContainText(/\d\s*ml\b/i)

    expect(appIssues(issues)).toEqual([])
  })

  test('Б · the results list scrolls with the mouse wheel and the last row is reachable', async ({
    page,
  }) => {
    test.setTimeout(120_000)

    await seedSession(page, session)
    await page.goto('/dashboard')
    await openAddBottleSheet(page)
    await openCatalogSearch(page, BG.searchLabel)
    await searchFor(page, BUSY_TERM)

    const list = listbox(page)
    const box = await list.boundingBox()
    expect(box).toBeTruthy()

    const overflows = await list.evaluate(el => el.scrollHeight > el.clientHeight)
    expect(overflows, 'the list must overflow for the wheel assertion to mean anything').toBe(true)

    // A trusted wheel event over the portalled popover — exactly what react-remove-scroll used to eat.
    await page.mouse.move(box!.x + box!.width / 2, box!.y + box!.height / 2)
    await page.mouse.wheel(0, 400)

    await expect
      .poll(() => list.evaluate(el => el.scrollTop), { message: 'wheel did not scroll the list' })
      .toBeGreaterThan(0)

    // And the tail is genuinely reachable, not merely clipped.
    await list.evaluate(el => {
      el.scrollTop = el.scrollHeight
    })
    await expect(page.getByRole('option').last()).toBeVisible()
  })

  test('В · on a short viewport the popover stays fully inside the screen wherever the field sits', async ({
    page,
  }) => {
    test.setTimeout(120_000)

    await seedSession(page, session)
    await page.goto('/dashboard')
    await openAddBottleSheet(page)

    // The sheet slides in; let it come to rest so the positions below are the final ones.
    await page
      .locator('[data-slot="sheet-content"]')
      .evaluate(el => Promise.all(el.getAnimations().map(a => a.finished)))

    // Addressed by id: while the modal popover is open the trigger is aria-hidden, and the role query
    // would match the search input inside the popover instead.
    const triggerId = await page.getByRole('combobox', { name: BG.searchLabel, exact: true }).getAttribute('id')
    const trigger = page.locator(`[id="${triggerId}"]`)
    const fieldTop = () => trigger.evaluate(el => Math.round(el.getBoundingClientRect().top))
    const popover = page.locator('[data-slot="popover-content"]')

    // Where the field sits on screen decides the outcome, and in a real session that depends on how far
    // the sheet happens to be scrolled. So every placement is set explicitly instead of left to chance:
    // mid-screen is the case that used to fail, because a full page of results fits on neither side.
    // After the first search the results come from the query cache, so the extra rounds cost little.
    for (const height of [480, 560]) {
      for (const block of ['start', 'center', 'end'] as const) {
        await test.step(`${height}px viewport, field at the ${block} of the screen`, async () => {
          await page.setViewportSize({ width: 1280, height })
          await trigger.evaluate((el, b) => el.scrollIntoView({ block: b }), block)
          const placedAt = await fieldTop()

          // A dispatched click, not trigger.click(): at the top of the screen the field pokes out by a
          // fraction of a pixel, and Playwright's click would first scroll it back into view wherever it
          // sees fit, undoing the placement this step is about.
          await trigger.dispatchEvent('click')
          await expect(listbox(page)).toBeVisible()
          await searchFor(page, BUSY_TERM)
          expect(await fieldTop(), 'the field moved after it was placed').toBe(placedAt)

          // Radix re-places the popover a frame after the results change its height (Floating UI's
          // autoUpdate), so wait for the settled placement rather than sampling the frame in between. A
          // popover that stays outside the screen still fails here, reporting by how many pixels.
          await expect
            .poll(
              () =>
                popover.evaluate(el => {
                  const r = el.getBoundingClientRect()
                  return Math.max(0, Math.round(-r.top), Math.round(r.bottom - window.innerHeight))
                }),
              { message: 'pixels of the popover outside the viewport', timeout: 5_000 },
            )
            .toBe(0)

          // Shrinking the popover must not cost the user access to the rows.
          expect(await listbox(page).evaluate(el => el.scrollHeight > el.clientHeight)).toBe(true)

          // Escape closes only the popover; the add-bottle sheet stays open for the next round.
          await page.keyboard.press('Escape')
          await expect(listbox(page)).toBeHidden()
          await expect(page.getByRole('heading', { name: BG.sheetTitle })).toBeVisible()
        })
      }
    }
  })

  test('Г · a term with no catalog match offers the product-request notice instead of options', async ({
    page,
  }) => {
    test.setTimeout(120_000)

    const nonsense = 'zzqx-no-such-bottle'
    expect(await searchCatalog(nonsense, MAX_RESULTS + 1)).toHaveLength(0)

    await seedSession(page, session)
    await page.goto('/dashboard')
    await openAddBottleSheet(page)
    await openCatalogSearch(page, BG.searchLabel)
    await searchFor(page, nonsense, false)

    await expect(page.getByRole('option')).toHaveCount(0)
    await expect(page.getByText(BG.noResults)).toBeVisible()
    await expect(page.getByText(BG.requestNotice, { exact: false })).toBeVisible()
  })

  test('Д · picking a product links it, and the volume suffix is translated in English too', async ({
    page,
  }) => {
    test.setTimeout(120_000)

    const rows = await searchCatalog(BUSY_TERM, MAX_RESULTS + 1)
    const sized = rows.slice(0, MAX_RESULTS).find(r => r.volumeMl != null)
    expect(sized).toBeTruthy()

    await seedSession(page, session)
    await page.goto('/dashboard')

    await page.getByRole('button', { name: 'BG', exact: true }).click()
    await page.getByRole('menuitemradio', { name: 'English' }).click()
    await expect(page.getByRole('button', { name: 'EN', exact: true })).toBeVisible()

    await page.getByRole('button', { name: EN.addBottle, exact: true }).click()
    await expect(page.getByRole('heading', { name: EN.sheetTitle })).toBeVisible()
    await openCatalogSearch(page, EN.searchLabel)
    await searchFor(page, BUSY_TERM)

    // Same key, English value: "ml", never the Bulgarian "мл".
    await expect(
      page.getByRole('option').filter({ hasText: new RegExp(`${sized!.volumeMl}\\s*ml`, 'i') }).first(),
    ).toBeVisible()
    await expect(listbox(page)).not.toContainText(/\d\s*мл\b/i)

    if (rows.length > MAX_RESULTS)
      await expect(page.getByText(EN.truncated(MAX_RESULTS), { exact: false })).toBeVisible()

    // Selecting closes the popover and records the link on the form.
    const chosen = page.getByRole('option').filter({ hasText: sized!.name }).first()
    await chosen.click()

    await expect(listbox(page)).toHaveCount(0)
    await expect(page.getByText(EN.linkedChip, { exact: false })).toBeVisible()
  })
})
