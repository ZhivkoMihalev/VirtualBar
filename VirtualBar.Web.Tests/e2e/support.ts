import {
  expect as baseExpect,
  test as baseTest,
  type BrowserContext,
  type Locator,
  type Page,
} from '@playwright/test'
import bg from '../../VirtualBar.Web/src/i18n/bg.json' with { type: 'json' }

/**
 * Shared helpers for the multi-user specs (marketplace, offers, messages). Every collector is registered fresh through
 * the API, so a test never depends on data another test or an earlier run left behind. The expected UI text comes from
 * the app's own bg.json, so a reworded translation cannot leave a spec checking for the old words.
 */

export { bg }

/**
 * Time limits for the multi-user flows. A test drives two or three browser contexts through several page loads and
 * server round-trips. In a parallel local run the browsers compete for the CPU while the Vite dev server serves every
 * module unbundled, and a step can take longer than the default 5 s although the API answers in milliseconds.
 */
export const FLOW_TIMEOUT = 90_000
export const expect = baseExpect.configure({ timeout: 15_000 })

export const API = 'http://localhost:5000/api'

export type Collector = { id: string; token: string; user: unknown; displayName: string; email: string }

export type SpiritCategory = 'Whisky' | 'Rum' | 'Cognac' | 'Vodka' | 'Gin' | 'Tequila' | 'Brandy' | 'Other'

const sleep = (ms: number) => new Promise(resolve => setTimeout(resolve, ms))

const randomSuffix = () => Math.random().toString(36).slice(2, 8)

/** A name no other run has used, without spaces so it stays the same in a query string. */
export function uniqueName(prefix: string): string {
  return `E2E-${prefix}-${randomSuffix()}`
}

/** Fills the `{{placeholders}}` of a translation, the way i18next does. */
export function fill(template: string, values: Record<string, string | number>): string {
  return template.replace(/{{\s*(\w+)\s*}}/g, (_, key: string) => String(values[key]))
}

/**
 * Registers a new collector. The E2E API runs with email confirmation off, so the answer carries a token. A 429 means
 * the auth rate limit is on (an API started without the E2E overrides): wait out its one-minute window and retry.
 */
export async function registerCollector(tag: string): Promise<Collector> {
  for (let attempt = 0; ; attempt++) {
    const suffix = randomSuffix()
    const email = `e2e-${tag}-${Date.now()}-${suffix}@test.com`
    const displayName = `${tag}-${suffix}`
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

/** A JSON request to the API as `who`. Throws with the response body on any non-2xx answer. */
export async function api<T = unknown>(who: Collector, method: string, path: string, body?: unknown): Promise<T> {
  const res = await fetch(`${API}${path}`, {
    method,
    headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${who.token}` },
    body: body === undefined ? undefined : JSON.stringify(body),
  })
  if (!res.ok) throw new Error(`${method} ${path} failed: ${res.status} ${await res.text()}`)
  const text = await res.text()
  return (text ? JSON.parse(text) : undefined) as T
}

export async function createBottle(
  owner: Collector,
  fields: { name: string; category?: SpiritCategory; quantity?: number; distilleryId?: string },
): Promise<{ id: string; name: string }> {
  return api(owner, 'POST', '/bottles', {
    category: 'Whisky',
    condition: 'Sealed',
    isLimited: false,
    quantity: 1,
    ...fields,
  })
}

/** The first seeded distillery of a category: wish list matching needs a real distillery id. */
export async function firstDistillery(category: SpiritCategory): Promise<{ id: string; name: string }> {
  const res = await fetch(`${API}/distilleries?category=${category}`)
  if (!res.ok) throw new Error(`GET /distilleries failed: ${res.status}`)
  const distilleries: { id: string; name: string }[] = await res.json()
  expect(distilleries.length, `no ${category} distillery is seeded`).toBeGreaterThan(0)
  return distilleries[0]
}

/**
 * `test` with an `openAs(who)` fixture: a page signed in as `who` (anonymous for null), in a browser context of its
 * own, so several collectors can act in one test without sharing a session. The UI starts in Bulgarian, as on a first
 * visit. Every context is closed when the test ends, failed or not: an open one keeps polling the API every 30 s (bell,
 * inbox) for the rest of the worker's run and slows the tests after it.
 */
export const test = baseTest.extend<{ openAs: (who: Collector | null) => Promise<Page> }>({
  openAs: async ({ browser, baseURL }, use) => {
    const contexts: BrowserContext[] = []
    await use(async who => {
      const context = await browser.newContext({ baseURL })
      contexts.push(context)
      await context.addInitScript(auth => {
        localStorage.setItem('vbar_lang', 'bg')
        if (auth) {
          localStorage.setItem('vbar_token', auth.token)
          localStorage.setItem('vbar_user', JSON.stringify(auth.user))
        }
      }, who ? { token: who.token, user: who.user } : null)
      return context.newPage()
    })
    await Promise.all(contexts.map(context => context.close()))
  },
})

/** The card (marketplace listing, wish, offer) that mentions `text`. */
export function card(page: Page, text: string): Locator {
  return page.locator('[data-slot="card"]').filter({ hasText: text })
}

/** Opens a bottle's detail panel from a bar shelf by clicking its name; returns the panel. */
export async function openBottlePanel(page: Page, bottleName: string): Promise<Locator> {
  // .first(): the in-flow plaque, not the hover portal the shelf appends to the body.
  await page.getByText(bottleName, { exact: true }).first().click()
  const panel = page.getByRole('dialog', { name: bottleName })
  await expect(panel).toBeVisible()
  return panel
}

/**
 * Opens the notification bell. The bell polls every 30 s, so a spec that expects a fresh notification loads the page
 * after the action that caused it, and the first fetch already has it.
 */
export async function openNotifications(page: Page): Promise<void> {
  await page.getByRole('button', { name: bg.notifications.title, exact: true }).click()
}

const escapeRegExp = (text: string) => text.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')

/** A notification in the open bell: the actor's name followed by the translated text. */
export function notification(page: Page, actorName: string, text: string): Locator {
  return page.getByRole('button', { name: new RegExp(`${escapeRegExp(actorName)}\\s+${escapeRegExp(text)}`) })
}

/**
 * The floating chat button. The NavBar's "messages" item has the same accessible name; the floating button is
 * mounted after the routes, so it is the last one.
 */
export function chatToggle(page: Page): Locator {
  return page.getByRole('button', { name: bg.messages.title, exact: true }).last()
}

/**
 * A message bubble in the open chat window. The inbox panel next to it previews the last message inside the
 * conversation's button, so the same text appears twice; a bubble is the copy outside any button.
 */
export function chatMessage(page: Page, text: string): Locator {
  return page.getByText(text, { exact: true }).and(page.locator(':not(button *)'))
}

/** Types the marketplace search and waits for the debounced request for exactly that term. */
export async function searchMarketplace(page: Page, term: string): Promise<void> {
  const answered = page.waitForResponse(
    res => res.url().includes('/api/bottles/marketplace') && res.url().includes(`search=${term}`),
  )
  await page.getByPlaceholder(bg.marketplace.searchPlaceholder).fill(term)
  await answered
}
