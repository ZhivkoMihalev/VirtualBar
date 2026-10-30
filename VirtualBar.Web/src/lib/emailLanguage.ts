import type { EmailLanguage } from '../types'

/** The language to ask the API to write an email in: English when the UI is in English, Bulgarian otherwise. */
export function emailLanguage(uiLanguage: string): EmailLanguage {
  return uiLanguage.startsWith('en') ? 'en' : 'bg'
}
