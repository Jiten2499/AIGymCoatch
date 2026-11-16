import { createBottomTabNavigator } from '@react-navigation/bottom-tabs';
import { useTranslation } from 'react-i18next';
import { ChatListScreen } from '../../features/chat/screens/ChatListScreen';
import { ChatTabBadge } from './ChatTabBadge';
import { TrainerIcon } from '../../features/trainers/icons';
import { TrainerHomeScreen } from '../../features/trainers/screens/TrainerHomeScreen';
import { TrainerMembersScreen } from '../../features/trainers/screens/TrainerMembersScreen';
import { TrainerMoreScreen } from '../../features/trainers/screens/TrainerMoreScreen';
import { TrainerPlansScreen } from '../../features/trainers/screens/TrainerPlansScreen';
import { featureCodes } from '../../constants/access';
import { useWorkspaceFeatures } from '../../features/licensing/api/featureQuery';
import { useTheme } from '../providers/ThemeProvider';
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
        tabBarActiveTintColor: theme.colors.accent,
        tabBarInactiveTintColor: theme.colors.muted,
        tabBarLabelPosition: 'below-icon',
        tabBarStyle: { backgroundColor: theme.colors.surface, borderTopColor: theme.colors.border },
      }}
    >
      <Tabs.Screen name="Home" component={TrainerHomeScreen} options={{ headerShown: false, title: t('nav.dashboard'), tabBarIcon: ({ color, size }) => <TrainerIcon color={color} name="home" size={size} /> }} />
      <Tabs.Screen name="Members" component={TrainerMembersScreen} options={{ headerShown: false, title: t('trainer.myMembers'), tabBarIcon: ({ color, size }) => <TrainerIcon color={color} name="users" size={size} /> }} />
      <Tabs.Screen name="Plans" component={TrainerPlansScreen} options={{ headerShown: false, title: t('trainer.plans'), tabBarIcon: ({ color, size }) => <TrainerIcon color={color} name="clipboard" size={size} /> }} />
      <Tabs.Screen name="Chats" component={ChatListScreen} options={{ title: t('nav.chat'), tabBarButton: chatEnabled ? undefined : () => null, tabBarIcon: ({ color, size }) => <ChatTabBadge><TrainerIcon color={color} name="chat" size={size} /></ChatTabBadge> }} />
      <Tabs.Screen name="More" component={TrainerMoreScreen} options={{ title: t('nav.more'), tabBarIcon: ({ color, size }) => <TrainerIcon color={color} name="ellipsis" size={size} /> }} />
    </Tabs.Navigator>
  );
}
