import type { AccentId } from '../../theme';
import { detectDeviceLocale } from '../localization/deviceLocale';
import { isLocaleId, type LocaleId } from '../localization/i18n';
import { getSecretDriver } from './secretDriver';

export type AppearanceMode = 'system' | 'light' | 'dark';

export type Preferences = {
  appearance: AppearanceMode;
  accent: AccentId;
  locale: LocaleId;
};

export const defaultPreferences: Preferences = {
  appearance: 'system',
  accent: 'lime',
  locale: 'en',
};

const key = 'lifefit.preferences';
const accents: AccentId[] = ['lime', 'teal', 'blue', 'violet', 'coral'];

export async function readPreferences(): Promise<Preferences> {
  const raw = await getSecretDriver().get(key);
  if (!raw) {
    return { ...defaultPreferences, locale: detectDeviceLocale() };
  }
  try {
    const parsed = JSON.parse(raw) as Partial<Preferences>;
    return {
      appearance: parsed.appearance === 'light' || parsed.appearance === 'dark' ? parsed.appearance : 'system',
      accent: parsed.accent && accents.includes(parsed.accent) ? parsed.accent : 'lime',
      locale: parsed.locale && isLocaleId(parsed.locale) ? parsed.locale : 'en',
    };
  } catch {
    return defaultPreferences;
  }
}

export async function writePreferences(preferences: Preferences): Promise<void> {
  await getSecretDriver().set(key, JSON.stringify(preferences));
}
