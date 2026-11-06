import { useNavigation } from '@react-navigation/native';
import type { NativeStackNavigationProp } from '@react-navigation/native-stack';
import { Pressable, ScrollView } from 'react-native';
import type { AppStackParamList } from '../../../app/navigation/types';
import { useTheme } from '../../../app/providers/ThemeProvider';
import { FeatureGate } from '../../../components/access/FeatureGate';
import { ScreenState } from '../../../components/feedback/ScreenState';
import { AppButton } from '../../../components/ui/AppButton';
import { AppCard } from '../../../components/ui/AppCard';
import { AppText } from '../../../components/ui/AppText';
import { featureCodes } from '../../../constants/access';
import { errorText } from '../../../services/api/problem';
import type { StaffLeaveListItem } from '../api/leaves.api';
import { useMyLeaves } from '../api/queries';

export function MyLeavesScreen() {
  return (
    <FeatureGate code={featureCodes.staffLeave}>
      <MyLeavesList enabled />
    </FeatureGate>
  );
}

export function MyLeavesList({ enabled }: { enabled: boolean }) {
  const theme = useTheme();
  const navigation = useNavigation<NativeStackNavigationProp<AppStackParamList>>();
  const leaves = useMyLeaves(enabled);
  const items = leaves.data ?? [];
  const unauthorized = leaves.isError && errorText(leaves.error, '').toLowerCase().includes('permission');

  return (
    <ScreenState
      empty={leaves.isFetched && items.length === 0}
      emptyBody="Submit a leave request when you need time away."
      emptyTitle="No leave requests"
      error={leaves.isError ? (unauthorized ? 'You do not have permission to view leave requests.' : errorText(leaves.error, 'Unable to load leave requests.')) : null}
      loading={leaves.isLoading}
      onRetry={() => void leaves.refetch()}
    >
      <ScrollView contentContainerStyle={{ gap: theme.spacing.md, padding: theme.spacing.xl }}>
        <AppText variant="title">My Leaves</AppText>
        <AppButton label="Apply Leave" onPress={() => navigation.navigate('ApplyLeave')} />
        {items.map((item) => (
          <LeaveRow item={item} key={item.id} onOpen={() => navigation.navigate('LeaveDetail', { leaveId: item.id })} />
        ))}
      </ScrollView>
    </ScreenState>
  );
}

function LeaveRow({ item, onOpen }: { item: StaffLeaveListItem; onOpen: () => void }) {
  return (
    <AppCard>
      <Pressable accessibilityRole="button" onPress={onOpen}>
        <AppText>{item.leaveType}</AppText>
        <AppText tone="muted">{item.startDate} → {item.endDate} · {item.durationDays} day(s)</AppText>
        <AppText tone="muted">{item.status} · {item.reason}</AppText>
      </Pressable>
    </AppCard>
  );
}
