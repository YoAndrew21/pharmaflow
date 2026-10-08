import i18n from 'i18next'
import { initReactI18next } from 'react-i18next'
import { en } from './en'
import es from './es.json'
import ja from './ja.json'
import { locales, readLocale, isLocale } from '../features/preferences/locale'

export const resources = {
  en: { translation: en },
  es: { translation: es satisfies typeof en },
  ja: { translation: ja satisfies typeof en },
}

function updateDocumentLanguage(language: string) {
  document.documentElement.lang = isLocale(language) ? language : 'en'
}

i18n.on('languageChanged', updateDocumentLanguage)

void i18n.use(initReactI18next).init({
  resources,
  lng: readLocale(),
  fallbackLng: 'en',
  supportedLngs: [...locales],
  interpolation: { escapeValue: false },
})

export default i18n
