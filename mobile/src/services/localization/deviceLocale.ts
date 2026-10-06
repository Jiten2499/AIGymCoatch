import { getLocales } from 'expo-localization';
import { isLocaleId, type LocaleId } from './i18n';

export function detectDeviceLocale(): LocaleId {
  try {
    const code = getLocales()[0]?.languageCode ?? '';
    return isLocaleId(code) ? code : 'en';
  } catch {
    return 'en';
  }
}
