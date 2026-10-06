import type { NativeStackNavigationProp } from '@react-navigation/native-stack';
import { useNavigation } from '@react-navigation/native';
import { Pressable, ScrollView } from 'react-native';
import { useTranslation } from 'react-i18next';
import type { AppStackParamList } from '../../app/navigation/types';
import { useTheme } from '../../app/providers/ThemeProvider';
import { RequireAccess } from '../../components/access/RequireAccess';
import { AppText } from '../../components/ui/AppText';
import { canAccessBusiness, canManageLicenses } from '../../constants/access';
import { useSessionStore } from '../../store/sessionStore';
import { useWorkspaceStore } from '../../store/workspaceStore';

const memberLinks = ['Fitness', 'AiCoach', 'Challenges', 'Notifications', 'Profile', 'Settings'] as const;
const businessLinks = ['BusinessDashboard', 'Members', 'Staff', 'Trainers', 'Memberships', 'Attendance', 'BusinessSettings'] as const;

export function MoreScreen() {
  const theme = useTheme();
  const { t } = useTranslation();
  const navigation = useNavigation<NativeStackNavigationProp<AppStackParamList>>();
  const current = useSessionStore((state) => state.currentTenant);
  const workspaces = useWorkspaceStore((state) => state.workspaces);
  const roles = workspaces.find((workspace) => workspace.id === current?.id)?.roles ?? [];
  const business = current ? canAccessBusiness(current.type, roles) : false;
  const licenses = current ? canManageLicenses(current.type, roles) : false;

  return (
    <ScrollView contentContainerStyle={{ gap: theme.spacing.sm, padding: theme.spacing.xl }}>
      {memberLinks.map((route) => (
        <Link key={route} label={t(labelKey(route))} onPress={() => navigation.navigate(route)} />
      ))}
      <RequireAccess allowed={business}>
        {businessLinks.map((route) => (
          <Link key={route} label={t(labelKey(route))} onPress={() => navigation.navigate(route)} />
        ))}
      </RequireAccess>
      <RequireAccess allowed={licenses}>
        <Link label={t('nav.licenses')} onPress={() => navigation.navigate('Licenses')} />
      </RequireAccess>
    </ScrollView>
  );
}

function Link({ label, onPress }: { label: string; onPress: () => void }) {
  const theme = useTheme();
  return (
    <Pressable accessibilityRole="button" onPress={onPress} style={{ justifyContent: 'center', minHeight: theme.control.minTouch }}>
      <AppText>{label}</AppText>
    </Pressable>
  );
}

function labelKey(route: string): string {
  const keys: Record<string, string> = {
    Fitness: 'nav.fitness',
    AiCoach: 'nav.ai',
    Challenges: 'nav.challenges',
    Notifications: 'nav.notifications',
    Profile: 'nav.profile',
    Settings: 'nav.settings',
    BusinessDashboard: 'nav.business',
    Members: 'nav.members',
    Staff: 'nav.staff',
    Trainers: 'nav.trainers',
    Memberships: 'nav.memberships',
    Attendance: 'nav.attendance',
    BusinessSettings: 'nav.businessSettings',
  };
  return keys[route] ?? 'nav.more';
}
