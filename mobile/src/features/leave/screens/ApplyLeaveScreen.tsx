import type { NativeStackScreenProps } from '@react-navigation/native-stack';
import { useMemo, useState } from 'react';
import { Pressable, ScrollView } from 'react-native';
import type { AppStackParamList } from '../../../app/navigation/types';
import { useTheme } from '../../../app/providers/ThemeProvider';
import { FeatureGate } from '../../../components/access/FeatureGate';
import { AppButton } from '../../../components/ui/AppButton';
import { AppText } from '../../../components/ui/AppText';
import { AppTextInput } from '../../../components/ui/AppTextInput';
import { featureCodes } from '../../../constants/access';
import { errorText } from '../../../services/api/problem';
import { leaveDuration } from '../api/leaves.api';
import { useApplyLeave } from '../api/queries';

const TYPES = ['Annual', 'Sick', 'Casual', 'Unpaid', 'Other'] as const;

type Props = NativeStackScreenProps<AppStackParamList, 'ApplyLeave'>;

export function ApplyLeaveScreen({ navigation }: Props) {
  return (
    <FeatureGate code={featureCodes.staffLeave}>
      <ApplyLeaveForm onSaved={() => navigation.replace('MyLeaves')} />
    </FeatureGate>
  );
}

export function ApplyLeaveForm({ onSaved }: { onSaved: () => void }) {
  const theme = useTheme();
  const apply = useApplyLeave();
  const [leaveType, setLeaveType] = useState<(typeof TYPES)[number]>('Annual');
  const [startDate, setStartDate] = useState('');
  const [endDate, setEndDate] = useState('');
  const [reason, setReason] = useState('');
  const [attachmentFileId, setAttachmentFileId] = useState('');
  const duration = useMemo(() => leaveDuration(startDate, endDate), [startDate, endDate]);

  return (
    <ScrollView contentContainerStyle={{ gap: theme.spacing.md, padding: theme.spacing.xl }}>
      <AppText variant="title">Apply Leave</AppText>
      <AppText>Leave type</AppText>
      {TYPES.map((type) => (
        <Pressable accessibilityRole="button" key={type} onPress={() => setLeaveType(type)}>
          <AppText>{leaveType === type ? `● ${type}` : `○ ${type}`}</AppText>
        </Pressable>
      ))}
      <AppTextInput accessibilityLabel="Start date" onChangeText={setStartDate} placeholder="Start date YYYY-MM-DD" value={startDate} />
      <AppTextInput accessibilityLabel="End date" onChangeText={setEndDate} placeholder="End date YYYY-MM-DD" value={endDate} />
      <AppText>Duration: {duration > 0 ? `${duration} day(s)` : 'Enter valid dates'}</AppText>
      <AppTextInput accessibilityLabel="Reason" onChangeText={setReason} placeholder="Reason" value={reason} />
      <AppTextInput accessibilityLabel="Attachment file id" onChangeText={setAttachmentFileId} placeholder="Optional Drive file id" value={attachmentFileId} />
      {apply.isError ? <AppText>{errorText(apply.error, 'Leave could not be submitted.')}</AppText> : null}
      <AppButton
        label={apply.isPending ? 'Submitting...' : 'Submit leave'}
        onPress={() =>
          void apply
            .mutateAsync({
              leaveType,
              startDate,
              endDate,
              reason,
              attachmentFileId: attachmentFileId.trim() || null,
            })
            .then(onSaved)
        }
      />
    </ScrollView>
  );
}
