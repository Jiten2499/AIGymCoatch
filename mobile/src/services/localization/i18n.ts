import i18n from 'i18next';
import { initReactI18next } from 'react-i18next';
import en from '../../locales/en/common.json';
import hi from '../../locales/hi/common.json';
import ne from '../../locales/ne/common.json';

export const supportedLocales = ['en', 'hi', 'ne'] as const;
export type LocaleId = (typeof supportedLocales)[number];

export function isLocaleId(value: string): value is LocaleId {
  return supportedLocales.includes(value as LocaleId);
}

void i18n.use(initReactI18next).init({
  resources: {
    en: { common: en },
    hi: { common: hi },
    ne: { common: ne },
  },
  lng: 'en',
  fallbackLng: 'en',
  defaultNS: 'common',
  interpolation: { escapeValue: false },
  returnNull: false,
});

export { i18n };
