import { test, expect, type Page } from '@playwright/test'

/**
 * E2E coverage for registration, login, logout and the auth route guards.
 *
 * Runs against the real API (:5000) and the Vite dev server (:5173, started by playwright.config.ts).
 * The API must run with the overrides the E2E workflow sets (see CLAUDE.md → CI):
 *  - Identity:RequireConfirmedEmail=false, so registering logs the user straight in;
 *  - RateLimiting:AuthPermitLimit raised, because this suite calls the auth endpoints far more than
 *    10 times a minute from one IP.
 *
 * App facts the tests rely on:
 *  - The UI is Bulgarian by default (i18n `lng: 'bg'`), so labels and messages are the bg.json strings.
 *  - The session lives in localStorage under vbar_token / vbar_user (src/api/client.ts).
 *  - LoginPage/RegisterPage navigate to /dashboard on success, while the /login and /register routes
 *    send an already authenticated user to /. Which of the two wins is timing, so a successful login
 *    is "left the form for / or /dashboard, with the user in the navbar".
 *  - A failed login or registration comes back as { error, reason } (backend: AuthErrorReasons). The form
 *    shows the translation of the reason (authErrors.<reason>), never the API's English text, and falls
 *    back to its generic errorFailed text only when there is no known reason.
 *  - Any 401 clears the session and hard-redirects to /login (axios interceptor in client.ts).
 */

const API = 'http://localhost:5000/api'
const PASSWORD = 'TestPass123'

// Exact UI strings (src/i18n/bg.json).
const BG = {
  register: {
    displayName: 'Потребителско Име',
    email: 'Имейл',
    password: 'Парола',
    confirmPassword: 'Потвърди Паролата',
    submit: 'Създай Акаунт',
    errorRequired: 'Всички полета са задължителни.',
    errorInvalidEmail: 'Моля, въведете валиден имейл адрес.',
    errorPasswordFormat: 'Паролата трябва да съдържа поне 8 символа, 1 главна буква, 1 цифра.',
    errorPasswordMatch: 'Паролите не съвпадат.',
  },
  login: {
    email: 'Имейл',
    password: 'Парола',
    submit: 'Вход',
    errorFailed: 'Входът е неуспешен. Моля, опитайте отново.',
  },
  authErrors: {
    invalidCredentials: 'Грешен имейл или парола.',
    lockedOut: 'Акаунтът е временно заключен след твърде много неуспешни опита. Моля, опитайте отново по-късно.',
    emailTaken: 'Вече има акаунт с този имейл. Влезте в него или използвайте друг имейл.',
  },
  nav: { logout: 'ИЗХОД' },
}

// Exact UI strings (src/i18n/en.json), for the check that the reasons are translated, not just mapped.
const EN = {
  login: { email: 'Email', password: 'Password', submit: 'Sign In' },
  authErrors: { invalidCredentials: 'Incorrect email or password.' },
}

// A successful login or registration leaves the form for / or /dashboard (see the header comment).
const LOGGED_IN_URL = /\/(dashboard)?$/

type Session = { token: string | null; user: { email?: string; displayName?: string } | null }

/** A fresh address per call, so a retried test never collides with the user its first attempt made. */
function uniqueEmail(tag: string): string {
  return `auth-${tag}-${Date.now()}-${Math.random().toString(36).slice(2, 8)}@test.com`
}

async function registerViaApi(email: string, displayName: string, password = PASSWORD) {
  const res = await fetch(`${API}/auth/register`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ email, password, displayName }),
  })
  if (res.status === 429) {
    throw new Error('Auth rate limit hit: run the API with RateLimiting:AuthPermitLimit raised (CLAUDE.md → CI).')
  }
  if (!res.ok) throw new Error(`register(${email}) failed: ${res.status} ${await res.text()}`)
  const body = await res.json()
  if (!body.token) {
    throw new Error('Registration did not log the user in: run the API with Identity:RequireConfirmedEmail=false.')
  }
  return body as { token: string; user: { id: string; email: string; displayName: string } }
}

/** Saved session as the app stores it. The page must already be on the app's origin. */
async function readSession(page: Page): Promise<Session> {
  return page.evaluate(() => {
    const user = localStorage.getItem('vbar_user')
    return { token: localStorage.getItem('vbar_token'), user: user ? JSON.parse(user) : null }
  })
}

