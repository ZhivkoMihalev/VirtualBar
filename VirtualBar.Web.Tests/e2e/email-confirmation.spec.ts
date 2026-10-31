import { readdir, readFile } from 'node:fs/promises'
import path from 'node:path'
import { test, expect, type Page } from '@playwright/test'

/**
 * E2E coverage for email confirmation (issue #33): the "check your email" screen after registering, the
 * /confirm-email page that the link in the email opens, and "send a new link" wherever an unconfirmed user can get
 * stuck.
 *
 * Runs against the real API (:5000) and the Vite dev server (:5173), with the overrides the E2E workflow sets
 * (see CLAUDE.md → CI). Two of them matter here:
 *  - Email:PickupDirectory: the API writes every email as an .eml file instead of sending it. E2E_MAIL_DIR points
 *    these tests at the same directory, so they open the very link the API put in the email.
 *  - Identity:RequireConfirmedEmail=false, as for the rest of the suite. New accounts still start unconfirmed, so
 *    resend-confirmation sends them a real link and confirm-email accepts it. What the stack cannot produce with
 *    confirmation off are the two answers the API gives when it is on: a registration without a token, and a login
 *    refused with EmailNotConfirmed. The tests that need them rewrite that one response in the browser; every
 *    other request goes to the real API.
 */

const API = 'http://localhost:5000/api'
const PASSWORD = 'TestPass123'
const MAIL_DIR = process.env.E2E_MAIL_DIR

// Exact UI strings (src/i18n/bg.json).
const BG = {
  register: {
    displayName: 'Потребителско Име',
    email: 'Имейл',
    password: 'Парола',
    confirmPassword: 'Потвърди Паролата',
    submit: 'Създай Акаунт',
  },
  login: { email: 'Имейл', password: 'Парола', submit: 'Вход' },
  authErrors: {
    emailNotConfirmed: 'Имейлът ви все още не е потвърден. Отворете линка, който ви изпратихме, или поискайте нов.',
  },
  confirmEmail: {
    checkTitle: 'Проверете имейла си',
    checkMessage: (email: string) =>
      `Изпратихме линк за потвърждение на ${email}. Отворете го, за да активирате акаунта си.`,
    success: 'Имейлът е потвърден. Вече можете да влезете в акаунта си.',
    loginBtn: 'Към вход',
    invalidLink: 'Линкът за потвърждение е невалиден или е изтекъл.',
    errorFailed: 'Не успяхме да потвърдим имейла. Моля, опитайте отново.',
    retryBtn: 'Опитай отново',
    resendBtn: 'Изпрати нов линк',
    resendSuccess: (email: string) => `Ако акаунтът с имейл ${email} още не е потвърден, изпратихме му нов линк.`,
  },
}

// A successful login leaves the form for / or /dashboard (see auth.spec.ts).
const LOGGED_IN_URL = /\/(dashboard)?$/

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

async function readSession(page: Page) {
  return page.evaluate(() => ({ token: localStorage.getItem('vbar_token'), user: localStorage.getItem('vbar_user') }))
}

function decodeQuotedPrintable(text: string): string {
  const joined = text.replace(/=\r?\n/g, '')
  const bytes: number[] = []
  for (let i = 0; i < joined.length; i++) {
    const hex = joined.slice(i + 1, i + 3)
    if (joined[i] === '=' && /^[0-9A-F]{2}$/i.test(hex)) {
      bytes.push(parseInt(hex, 16))
      i += 2
    } else {
      bytes.push(...Buffer.from(joined[i], 'utf8'))
    }
  }
  return Buffer.from(bytes).toString('utf8')
}

/** Headers (folded lines joined) and decoded body of a single-part email, whichever transfer encoding MIME chose. */
function parseEmail(raw: string): { headers: string; body: string } {
  const split = raw.search(/\r?\n\r?\n/)
  const headers = raw.slice(0, split).replace(/\r?\n[ \t]+/g, ' ')
  const body = raw.slice(split).replace(/^\r?\n\r?\n/, '')
  const encoding = /^content-transfer-encoding:\s*(\S+)/im.exec(headers)?.[1]?.toLowerCase()
  if (encoding === 'base64') return { headers, body: Buffer.from(body.replace(/\s+/g, ''), 'base64').toString('utf8') }
  if (encoding === 'quoted-printable') return { headers, body: decodeQuotedPrintable(body) }
  return { headers, body }
}

