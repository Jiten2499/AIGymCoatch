import type { NativeStackScreenProps } from '@react-navigation/native-stack';
import { useTranslation } from 'react-i18next';
import { ScrollView } from 'react-native';
import type { AppStackParamList } from '../../../app/navigation/types';
import { useTheme } from '../../../app/providers/ThemeProvider';
import { FeatureGate } from '../../../components/access/FeatureGate';
import { ScreenState } from '../../../components/feedback/ScreenState';
import { AppButton } from '../../../components/ui/AppButton';
import { MemberPhoto } from '../../../components/ui/MemberPhoto';
import { AppText } from '../../../components/ui/AppText';
import { featureCodes } from '../../../constants/access';
import { errorText } from '../../../services/api/problem';
import { useWorkspaceFeatures } from '../../licensing/api/featureQuery';
import { MemberMemberships } from '../../memberships/screens/MemberMemberships';
import { useMember, useMemberStatus } from '../api/queries';
import { MemberAppAccessPanel } from './MemberAppAccess';

type Props = NativeStackScreenProps<AppStackParamList, 'MemberDetail'>;

export function MemberDetailScreen({ navigation, route }: Props) {
  return (
    <FeatureGate code={featureCodes.memberManagement}>
      <MemberDetail memberId={route.params.memberId} onEdit={() => navigation.navigate('MemberForm', { memberId: route.params.memberId })} />
    </FeatureGate>
  );
}

export function MemberDetail({ memberId, onEdit }: { memberId: string; onEdit: () => void }) {
  const theme = useTheme();
  const { t } = useTranslation();
  const features = useWorkspaceFeatures();
  const member = useMember(memberId, true);
  const status = useMemberStatus(memberId);
  const unauthorized = member.isError && errorText(member.error, '').toLowerCase().includes('permission');

  return (
    <ScreenState
      error={member.isError ? (unauthorized ? t('members.unauthorized') : errorText(member.error, t('common.offline'))) : null}
      loading={member.isLoading}
      onRetry={() => void member.refetch()}
    >
      {member.data ? (
        <ScrollView contentContainerStyle={{ gap: theme.spacing.md, padding: theme.spacing.xl }}>
          <MemberPhoto label={member.data.fullName} path={member.data.hasPhoto ? `/members/${memberId}/profile-photo` : null} size={120} />
          <AppText variant="title">{member.data.fullName}</AppText>
          <AppText>{member.data.phone ?? member.data.mobile}</AppText>
          <AppText tone="muted">{member.data.status} · {member.data.joinDate}</AppText>
          <AppText>{member.data.fitnessGoal}</AppText>
          <AppText>{member.data.address}</AppText>
          <AppText>{member.data.emergencyContactName} {member.data.emergencyContactRelationship} {member.data.emergencyContactPhone}</AppText>
          <AppText>{member.data.assignedTrainerName}</AppText>
          <MemberMemberships enabled={features.data?.[featureCodes.membershipManagement] === true} memberId={memberId} />
          <MemberAppAccessPanel enabled={features.data?.[featureCodes.memberManagement] === true} memberId={memberId} />
          <AppButton label={t('members.edit')} onPress={onEdit} variant="secondary" />
          <AppButton
            label={member.data.status === 'Active' ? t('members.inactive') : t('members.active')}
            loading={status.isPending}
            onPress={() => void status.mutate(member.data?.status === 'Active' ? 'Inactive' : 'Active')}
          />
        </ScrollView>
      ) : null}
    </ScreenState>
  );
}
