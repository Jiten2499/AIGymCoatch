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
import { useExpense, useVoidExpense } from '../api/queries';

type Props = NativeStackScreenProps<AppStackParamList, 'ExpenseDetail'>;

export function ExpenseDetailScreen({ navigation, route }: Props) {
  return (
    <FeatureGate code={featureCodes.gymExpenseManagement}>
      <ExpenseDetail enabled expenseId={route.params.expenseId} onEdit={() => navigation.navigate('ExpenseForm', { expenseId: route.params.expenseId })} />
    </FeatureGate>
  );
}

export function ExpenseDetail({ expenseId, enabled, onEdit }: { expenseId: string; enabled: boolean; onEdit: () => void }) {
  const theme = useTheme();
  const { t } = useTranslation();
  const expense = useExpense(expenseId, enabled);
  const voidExpense = useVoidExpense(expenseId);
  const unauthorized = expense.isError && errorText(expense.error, '').toLowerCase().includes('permission');

  return (
    <ScreenState
      empty={false}
      error={expense.isError ? (unauthorized ? t('expenses.unauthorized') : errorText(expense.error, t('common.offline'))) : null}
      loading={expense.isLoading}
      onRetry={() => void expense.refetch()}
    >
      <ScrollView contentContainerStyle={{ gap: theme.spacing.md, padding: theme.spacing.xl }}>
        <AppText variant="title">{expense.data?.description}</AppText>
        <AppText>{expense.data?.category}</AppText>
        <AppText>{expense.data?.amount} {expense.data?.currency}</AppText>
        <AppText>{expense.data?.expenseDate}</AppText>
        <AppText>{expense.data?.paymentMethod}</AppText>
        <AppText>{expense.data?.recordedBy}</AppText>
        <AppText>{expense.data?.status}</AppText>
        <AppText>{expense.data?.notes}</AppText>
        <AppButton label={t('expenses.edit')} onPress={onEdit} />
        <AppButton label={t('expenses.void')} onPress={() => void voidExpense.mutateAsync()} />
      </ScrollView>
    </ScreenState>
  );
}
