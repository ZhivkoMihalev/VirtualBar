import i18n from 'i18next'
import { initReactI18next } from 'react-i18next'
import LanguageDetector from 'i18next-browser-languagedetector'
import bg from './bg.json'
import en from './en.json'

i18n
  .use(LanguageDetector)
  .use(initReactI18next)
  .init({
    resources: {
      bg: { translation: bg },
      en: { translation: en },
    },
    // No `lng`: it would override detection and ignore the saved choice. Detection reads only the saved choice, never
    // the browser language, so a first visit is in Bulgarian (fallbackLng) and a choice made in the LanguageSwitcher
    // is kept from then on. supportedLngs sends an unknown saved value to Bulgarian too.
    fallbackLng: 'bg',
    supportedLngs: ['bg', 'en'],
    detection: {
      order: ['localStorage'],
      caches: ['localStorage'],
      lookupLocalStorage: 'vbar_lang',
    },
    interpolation: {
      escapeValue: false,
    },
  })

if (typeof document !== 'undefined') {
  document.documentElement.lang = i18n.language || 'bg'
  i18n.on('languageChanged', lng => {
    document.documentElement.lang = lng
  })
}

export default i18n
