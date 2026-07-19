import { test, expect, Page } from '@playwright/test'

/**
 * E2E coverage for the Badges / Achievements frontend feature.
 * Gate: docs/badges/06-frontend.md (§Gate) + docs/badges/00-OVERVIEW.md §3 (the 18-badge catalog).
 *
 * Real app: Vite dev server (:5173, started by Playwright's webServer config) against the
 * running backend API (:5000). Fresh users/bottles are created per test via the API — the
 * established pattern from reviews.spec.ts / auth.spec.ts.
 *
 * App facts verified while writing these tests (backend smoke test + source read):
 *  - localStorage keys (src/api/client.ts): vbar_token, vbar_user. Session is seeded before
 *    navigation via addInitScript so the user is authenticated from the first paint (no
 *    unauthenticated notification poll → no 401s to ignore).
 *  - i18n init hardcodes lng:'bg' (default); English is switched at runtime via the NavBar
 *    LanguageSwitcher (БГ/EN dropdown), NOT by seeding vbar_lang.
 *  - Adding a bottle awards the FirstBottle badge SYNCHRONOUSLY inside AddBottleAsync
 *    (badgeService.EvaluateAsync is awaited) → the BadgeEarned notification exists the moment
 *    POST /api/bottles returns. The bell polls every 30 s; instead of waiting we reload.
 *  - The BadgeEarned notification: type 'BadgeEarned', resourceName = 'FirstBottle', resourceId
 *    null, actor == recipient. NotificationBell renders it WITHOUT actor emphasis as
 *    `${t('notifications.badgeEarned')} ${t('badges.FirstBottle.name')}` and navigates to /profile.
 *  - GET /api/badges/progress returns all 18 catalog rows. A fresh user with 1 bottle:
 *    FirstBottle earned; every other badge unearned. Collector5 shows current 1 / threshold 5.
 *    NOTE: Explorer5 also renders "1/5" (5 categories, 1 distinct) — so the "1/5" assertion is
 *    scoped to the Collector5 chip's own progress wrapper, never a bare page-level getByText.
 *  - BadgeChip earned name color = GOLD_LIGHT (#E8C870 → rgb(232, 200, 112)); dimmed/unearned
 *    name color is a muted grey (asserted only as "not gold", to avoid brittle rgba matching).
 */

const API = 'http://localhost:5000/api'
const SHOTS =
  'C:/Users/jivko/AppData/Local/Temp/claude/C--Users-jivko-source-repos-VirtualBar/345f6fa1-3543-4b57-9b3d-fdc50e890310/scratchpad/shots'

const GOLD = 'rgb(232, 200, 112)' // BadgeChip earned name color (#E8C870)

// ---- Exact expected UI strings (from src/i18n/{bg,en}.json) ----
const BG = {
  bell: 'Известия', // notifications.title (bell aria-label)
  badgesTitle: 'Постижения', // badges.title
  progressTitle: 'Напредък', // badges.progressTitle
  firstBottle: 'Първа бутилка', // badges.FirstBottle.name
  collector5: 'Ценител', // badges.Collector5.name
  collector10: 'Колекционер', // badges.Collector10.name
  badgeEarned: 'Спечелихте значка:', // notifications.badgeEarned
}
const EN = {
  bell: 'Notifications',
  badgesTitle: 'Achievements',
  firstBottle: 'First Bottle',
  badgeEarned: 'You earned a badge:',
}

// ---- API helpers (register limiter-aware, mirroring reviews.spec.ts) ----
type Session = { id: string; token: string; user: unknown; displayName: string; email: string }

const sleep = (ms: number) => new Promise(r => setTimeout(r, ms))

