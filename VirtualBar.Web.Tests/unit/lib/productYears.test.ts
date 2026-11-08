import { describe, expect, it } from 'vitest'
import { isYear, latestYear, withYear } from '@/lib/productYears'

describe('productYears (#59)', () => {
  it.each([
    ['1800', true],
    ['1988', true],
    [String(latestYear()), true],
    ['1799', false],
    [String(latestYear() + 1), false],
    ['988', false],
    ['19880', false],
    ['19a8', false],
    ['', false],
  ])('isYear(%s) is %s', (text, expected) => {
    expect(isYear(text)).toBe(expected)
  })

  it("swaps the line's year for another", () => {
    expect(withYear('Millésime 1988', 1988, 1983)).toBe('Millésime 1983')
    expect(withYear('1998 Very Young', 1998, 1997)).toBe('1997 Very Young')
  })

  it('replaces the year only as a whole number', () => {
    expect(withYear('Cask 19880 Millésime 1988', 1988, 1983)).toBe('Cask 19880 Millésime 1983')
  })
})