/**
 * readSession for a page that may still be reloading. A read that lands in the middle of a reload throws
 * "Execution context was destroyed", and expect.poll does not retry a callback that throws: it only retries
 * a value that does not match yet. So that one error becomes a value that does not match.
 */
async function readSessionWhileReloading(page: Page): Promise<Session | 'reloading'> {
  try {
    return await readSession(page)
  } catch (error) {
    if (error instanceof Error && error.message.includes('Execution context was destroyed')) return 'reloading'
    throw error
  }
}

/** Uncaught exceptions in the page; registered before navigation. */
function trackPageErrors(page: Page): string[] {
  const errors: string[] = []
  page.on('pageerror', err => errors.push(err.message))
  return errors
}

async function fillRegisterForm(
  page: Page,
  values: { displayName?: string; email?: string; password?: string; confirmPassword?: string },
) {
  if (values.displayName !== undefined) {
    await page.getByLabel(BG.register.displayName, { exact: true }).fill(values.displayName)
  }
  if (values.email !== undefined) await page.getByLabel(BG.register.email, { exact: true }).fill(values.email)
  if (values.password !== undefined) {
    await page.getByLabel(BG.register.password, { exact: true }).fill(values.password)
  }
  if (values.confirmPassword !== undefined) {
    await page.getByLabel(BG.register.confirmPassword, { exact: true }).fill(values.confirmPassword)
  }
  await page.getByRole('button', { name: BG.register.submit, exact: true }).click()
}

async function submitLogin(
  page: Page,
  email: string,
  password: string,
  labels: { email: string; password: string; submit: string } = BG.login,
) {
  await page.getByLabel(labels.email, { exact: true }).fill(email)
  await page.getByLabel(labels.password, { exact: true }).fill(password)
  await page.getByRole('button', { name: labels.submit, exact: true }).click()
}

/** A login attempt straight against the API, for the attempts a test needs but does not look at. */
async function loginViaApi(email: string, password: string): Promise<number> {
  const res = await fetch(`${API}/auth/login`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ email, password }),
  })
  return res.status
}

/** The navbar's user menu button carries the display name once the user is logged in. */
function userMenu(page: Page, displayName: string) {
  return page.getByRole('button', { name: displayName })
}

/** Starts the page on the app with a valid session already saved, as after an earlier login. */
async function seedSession(page: Page, session: { token: string; user: unknown }) {
  await page.addInitScript(({ token, user }) => {
    localStorage.setItem('vbar_token', token)
    localStorage.setItem('vbar_user', JSON.stringify(user))
  }, session)
}

