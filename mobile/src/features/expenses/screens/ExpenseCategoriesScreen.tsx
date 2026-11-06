import type { NativeStackScreenProps } from '@react-navigation/native-stack';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { ScrollView } from 'react-native';
import type { AppStackParamList } from '../../../app/navigation/types';
import { useTheme } from '../../../app/providers/ThemeProvider';
import { FeatureGate } from '../../../components/access/FeatureGate';
import { ScreenState } from '../../../components/feedback/ScreenState';
import { AppButton } from '../../../components/ui/AppButton';
import { AppText } from '../../../components/ui/AppText';
import { AppTextInput } from '../../../components/ui/AppTextInput';
import { featureCodes } from '../../../constants/access';
import { useExpenseCategories, useSaveExpenseCategory } from '../api/queries';

type Props = NativeStackScreenProps<AppStackParamList, 'ExpenseCategories'>;

export function ExpenseCategoriesScreen(_props: Props) {
  return (
    <FeatureGate code={featureCodes.gymExpenseManagement}>
      <CategoryList />
    </FeatureGate>
  );
}

export function CategoryList() {
  const theme = useTheme();
  const { t } = useTranslation();
  const categories = useExpenseCategories(true);
  const save = useSaveExpenseCategory();
  const [name, setName] = useState('');

  return (
    <ScreenState empty={categories.isFetched && (categories.data?.length ?? 0) === 0} emptyBody={t('expenses.noCategories')} emptyTitle={t('expenses.categories')} error={categories.isError ? t('common.offline') : null} loading={categories.isLoading} onRetry={() => void categories.refetch()}>
      <ScrollView contentContainerStyle={{ gap: theme.spacing.md, padding: theme.spacing.xl }}>
        <AppText variant="title">{t('expenses.categories')}</AppText>
        {(categories.data ?? []).map((category) => (
          <AppText key={category.id}>{category.name} · {category.isActive ? t('expenses.active') : t('expenses.inactive')}</AppText>
        ))}
        <AppTextInput accessibilityLabel={t('expenses.category')} onChangeText={setName} placeholder={t('expenses.category')} value={name} />
        <AppButton label={t('expenses.saveCategory')} onPress={() => void save.mutateAsync({ name })} />
        {categories.data?.[0] ? <AppButton label={t('expenses.deactivate')} onPress={() => void save.mutateAsync({ id: categories.data?.[0].id, name: categories.data[0].name, active: false })} /> : null}
      </ScrollView>
    </ScreenState>
  );
}
