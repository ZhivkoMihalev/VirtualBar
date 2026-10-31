import { test, expect, type Page } from '@playwright/test'

/**
 * E2E coverage for the language choice (issue #34). The UI starts in Bulgarian whatever the browser language is, and
 * the language picked in the LanguageSwitcher is saved in localStorage (vbar_lang) and kept across reloads and tabs.
 *
 * Only the login page is used: it renders without calling the API.
 */

const HEADING = { bg: 'Влезте в акаунта си', en: 'Sign in to your account' }

async function expectLanguage(page: Page, lang: 'bg' | 'en') {
  await expect(page.getByText(HEADING[lang], { exact: true })).toBeVisible()
  await expect(page.locator('html')).toHaveAttribute('lang', lang)
  expect(await page.evaluate(() => localStorage.getItem('vbar_lang'))).toBe(lang)
}

async function chooseLanguage(page: Page, current: 'BG' | 'EN', option: 'English' | 'Български') {
  await page.getByRole('button', { name: current, exact: true }).click()
  await page.getByRole('menuitemradio', { name: option, exact: true }).click()
}

test.describe('Language', () => {
  test.describe('with an English browser', () => {
    test.use({ locale: 'en-US' })

    test('a first visit is in Bulgarian', async ({ page }) => {
      await page.goto('/login')

      await expectLanguage(page, 'bg')
    })
  })

  test('the chosen language survives a reload and a new tab', async ({ page, context }) => {
    await page.goto('/login')
    await chooseLanguage(page, 'BG', 'English')
    await expectLanguage(page, 'en')

    await page.reload()
    await expectLanguage(page, 'en')

    const tab = await context.newPage()
    await tab.goto('/login')
    await expectLanguage(tab, 'en')
  })

  test('switching back to Bulgarian is kept too', async ({ page }) => {
    await page.goto('/login')
    await chooseLanguage(page, 'BG', 'English')
    await expectLanguage(page, 'en')
    await chooseLanguage(page, 'EN', 'Български')

    await page.reload()
    await expectLanguage(page, 'bg')
  })

  test('an unknown saved language falls back to Bulgarian', async ({ page }) => {
    await page.addInitScript(() => localStorage.setItem('vbar_lang', 'fr'))

    await page.goto('/login')

    await expectLanguage(page, 'bg')
  })
})