test.describe('VirtualBar Auth Flow', () => {
  test('1. User Registration Flow - registers, logs in and stores the session', async ({ page }) => {
    const errors = trackPageErrors(page)
    const email = uniqueEmail('register')
    const displayName = 'Test Collector'

    await page.goto('/register')
    await fillRegisterForm(page, { displayName, email, password: PASSWORD, confirmPassword: PASSWORD })

    await expect(page).toHaveURL(LOGGED_IN_URL)
    await expect(userMenu(page, displayName)).toBeVisible()

    const session = await readSession(page)
    expect(session.token).toBeTruthy()
    expect(session.user?.email).toBe(email)
    expect(session.user?.displayName).toBe(displayName)
    expect(errors).toEqual([])
  })

  test('2. User Login Flow - logs in an existing user and stores the session', async ({ page }) => {
    const errors = trackPageErrors(page)
    const email = uniqueEmail('login')
    const displayName = 'Login Test User'
    await registerViaApi(email, displayName)

    await page.goto('/login')
    await submitLogin(page, email, PASSWORD)

    await expect(page).toHaveURL(LOGGED_IN_URL)
    await expect(userMenu(page, displayName)).toBeVisible()

    const session = await readSession(page)
    expect(session.token).toBeTruthy()
    expect(session.user?.email).toBe(email)
    expect(errors).toEqual([])
  })

  test('3. Registration Validation - email required', async ({ page }) => {
    await page.goto('/register')
    await fillRegisterForm(page, { displayName: 'Test User', password: PASSWORD, confirmPassword: PASSWORD })

    await expect(page.getByText(BG.register.errorRequired)).toBeVisible()
    await expect(page).toHaveURL('/register')
  })

  test('3. Registration Validation - invalid email format', async ({ page }) => {
    await page.goto('/register')
    await fillRegisterForm(page, {
      displayName: 'Test User',
      email: 'invalid-email',
      password: PASSWORD,
      confirmPassword: PASSWORD,
    })

    await expect(page.getByText(BG.register.errorInvalidEmail)).toBeVisible()
    await expect(page).toHaveURL('/register')
  })

  test('3. Registration Validation - password too short', async ({ page }) => {
    await page.goto('/register')
    await fillRegisterForm(page, {
      displayName: 'Test User',
      email: uniqueEmail('short'),
      password: 'Short1',
      confirmPassword: 'Short1',
    })

    await expect(page.getByText(BG.register.errorPasswordFormat)).toBeVisible()
    await expect(page).toHaveURL('/register')
  })

  test('3. Registration Validation - password missing uppercase', async ({ page }) => {
    await page.goto('/register')
    await fillRegisterForm(page, {
      displayName: 'Test User',
      email: uniqueEmail('lower'),
      password: 'testpass123',
      confirmPassword: 'testpass123',
    })

    await expect(page.getByText(BG.register.errorPasswordFormat)).toBeVisible()
    await expect(page).toHaveURL('/register')
  })

  test('3. Registration Validation - password missing digit', async ({ page }) => {
    await page.goto('/register')
    await fillRegisterForm(page, {
      displayName: 'Test User',
      email: uniqueEmail('nodigit'),
      password: 'TestPass',
      confirmPassword: 'TestPass',
    })

    await expect(page.getByText(BG.register.errorPasswordFormat)).toBeVisible()
    await expect(page).toHaveURL('/register')
  })

  test('3. Registration Validation - passwords do not match', async ({ page }) => {
    await page.goto('/register')
    await fillRegisterForm(page, {
      displayName: 'Test User',
      email: uniqueEmail('mismatch'),
      password: PASSWORD,
      confirmPassword: 'DifferentPass123',
    })

    await expect(page.getByText(BG.register.errorPasswordMatch)).toBeVisible()
    await expect(page).toHaveURL('/register')
  })

  test('3. Registration Validation - email already registered', async ({ page }) => {
    const email = uniqueEmail('taken')
    await registerViaApi(email, 'First Owner')

    await page.goto('/register')
    await fillRegisterForm(page, { displayName: 'Second Owner', email, password: PASSWORD, confirmPassword: PASSWORD })

    await expect(page.getByText(BG.authErrors.emailTaken)).toBeVisible()
    await expect(page).toHaveURL('/register')
    expect((await readSession(page)).token).toBeNull()
  })

  // Same message as a wrong password on purpose: login must not reveal which emails have an account.
  test('4. Login Validation - non-existent email', async ({ page }) => {
    await page.goto('/login')
    await submitLogin(page, uniqueEmail('nobody'), PASSWORD)

    await expect(page.getByText(BG.authErrors.invalidCredentials)).toBeVisible()
    await expect(page.getByText(BG.login.errorFailed)).toHaveCount(0)
    await expect(page).toHaveURL('/login')
    expect((await readSession(page)).token).toBeNull()
  })

  test('4. Login Validation - wrong password', async ({ page }) => {
    const email = uniqueEmail('wrongpwd')
    await registerViaApi(email, 'Wrong Pwd Test', 'CorrectPass123')

    await page.goto('/login')
    await submitLogin(page, email, 'WrongPassword123')

    await expect(page.getByText(BG.authErrors.invalidCredentials)).toBeVisible()
    await expect(page.getByText(BG.login.errorFailed)).toHaveCount(0)
    await expect(page).toHaveURL('/login')
    expect((await readSession(page)).token).toBeNull()
  })

  test('4. Login Validation - wrong password, in English', async ({ page }) => {
    const email = uniqueEmail('wrongpwd-en')
    await registerViaApi(email, 'Wrong Pwd EN', 'CorrectPass123')

    // i18n starts in Bulgarian whatever is saved (lng: 'bg'), so switch with the page's own language menu.
    await page.goto('/login')
    await page.getByRole('button', { name: 'BG', exact: true }).click()
    await page.getByRole('menuitemradio', { name: 'English' }).click()
    await submitLogin(page, email, 'WrongPassword123', EN.login)

    await expect(page.getByText(EN.authErrors.invalidCredentials)).toBeVisible()
    await expect(page).toHaveURL('/login')
  })

  test('4. Login Validation - locked after five failed attempts, even with the right password', async ({ page }) => {
    const email = uniqueEmail('locked')
    await registerViaApi(email, 'Locked Test', 'CorrectPass123')

    // Program.cs locks an account after 5 failed attempts (Lockout.MaxFailedAccessAttempts).
    for (let attempt = 1; attempt <= 5; attempt++) {
      expect(await loginViaApi(email, 'WrongPassword123'), `failed attempt ${attempt}`).toBe(400)
    }

    await page.goto('/login')
    await submitLogin(page, email, 'CorrectPass123')

    await expect(page.getByText(BG.authErrors.lockedOut)).toBeVisible()
    await expect(page).toHaveURL('/login')
    expect((await readSession(page)).token).toBeNull()
  })

  test('5. Logout Flow - clears the session and leaves the protected page for /login', async ({ page }) => {
    const email = uniqueEmail('logout')
    const displayName = 'Logout Test'
    await registerViaApi(email, displayName)

    await page.goto('/login')
    await submitLogin(page, email, PASSWORD)
    await expect(userMenu(page, displayName)).toBeVisible()

    await page.goto('/dashboard')
    await userMenu(page, displayName).click()
    await page.getByRole('menuitem', { name: BG.nav.logout }).click()

    await expect(page).toHaveURL('/login')
    // Requests the dashboard still had in flight come back 401 without a token, and the axios interceptor
    // reloads /login once more; the poll rides out that reload (see readSessionWhileReloading).
    await expect.poll(() => readSessionWhileReloading(page)).toEqual({ token: null, user: null })
    await expect(page.getByRole('button', { name: BG.login.submit, exact: true })).toBeVisible()
  })

  test('6. Protected Route - accessing dashboard without token redirects to login', async ({ page }) => {
    await page.goto('/dashboard')

    await expect(page).toHaveURL('/login')
  })

  test('6. Protected Route - an invalid saved token is cleared and redirects to login', async ({ page }) => {
    // Save a session the API will reject. It is written after a first load rather than with an init
    // script, so the redirect's own page load does not put it back.
    await page.goto('/login')
    await page.evaluate(() => {
      localStorage.setItem('vbar_token', 'invalid-token-xyz')
      localStorage.setItem(
        'vbar_user',
        JSON.stringify({
          id: '00000000-0000-0000-0000-000000000000',
          email: 'fake@test.com',
          displayName: 'Fake User',
          createdAt: '2026-01-01T00:00:00Z',
          isAdmin: false,
        }),
      )
    })

    // The dashboard's authorized requests get 401, and the axios interceptor drops the session. Each 401
    // starts its own reload of /login, so poll rather than read storage in the middle of one.
    await page.goto('/dashboard')

    await expect(page).toHaveURL('/login')
    await expect.poll(() => readSessionWhileReloading(page)).toEqual({ token: null, user: null })
  })

  test('7. Auth Persistence - token and user survive page reload', async ({ page }) => {
    const errors = trackPageErrors(page)
    const email = uniqueEmail('persist')
    const displayName = 'Persist Test User'

    await page.goto('/register')
    await fillRegisterForm(page, { displayName, email, password: PASSWORD, confirmPassword: PASSWORD })
    await expect(userMenu(page, displayName)).toBeVisible()
    const before = await readSession(page)
    expect(before.token).toBeTruthy()

    await page.reload()

    await expect(page).toHaveURL(LOGGED_IN_URL)
    await expect(userMenu(page, displayName)).toBeVisible()
    const after = await readSession(page)
    expect(after.token).toBe(before.token)
    expect(after.user?.displayName).toBe(displayName)
    expect(errors).toEqual([])
  })

  test('8. Authenticated User - the login page redirects to home', async ({ page }) => {
    const { token, user } = await registerViaApi(uniqueEmail('authlogin'), 'Redirect Login')
    await seedSession(page, { token, user })

    await page.goto('/login')

    await expect(page).toHaveURL('/')
    await expect(userMenu(page, user.displayName)).toBeVisible()
  })

  test('8. Authenticated User - the register page redirects to home', async ({ page }) => {
    const { token, user } = await registerViaApi(uniqueEmail('authreg'), 'Redirect Register')
    await seedSession(page, { token, user })

    await page.goto('/register')

    await expect(page).toHaveURL('/')
    await expect(userMenu(page, user.displayName)).toBeVisible()
  })
})
