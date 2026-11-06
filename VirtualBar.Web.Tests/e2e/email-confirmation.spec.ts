import { test, expect, type Page } from '@playwright/test'
import { waitForConfirmationLink } from './mail'

/**
 * E2E coverage for the /confirm-email page (issue #33): the link in the email, a wrong or incomplete link, a failed
 * confirmation that can be retried, and "send a new link" from there.
 *
 * Runs with the rest of the suite, against the API with `Identity:RequireConfirmedEmail=false`. New accounts still start
 * unconfirmed, so resend-confirmation sends them a real link and confirm-email accepts it. Registering and logging in
 * with confirmation **on** (no session after registering, a login refused with EmailNotConfirmed, the link from the
 * registration email) run against the real API in `email-confirmation-on.spec.ts` (#47); the tests that used to
 * rewrite those answers in the browser were dropped for it.
 */

const API = 'http://localhost:5000/api'
const PASSWORD = 'TestPass123'

// Exact UI strings (src/i18n/bg.json).
const BG = {
  confirmEmail: {
    success: 'Имейлът е потвърден. Вече можете да влезете в акаунта си.',
    invalidLink: 'Линкът за потвърждение е невалиден или е изтекъл.',
    errorFailed: 'Не успяхме да потвърдим имейла. Моля, опитайте отново.',
    retryBtn: 'Опитай отново',
    resendBtn: 'Изпрати нов линк',
    resendSuccess: (email: string) => `Ако акаунтът с имейл ${email} още не е потвърден, изпратихме му нов линк.`,
  },
}

/** A fresh address per call, so a retried test never collides with the user its first attempt made. */
function uniqueEmail(tag: string): string {
  return `confirm-${tag}-${Date.now()}-${Math.random().toString(36).slice(2, 8)}@test.com`
}

async function registerViaApi(email: string, displayName = 'Confirm Test User') {
  const res = await fetch(`${API}/auth/register`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ email, password: PASSWORD, displayName }),
  })
  if (!res.ok) throw new Error(`register(${email}) failed: ${res.status} ${await res.text()}`)
}

async function resendViaApi(email: string) {
  const res = await fetch(`${API}/auth/resend-confirmation`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ email }),
  })
  if (!res.ok) throw new Error(`resend-confirmation(${email}) failed: ${res.status} ${await res.text()}`)
}

/** Uncaught exceptions in the page; registered before navigation. */
function trackPageErrors(page: Page): string[] {
  const errors: string[] = []
  page.on('pageerror', err => errors.push(err.message))
  return errors
}

async function clickResend(page: Page, email: string) {
  await page.getByRole('button', { name: BG.confirmEmail.resendBtn, exact: true }).click()
  await expect(page.getByText(BG.confirmEmail.resendSuccess(email), { exact: true })).toBeVisible()
}

test.describe('Email confirmation', () => {
  test('a wrong or expired link shows an error and sends a new link that works', async ({ page }) => {
    const errors = trackPageErrors(page)
    const email = uniqueEmail('invalid')
    await registerViaApi(email)

    await page.goto(`/confirm-email?email=${encodeURIComponent(email)}&token=not-a-real-token`)

    await expect(page.getByText(BG.confirmEmail.invalidLink, { exact: true })).toBeVisible()
    await clickResend(page, email)
    await page.goto(await waitForConfirmationLink(email))

    await expect(page.getByText(BG.confirmEmail.success, { exact: true })).toBeVisible()
    expect(errors).toEqual([])
  })

  test('a link missing its token or email is rejected without asking the API', async ({ page }) => {
    const confirmCalls: string[] = []
    page.on('request', request => {
      if (request.url().includes('/api/auth/confirm-email')) confirmCalls.push(request.url())
    })

    await page.goto('/confirm-email')
    await expect(page.getByText(BG.confirmEmail.invalidLink, { exact: true })).toBeVisible()
    // Without an email there is nobody to send a new link to.
    await expect(page.getByRole('button', { name: BG.confirmEmail.resendBtn, exact: true })).toHaveCount(0)

    await page.goto(`/confirm-email?email=${encodeURIComponent(uniqueEmail('no-token'))}`)
    await expect(page.getByText(BG.confirmEmail.invalidLink, { exact: true })).toBeVisible()
    await expect(page.getByRole('button', { name: BG.confirmEmail.resendBtn, exact: true })).toBeVisible()

    expect(confirmCalls).toEqual([])
  })

  test('a confirmation that fails for another reason can be retried with the same link', async ({ page }) => {
    const email = uniqueEmail('retry')
    await registerViaApi(email)
    await resendViaApi(email)
    const link = await waitForConfirmationLink(email)

    // The first attempt meets a server error, which says nothing about the link; the retry reaches the real API.
    let attempts = 0
    await page.route('**/api/auth/confirm-email', async route => {
      attempts++
      if (attempts === 1) await route.fulfill({ status: 503, json: { error: 'Service unavailable.' } })
      else await route.continue()
    })

    await page.goto(link)
    await expect(page.getByText(BG.confirmEmail.errorFailed, { exact: true })).toBeVisible()
    await page.getByRole('button', { name: BG.confirmEmail.retryBtn, exact: true }).click()

    await expect(page.getByText(BG.confirmEmail.success, { exact: true })).toBeVisible()
    expect(attempts).toBe(2)
  })
})