// The /api/auth policy is a fixed window of 10 req/min per IP (Program.cs). Registrations can
// 429 if the suite is run repeatedly within a minute — wait out the window and retry.
async function registerUser(tag: string): Promise<Session> {
  for (let attempt = 0; ; attempt++) {
    const suffix = `${Date.now()}-${Math.random().toString(36).slice(2, 7)}`
    const email = `badge-${tag}-${suffix}@test.com`
    const displayName = `Badge-${tag}-${Math.random().toString(36).slice(2, 6)}`
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

// Minimal valid bottle (name + category + condition). Triggers BadgeTrigger.BottleAdded →
// FirstBottle award + BadgeEarned notification, all committed before this resolves.
async function createBottle(token: string, name: string) {
  const res = await fetch(`${API}/bottles`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${token}` },
    body: JSON.stringify({ name, category: 'Whisky', condition: 'Sealed', isLimited: false }),
  })
  if (!res.ok) throw new Error(`createBottle failed: ${res.status} ${await res.text()}`)
  return res.json()
}

// ---- Page helpers ----
async function seedSession(page: Page, auth: { token: string; user: unknown } | null) {
  await page.addInitScript(
    ({ auth }) => {
      localStorage.setItem('vbar_lang', 'bg') // app default; EN is switched via the UI
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

/** Infra noise unrelated to the feature (favicon). Everything else must be zero. */
function appIssues(issues: string[]): string[] {
  return issues.filter(i => !/favicon/i.test(i))
}

/** The profile "Постижения"/"Achievements" section (scoped by its heading — robust vs Footer). */
function badgesSection(page: Page, title: string) {
  return page.locator('section').filter({ has: page.getByRole('heading', { name: title }) })
}

async function openBell(page: Page, label: string) {
  await page.getByRole('button', { name: label, exact: true }).click()
}

async function switchToEnglish(page: Page) {
  await page.getByRole('button', { name: 'BG', exact: true }).click()
  await page.getByRole('menuitemradio', { name: 'English' }).click()
  await expect(page.getByRole('button', { name: 'EN', exact: true })).toBeVisible()
}

// =====================================================================================

test.describe('Badges / Achievements (frontend)', () => {
  test('А · bg · fresh user → FirstBottle in bell → profile 18 chips (FirstBottle gold, Collector5 1/5) → notification navigates to /profile', async ({
    page,
  }) => {
    test.setTimeout(120_000)

    const userA = await registerUser('a')
    const issues = captureConsole(page)
    await seedSession(page, { token: userA.token, user: userA.user })

    await test.step('load own dashboard (bell present, no badge yet)', async () => {
      await page.goto('/dashboard')
      await expect(page.getByRole('button', { name: BG.bell, exact: true })).toBeVisible()
    })

    // Add the first bottle AFTER the page is loaded, then surface the badge by reloading
    // instead of waiting out the 30 s bell poll (the gate's prescribed technique).
    await test.step('add first bottle (API) then reload to refresh the bell', async () => {
      await createBottle(userA.token, `A Bottle ${Math.random().toString(36).slice(2, 7)}`)
      await page.reload()
      await expect(page.getByRole('button', { name: BG.bell, exact: true })).toBeVisible()
    })

    await test.step('bell shows the BadgeEarned notification (bg text + translated badge name)', async () => {
      await openBell(page, BG.bell)
      const notif = page.getByRole('button', { name: new RegExp(BG.badgeEarned) })
      await expect(notif).toBeVisible({ timeout: 10_000 })
      await expect(notif).toContainText(BG.firstBottle) // "Спечелихте значка: Първа бутилка"
    })

    await test.step('clicking the BadgeEarned notification navigates to /profile', async () => {
      await page.getByRole('button', { name: new RegExp(BG.badgeEarned) }).click()
      await expect(page).toHaveURL('/profile')
    })

    await test.step('profile "Постижения": exactly 18 chips; FirstBottle gold; Collector5 shows 1/5', async () => {
      const section = badgesSection(page, BG.badgesTitle)
      await expect(page.getByRole('heading', { name: BG.badgesTitle })).toBeVisible()

      // Exactly 18 badge chips (each BadgeChip renders exactly one lucide <svg>).
      await expect(section.locator('svg')).toHaveCount(18, { timeout: 15_000 })

      // FirstBottle is EARNED → gold name; a few catalog names render (translated, not raw keys).
      const firstBottle = section.getByText(BG.firstBottle, { exact: true })
      await expect(firstBottle).toBeVisible()
      await expect(firstBottle).toHaveCSS('color', GOLD)

      // The unearned "Напредък" group exists with dimmed chips + progress labels.
      await expect(page.getByRole('heading', { name: BG.progressTitle })).toBeVisible()

      // Collector5 is UNEARNED (dimmed = not gold) and its own progress wrapper shows "1/5"
      // (scoped to the chip because Explorer5 also renders "1/5").
      const collector5Name = section.getByText(BG.collector5, { exact: true })
      await expect(collector5Name).toBeVisible()
      await expect(collector5Name).not.toHaveCSS('color', GOLD)
      const collector5Item = section
        .locator('div.flex.flex-col.items-center.gap-2')
        .filter({ hasText: BG.collector5 })
      await expect(collector5Item).toHaveCount(1)
      await expect(collector5Item.getByText('1/5', { exact: true })).toBeVisible()

      try {
        await section.screenshot({ path: `${SHOTS}/profile-badges-bg.png` })
      } catch {
        /* screenshot is best-effort */
      }
    })

    await test.step('zero console errors', async () => {
      expect(appIssues(issues), appIssues(issues).join('\n')).toEqual([])
    })
  })

  test('Б · en · language switch renders "Achievements" + "First Bottle" (gold) + bell "You earned a badge:"', async ({
    page,
  }) => {
    test.setTimeout(120_000)

    const user = await registerUser('en')
    await createBottle(user.token, `EN Bottle ${Math.random().toString(36).slice(2, 7)}`)

    const issues = captureConsole(page)
    await seedSession(page, { token: user.token, user: user.user })

    await test.step('load profile in bg, then switch language to English', async () => {
      await page.goto('/profile')
      await expect(page.getByRole('heading', { name: BG.badgesTitle })).toBeVisible() // bg baseline
      await switchToEnglish(page)
    })

    await test.step('profile section + FirstBottle render in English (no bg, no raw keys)', async () => {
      const section = badgesSection(page, EN.badgesTitle)
      await expect(page.getByRole('heading', { name: EN.badgesTitle })).toBeVisible()
      await expect(section.locator('svg')).toHaveCount(18, { timeout: 15_000 })

      const firstBottle = section.getByText(EN.firstBottle, { exact: true })
      await expect(firstBottle).toBeVisible()
      await expect(firstBottle).toHaveCSS('color', GOLD)

      await expect(section).not.toContainText(BG.badgesTitle) // no Bulgarian leak
      await expect(section).not.toContainText('badges.') // no raw i18n keys

      try {
        await page.screenshot({ path: `${SHOTS}/profile-badges-en.png`, fullPage: true })
      } catch {
        /* best-effort */
      }
    })

    await test.step('the existing notification renders translated in the bell', async () => {
      await openBell(page, EN.bell)
      const notif = page.getByRole('button', { name: new RegExp(EN.badgeEarned) })
      await expect(notif).toBeVisible({ timeout: 10_000 })
      await expect(notif).toContainText(EN.firstBottle) // "You earned a badge: First Bottle"
    })

    await test.step('zero console errors', async () => {
      expect(appIssues(issues), appIssues(issues).join('\n')).toEqual([])
    })
  })

  test('В · public bars · viewer sees owner earned-only strip (FirstBottle, no dimmed); empty strip renders nothing', async ({
    page,
  }) => {
    test.setTimeout(120_000)

    const owner = await registerUser('owner') // has a FirstBottle badge
    await createBottle(owner.token, `Owner Bottle ${Math.random().toString(36).slice(2, 7)}`)
    const viewer = await registerUser('viewer') // no bottles, no badges

    const issues = captureConsole(page)
    await seedSession(page, { token: viewer.token, user: viewer.user })

    await test.step("owner's public bar shows an EARNED-ONLY strip containing FirstBottle", async () => {
      await page.goto(`/bar/${owner.id}`)
      await expect(page.getByRole('heading', { name: owner.displayName })).toBeVisible()

      // FirstBottle chip present…
      await expect(page.getByText(BG.firstBottle, { exact: true })).toBeVisible()
      // …and NO unearned/dimmed chips (public strip is earned-only) + no progress heading.
      await expect(page.getByText(BG.collector5, { exact: true })).toHaveCount(0)
      await expect(page.getByText(BG.collector10, { exact: true })).toHaveCount(0)
      await expect(page.getByRole('heading', { name: BG.progressTitle })).toHaveCount(0)

      try {
        await page.screenshot({ path: `${SHOTS}/public-strip-owner.png`, fullPage: true })
      } catch {
        /* best-effort */
      }
    })

    await test.step("a bar with no badges renders no strip at all (not an empty section)", async () => {
      await page.goto(`/bar/${viewer.id}`)
      await expect(page.getByRole('heading', { name: viewer.displayName })).toBeVisible()
      // Zero earned badges → EarnedBadgesStrip returns null → no badge chip anywhere.
      await expect(page.getByText(BG.firstBottle, { exact: true })).toHaveCount(0)
      await expect(page.getByText(BG.collector5, { exact: true })).toHaveCount(0)
    })

    await test.step('zero console errors', async () => {
      expect(appIssues(issues), appIssues(issues).join('\n')).toEqual([])
    })
  })
})
