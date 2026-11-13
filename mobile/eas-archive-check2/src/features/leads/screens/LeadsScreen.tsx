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
import { AppTextInput } from '../../../components/ui/AppTextInput';
import { featureCodes } from '../../../constants/access';
import { errorText } from '../../../services/api/problem';
import type { LeadListItem } from '../api/leads.api';
import { useFollowUpQueue, useLeads } from '../api/queries';

export function LeadsScreen() {
  return (
    <FeatureGate code={featureCodes.gymCrm}>
      <LeadList enabled />
    </FeatureGate>
  );
}

export function LeadList({ enabled }: { enabled: boolean }) {
  const theme = useTheme();
  const { t } = useTranslation();
  const navigation = useNavigation<NativeStackNavigationProp<AppStackParamList>>();
  const [term, setTerm] = useState('');
  const [submitted, setSubmitted] = useState('');
  const [status, setStatus] = useState('');
  const [source, setSource] = useState('');
  const [followUp, setFollowUp] = useState('');
  const [page, setPage] = useState(1);
  const leads = useLeads(submitted, status, source, followUp, page, enabled);
  const queue = useFollowUpQueue(followUp || 'today', enabled && followUp.length > 0);
  const items = leads.data?.items ?? [];
  const unauthorized = leads.isError && errorText(leads.error, '').toLowerCase().includes('permission');

  return (
    <ScreenState
      empty={leads.isFetched && items.length === 0}
      emptyBody={t('leads.empty')}
      emptyTitle={t('leads.title')}
      error={leads.isError ? (unauthorized ? t('leads.unauthorized') : errorText(leads.error, t('common.offline'))) : null}
      loading={leads.isLoading}
      onRetry={() => void leads.refetch()}
    >
      <ScrollView contentContainerStyle={{ gap: theme.spacing.md, padding: theme.spacing.xl }}>
        <AppText variant="title">{t('leads.title')}</AppText>
        <AppTextInput accessibilityLabel={t('leads.search')} onChangeText={(value) => { setTerm(value); setPage(1); setSubmitted(value.trim()); }} placeholder={t('leads.search')} value={term} />
        <Pressable accessibilityRole="button" onPress={() => { setPage(1); setStatus((current) => (current === 'New' ? '' : 'New')); }}>
          <AppText>{status || t('leads.status')}</AppText>
        </Pressable>
        <Pressable accessibilityLabel={t('leads.source')} accessibilityRole="button" onPress={() => { setPage(1); setSource((current) => (current === 'WalkIn' ? '' : 'WalkIn')); }}>
          <AppText>{source || t('leads.source')}</AppText>
        </Pressable>
        {(['today', 'overdue', 'upcoming'] as const).map((bucket) => (
          <Pressable accessibilityRole="button" key={bucket} onPress={() => { setPage(1); setFollowUp(bucket); }}>
            <AppText tone={followUp === bucket ? 'accent' : 'default'}>{t(`leads.${bucket}`)}</AppText>
          </Pressable>
        ))}
        {followUp ? <AppText>{queue.data?.totalCount ?? 0}</AppText> : null}
        {items.map((item) => <LeadRow item={item} key={item.id} onOpen={() => navigation.navigate('LeadDetail', { leadId: item.id })} />)}
        {leads.data && leads.data.page * leads.data.pageSize < leads.data.totalCount ? <AppButton label={t('leads.loadMore')} onPress={() => setPage((current) => current + 1)} /> : null}
        <AppButton label={t('leads.add')} onPress={() => navigation.navigate('LeadForm')} />
      </ScrollView>
    </ScreenState>
  );
}

function LeadRow({ item, onOpen }: { item: LeadListItem; onOpen: () => void }) {
  return (
    <AppCard>
      <Pressable accessibilityRole="button" onPress={onOpen}>
        <AppText>{item.fullName}</AppText>
        <AppText tone="muted">{item.mobile} · {item.source} · {item.status}</AppText>
        <AppText tone="muted">{item.interestedPackage ?? ''} {item.assignedTo ?? ''} {item.nextFollowUpAt ?? ''} {item.trialAt ?? ''}</AppText>
      </Pressable>
    </AppCard>
  );
}
