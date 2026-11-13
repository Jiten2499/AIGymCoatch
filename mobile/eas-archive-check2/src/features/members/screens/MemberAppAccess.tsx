import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { useTheme } from '../../../app/providers/ThemeProvider';
import { AppButton } from '../../../components/ui/AppButton';
import { AppText } from '../../../components/ui/AppText';
import { AppTextInput } from '../../../components/ui/AppTextInput';
import { errorText } from '../../../services/api/problem';
import { useTenantId } from '../../fitness/api/queries';
import {
  assignMemberAppAccess,
  enableMemberBasicAccess,
  getMemberAppAccess,
  listWorkspacePlans,
  memberAppAccessQueryKey,
  removeMemberAppAccess,
  workspacePlansQueryKey,
} from '../api/appAccess.api';

export function MemberAppAccessPanel({ memberId, enabled }: { memberId: string; enabled: boolean }) {
  const theme = useTheme();
  const { t } = useTranslation();
  const tenantId = useTenantId();
  const client = useQueryClient();
  const [email, setEmail] = useState('');
  const [subscriptionId, setSubscriptionId] = useState('');
  const access = useQuery({
    queryKey: memberAppAccessQueryKey(tenantId, memberId),
    enabled: Boolean(tenantId) && enabled && Boolean(memberId),
    queryFn: ({ signal }) => getMemberAppAccess(memberId, signal),
  });
  const plans = useQuery({
    queryKey: workspacePlansQueryKey(tenantId),
    enabled: Boolean(tenantId) && enabled,
    queryFn: ({ signal }) => listWorkspacePlans(signal),
  });
  const usable = (plans.data ?? []).filter((plan) => plan.usable);
  const picked = subscriptionId || usable[0]?.subscriptionId || '';

  async function invalidate() {
    await client.invalidateQueries({ queryKey: memberAppAccessQueryKey(tenantId, memberId) });
    await client.invalidateQueries({ queryKey: workspacePlansQueryKey(tenantId) });
  }

  const enableBasic = useMutation({
    mutationFn: () => enableMemberBasicAccess(memberId, email),
    onSuccess: invalidate,
  });
  const assign = useMutation({
    mutationFn: () => assignMemberAppAccess(memberId, picked),
    onSuccess: invalidate,
  });
  const remove = useMutation({
    mutationFn: () => removeMemberAppAccess(memberId),
    onSuccess: invalidate,
  });

  if (!enabled) {
    return null;
  }

  return (
    <>
      <AppText variant="label">{t('memberships.appAccess')}</AppText>
      <AppText tone="muted">{t('memberships.appAccessModel')}</AppText>
      {access.isError ? <AppText>{errorText(access.error, t('common.offline'))}</AppText> : null}

      <AppText variant="label">{t('memberships.basicAccess')}</AppText>
      {access.data?.hasBasicAccess ? (
        <AppText>{t('memberships.basicAccessActive')}</AppText>
      ) : (
        <>
          <AppTextInput accessibilityLabel={t('memberships.loginEmail')} onChangeText={setEmail} value={email} />
          <AppButton
            disabled={!email.trim()}
            label={t('memberships.enableBasicAccess')}
            loading={enableBasic.isPending}
            onPress={() => void enableBasic.mutate()}
          />
          {enableBasic.error ? <AppText>{errorText(enableBasic.error, t('common.offline'))}</AppText> : null}
        </>
      )}

      <AppText variant="label" style={{ marginTop: theme.spacing.md }}>{t('memberships.appPlan')}</AppText>
      {access.data?.hasAppPlan ? (
        <>
          <AppText>{access.data.planName ?? access.data.planCode} · {access.data.planStatus}</AppText>
          <AppButton label={t('memberships.removeAppPlan')} loading={remove.isPending} onPress={() => void remove.mutate()} variant="secondary" />
        </>
      ) : (
        <>
          <AppText tone="muted" style={{ marginBottom: theme.spacing.sm }}>{t('memberships.appPlanHint')}</AppText>
          {usable.map((plan) => (
            <AppButton
              key={plan.subscriptionId}
              label={`${plan.planName} · ${plan.available} ${t('memberships.seatsAvailable')}`}
              onPress={() => setSubscriptionId(plan.subscriptionId)}
              variant={picked === plan.subscriptionId ? 'primary' : 'secondary'}
            />
          ))}
          <AppButton
            disabled={!picked || !access.data?.hasBasicAccess}
            label={t('memberships.assignAppPlan')}
            loading={assign.isPending}
            onPress={() => void assign.mutate()}
          />
          {!access.data?.hasBasicAccess ? <AppText tone="muted">{t('memberships.basicFirst')}</AppText> : null}
          {assign.error ? <AppText>{errorText(assign.error, t('common.offline'))}</AppText> : null}
        </>
      )}
    </>
  );
}
