import { test, expect, Page } from '@playwright/test'

/**
 * E2E coverage for the Bottle Reviews frontend feature.
 * Gate: docs/bottle-reviews/06-frontend.md (§Gate).
 *
 * Real app: Vite dev server (:5173, started by Playwright's webServer config) against the
 * running backend API (:5000). Fresh users/bottles are created per test via the API (the
 * established pattern from auth.spec.ts).
 *
 * App facts verified while writing these tests:
 *  - localStorage keys (src/api/client.ts): vbar_token, vbar_user. (auth.spec.ts uses the
 *    stale keys token/user — that spec predates this and is out of scope.)
 *  - i18n init hardcodes `lng: 'bg'`, which overrides the localStorage language detector on
 *    load, so seeding vbar_lang does NOT switch language — language is switched via the UI
 *    LanguageSwitcher (БГ/EN dropdown in the NavBar).
 *  - /bar/:userId is a PUBLIC route, BUT the bottle panel's EstimateSection calls
 *    GET /api/prices/bottle/{id}, which is [Authorize] → 401 for anonymous users, and the
 *    global axios interceptor force-redirects to /login. So a truly-anonymous panel open
 *    bounces to /login (documented by a dedicated test below). The anonymous reviews check
 *    stubs ONLY that unrelated pricing call so the reviews feature itself can be verified.
 */

const API = 'http://localhost:5000/api'
const SHOTS =
  'C:/Users/jivko/AppData/Local/Temp/claude/C--Users-jivko-source-repos-VirtualBar/5e20ec85-e692-47c1-97a2-23ffebd28a6f/scratchpad/shots'

// ---- Exact expected UI strings (from src/i18n/{bg,en}.json) ----
const BG = {
  reviewsTitle: 'Оценки',
  estimateLabel: 'Ориентировъчна пазарна стойност', // collectionValue.estimateLabel (EstimateSection)
  invite: 'Бъдете първият, който оценява тази бутилка.',
  averageLabel: 'Средна',
  write: 'Напишете оценка',
  edit: 'Редактирайте оценката си',
  save: 'Запази',
  del: 'Изтрий',
  cancel: 'Отказ',
  deleteConfirm: 'Изтриването на оценката е необратимо. Сигурни ли сте?',
  scoreLabel: /Оценка \(0/,
  count1: '1 оценка',
  tastingToggle: 'Бележки за дегустация',
  nose: 'Нос',
  finish: 'Послевкус',
  flavor: { Smoky: 'Опушен', Vanilla: 'Ванилия', Oak: 'Дъб', Honey: 'Мед', Coffee: 'Кафе', Peaty: 'Торфен' },
}
const EN = {
  reviewsTitle: 'Reviews',
  invite: 'Be the first to rate this bottle.',
  write: 'Write a review',
  edit: 'Edit your review',
  save: 'Save',
  del: 'Delete',
  scoreLabel: /Score \(0/,
  count1: '1 review',
}

// ---- API helpers ----
type Session = { id: string; token: string; user: unknown; displayName: string; email: string }

const sleep = (ms: number) => new Promise(r => setTimeout(r, ms))

// The /api/auth policy is a fixed window of 10 req/min per IP (Program.cs). Registrations can
// 429 if the suite is run repeatedly within a minute — wait out the window and retry.
async function registerUser(tag: string): Promise<Session> {
  for (let attempt = 0; ; attempt++) {
    const suffix = `${Date.now()}-${Math.random().toString(36).slice(2, 7)}`
    const email = `rev-${tag}-${suffix}@test.com`
    const displayName = `Rev-${tag}-${Math.random().toString(36).slice(2, 6)}`
    const res = await fetch(`${API}/auth/register`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ email, password: 'TestPass123', displayName }),
    })
    if (res.ok) {
      const body = await res.json()
      return { id: body.user.id, token: body.token, user: body.user, displayName, email }
    }
    if (res.status === 429 && attempt < 6) {
      await sleep(11_000)
      continue
    }
    throw new Error(`register(${tag}) failed: ${res.status} ${await res.text()}`)
  }
}

async function createBottle(token: string, name: string) {
  const res = await fetch(`${API}/bottles`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${token}` },
    body: JSON.stringify({ name, category: 'Whisky', condition: 'Sealed', isLimited: false }),
  })
  if (!res.ok) throw new Error(`createBottle failed: ${res.status} ${await res.text()}`)
  return res.json()
}

async function createReviewApi(token: string, bottleId: string, payload: Record<string, unknown>) {
  const res = await fetch(`${API}/bottles/${bottleId}/reviews`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${token}` },
    body: JSON.stringify(payload),
  })
  if (!res.ok) throw new Error(`createReviewApi failed: ${res.status} ${await res.text()}`)
  return res.json()
}

