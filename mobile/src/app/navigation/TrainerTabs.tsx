import { createBottomTabNavigator } from '@react-navigation/bottom-tabs';
import { useTranslation } from 'react-i18next';
import { featureCodes } from '../../constants/access';
import { ChatListScreen } from '../../features/chat/screens/ChatListScreen';
import { useWorkspaceFeatures } from '../../features/licensing/api/featureQuery';
import { useTheme } from '../providers/ThemeProvider';
import { TrainerHomeScreen } from '../../features/trainers/screens/TrainerHomeScreen';
import { TrainerMembersScreen } from '../../features/trainers/screens/TrainerMembersScreen';
import { TrainerMoreScreen } from '../../features/trainers/screens/TrainerMoreScreen';
import { TrainerPerformanceScreen } from '../../features/trainers/screens/TrainerPerformanceScreen';
import { TrainerScheduleScreen } from '../../features/trainers/screens/TrainerScheduleScreen';
import { NotificationBellButton } from './NotificationBellButton';
import { TabGlyph } from './TabGlyph';
import type { TrainerTabParamList } from './types';

const Tabs = createBottomTabNavigator<TrainerTabParamList>();

export function TrainerTabs() {
  const theme = useTheme();
  const { t } = useTranslation();
  const features = useWorkspaceFeatures();
  const chatEnabled = features.data?.[featureCodes.gymChat] === true;
  return (
    <Tabs.Navigator
      screenOptions={{
        headerShadowVisible: false,
        headerStyle: { backgroundColor: theme.colors.background },
        headerTintColor: theme.colors.text,
        headerRight: () => <NotificationBellButton />,
        tabBarActiveTintColor: theme.colors.accent,
        tabBarInactiveTintColor: theme.colors.text,
        tabBarLabelPosition: 'below-icon',
        tabBarStyle: { backgroundColor: theme.colors.surface, borderTopColor: theme.colors.border },
      }}
    >
      <Tabs.Screen name="Home" component={TrainerHomeScreen} options={{ title: t('nav.dashboard'), tabBarIcon: ({ color, size }) => <TabGlyph color={color} name="home" size={size} /> }} />
      <Tabs.Screen name="Schedule" component={TrainerScheduleScreen} options={{ title: t('trainer.schedule'), tabBarIcon: ({ color, size }) => <TabGlyph color={color} name="calendar" size={size} /> }} />
      <Tabs.Screen name="Members" component={TrainerMembersScreen} options={{ title: t('trainer.members'), tabBarIcon: ({ color, size }) => <TabGlyph color={color} name="people" size={size} /> }} />
      <Tabs.Screen name="Performance" component={TrainerPerformanceScreen} options={{ title: t('trainer.performance'), tabBarIcon: ({ color, size }) => <TabGlyph color={color} name="chart" size={size} /> }} />
      <Tabs.Screen
        name="Chats"
        component={ChatListScreen}
        options={{
          title: t('nav.chat'),
          tabBarIcon: ({ color, size }) => <TabGlyph color={color} name="chat" size={size} />,
          tabBarButton: chatEnabled ? undefined : () => null,
        }}
      />
      <Tabs.Screen name="More" component={TrainerMoreScreen} options={{ title: t('nav.more'), tabBarIcon: ({ color, size }) => <TabGlyph color={color} name="more" size={size} /> }} />
    </Tabs.Navigator>
  );
}
