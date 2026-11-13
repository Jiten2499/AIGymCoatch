import { useState } from 'react';
import { Pressable, ScrollView, View } from 'react-native';
import { useTranslation } from 'react-i18next';
import { useTheme } from '../../../app/providers/ThemeProvider';
import { AppButton } from '../../../components/ui/AppButton';
import { AppCard } from '../../../components/ui/AppCard';
import { AppText } from '../../../components/ui/AppText';
import { endSession } from '../../../services/auth/session';
import { supportedLocales, type LocaleId } from '../../../services/localization/i18n';
import { usePreferencesStore } from '../../../store/preferencesStore';
import { accents, type AccentId } from '../../../theme';
import type { AppearanceMode } from '../../../services/storage/preferences';

const modes: AppearanceMode[] = ['system', 'light', 'dark'];

export function SettingsScreen() {
  const theme = useTheme();
  const { t } = useTranslation();
  const appearance = usePreferencesStore((state) => state.appearance);
  const accent = usePreferencesStore((state) => state.accent);
  const locale = usePreferencesStore((state) => state.locale);
  const setAppearance = usePreferencesStore((state) => state.setAppearance);
  const setAccent = usePreferencesStore((state) => state.setAccent);
  const setLocale = usePreferencesStore((state) => state.setLocale);
  const [signingOut, setSigningOut] = useState(false);

  return (
    <ScrollView contentContainerStyle={{ gap: theme.spacing.xl, padding: theme.spacing.xl }}>
      <View style={{ gap: theme.spacing.sm }}>
        <AppText variant="title">{t('settings.appearance')}</AppText>
        {modes.map((mode) => (
          <Choice key={mode} label={t(`settings.${mode}`)} selected={appearance === mode} onPress={() => setAppearance(mode)} />
        ))}
      </View>
      <View style={{ gap: theme.spacing.sm }}>
        <AppText variant="title">{t('settings.accent')}</AppText>
        <View style={{ flexDirection: 'row', gap: theme.spacing.md }}>
          {(Object.keys(accents) as AccentId[]).map((id) => (
            <Pressable
              accessibilityLabel={id}
              accessibilityRole="button"
              accessibilityState={{ selected: accent === id }}
              key={id}
              onPress={() => setAccent(id)}
              style={{ backgroundColor: accents[id][theme.scheme], borderColor: accent === id ? theme.colors.text : 'transparent', borderRadius: theme.radius.pill, borderWidth: 2, height: theme.control.minTouch, width: theme.control.minTouch }}
            />
          ))}
        </View>
      </View>
      <View style={{ gap: theme.spacing.sm }}>
        <AppText variant="title">{t('settings.language')}</AppText>
        {supportedLocales.map((id) => (
          <Choice key={id} label={t(`language.${id}`)} selected={locale === id} onPress={() => setLocale(id as LocaleId)} />
        ))}
        <AppText tone="muted">{t('settings.languageFallback')}</AppText>
      </View>
      <AppCard>
        <AppText variant="title">{t('settings.account')}</AppText>
        <AppText tone="muted">{t('settings.preferencesLocal')}</AppText>
        <AppText tone="muted">{t('settings.notificationsDeferred')}</AppText>
        <AppButton label={t('profile.logout')} loading={signingOut} onPress={() => { setSigningOut(true); void endSession(); }} />
      </AppCard>
    </ScrollView>
  );
}

function Choice({ label, selected, onPress }: { label: string; selected: boolean; onPress: () => void }) {
  const theme = useTheme();
  return (
    <Pressable accessibilityRole="button" accessibilityState={{ selected }} onPress={onPress} style={{ justifyContent: 'center', minHeight: theme.control.minTouch }}>
      <AppText tone={selected ? 'accent' : 'default'}>{label}</AppText>
    </Pressable>
  );
}
