import type { NativeStackScreenProps } from '@react-navigation/native-stack';
import { useTranslation } from 'react-i18next';
import { ScrollView } from 'react-native';
import type { AppStackParamList } from '../../../app/navigation/types';
import { useTheme } from '../../../app/providers/ThemeProvider';
import { FeatureGate } from '../../../components/access/FeatureGate';
import { ScreenState } from '../../../components/feedback/ScreenState';
import { AppButton } from '../../../components/ui/AppButton';
import { AppText } from '../../../components/ui/AppText';
import { featureCodes } from '../../../constants/access';
import { errorText } from '../../../services/api/problem';
import { useFollowUps, useLead, useLeadActions } from '../api/queries';

type Props = NativeStackScreenProps<AppStackParamList, 'LeadDetail'>;

export function LeadDetailScreen({ route }: Props) {
  return (
    <FeatureGate code={featureCodes.gymCrm}>
      <LeadDetail enabled leadId={route.params.leadId} />
    </FeatureGate>
  );
}

export function LeadDetail({ leadId, enabled }: { leadId: string; enabled: boolean }) {
  const theme = useTheme();
  const { t } = useTranslation();
  const lead = useLead(leadId, enabled);
  const followUps = useFollowUps(leadId, enabled && Boolean(lead.data));
  const actions = useLeadActions(leadId);
  const unauthorized = lead.isError && errorText(lead.error, '').toLowerCase().includes('permission');
  const when = new Date(Date.now() + 86_400_000).toISOString();

  return (
    <ScreenState
      empty={false}
      error={lead.isError ? (unauthorized ? t('leads.unauthorized') : errorText(lead.error, t('common.offline'))) : null}
      loading={lead.isLoading}
      onRetry={() => void lead.refetch()}
    >
      <ScrollView contentContainerStyle={{ gap: theme.spacing.md, padding: theme.spacing.xl }}>
        <AppText variant="title">{lead.data?.fullName}</AppText>
        <AppText>{lead.data?.mobile}</AppText>
        <AppText>{lead.data?.email}</AppText>
        <AppText>{lead.data?.source}</AppText>
        <AppText>{lead.data?.status}</AppText>
        <AppText>{lead.data?.interestedPackage}</AppText>
        <AppText>{lead.data?.assignedTo}</AppText>
        <AppText>{lead.data?.nextFollowUpAt}</AppText>
        {(followUps.data ?? []).map((item) => (
          <AppText key={item.id}>{item.notes} {item.outcome}</AppText>
        ))}
        <AppButton label={t('leads.assign')} onPress={() => void actions.assign.mutateAsync('11111111-1111-4111-8111-111111111111')} />
        <AppButton label={t('leads.followUp')} onPress={() => void actions.followUp.mutateAsync(when)} />
        <AppButton label={t('leads.complete')} onPress={() => void actions.complete.mutateAsync(followUps.data?.[0]?.id ?? '')} />
        <AppButton label={t('leads.trial')} onPress={() => void actions.trial.mutateAsync(when)} />
        <AppButton label={t('leads.convert')} onPress={() => void actions.convert.mutateAsync(undefined)} />
        <AppButton label={t('leads.lost')} onPress={() => void actions.lost.mutateAsync()} />
      </ScrollView>
    </ScreenState>
  );
}
