import { useEffect, useState } from 'react';
import { Pressable, View } from 'react-native';
import { useTranslation } from 'react-i18next';
import { useTheme } from '../../app/providers/ThemeProvider';
import { AppText } from '../../components/ui/AppText';
import { getNotificationPreferences, updateNotificationPreferences, type NotificationPreferences } from '../notifications/api/notifications.api';
import { configurePushChannels, readPushPermission } from '../../services/push/pushBridge';

const fields = ['chatEnabled', 'workoutRemindersEnabled', 'workoutUpdatesEnabled', 'progressEnabled', 'accountEnabled', 'soundEnabled', 'vibrationEnabled'] as const;

export function NotificationPreferencesCard() {
  const theme = useTheme();
  const { t } = useTranslation();
  const [preferences, setPreferences] = useState<NotificationPreferences | null>(null);
  const [permission, setPermission] = useState<'granted' | 'denied' | 'unknown'>('unknown');

  useEffect(() => {
    let active = true;
    void getNotificationPreferences().then((value) => {
      if (active) {
        setPreferences(value);
      }
    }).catch(() => undefined);
    void readPushPermission().then((value) => {
      if (active) {
        setPermission(value);
      }
    });
    return () => {
      active = false;
    };
  }, []);

  async function toggle(field: (typeof fields)[number]) {
    if (!preferences) {
      return;
    }
    const next = { ...preferences, [field]: !preferences[field] };
    setPreferences(next);
    try {
      setPreferences(await updateNotificationPreferences(next));
      await configurePushChannels(next.soundEnabled, next.vibrationEnabled);
    } catch {
      setPreferences(preferences);
    }
  }

  if (!preferences) {
    return <AppText tone="muted">{t('settings.notificationsLoading')}</AppText>;
  }

  return (
    <View style={{ gap: theme.spacing.sm }}>
      <AppText variant="title">{t('settings.notifications')}</AppText>
      {permission === 'denied' ? <AppText tone="muted">{t('settings.notificationsBlocked')}</AppText> : null}
      {fields.map((field) => (
        <Pressable accessibilityRole="button" accessibilityState={{ selected: preferences[field] }} key={field} onPress={() => { void toggle(field); }} style={{ justifyContent: 'center', minHeight: theme.control.minTouch }}>
          <AppText tone={preferences[field] ? 'accent' : 'default'}>{t(`settings.${field}`)}</AppText>
        </Pressable>
      ))}
    </View>
  );
}
