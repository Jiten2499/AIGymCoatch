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
import type { ExpenseListItem } from '../api/expenses.api';
import { useExpenseSummary, useExpenses } from '../api/queries';

export function ExpensesScreen() {
  return (
    <FeatureGate code={featureCodes.gymExpenseManagement}>
      <ExpenseList enabled />
    </FeatureGate>
  );
}

export function ExpenseList({ enabled }: { enabled: boolean }) {
  const theme = useTheme();
  const { t } = useTranslation();
  const navigation = useNavigation<NativeStackNavigationProp<AppStackParamList>>();
  const [term, setTerm] = useState('');
  const [submitted, setSubmitted] = useState('');
  const [status, setStatus] = useState('');
  const [page, setPage] = useState(1);
  const expenses = useExpenses(submitted, '', status, page, enabled);
  const summary = useExpenseSummary(enabled);
  const items = expenses.data?.items ?? [];
  const unauthorized = expenses.isError && errorText(expenses.error, '').toLowerCase().includes('permission');

  return (
    <ScreenState
      empty={expenses.isFetched && items.length === 0}
      emptyBody={t('expenses.empty')}
      emptyTitle={t('expenses.title')}
      error={expenses.isError ? (unauthorized ? t('expenses.unauthorized') : errorText(expenses.error, t('common.offline'))) : null}
      loading={expenses.isLoading}
      onRetry={() => void expenses.refetch()}
    >
      <ScrollView contentContainerStyle={{ gap: theme.spacing.md, padding: theme.spacing.xl }}>
        <AppText variant="title">{t('expenses.title')}</AppText>
        <AppText>{t('expenses.today')}: {summary.data?.todayAmount ?? 0}</AppText>
        <AppText>{t('expenses.month')}: {summary.data?.monthAmount ?? 0}</AppText>
        <AppText>{t('expenses.count')}: {summary.data?.totalCount ?? 0}</AppText>
        {(summary.data?.categories ?? []).map((category) => (
          <AppText key={category.categoryId}>{category.name} {category.amount}</AppText>
        ))}
        <AppTextInput accessibilityLabel={t('expenses.search')} onChangeText={(value) => { setTerm(value); setPage(1); setSubmitted(value.trim()); }} placeholder={t('expenses.search')} value={term} />
        <Pressable accessibilityRole="button" onPress={() => { setPage(1); setStatus((current) => (current === 'Active' ? '' : 'Active')); }}>
          <AppText>{status || t('expenses.status')}</AppText>
        </Pressable>
        {items.map((item) => <ExpenseRow item={item} key={item.id} onOpen={() => navigation.navigate('ExpenseDetail', { expenseId: item.id })} />)}
        {expenses.data && expenses.data.page * expenses.data.pageSize < expenses.data.totalCount ? <AppButton label={t('expenses.loadMore')} onPress={() => setPage((current) => current + 1)} /> : null}
        <AppButton label={t('expenses.add')} onPress={() => navigation.navigate('ExpenseForm')} />
        <AppButton label={t('expenses.categories')} onPress={() => navigation.navigate('ExpenseCategories')} />
      </ScrollView>
    </ScreenState>
  );
}

function ExpenseRow({ item, onOpen }: { item: ExpenseListItem; onOpen: () => void }) {
  return (
    <AppCard>
      <Pressable accessibilityRole="button" onPress={onOpen}>
        <AppText>{item.description}</AppText>
        <AppText tone="muted">{item.category} · {item.amount} {item.currency} · {item.expenseDate} · {item.paymentMethod} · {item.status}</AppText>
        <AppText tone="muted">{item.recordedBy}</AppText>
      </Pressable>
    </AppCard>
  );
}
