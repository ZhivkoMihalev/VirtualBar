import type { Page } from '@playwright/test'
import { waitForConfirmationLink } from './mail'
import { bg, expect, fill, test } from './support'

/**
 * Email confirmation as production runs it: the API with `Identity:RequireConfirmedEmail=true` (#47). Nothing is
 * rewritten in the browser: the registration, the email, the confirmation and every login answer come from the real
 * API.
 *
 * Tagged `@confirmation-on`: only the E2E workflow's "confirmation on" run executes these, against an API started with
 * confirmation on, and every other run skips them (`playwright.config.ts`, `E2E_CONFIRMATION`). The rest of the suite
 * needs confirmation off, because it registers collectors and logs them straight in. Run them locally with the
 * `api-e2e-confirmation-on` launch configuration and `E2E_CONFIRMATION=on` (`.claude/rules/ci/e2e.md`).
 */

const PASSWORD = 'TestPass123'

// A successful login leaves the form for / or /dashboard (see auth.spec.ts).
const LOGGED_IN_URL = /\/(dashboard)?$/

/** A fresh address per call, so a retried test never collides with the account its first attempt made. */
function uniqueEmail(tag: string): string {
  return `confirm-on-${tag}-${Date.now()}-${Math.random().toString(36).slice(2, 8)}@test.com`
}

async function register(page: Page, email: string) {
  await page.goto('/register')
  await page.getByLabel(bg.register.displayNameLabel, { exact: true }).fill('Confirm On User')
  await page.getByLabel(bg.register.emailLabel, { exact: true }).fill(email)
  await page.getByLabel(bg.register.passwordLabel, { exact: true }).fill(PASSWORD)
  await page.getByLabel(bg.register.confirmPasswordLabel, { exact: true }).fill(PASSWORD)
  await page.getByRole('button', { name: bg.register.submitBtn, exact: true }).click()
  await expect(page.getByText(bg.confirmEmail.checkTitle, { exact: true })).toBeVisible()
}

async function logIn(page: Page, email: string) {
  await page.getByLabel(bg.login.emailLabel, { exact: true }).fill(email)
  await page.getByLabel(bg.login.passwordLabel, { exact: true }).fill(PASSWORD)
  await page.getByRole('button', { name: bg.login.submitBtn, exact: true }).click()
}

const storedToken = (page: Page) => page.evaluate(() => localStorage.getItem('vbar_token'))

/** Opens a confirmation link and, once the account is confirmed, logs in from the page it leads to. */
async function confirmAndLogIn(page: Page, link: string, email: string) {
  await page.goto(link)
  await expect(page.getByText(bg.confirmEmail.success, { exact: true })).toBeVisible()
  await page.getByRole('link', { name: bg.confirmEmail.loginBtn, exact: true }).click()
  await expect(page).toHaveURL(/\/login$/)

  await logIn(page, email)

  await expect(page).toHaveURL(LOGGED_IN_URL)
  expect(await storedToken(page)).toBeTruthy()
}

test.describe('Email confirmation on', { tag: '@confirmation-on' }, () => {
  test('registering signs nobody in, and the API refuses to log in until the email is confirmed', async ({ page }) => {
    const email = uniqueEmail('register')
    const registered = page.waitForRequest('**/api/auth/register')

    await register(page, email)

    await expect(page.getByText(fill(bg.confirmEmail.checkMessage, { email }), { exact: true })).toBeVisible()
    await expect(page).toHaveURL(/\/register$/)
    // The confirmation email is written in the language of the UI the collector registered in.
    expect((await registered).postDataJSON().language).toBe('bg')
    expect(await storedToken(page)).toBeNull()

    await page.goto('/login')
    await logIn(page, email)

    await expect(page.getByText(bg.authErrors.EmailNotConfirmed, { exact: true })).toBeVisible()
    await expect(page).toHaveURL(/\/login$/)
    expect(await storedToken(page)).toBeNull()
  })

  test('the link in the registration email confirms the account, and logging in then works', async ({ page }) => {
    const email = uniqueEmail('link')
    await register(page, email)

    await confirmAndLogIn(page, await waitForConfirmationLink(email), email)
  })

  test('a login refused for an unconfirmed email offers a new link, and the new link works', async ({ page }) => {
    const email = uniqueEmail('resend')
    await register(page, email)
    const registrationLink = await waitForConfirmationLink(email)

    await page.goto('/login')
    await logIn(page, email)
    await expect(page.getByText(bg.authErrors.EmailNotConfirmed, { exact: true })).toBeVisible()
    await page.getByRole('button', { name: bg.confirmEmail.resendBtn, exact: true }).click()
    await expect(page.getByText(fill(bg.confirmEmail.resendSuccess, { email }), { exact: true })).toBeVisible()

    await confirmAndLogIn(page, await waitForConfirmationLink(email, registrationLink), email)
  })
})
