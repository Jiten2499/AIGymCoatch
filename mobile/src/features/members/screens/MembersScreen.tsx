import type { NativeStackNavigationProp } from '@react-navigation/native-stack';
import { useNavigation } from '@react-navigation/native';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Pressable, ScrollView, View } from 'react-native';
import type { AppStackParamList } from '../../../app/navigation/types';
import { useTheme } from '../../../app/providers/ThemeProvider';
import { FeatureGate } from '../../../components/access/FeatureGate';
import { ScreenState } from '../../../components/feedback/ScreenState';
import { AppButton } from '../../../components/ui/AppButton';
import { AppCard } from '../../../components/ui/AppCard';
import { AppText } from '../../../components/ui/AppText';
import { MemberPhoto } from '../../../components/ui/MemberPhoto';
import { AppTextInput } from '../../../components/ui/AppTextInput';
import { featureCodes } from '../../../constants/access';
import { useWorkspaceFeatures } from '../../licensing/api/featureQuery';
import { errorText } from '../../../services/api/problem';
import { useMembers } from '../api/queries';
import type { MemberListItem, MemberStatus } from '../api/members.api';

const filters = ['', 'Active', 'Inactive'] as const;

export function MembersScreen() {
  const features = useWorkspaceFeatures();
  const enabled = features.data?.[featureCodes.memberManagement] === true;
  return (
    <FeatureGate code={featureCodes.memberManagement}>
      <MembersList enabled={enabled} />
    </FeatureGate>
  );
}

export function MembersList({ enabled }: { enabled: boolean }) {
  const theme = useTheme();
  const { t } = useTranslation();
  const navigation = useNavigation<NativeStackNavigationProp<AppStackParamList>>();
  const [term, setTerm] = useState('');
  const [submitted, setSubmitted] = useState('');
  const [status, setStatus] = useState<'' | MemberStatus>('');
  const [page, setPage] = useState(1);
  const members = useMembers(submitted, status, page, enabled);
  const items = members.data?.items ?? [];
  const unauthorized = members.isError && errorText(members.error, '').toLowerCase().includes('permission');

  return (
    <ScreenState
      empty={members.isFetched && items.length === 0}
      emptyBody={t('members.empty')}
      emptyTitle={t('members.title')}
      error={members.isError ? (unauthorized ? t('members.unauthorized') : errorText(members.error, t('common.offline'))) : null}
      loading={members.isLoading}
      onRetry={() => void members.refetch()}
    >
      <ScrollView contentContainerStyle={{ gap: theme.spacing.md, padding: theme.spacing.xl }}>
        <AppText variant="title">{t('members.title')}</AppText>
        <AppTextInput accessibilityLabel={t('members.search')} onChangeText={(value) => { setTerm(value); setPage(1); setSubmitted(value.trim()); }} placeholder={t('members.search')} value={term} />
        <ScrollView horizontal contentContainerStyle={{ gap: theme.spacing.sm }}>
          {filters.map((filter) => (
            <Pressable accessibilityRole="button" key={filter || 'all'} onPress={() => { setPage(1); setStatus(filter); }}>
              <AppText tone={status === filter ? 'accent' : 'default'}>{t(filter === 'Active' ? 'members.active' : filter === 'Inactive' ? 'members.inactive' : 'members.all')}</AppText>
            </Pressable>
          ))}
        </ScrollView>
        {items.map((member) => (
          <MemberRow key={member.id} member={member} onPress={() => navigation.navigate('MemberDetail', { memberId: member.id })} />
        ))}
        {members.data && page * members.data.pageSize < members.data.totalCount ? <AppButton label={t('members.loadMore')} onPress={() => setPage((current) => current + 1)} variant="secondary" /> : null}
        <AppButton label={t('members.add')} onPress={() => navigation.navigate('MemberForm')} />
      </ScrollView>
    </ScreenState>
  );
}

function MemberRow({ member, onPress }: { member: MemberListItem; onPress: () => void }) {
  return (
    <AppCard>
      <Pressable accessibilityRole="button" onPress={onPress} style={{ alignItems: 'center', flexDirection: 'row', gap: 12 }}>
        <MemberPhoto label={member.fullName} path={member.hasPhoto ? `/members/${member.id}/profile-photo` : null} />
        <View style={{ flex: 1 }}>
          <AppText variant="label">{member.fullName}</AppText>
          <AppText tone="muted">{member.mobile ?? member.status}</AppText>
          <AppText tone="muted">{member.status} · {member.joinDate}</AppText>
        </View>
      </Pressable>
    </AppCard>
  );
}
