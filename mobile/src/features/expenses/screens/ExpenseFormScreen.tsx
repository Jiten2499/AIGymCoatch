import type { NativeStackScreenProps } from '@react-navigation/native-stack';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { ScrollView } from 'react-native';
import type { AppStackParamList } from '../../../app/navigation/types';
import { useTheme } from '../../../app/providers/ThemeProvider';
import { AppButton } from '../../../components/ui/AppButton';
import { AppText } from '../../../components/ui/AppText';
import { AppTextInput } from '../../../components/ui/AppTextInput';
import { useExpenseCategories, useSaveExpense } from '../api/queries';

type Props = NativeStackScreenProps<AppStackParamList, 'ExpenseForm'>;

export function ExpenseFormScreen({ navigation, route }: Props) {
  return <ExpenseForm expenseId={route.params?.expenseId} onSaved={() => navigation.replace('Expenses')} />;
}

export function ExpenseForm({ expenseId, onSaved }: { expenseId?: string; onSaved: () => void }) {
  const theme = useTheme();
  const { t } = useTranslation();
  const save = useSaveExpense();
  const categories = useExpenseCategories(true);
  const [description, setDescription] = useState('');
  const [amount, setAmount] = useState('');
  const [date, setDate] = useState('2026-10-07');
  const [method, setMethod] = useState('Cash');
  const [notes, setNotes] = useState('');
  const categoryId = categories.data?.[0]?.id ?? '';

  return (
    <ScrollView contentContainerStyle={{ gap: theme.spacing.md, padding: theme.spacing.xl }}>
      <AppText variant="title">{expenseId ? t('expenses.edit') : t('expenses.add')}</AppText>
      <AppText>{categories.data?.[0]?.name ?? t('expenses.category')}</AppText>
      <AppTextInput accessibilityLabel={t('expenses.description')} onChangeText={setDescription} placeholder={t('expenses.description')} value={description} />
      <AppTextInput accessibilityLabel={t('expenses.amount')} onChangeText={setAmount} placeholder={t('expenses.amount')} value={amount} />
      <AppTextInput accessibilityLabel={t('expenses.date')} onChangeText={setDate} placeholder={t('expenses.date')} value={date} />
      <AppTextInput accessibilityLabel={t('expenses.method')} onChangeText={setMethod} placeholder={t('expenses.method')} value={method} />
      <AppTextInput accessibilityLabel={t('expenses.notes')} onChangeText={setNotes} placeholder={t('expenses.notes')} value={notes} />
      <AppButton label={t('expenses.save')} onPress={() => void save.mutateAsync({ id: expenseId, input: { categoryId, description, amount: Number(amount), expenseDate: date, paymentMethod: method, notes } }).then(onSaved)} />
    </ScrollView>
  );
}
