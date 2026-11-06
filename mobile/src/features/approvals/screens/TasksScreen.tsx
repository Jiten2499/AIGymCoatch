import { useNavigation } from '@react-navigation/native';
import type { NativeStackNavigationProp } from '@react-navigation/native-stack';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
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
import type { ApprovalRequestItem } from '../api/approvals.api';
import { useApprovalRequests } from '../api/queries';

export function TasksScreen() {
  return (
    <FeatureGate code={featureCodes.membershipManagement} alternateCode={featureCodes.staffManagement}>
      <ApprovalList enabled />
    </FeatureGate>
  );
}

export function ApprovalList({ enabled }: { enabled: boolean }) {
  const theme = useTheme();
  const { t } = useTranslation();
  const navigation = useNavigation<NativeStackNavigationProp<AppStackParamList>>();
  const [status, setStatus] = useState('Pending');
  const [requestType, setRequestType] = useState('');
  const [page, setPage] = useState(1);
  const requests = useApprovalRequests(status, requestType, page, enabled);
  const items = requests.data?.items ?? [];
  const unauthorized = requests.isError && errorText(requests.error, '').toLowerCase().includes('permission');

  return (
    <ScreenState
      empty={requests.isFetched && items.length === 0}
      emptyBody={t('tasks.empty')}
      emptyTitle={t('tasks.approvals')}
      error={requests.isError ? (unauthorized ? t('tasks.unauthorized') : errorText(requests.error, t('common.offline'))) : null}
      loading={requests.isLoading}
      onRetry={() => void requests.refetch()}
    >
      <ScrollView contentContainerStyle={{ gap: theme.spacing.md, padding: theme.spacing.xl }}>
        <AppText variant="title">{t('nav.tasks')}</AppText>
        <AppText>{t('tasks.approvals')}</AppText>
        <Pressable accessibilityRole="button" onPress={() => { setPage(1); setStatus((current) => (current === 'Pending' ? '' : 'Pending')); }}>
          <AppText>{status || t('tasks.allStatuses')}</AppText>
        </Pressable>
        <Pressable accessibilityRole="button" onPress={() => { setPage(1); setRequestType((current) => (current === 'MembershipFreeze' ? 'MembershipExtension' : current === 'MembershipExtension' ? 'TrainerRewardAward' : current === 'TrainerRewardAward' ? '' : 'MembershipFreeze')); }}>
          <AppText>{requestType || t('tasks.allTypes')}</AppText>
        </Pressable>
        {items.map((item) => <RequestRow item={item} key={item.id} onOpen={() => navigation.navigate('ApprovalDetail', { requestId: item.id })} />)}
        {requests.data && requests.data.page * requests.data.pageSize < requests.data.totalCount ? <AppButton label={t('tasks.loadMore')} onPress={() => setPage((current) => current + 1)} /> : null}
      </ScrollView>
    </ScreenState>
  );
}

function RequestRow({ item, onOpen }: { item: ApprovalRequestItem; onOpen: () => void }) {
  const { t } = useTranslation();
  return (
    <AppCard>
      <Pressable accessibilityRole="button" onPress={onOpen}>
        <AppText>{item.title}</AppText>
        <AppText>{item.relatedLabel}</AppText>
        <AppText tone="muted">{t('tasks.requestedBy')} {item.requestedByName}</AppText>
        <AppText tone="muted">{item.summary}</AppText>
        <AppText tone="muted">{item.requestedAt} · {item.status}</AppText>
        <AppText>{t('tasks.review')}</AppText>
      </Pressable>
    </AppCard>
  );
}
