import { beforeEach, describe, expect, it, vi } from 'vitest'

const LANGUAGE_KEY = 'vbar_lang'

/** Loads src/i18n/index.ts afresh, as a page load does, so it detects the language again. */
async function loadI18n() {
  vi.resetModules()
  return (await import('@/i18n')).default
}

describe('i18n language on load', () => {
  beforeEach(() => {
    // Detection must never read the browser language: make it one the app supports, so a wrong order would show.
    vi.spyOn(navigator, 'language', 'get').mockReturnValue('en-US')
    vi.spyOn(navigator, 'languages', 'get').mockReturnValue(['en-US', 'en'])
  })

  it('is Bulgarian on a first visit, whatever the browser language', async () => {
    const i18n = await loadI18n()

    expect(i18n.language).toBe('bg')
    expect(document.documentElement.lang).toBe('bg')
  })

  it('uses the language saved by the language switcher', async () => {
    localStorage.setItem(LANGUAGE_KEY, 'en')

    const i18n = await loadI18n()

    expect(i18n.language).toBe('en')
    expect(document.documentElement.lang).toBe('en')
  })

  it('falls back to Bulgarian for a saved language the app does not support', async () => {
    localStorage.setItem(LANGUAGE_KEY, 'de')

    const i18n = await loadI18n()

    expect(i18n.language).toBe('bg')
  })

  it('saves a language chosen later, so the next load keeps it', async () => {
    const i18n = await loadI18n()

    await i18n.changeLanguage('en')

    expect(localStorage.getItem(LANGUAGE_KEY)).toBe('en')
    expect(document.documentElement.lang).toBe('en')
    expect((await loadI18n()).language).toBe('en')
  })
})