async function getNotifications(token: string) {
  const res = await fetch(`${API}/notifications`, { headers: { Authorization: `Bearer ${token}` } })
  if (!res.ok) throw new Error(`getNotifications failed: ${res.status}`)
  return res.json()
}

// ---- Page helpers ----
async function seedSession(page: Page, opts: { auth?: { token: string; user: unknown } }) {
  await page.addInitScript(
    ({ auth }) => {
      localStorage.setItem('vbar_lang', 'bg') // app default anyway; EN is switched via the UI
      if (auth) {
        localStorage.setItem('vbar_token', auth.token)
        localStorage.setItem('vbar_user', JSON.stringify(auth.user))
      } else {
        localStorage.removeItem('vbar_token')
        localStorage.removeItem('vbar_user')
      }
    },
    { auth: opts.auth ?? null },
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

/** Infra noise unrelated to the feature (favicon). Everything else must be zero. */
function appIssues(issues: string[]): string[] {
  return issues.filter(i => !/favicon/i.test(i))
}

async function openPanel(page: Page, bottleName: string) {
  // The bottle-card plaque shows the name; clicking it bubbles to the card's onClick.
  // .first() picks the in-flow plaque over the body-appended hover portal.
  await page.getByText(bottleName, { exact: true }).first().click()
  await expect(page.getByRole('dialog')).toBeVisible()
}

async function closePanel(page: Page) {
  await page.keyboard.press('Escape')
  await expect(page.getByRole('dialog')).toHaveCount(0)
}

async function switchToEnglish(page: Page) {
  await page.getByRole('button', { name: 'BG', exact: true }).click()
  await page.getByRole('menuitemradio', { name: 'English' }).click()
  await expect(page.getByRole('button', { name: 'EN', exact: true })).toBeVisible()
}

// =====================================================================================

test.describe('Bottle Reviews (frontend)', () => {
  // One shared reviewer for all tests (each reviews a DIFFERENT bottle, so the
  // one-review-per-(bottle,user) constraint never bites). Keeps auth registrations low so the
  // 10-req/min limiter is not tripped. Owners stay per-test so each public bar is clean.
  let reviewer: Session

  test.beforeAll(async () => {
    test.setTimeout(120_000)
    reviewer = await registerUser('reviewer')
  })

  test('BG · rate → edit(full note + flavors, max-5) → card badge → owner notification → delete', async ({
    page,
  }) => {
    test.setTimeout(120_000)

    const owner = await registerUser('bg-owner')
    const bottleName = `BG Reviewed ${Math.random().toString(36).slice(2, 7)}`
    const bottle = await createBottle(owner.token, bottleName)

    const issues = captureConsole(page)
    await seedSession(page, { auth: { token: reviewer.token, user: reviewer.user } })
    await page.goto(`/bar/${owner.id}`)

    await expect(page.getByRole('heading', { name: owner.displayName })).toBeVisible()

    const panel = page.getByRole('dialog')
    // `has` must be a relative locator (not panel-rooted), else it looks for a dialog inside the form.
    const reviewForm = panel.locator('form').filter({ has: page.getByRole('spinbutton') })
    const avg = panel.locator('.text-4xl')

    await test.step('Scenario 3a — no ★ badge before any review', async () => {
      await expect(page.getByText('★', { exact: false })).toHaveCount(0)
    })

    await test.step('Scenario 1 — empty aggregate, then rate quick-score 92', async () => {
      await openPanel(page, bottleName)
      await expect(panel.getByText(BG.reviewsTitle, { exact: true })).toBeVisible()
      // Empty state: em-dash + invite, write-form (not edit), no delete.
      await expect(avg).toHaveText(/^\s*[—–-]\s*$/)
      await expect(panel.getByText(BG.invite)).toBeVisible()
      await expect(reviewForm.getByText(BG.write)).toBeVisible()
      await expect(reviewForm.getByRole('button', { name: BG.del, exact: true })).toHaveCount(0)

      await panel.getByLabel(BG.scoreLabel).fill('92')
      await reviewForm.getByRole('button', { name: BG.save, exact: true }).click()

      // Form flips to EDIT mode; aggregate shows 92.0 /100 + count 1 + average label.
      await expect(reviewForm.getByText(BG.edit)).toBeVisible({ timeout: 10_000 })
      await expect(reviewForm.getByRole('button', { name: BG.del, exact: true })).toBeVisible()
      await expect(avg).toHaveText('92.0')
      await expect(panel.getByText('/100').first()).toBeVisible()
      await expect(panel.getByText(BG.count1, { exact: true })).toBeVisible()
      await expect(panel.getByText(BG.averageLabel)).toBeVisible()
    })

    await test.step('Scenario 3b — card badge ★ 92 refreshes live after review', async () => {
      await closePanel(page)
      await expect(page.getByText('★ 92')).toBeVisible({ timeout: 10_000 })
    })

    await test.step('Scenario 4 — owner receives a BottleReviewed notification (API)', async () => {
      const notif = await getNotifications(owner.token)
      const match = (notif.notifications ?? []).find(
        (n: { type: string; resourceId?: string }) => n.type === 'BottleReviewed' && n.resourceId === bottle.id,
      )
      expect(match, 'owner should have a BottleReviewed notification for this bottle').toBeTruthy()
      expect(match.actorId).toBe(reviewer.id)
    })

    await test.step('Scenario 2 — edit into full tasting note + flavors (with max-5 behaviour)', async () => {
      await openPanel(page, bottleName)
      await expect(reviewForm.getByText(BG.edit)).toBeVisible()

      // change score 92 -> 88
      await panel.getByLabel(BG.scoreLabel).fill('88')

      // expand collapsible tasting notes (target by name — the Delete AlertDialogTrigger
      // also carries aria-expanded, so a bare [aria-expanded] selector is ambiguous)
      const toggle = reviewForm.getByRole('button', { name: BG.tastingToggle })
      await expect(toggle).toHaveAttribute('aria-expanded', 'false')
      await toggle.click()
      await expect(toggle).toHaveAttribute('aria-expanded', 'true')

      const notes = reviewForm.getByRole('textbox')
      await expect(notes).toHaveCount(4)
      await notes.nth(0).fill('Vanilla and toasted oak')
      await notes.nth(1).fill('Rich caramel and dried fruit')
      await notes.nth(2).fill('Long and warming')
      await notes.nth(3).fill('A superb after-dinner dram')

      // max-5: select 5 flavors, then a 6th unselected chip must be disabled
      const chip = (n: string) => reviewForm.getByRole('button', { name: n, exact: true })
      for (const f of [BG.flavor.Smoky, BG.flavor.Vanilla, BG.flavor.Oak, BG.flavor.Honey, BG.flavor.Coffee]) {
        await chip(f).click()
      }
      await expect(chip(BG.flavor.Peaty)).toBeDisabled()
      await expect(chip(BG.flavor.Smoky)).toBeEnabled() // selected chips stay toggleable

      // back down to 3 selected -> the 6th becomes enabled again
      await chip(BG.flavor.Honey).click()
      await chip(BG.flavor.Coffee).click()
      await expect(chip(BG.flavor.Peaty)).toBeEnabled()

      await reviewForm.getByRole('button', { name: BG.save, exact: true }).click()

      // aggregate updates; form persists in edit mode with prefilled notes + pressed chips.
      await expect(avg).toHaveText('88.0', { timeout: 10_000 })
      await expect(reviewForm.getByText(BG.edit)).toBeVisible()
      await expect(notes.nth(0)).toHaveValue('Vanilla and toasted oak')
      await expect(notes.nth(3)).toHaveValue('A superb after-dinner dram')
      await expect(chip(BG.flavor.Smoky)).toHaveAttribute('aria-pressed', 'true')
      await expect(chip(BG.flavor.Vanilla)).toHaveAttribute('aria-pressed', 'true')
      await expect(chip(BG.flavor.Oak)).toHaveAttribute('aria-pressed', 'true')
      await expect(chip(BG.flavor.Honey)).toHaveAttribute('aria-pressed', 'false')
      // aggregate top-flavor chips reflect the saved flavors
      await expect(panel.getByText(BG.flavor.Smoky).first()).toBeVisible()
      await expect(panel.getByText(BG.flavor.Oak).first()).toBeVisible()

      try {
        await panel.screenshot({ path: `${SHOTS}/bg-edit-mode.png` })
      } catch {
        /* screenshot is best-effort */
      }
    })

    await test.step('Scenario 5 — delete own review via confirm dialog', async () => {
      await reviewForm.getByRole('button', { name: BG.del, exact: true }).click()
      const confirm = page.getByRole('alertdialog')
      await expect(confirm.getByText(BG.deleteConfirm)).toBeVisible()
      await confirm.getByRole('button', { name: BG.del, exact: true }).click()

      // back to empty aggregate + create form returns; delete button gone.
      await expect(avg).toHaveText(/^\s*[—–-]\s*$/, { timeout: 10_000 })
      await expect(panel.getByText(BG.invite)).toBeVisible()
      await expect(reviewForm.getByText(BG.write)).toBeVisible()
      await expect(reviewForm.getByRole('button', { name: BG.del, exact: true })).toHaveCount(0)

      await closePanel(page)
      await expect(page.getByText('★', { exact: false })).toHaveCount(0)
    })

    await test.step('Scenario 8 — zero console errors', async () => {
      expect(appIssues(issues), appIssues(issues).join('\n')).toEqual([])
    })
  })

  test('Anonymous · reviews list + aggregate visible, no review form (scenario 6)', async ({ page }) => {
    test.setTimeout(120_000)

    const owner = await registerUser('anon-owner')
    const bottleName = `Anon Reviewed ${Math.random().toString(36).slice(2, 7)}`
    const bottle = await createBottle(owner.token, bottleName)
    await createReviewApi(reviewer.token, bottle.id, {
      score: 88,
      nose: 'Sea spray and smoke',
      palate: 'Peppery and oily',
      finish: 'Dry and lingering',
      summary: 'A coastal classic',
      flavors: ['Smoky', 'Maritime', 'Pepper'],
    })

    const issues = captureConsole(page)
    await seedSession(page, {}) // anonymous: no token/user (no pricing stub — Bug #1 is fixed)
    await page.goto(`/bar/${owner.id}`)

    await expect(page.getByRole('heading', { name: owner.displayName })).toBeVisible()
    expect(page.url()).toContain(`/bar/${owner.id}`)

    await test.step('card badge visible for anonymous viewer', async () => {
      await expect(page.getByText('★ 88')).toBeVisible()
    })

    const panel = page.getByRole('dialog')
    const avg = panel.locator('.text-4xl')

    await test.step('aggregate + review card render', async () => {
      await openPanel(page, bottleName)
      await expect(panel.getByText(BG.reviewsTitle, { exact: true })).toBeVisible()
      await expect(avg).toHaveText('88.0')
      await expect(panel.getByText(BG.count1, { exact: true })).toBeVisible()

      // review card: author + labelled notes + values + flavor chip
      await expect(panel.getByText(reviewer.displayName)).toBeVisible()
      await expect(panel.getByText('Sea spray and smoke')).toBeVisible()
      await expect(panel.getByText('Peppery and oily')).toBeVisible()
      await expect(panel.getByText('Dry and lingering')).toBeVisible()
      await expect(panel.getByText('A coastal classic')).toBeVisible()
      await expect(panel.getByText(BG.nose, { exact: true }).first()).toBeVisible()
      await expect(panel.getByText(BG.finish, { exact: true }).first()).toBeVisible()
      await expect(panel.getByText(BG.flavor.Smoky).first()).toBeVisible()

      try {
        await panel.screenshot({ path: `${SHOTS}/anon-review-card.png` })
      } catch {
        /* best-effort */
      }
    })

    await test.step('NO review form for anonymous', async () => {
      await expect(panel.getByRole('spinbutton')).toHaveCount(0)
      await expect(panel.getByRole('button', { name: BG.save, exact: true })).toHaveCount(0)
      await expect(panel.getByText(BG.write)).toHaveCount(0)
      await expect(panel.getByText(BG.edit)).toHaveCount(0)
    })

    await test.step('Scenario 8 — zero console errors', async () => {
      expect(appIssues(issues), appIssues(issues).join('\n')).toEqual([])
    })
  })

  test('Anonymous · bottle panel opens (no /login redirect), no estimate section, no /api/prices call (Bug #1 fixed)', async ({
    page,
  }) => {
    test.setTimeout(120_000)

    const owner = await registerUser('anon-fix-owner')
    const bottleName = `Anon Fix ${Math.random().toString(36).slice(2, 7)}`
    const bottle = await createBottle(owner.token, bottleName)
    await createReviewApi(reviewer.token, bottle.id, {
      score: 91,
      nose: 'Orchard fruit and honey',
      summary: 'Bright and clean',
      flavors: ['Fruity', 'Floral'],
    })

    const issues = captureConsole(page)
    const priceRequests: string[] = []
    page.on('request', req => {
      if (req.url().includes('/api/prices/bottle/')) priceRequests.push(req.url())
    })

    await seedSession(page, {}) // anonymous, NO stub — exercise the real (fixed) behaviour
    await page.goto(`/bar/${owner.id}`)
    await expect(page.getByRole('heading', { name: owner.displayName })).toBeVisible()

    const panel = page.getByRole('dialog')
    const avg = panel.locator('.text-4xl')

    await test.step('panel OPENS without a /login redirect', async () => {
      await openPanel(page, bottleName)
      await expect(page).not.toHaveURL(/\/login/)
      expect(page.url()).toContain(`/bar/${owner.id}`)
    })

    await test.step('reviews aggregate + list visible, no form, no estimate section', async () => {
      await expect(avg).toHaveText('91.0')
      await expect(panel.getByText(BG.count1, { exact: true })).toBeVisible()
      await expect(panel.getByText(reviewer.displayName)).toBeVisible()
      await expect(panel.getByText('Orchard fruit and honey')).toBeVisible()
      // no review form for an anonymous viewer
      await expect(panel.getByRole('spinbutton')).toHaveCount(0)
      await expect(panel.getByRole('button', { name: BG.save, exact: true })).toHaveCount(0)
      // estimate section is gated to authenticated users -> its label must be absent
      await expect(panel.getByText(BG.estimateLabel)).toHaveCount(0)
    })

    await test.step('no /api/prices/bottle request fired + zero console errors', async () => {
      expect(priceRequests, `unexpected price requests: ${priceRequests.join(', ')}`).toEqual([])
      expect(appIssues(issues), appIssues(issues).join('\n')).toEqual([])
    })
  })

  test('EN · reviews UI renders in English + quick rate via quick-pick (scenario 7)', async ({ page }) => {
    test.setTimeout(120_000)

    const owner = await registerUser('en-owner')
    const bottleName = `EN Reviewed ${Math.random().toString(36).slice(2, 7)}`
    await createBottle(owner.token, bottleName)

    const issues = captureConsole(page)
    await seedSession(page, { auth: { token: reviewer.token, user: reviewer.user } })
    await page.goto(`/bar/${owner.id}`)

    await expect(page.getByRole('heading', { name: owner.displayName })).toBeVisible()
    await switchToEnglish(page)

    const panel = page.getByRole('dialog')
    const reviewForm = panel.locator('form').filter({ has: page.getByRole('spinbutton') })
    const avg = panel.locator('.text-4xl')

    await test.step('English strings render (not raw keys, no Bulgarian)', async () => {
      await openPanel(page, bottleName)
      await expect(panel.getByText(EN.reviewsTitle, { exact: true })).toBeVisible()
      await expect(panel.getByText(EN.invite)).toBeVisible()
      await expect(reviewForm.getByText(EN.write)).toBeVisible()
      await expect(panel.getByLabel(EN.scoreLabel)).toBeVisible()
      await expect(reviewForm.getByRole('button', { name: EN.save, exact: true })).toBeVisible()
      // no Bulgarian, no raw i18n keys leaking through
      await expect(panel).not.toContainText(BG.reviewsTitle)
      await expect(panel).not.toContainText(BG.save)
      await expect(panel).not.toContainText('reviews.')
      await expect(panel).not.toContainText('flavors.')
    })

    await test.step('quick-pick 90 then Save flips to English edit mode + aggregate', async () => {
      const quick90 = reviewForm.getByRole('button', { name: '90', exact: true })
      await quick90.click()
      await expect(quick90).toHaveAttribute('aria-pressed', 'true')
      await reviewForm.getByRole('button', { name: EN.save, exact: true }).click()

      await expect(reviewForm.getByText(EN.edit)).toBeVisible({ timeout: 10_000 })
      await expect(reviewForm.getByRole('button', { name: EN.del, exact: true })).toBeVisible()
      await expect(avg).toHaveText('90.0')
      await expect(panel.getByText(EN.count1, { exact: true })).toBeVisible()

      try {
        await panel.screenshot({ path: `${SHOTS}/en-edit-mode.png` })
      } catch {
        /* best-effort */
      }
    })

    await test.step('Scenario 8 — zero console errors', async () => {
      expect(appIssues(issues), appIssues(issues).join('\n')).toEqual([])
    })
  })
})