/** The app path of the newest confirmation link emailed to this address, or null while there is none. */
async function findConfirmationLink(email: string): Promise<string | null> {
  if (!MAIL_DIR) {
    throw new Error("Set E2E_MAIL_DIR to the directory of the API's Email:PickupDirectory (CLAUDE.md → CI).")
  }
  let files: string[]
  try {
    files = await readdir(MAIL_DIR)
  } catch (error) {
    if ((error as NodeJS.ErrnoException).code === 'ENOENT') return null // no email written yet
    throw error
  }
  // The file names start with a UTC timestamp, so the newest email sorts last.
  for (const file of files.filter(name => name.endsWith('.eml')).sort().reverse()) {
    const { headers, body } = parseEmail(await readFile(path.join(MAIL_DIR, file), 'utf8'))
    const to = /^to:(.*)$/im.exec(headers)?.[1] ?? ''
    if (!to.toLowerCase().includes(email.toLowerCase())) continue
    const href = /href="([^"]*\/confirm-email\?[^"]*)"/.exec(body)?.[1]
    if (!href) continue
    const url = new URL(href.replaceAll('&amp;', '&'))
    return url.pathname + url.search
  }
  return null
}

/** Waits for the API to write a confirmation email to this address and returns the app path its link opens. */
async function waitForConfirmationLink(email: string): Promise<string> {
  let link: string | null = null
  await expect
    .poll(async () => (link = await findConfirmationLink(email)), {
      message: `a confirmation email to ${email} in ${MAIL_DIR}`,
      timeout: 10_000,
    })
    .not.toBeNull()
  return link!
}

async function clickResend(page: Page, email: string) {
  await page.getByRole('button', { name: BG.confirmEmail.resendBtn, exact: true }).click()
  await expect(page.getByText(BG.confirmEmail.resendSuccess(email), { exact: true })).toBeVisible()
}

async function submitLogin(page: Page, email: string) {
  await page.getByLabel(BG.login.email, { exact: true }).fill(email)
  await page.getByLabel(BG.login.password, { exact: true }).fill(PASSWORD)
  await page.getByRole('button', { name: BG.login.submit, exact: true }).click()
}

test.describe('Email confirmation', () => {
  test('registering with confirmation on: check your email, open the link, sign in', async ({ page }) => {
    const errors = trackPageErrors(page)
    const email = uniqueEmail('register')
    let registerLanguage: unknown

    // The real API registers the account; the answer is rewritten into the one it gives with confirmation on.
    await page.route('**/api/auth/register', async route => {
      registerLanguage = route.request().postDataJSON().language
      const response = await route.fetch()
      const body = await response.json()
      await route.fulfill({ response, json: { ...body, token: '', user: null, requiresEmailConfirmation: true } })
    })

    await page.goto('/register')
    await page.getByLabel(BG.register.displayName, { exact: true }).fill('Confirm Test User')
    await page.getByLabel(BG.register.email, { exact: true }).fill(email)
    await page.getByLabel(BG.register.password, { exact: true }).fill(PASSWORD)
    await page.getByLabel(BG.register.confirmPassword, { exact: true }).fill(PASSWORD)
    await page.getByRole('button', { name: BG.register.submit, exact: true }).click()

    await expect(page.getByText(BG.confirmEmail.checkTitle, { exact: true })).toBeVisible()
    await expect(page.getByText(BG.confirmEmail.checkMessage(email), { exact: true })).toBeVisible()
    await expect(page).toHaveURL(/\/register$/)
    expect(registerLanguage).toBe('bg') // the confirmation email is written in the UI's language
    expect(await readSession(page)).toEqual({ token: null, user: null })

    await clickResend(page, email)
    await page.goto(await waitForConfirmationLink(email))

    await expect(page.getByText(BG.confirmEmail.success, { exact: true })).toBeVisible()
    await page.getByRole('link', { name: BG.confirmEmail.loginBtn, exact: true }).click()
    await expect(page).toHaveURL(/\/login$/)
    await submitLogin(page, email)
    await expect(page).toHaveURL(LOGGED_IN_URL)
    expect((await readSession(page)).token).toBeTruthy()
    expect(errors).toEqual([])
  })

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

  test('a login refused for an unconfirmed email offers a new link', async ({ page }) => {
    const errors = trackPageErrors(page)
    const email = uniqueEmail('login')
    await registerViaApi(email)

    // The answer the API gives with confirmation on (AuthValidationDecorator.LoginAsync).
    await page.route('**/api/auth/login', route =>
      route.fulfill({
        status: 400,
        json: { error: 'Please confirm your email address before signing in.', reason: 'EmailNotConfirmed' },
      }),
    )

    await page.goto('/login')
    await submitLogin(page, email)

    await expect(page.getByText(BG.authErrors.emailNotConfirmed, { exact: true })).toBeVisible()
    await clickResend(page, email)
    expect(await waitForConfirmationLink(email)).toMatch(/^\/confirm-email\?/)
    expect(errors).toEqual([])
  })
})
