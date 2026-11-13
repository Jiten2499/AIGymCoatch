import type { NativeStackNavigationProp } from '@react-navigation/native-stack';
import { useNavigation } from '@react-navigation/native';
import { useQuery } from '@tanstack/react-query';
import { Pressable, ScrollView } from 'react-native';
import { useTranslation } from 'react-i18next';
import { getApprovalCount } from '../approvals/api/approvals.api';
import type { AppStackParamList } from '../../app/navigation/types';
import { useTheme } from '../../app/providers/ThemeProvider';
import { RequireAccess } from '../../components/access/RequireAccess';
import { AppText } from '../../components/ui/AppText';
import { canAccessBusiness, canManageLicenses, featureCodes } from '../../constants/access';
import { useWorkspaceFeatures } from '../licensing/api/featureQuery';
import { useSessionStore } from '../../store/sessionStore';
import { useWorkspaceStore } from '../../store/workspaceStore';

const memberLinks = ['Fitness', 'AiCoach', 'Challenges', 'Profile', 'Settings'] as const;
const businessLinks = ['BusinessDashboard', 'Members', 'Staff', 'Trainers', 'Memberships', 'Tasks', 'Leads', 'Expenses', 'Ledger', 'Integrations', 'Attendance', 'MyLeaves', 'ApplyLeave', 'BusinessSettings'] as const;

export function MoreScreen() {
  const theme = useTheme();
  const { t } = useTranslation();
  const navigation = useNavigation<NativeStackNavigationProp<AppStackParamList>>();
  const current = useSessionStore((state) => state.currentTenant);
  const workspaces = useWorkspaceStore((state) => state.workspaces);
  const roles = workspaces.find((workspace) => workspace.id === current?.id)?.roles ?? [];
  const business = current ? canAccessBusiness(current.type, roles) : false;
  const isTrainer = roles.includes('Trainer');
  const licenses = current ? canManageLicenses(current.type, roles) : false;
  const features = useWorkspaceFeatures();
  const membersEnabled = features.data?.[featureCodes.memberManagement] === true;
  const membershipsEnabled = features.data?.[featureCodes.membershipManagement] === true;
  const leadsEnabled = features.data?.[featureCodes.gymCrm] === true;
  const expensesEnabled = features.data?.[featureCodes.gymExpenseManagement] === true;
  const ledgerEnabled = features.data?.[featureCodes.gymFinancialLedger] === true;
  const integrationsEnabled = features.data?.[featureCodes.googleDriveStorage] === true;
  const leaveEnabled = features.data?.[featureCodes.staffLeave] === true;
  const chatEnabled = current?.type !== 'Personal' && features.data?.[featureCodes.gymChat] === true;
  const approvals = useQuery({
    queryKey: ['approval-count', current?.id ?? ''],
    enabled: business && Boolean(current?.id),
    retry: false,
    queryFn: ({ signal }) => getApprovalCount(signal),
  });
  const links = businessLinks.filter((route) => (route !== 'Members' || membersEnabled) && (route !== 'Memberships' || membershipsEnabled) && (route !== 'Tasks' || approvals.isSuccess) && (route !== 'Leads' || leadsEnabled) && (route !== 'Expenses' || expensesEnabled) && (route !== 'Ledger' || ledgerEnabled) && (route !== 'Integrations' || integrationsEnabled) && ((route !== 'MyLeaves' && route !== 'ApplyLeave') || leaveEnabled));

  return (
    <ScrollView contentContainerStyle={{ gap: theme.spacing.sm, padding: theme.spacing.xl }}>
      {chatEnabled ? <Link label={t('nav.chat')} onPress={() => navigation.navigate('Chats')} /> : null}
      {memberLinks.map((route) => (
        <Link key={route} label={t(labelKey(route))} onPress={() => navigation.navigate(route)} />
      ))}
      {isTrainer ? <Link label={t('trainerTargets.title')} onPress={() => navigation.navigate('MyTargets')} /> : null}
      <RequireAccess allowed={business}>
        {links.map((route) => (
          <Link key={route} label={route === 'Tasks' && (approvals.data?.pendingCount ?? 0) > 0 ? `${t(labelKey(route))} (${approvals.data?.pendingCount})` : t(labelKey(route))} onPress={() => navigation.navigate(route)} />
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
    Profile: 'nav.profile',
    Settings: 'nav.settings',
    BusinessDashboard: 'nav.business',
    Members: 'nav.members',
    Staff: 'nav.staff',
    Trainers: 'nav.trainers',
    MyTargets: 'trainerTargets.title',
    Memberships: 'nav.memberships',
    Tasks: 'nav.tasks',
    Leads: 'nav.leads',
    Expenses: 'nav.expenses',
    Ledger: 'nav.ledger',
    Integrations: 'nav.integrations',
    Attendance: 'nav.attendance',
    MyLeaves: 'nav.myLeaves',
    ApplyLeave: 'nav.applyLeave',
    BusinessSettings: 'nav.businessSettings',
  };
  return keys[route] ?? 'nav.more';
}
