import { useMemo, useState } from 'react';
import { Switch, View } from 'react-native';
import { useTranslation } from 'react-i18next';
import { useTheme } from '../../../app/providers/ThemeProvider';
import { FeatureGate } from '../../../components/access/FeatureGate';
import { ScreenState } from '../../../components/feedback/ScreenState';
import { AppButton } from '../../../components/ui/AppButton';
import { AppText } from '../../../components/ui/AppText';
import { AppTextInput } from '../../../components/ui/AppTextInput';
import { featureCodes } from '../../../constants/access';
import { errorText } from '../../../services/api/problem';
import { useWorkspaceFeatures } from '../../licensing/api/featureQuery';
import {
  useCollectMembershipPayment,
  useMemberMemberships,
  useMembershipAction,
  useMembershipPaymentSummary,
  useMembershipPayments,
  usePackages,
  useWorkspacePlans,
} from '../api/queries';
import { paymentMethods, type MembershipPaymentMethod } from '../api/payments.api';

export function MemberMemberships({ memberId, enabled }: { memberId: string; enabled: boolean }) {
  const theme = useTheme();
  const { t } = useTranslation();
  const features = useWorkspaceFeatures();
  const paymentsEnabled = features.data?.[featureCodes.gymPaymentManagement] === true;
  const memberships = useMemberMemberships(memberId, enabled);
  const packages = usePackages('', true, 1, enabled);
  const workspacePlans = useWorkspacePlans(enabled);
  const action = useMembershipAction(memberId);
  const [branchId, setBranchId] = useState('');
  const [startDate, setStartDate] = useState('2026-10-07');
  const [assignAppAccess, setAssignAppAccess] = useState(false);
  const [appSubscriptionId, setAppSubscriptionId] = useState('');
  const [planId, setPlanId] = useState('');
  const [paymentAmount, setPaymentAmount] = useState('');
  const [paymentMethod, setPaymentMethod] = useState<MembershipPaymentMethod>('Cash');
  const [paymentDate, setPaymentDate] = useState('2026-10-07');
  const [paymentReference, setPaymentReference] = useState('');
  const [paymentNotes, setPaymentNotes] = useState('');
  const [collectOpen, setCollectOpen] = useState(false);
  if (!enabled) {
    return null;
  }
  const items = memberships.data?.items ?? [];
  const current = items.find((item) => item.status === 'Active' || item.status === 'Frozen' || item.status === 'Pending');
  const unauthorized = memberships.isError && errorText(memberships.error, '').toLowerCase().includes('permission');
  const selectedPlan = packages.data?.items.find((item) => item.id === planId) ?? packages.data?.items[0];
  const resolvedPlanId = planId || selectedPlan?.id;
  const packagePrice = selectedPlan?.price ?? 0;
  const finalAmount = packagePrice;
  const paidNow = paymentAmount.trim() ? Number(paymentAmount) : 0;
  const pending = Math.max(0, finalAmount - (Number.isFinite(paidNow) ? paidNow : 0));
  const usablePlans = (workspacePlans.data ?? []).filter((plan) => plan.usable);
  const selectedAppPlan = usablePlans.find((plan) => plan.subscriptionId === appSubscriptionId) ?? usablePlans[0];

  return (
    <ScreenState
      error={memberships.isError ? (unauthorized ? t('memberships.unauthorized') : errorText(memberships.error, t('common.offline'))) : null}
      loading={memberships.isLoading}
      onRetry={() => void memberships.refetch()}
    >
      <AppText variant="label">{t('memberships.history')}</AppText>
      {memberships.isFetched && items.length === 0 ? <AppText>{t('memberships.none')}</AppText> : null}
      {items.map((item) => (
        <View key={item.id} style={{ gap: theme.spacing.xs, marginBottom: theme.spacing.sm }}>
          <AppText>{item.planName ?? item.membershipNumber} · {item.status} · {item.startDate} → {item.endDate}</AppText>
          <AppText tone="muted">
            {t('payments.summaryLine', {
              total: item.finalAmount ?? 0,
              paid: item.totalPaid ?? 0,
              pending: item.outstanding ?? 0,
              currency: item.currency ?? '',
              status: item.settlementStatus ?? 'Unpaid',
            })}
          </AppText>
        </View>
      ))}
      {current ? (
        <>
          <AppText variant="label">{t('memberships.current')}</AppText>
          <AppText>{current.planName} · {current.status} · {current.endDate}</AppText>
          <AppButton label={t('memberships.renew')} onPress={() => void action.mutate({ id: current.id, action: 'renew' })} />
          {current.status === 'Frozen'
            ? <AppButton label={t('memberships.unfreeze')} onPress={() => void action.mutate({ id: current.id, action: 'unfreeze' })} variant="secondary" />
            : <AppButton label={t('memberships.freeze')} onPress={() => void action.mutate({ id: current.id, action: 'freeze' })} variant="secondary" />}
          <AppButton label={t('memberships.extend')} onPress={() => void action.mutate({ id: current.id, action: 'extend', days: 7 })} variant="secondary" />
          <AppButton label={t('memberships.cancel')} onPress={() => void action.mutate({ id: current.id, action: 'cancel' })} variant="secondary" />
          {paymentsEnabled ? (
            <>
              <AppButton label={t('payments.collect')} onPress={() => setCollectOpen((value) => !value)} variant="secondary" />
              {collectOpen ? <CollectPaymentPanel memberId={memberId} membershipId={current.id} /> : null}
              <PaymentHistoryPanel membershipId={current.id} enabled={paymentsEnabled} />
            </>
          ) : null}
        </>
      ) : null}
      <AppText variant="label">{t('memberships.assign')}</AppText>
      <AppTextInput accessibilityLabel={t('memberships.start')} onChangeText={setStartDate} value={startDate} />
      <AppTextInput accessibilityLabel="Branch" onChangeText={setBranchId} value={branchId} />
      {(packages.data?.items ?? []).map((item) => (
        <AppButton
          key={item.id}
          label={`${item.name} · ${item.price} ${item.currency}`}
          onPress={() => setPlanId(item.id)}
          variant={resolvedPlanId === item.id ? 'primary' : 'secondary'}
        />
      ))}
      <AppText variant="label">{t('payments.pricing')}</AppText>
      <AppText>{t('payments.packagePrice')}: {packagePrice}</AppText>
      <AppText>{t('payments.finalAmount')}: {finalAmount}</AppText>
      {paymentsEnabled ? (
        <>
          <AppText variant="label">{t('payments.optionalPayment')}</AppText>
          <AppTextInput accessibilityLabel={t('payments.amount')} onChangeText={setPaymentAmount} value={paymentAmount} />
          <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: theme.spacing.sm }}>
            {paymentMethods.map((method) => (
              <AppButton key={method} label={method} onPress={() => setPaymentMethod(method)} variant={paymentMethod === method ? 'primary' : 'secondary'} />
            ))}
          </View>
          <AppTextInput accessibilityLabel={t('payments.date')} onChangeText={setPaymentDate} value={paymentDate} />
          <AppTextInput accessibilityLabel={t('payments.reference')} onChangeText={setPaymentReference} value={paymentReference} />
          <AppTextInput accessibilityLabel={t('payments.notes')} onChangeText={setPaymentNotes} value={paymentNotes} />
          <AppText tone="muted">{t('payments.assignSummary', { total: finalAmount, paid: Number.isFinite(paidNow) ? paidNow : 0, pending })}</AppText>
        </>
      ) : null}
      <View style={{ gap: theme.spacing.sm, marginBottom: theme.spacing.md }}>
        <AppText variant="label">{t('memberships.appPlan')}</AppText>
        <AppText tone="muted">{t('memberships.appAccessModel')}</AppText>
        <View style={{ flexDirection: 'row', alignItems: 'center', gap: theme.spacing.sm }}>
          <Switch
            accessibilityLabel={t('memberships.enableAppAccess')}
            onValueChange={(value) => {
              setAssignAppAccess(value);
              if (!value) {
                setAppSubscriptionId('');
              } else if (!appSubscriptionId && usablePlans[0]) {
                setAppSubscriptionId(usablePlans[0].subscriptionId);
              }
            }}
            value={assignAppAccess}
          />
          <AppText>{t('memberships.enableAppAccess')}</AppText>
        </View>
        {assignAppAccess ? (
          <>
            {usablePlans.map((plan) => (
              <AppButton
                key={plan.subscriptionId}
                label={`${plan.planName} · ${plan.available} ${t('memberships.seatsAvailable')}`}
                onPress={() => setAppSubscriptionId(plan.subscriptionId)}
                variant={appSubscriptionId === plan.subscriptionId || (!appSubscriptionId && plan.subscriptionId === selectedAppPlan?.subscriptionId) ? 'primary' : 'secondary'}
              />
            ))}
            {selectedAppPlan ? (
              <AppText>
                {t('memberships.seatSummary', { purchased: selectedAppPlan.seats, assigned: selectedAppPlan.assigned, available: selectedAppPlan.available })}
              </AppText>
            ) : null}
            {selectedAppPlan && selectedAppPlan.available <= 0 ? <AppText>{t('memberships.noSeats')}</AppText> : null}
            <AppText tone="muted">{t('memberships.seatUsesOne')}</AppText>
          </>
        ) : (
          <AppText tone="muted">{t('memberships.basicAccessActive')}</AppText>
        )}
      </View>
      <AppButton
        label={t('memberships.assign')}
        disabled={!resolvedPlanId || (assignAppAccess && !selectedAppPlan)}
        onPress={() => void action.mutate({
          action: 'assign',
          branchId: branchId || null,
          planId: resolvedPlanId ?? '',
          startDate,
          assignAppAccess,
          appSubscriptionId: assignAppAccess ? (selectedAppPlan?.subscriptionId ?? null) : null,
          paymentAmount: paymentsEnabled && paymentAmount.trim() ? Number(paymentAmount) : null,
          paymentMethod: paymentsEnabled && paymentAmount.trim() ? paymentMethod : null,
          paymentDate: paymentsEnabled && paymentAmount.trim() ? paymentDate : null,
          paymentReferenceNumber: paymentsEnabled ? paymentReference || null : null,
          paymentNotes: paymentsEnabled ? paymentNotes || null : null,
        })}
      />
    </ScreenState>
  );
}

