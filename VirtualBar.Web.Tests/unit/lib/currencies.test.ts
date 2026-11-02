import { describe, expect, it } from 'vitest'
import { CURRENCIES, DEFAULT_CURRENCY, formCurrency } from '@/lib/currencies'

describe('currencies', () => {
  it('offers only EUR, GBP and USD (#98)', () => {
    expect(CURRENCIES).toEqual(['EUR', 'GBP', 'USD'])
  })

  it('defaults to EUR', () => {
    expect(DEFAULT_CURRENCY).toBe('EUR')
  })
})

describe('formCurrency', () => {
  it.each(['EUR', 'GBP', 'USD'])('keeps the bottle currency %s, which the app accepts', currency => {
    expect(formCurrency(currency)).toBe(currency)
  })

  it.each(['BGN', 'CHF', 'eur', ''])('falls back to EUR for "%s", which the API would refuse', currency => {
    expect(formCurrency(currency)).toBe('EUR')
  })

  it.each([null, undefined])('falls back to EUR when the bottle has no currency (%s)', currency => {
    expect(formCurrency(currency)).toBe('EUR')
  })
})
