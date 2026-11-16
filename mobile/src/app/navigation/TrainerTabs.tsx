import { createBottomTabNavigator } from '@react-navigation/bottom-tabs';
import { useTranslation } from 'react-i18next';
import { useTheme } from '../providers/ThemeProvider';
import { TrainerIcon } from '../../features/trainers/icons';
import { TrainerHomeScreen } from '../../features/trainers/screens/TrainerHomeScreen';
import { TrainerMembersScreen } from '../../features/trainers/screens/TrainerMembersScreen';
import { TrainerMoreScreen } from '../../features/trainers/screens/TrainerMoreScreen';
import { TrainerPlansScreen } from '../../features/trainers/screens/TrainerPlansScreen';
import type { TrainerTabParamList } from './types';

const Tabs = createBottomTabNavigator<TrainerTabParamList>();

export function TrainerTabs() {
  const theme = useTheme();
  const { t } = useTranslation();
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
      <Tabs.Screen name="More" component={TrainerMoreScreen} options={{ title: t('nav.more'), tabBarIcon: ({ color, size }) => <TrainerIcon color={color} name="ellipsis" size={size} /> }} />
    </Tabs.Navigator>
  );
}
