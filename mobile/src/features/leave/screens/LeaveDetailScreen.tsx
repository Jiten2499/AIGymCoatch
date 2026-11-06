import type { NativeStackScreenProps } from '@react-navigation/native-stack';
import { ScrollView } from 'react-native';
import type { AppStackParamList } from '../../../app/navigation/types';
import { useTheme } from '../../../app/providers/ThemeProvider';
import { FeatureGate } from '../../../components/access/FeatureGate';
import { ScreenState } from '../../../components/feedback/ScreenState';
import { AppButton } from '../../../components/ui/AppButton';
import { AppText } from '../../../components/ui/AppText';
import { featureCodes } from '../../../constants/access';
import { errorText } from '../../../services/api/problem';
import { useCancelLeave, useLeave } from '../api/queries';

type Props = NativeStackScreenProps<AppStackParamList, 'LeaveDetail'>;

export function LeaveDetailScreen({ navigation, route }: Props) {
  return (
    <FeatureGate code={featureCodes.staffLeave}>
      <LeaveDetail leaveId={route.params.leaveId} onCancelled={() => navigation.replace('MyLeaves')} />
    </FeatureGate>
  );
}

export function LeaveDetail({ leaveId, onCancelled }: { leaveId: string; onCancelled: () => void }) {
  const theme = useTheme();
  const leave = useLeave(leaveId, true);
  const cancel = useCancelLeave();
  const item = leave.data;

  return (
    <ScreenState
      error={leave.isError ? errorText(leave.error, 'Leave details could not be loaded.') : null}
      loading={leave.isLoading}
      onRetry={() => void leave.refetch()}
    >
      <ScrollView contentContainerStyle={{ gap: theme.spacing.md, padding: theme.spacing.xl }}>
        {item ? (
          <>
            <AppText variant="title">{item.leaveType}</AppText>
            <AppText>Status: {item.status}</AppText>
            <AppText>{item.startDate} → {item.endDate} ({item.durationDays} day(s))</AppText>
            <AppText>Reason: {item.reason}</AppText>
            {item.decisionComment ? <AppText>Decision: {item.decisionComment}</AppText> : null}
            {item.attachmentFileId ? <AppText>Attachment: {item.attachmentFileId}</AppText> : null}
            <AppText tone="muted">Submitted {new Date(item.submittedAt).toLocaleString()}</AppText>
            {item.canCancel ? (
              <AppButton
                label={cancel.isPending ? 'Cancelling...' : 'Cancel request'}
                onPress={() => void cancel.mutateAsync(item.id).then(onCancelled)}
              />
            ) : null}
            {cancel.isError ? <AppText>{errorText(cancel.error, 'Leave could not be cancelled.')}</AppText> : null}
          </>
        ) : null}
      </ScrollView>
    </ScreenState>
  );
}
