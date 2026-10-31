import { describe, expect, it } from 'vitest'
import { emailLanguage } from '@/lib/emailLanguage'

describe('emailLanguage', () => {
  it.each(['en', 'en-US', 'en-GB'])('asks for an English email when the UI language is %s', uiLanguage => {
    expect(emailLanguage(uiLanguage)).toBe('en')
  })

  it.each(['bg', 'bg-BG', 'de', ''])('asks for a Bulgarian email when the UI language is "%s"', uiLanguage => {
    expect(emailLanguage(uiLanguage)).toBe('bg')
  })
})
