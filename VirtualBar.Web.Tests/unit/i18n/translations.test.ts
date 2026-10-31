import { describe, expect, it } from 'vitest'
import bg from '@/i18n/bg.json'
import en from '@/i18n/en.json'

type TranslationTree = { [key: string]: unknown }

/** Every leaf of a translation file under its dotted key, e.g. `confirmEmail.title`. */
function flatten(tree: TranslationTree, prefix = ''): Map<string, unknown> {
  const entries = new Map<string, unknown>()
  for (const [key, value] of Object.entries(tree)) {
    const path = prefix + key
    if (typeof value === 'object' && value !== null) {
      for (const [childKey, childValue] of flatten(value as TranslationTree, `${path}.`)) {
        entries.set(childKey, childValue)
      }
    } else {
      entries.set(path, value)
    }
  }
  return entries
}

/** The interpolation names a translation uses: `{{email}}` and `{{count, number}}` give `count,email`. */
function placeholders(text: unknown): string {
  return [...String(text).matchAll(/{{\s*([\w.]+)[^}]*}}/g)]
    .map(match => match[1])
    .sort()
    .join(',')
}

const bgEntries = flatten(bg)
const enEntries = flatten(en)

// A key missing from one file shows up as the raw key in the UI, and a dropped placeholder as text with a hole in it.
describe('translations', () => {
  it('en.json has every key bg.json has', () => {
    const missing = [...bgEntries.keys()].filter(key => !enEntries.has(key))
    expect(missing).toEqual([])
  })

  it('bg.json has every key en.json has', () => {
    const missing = [...enEntries.keys()].filter(key => !bgEntries.has(key))
    expect(missing).toEqual([])
  })

  it('every translation is a non-empty string', () => {
    const invalid = [
      ...[...bgEntries].map(([key, value]) => ['bg', key, value] as const),
      ...[...enEntries].map(([key, value]) => ['en', key, value] as const),
    ]
      .filter(([, , value]) => typeof value !== 'string' || value.trim() === '')
      .map(([language, key]) => `${language}: ${key}`)
    expect(invalid).toEqual([])
  })

  it('a key uses the same placeholders in both languages', () => {
    const mismatched = [...bgEntries.keys()]
      .filter(key => enEntries.has(key))
      .filter(key => placeholders(bgEntries.get(key)) !== placeholders(enEntries.get(key)))
      .map(key => `${key}: bg {${placeholders(bgEntries.get(key))}} en {${placeholders(enEntries.get(key))}}`)
    expect(mismatched).toEqual([])
  })
})
