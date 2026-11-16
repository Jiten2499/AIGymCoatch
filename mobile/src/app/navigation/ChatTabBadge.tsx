import type { ReactNode } from 'react';
import { View } from 'react-native';
import { useChatSummary } from '../../features/chat/api/queries';
import { useTheme } from '../providers/ThemeProvider';

export function ChatTabBadge({ children }: { children: ReactNode }) {
  const theme = useTheme();
  const summary = useChatSummary();
  const unread = (summary.data?.unreadConversations ?? 0) > 0;

  return (
    <View>
      {children}
      {unread ? (
        <View
          accessibilityElementsHidden
          importantForAccessibility="no"
          style={{
            backgroundColor: theme.colors.danger,
            borderRadius: 5,
            height: 10,
            position: 'absolute',
            right: -4,
            top: -2,
            width: 10,
          }}
        />
      ) : null}
    </View>
  );
}
