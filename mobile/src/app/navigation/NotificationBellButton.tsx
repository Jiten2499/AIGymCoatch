import { CommonActions, useNavigation } from '@react-navigation/native';
import { useTranslation } from 'react-i18next';
import { Pressable, View } from 'react-native';
import { useTheme } from '../providers/ThemeProvider';
import { useNotificationSummary } from '../../features/notifications/api/queries';
import { TabGlyph } from './TabGlyph';

export function NotificationBellButton() {
  const theme = useTheme();
  const { t } = useTranslation();
  const navigation = useNavigation();
  const summary = useNotificationSummary();
  const unread = summary.data?.unreadCount ?? 0;

  return (
    <Pressable
      accessibilityLabel={t('nav.notifications')}
      accessibilityRole="button"
      hitSlop={8}
      onPress={() => navigation.dispatch(CommonActions.navigate({ name: 'Notifications' }))}
      style={{
        alignItems: 'center',
        justifyContent: 'center',
        marginRight: 4,
        minHeight: theme.control.minTouch,
        minWidth: theme.control.minTouch,
      }}
    >
      <View>
        <TabGlyph color={theme.colors.text} name="bell" size={22} />
        {unread > 0 ? (
          <View
            accessibilityElementsHidden
            importantForAccessibility="no"
            style={{
              backgroundColor: theme.colors.accent,
              borderRadius: 4,
              height: 8,
              position: 'absolute',
              right: -2,
              top: -2,
              width: 8,
            }}
          />
        ) : null}
      </View>
    </Pressable>
  );
}
