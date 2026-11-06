import { useNavigation } from '@react-navigation/native';
import type { NativeStackNavigationProp } from '@react-navigation/native-stack';
import { useMemo, useState } from 'react';
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
import type { LedgerTransaction } from '../api/ledger.api';
import { useFinancialAccounts, useLedgerSummary, useLedgerTransactions, useVoidLedgerTransaction } from '../api/queries';

function iso(date: Date) {
  return date.toISOString().slice(0, 10);
}

function startOfWeek(date: Date) {
  const day = date.getDay();
  const mondayOffset = day === 0 ? -6 : 1 - day;
  const start = new Date(date);
  start.setDate(date.getDate() + mondayOffset);
  return start;
}

export function LedgerScreen() {
  return (
    <FeatureGate code={featureCodes.gymFinancialLedger}>
      <LedgerBody enabled />
    </FeatureGate>
  );
}

export function LedgerBody({ enabled }: { enabled: boolean }) {
  const theme = useTheme();
  const { t } = useTranslation();
  const navigation = useNavigation<NativeStackNavigationProp<AppStackParamList>>();
  const today = useMemo(() => new Date(), []);
  const [range, setRange] = useState<'today' | 'week' | 'month'>('month');
  const [entryType, setEntryType] = useState('');
  const [accountId, setAccountId] = useState('');
  const [status, setStatus] = useState('');
  const [page, setPage] = useState(1);
  const bounds = useMemo(() => {
    if (range === 'today') return { from: iso(today), to: iso(today) };
    if (range === 'week') return { from: iso(startOfWeek(today)), to: iso(today) };
    return { from: iso(new Date(today.getFullYear(), today.getMonth(), 1)), to: iso(today) };
  }, [range, today]);
  const summary = useLedgerSummary(bounds.from, bounds.to, enabled);
  const accounts = useFinancialAccounts(enabled);
  const transactions = useLedgerTransactions(bounds.from, bounds.to, entryType, accountId, '', status, page, enabled);
  const voidEntry = useVoidLedgerTransaction();
  const items = transactions.data?.items ?? [];
  const unauthorized = transactions.isError && errorText(transactions.error, '').toLowerCase().includes('permission');
  const balance = (accounts.data ?? []).filter((account) => account.isActive).reduce((sum, account) => sum + account.balance, 0);

  return (
    <ScreenState
      empty={transactions.isFetched && items.length === 0}
      emptyBody={t('ledger.empty')}
      emptyTitle={t('ledger.title')}
      error={transactions.isError ? (unauthorized ? t('ledger.unauthorized') : errorText(transactions.error, t('common.offline'))) : null}
      loading={transactions.isLoading || summary.isLoading}
      onRetry={() => void transactions.refetch()}
    >
      <ScrollView contentContainerStyle={{ gap: theme.spacing.md, padding: theme.spacing.xl }}>
        <AppText variant="title">{t('ledger.title')}</AppText>
        <AppText>{t('ledger.moneyIn')}: {summary.data?.moneyIn ?? 0}</AppText>
        <AppText>{t('ledger.moneyOut')}: {summary.data?.moneyOut ?? 0}</AppText>
        <AppText>{t('ledger.net')}: {summary.data?.netCashFlow ?? 0}</AppText>
        <AppText>{t('ledger.balance')}: {balance}</AppText>
        <AppText>{t('ledger.count')}: {summary.data?.transactionCount ?? 0}</AppText>
        <Pressable accessibilityRole="button" onPress={() => { setPage(1); setRange((current) => (current === 'today' ? 'week' : current === 'week' ? 'month' : 'today')); }}>
          <AppText>{t(`ledger.range.${range}`)}</AppText>
        </Pressable>
        <Pressable accessibilityRole="button" onPress={() => { setPage(1); setEntryType((current) => (current === '' ? 'MoneyIn' : current === 'MoneyIn' ? 'MoneyOut' : current === 'MoneyOut' ? 'Transfer' : '')); }}>
          <AppText>{entryType || t('ledger.type')}</AppText>
        </Pressable>
        <Pressable accessibilityRole="button" onPress={() => { setPage(1); setStatus((current) => (current === '' ? 'Posted' : current === 'Posted' ? 'Voided' : '')); }}>
          <AppText>{status || t('ledger.status')}</AppText>
        </Pressable>
        {(accounts.data ?? []).map((account) => (
          <Pressable accessibilityRole="button" key={account.id} onPress={() => { setPage(1); setAccountId((current) => (current === account.id ? '' : account.id)); }}>
            <AppText>{account.name} · {account.balance} {account.currency}{accountId === account.id ? ' · selected' : ''}</AppText>
          </Pressable>
        ))}
        {items.map((item) => (
          <TransactionRow
            item={item}
            key={item.id}
            onVoid={() => void voidEntry.mutateAsync(item.id)}
          />
        ))}
        {transactions.data && transactions.data.page * transactions.data.pageSize < transactions.data.totalCount ? (
          <AppButton label={t('ledger.loadMore')} onPress={() => setPage((current) => current + 1)} />
        ) : null}
        <AppButton label={t('ledger.receiveMoney')} onPress={() => navigation.navigate('Memberships')} />
        <AppButton label={t('ledger.addExpense')} onPress={() => navigation.navigate('Expenses')} />
        <AppButton label={t('ledger.transfer')} onPress={() => navigation.navigate('LedgerTransfer')} />
        <AppButton label={t('ledger.accounts')} onPress={() => navigation.navigate('LedgerAccounts')} />
      </ScrollView>
    </ScreenState>
  );
}

function TransactionRow({ item, onVoid }: { item: LedgerTransaction; onVoid: () => void }) {
  return (
    <AppCard>
      <AppText>{item.description}</AppText>
      <AppText tone="muted">{item.entryDate} · {item.entryType} · {item.accountName ?? '—'} · {item.category ?? '—'} · {item.amount} {item.currency} · {item.status}</AppText>
      <AppText tone="muted">{item.recordedBy}</AppText>
      {item.status === 'Posted' ? <AppButton label="Void" onPress={onVoid} /> : null}
    </AppCard>
  );
}