function CollectPaymentPanel({ memberId, membershipId }: { memberId: string; membershipId: string }) {
  const { t } = useTranslation();
  const theme = useTheme();
  const summary = useMembershipPaymentSummary(membershipId, true);
  const collect = useCollectMembershipPayment(memberId, membershipId);
  const [amount, setAmount] = useState('');
  const [method, setMethod] = useState<MembershipPaymentMethod>('Cash');
  const [date, setDate] = useState('2026-10-07');
  const [reference, setReference] = useState('');
  const [notes, setNotes] = useState('');
  const data = summary.data;

  return (
    <FeatureGate code={featureCodes.gymPaymentManagement}>
      <View style={{ gap: theme.spacing.sm }}>
        <AppText variant="label">{t('payments.collect')}</AppText>
        {data ? (
          <AppText>
            {t('payments.balanceBoard', {
              total: data.finalAmount,
              paid: data.totalPaid,
              outstanding: data.outstanding,
              currency: data.currency,
            })}
          </AppText>
        ) : null}
        <AppTextInput accessibilityLabel={t('payments.amount')} onChangeText={setAmount} value={amount} />
        <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: theme.spacing.sm }}>
          {paymentMethods.map((item) => (
            <AppButton key={item} label={item} onPress={() => setMethod(item)} variant={method === item ? 'primary' : 'secondary'} />
          ))}
        </View>
        <AppTextInput accessibilityLabel={t('payments.date')} onChangeText={setDate} value={date} />
        <AppTextInput accessibilityLabel={t('payments.reference')} onChangeText={setReference} value={reference} />
        <AppTextInput accessibilityLabel={t('payments.notes')} onChangeText={setNotes} value={notes} />
        {collect.isError ? <AppText tone="danger">{errorText(collect.error, t('common.offline'))}</AppText> : null}
        <AppButton
          label={t('payments.collect')}
          loading={collect.isPending}
          onPress={() => void collect.mutate({
            amount: Number(amount),
            paymentMethod: method,
            paymentDate: date,
            referenceNumber: reference || null,
            notes: notes || null,
          })}
        />
      </View>
    </FeatureGate>
  );
}

function PaymentHistoryPanel({ membershipId, enabled }: { membershipId: string; enabled: boolean }) {
  const { t } = useTranslation();
  const history = useMembershipPayments(membershipId, 1, enabled);
  const rows = useMemo(() => history.data?.items ?? [], [history.data?.items]);
  if (!enabled) {
    return null;
  }
  return (
    <View>
      <AppText variant="label">{t('payments.history')}</AppText>
      {rows.length === 0 ? <AppText tone="muted">{t('payments.historyEmpty')}</AppText> : null}
      {rows.map((row) => (
        <AppText key={row.id}>
          {row.paymentDate} · {row.amount} {row.currency} · {row.paymentMethod} · {row.referenceNumber ?? '—'} · {row.receivedBy ?? '—'} · {row.status}
        </AppText>
      ))}
    </View>
  );
}
