import { create } from 'zustand';
import { i18n, type LocaleId } from '../services/localization/i18n';
import { defaultPreferences, writePreferences, type AppearanceMode, type Preferences } from '../services/storage/preferences';
import type { AccentId } from '../theme';

type PreferencesState = Preferences & {
  hydrated: boolean;
  hydrate: (preferences: Preferences) => void;
  setAppearance: (appearance: AppearanceMode) => void;
  setAccent: (accent: AccentId) => void;
  setLocale: (locale: LocaleId) => void;
};

export const usePreferencesStore = create<PreferencesState>((set, get) => ({
  ...defaultPreferences,
  hydrated: false,
  hydrate: (preferences) => {
    void i18n.changeLanguage(preferences.locale);
    set({ ...preferences, hydrated: true });
  },
  setAppearance: (appearance) => {
    set({ appearance });
    void persist(get());
  },
  setAccent: (accent) => {
    set({ accent });
    void persist(get());
  },
  setLocale: (locale) => {
    void i18n.changeLanguage(locale);
    set({ locale });
    void persist(get());
  },
}));

function persist(state: PreferencesState): Promise<void> {
  return writePreferences({ appearance: state.appearance, accent: state.accent, locale: state.locale });
}
