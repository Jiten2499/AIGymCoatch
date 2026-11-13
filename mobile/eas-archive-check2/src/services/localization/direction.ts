import { i18n } from './i18n';

export function layoutDirection(locale: string): 'ltr' | 'rtl' {
  return i18n.dir(locale) === 'rtl' ? 'rtl' : 'ltr';
}
