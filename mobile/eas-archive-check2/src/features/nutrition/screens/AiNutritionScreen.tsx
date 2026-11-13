import { useMutation } from '@tanstack/react-query';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { ScrollView } from 'react-native';
import { useTheme } from '../../../app/providers/ThemeProvider';
import { FeatureGate } from '../../../components/access/FeatureGate';
import { FormField } from '../../../components/forms/FormField';
import { ScreenState } from '../../../components/feedback/ScreenState';
import { AppButton } from '../../../components/ui/AppButton';
import { AppCard } from '../../../components/ui/AppCard';
import { AppText } from '../../../components/ui/AppText';
import { AppTextInput } from '../../../components/ui/AppTextInput';
import { featureCodes } from '../../../constants/access';
import { formatNumber } from '../../../services/localization/format';
import { errorText } from '../../../services/api/problem';
import { nutritionAdvice } from '../../aiCoach/api/ai.api';
import { useWorkspaceFeatures } from '../../licensing/api/featureQuery';
import { useNutritionSummary } from '../api/queries';
import { localDate } from '../dates';

export function AiNutritionScreen() {
  const theme = useTheme();
  const { t, i18n } = useTranslation();
  const features = useWorkspaceFeatures();
  const nutritionEnabled = features.data?.[featureCodes.nutrition] === true;
  const summary = useNutritionSummary(nutritionEnabled ? localDate() : '');
  const [question, setQuestion] = useState('');
  const advice = useMutation({ mutationFn: () => nutritionAdvice(question.trim() ? question.trim() : null) });

  return (
    <FeatureGate code={featureCodes.aiNutrition}>
      <ScreenState error={summary.isError ? errorText(summary.error, t('common.offline')) : null} loading={nutritionEnabled && summary.isLoading} onRetry={() => void summary.refetch()}>
        <ScrollView contentContainerStyle={{ gap: theme.spacing.lg, padding: theme.spacing.xl }} keyboardShouldPersistTaps="handled">
          <AppText variant="title">{t('nutrition.measured')}</AppText>
          {nutritionEnabled && summary.data ? (
            <AppCard>
              <AppText>{t('nutrition.calories')}: {formatNumber(summary.data.calories, i18n.language)}</AppText>
              <AppText>{t('nutrition.protein')}: {formatNumber(summary.data.protein, i18n.language)}</AppText>
              <AppText>{t('nutrition.carbohydrates')}: {formatNumber(summary.data.carbohydrates, i18n.language)}</AppText>
              <AppText>{t('nutrition.fat')}: {formatNumber(summary.data.fat, i18n.language)}</AppText>
            </AppCard>
          ) : <AppText tone="muted">{nutritionEnabled ? t('nutrition.noMeals') : t('journey.featureDisabled')}</AppText>}
          <AppText variant="title">{t('nutrition.aiTitle')}</AppText>
          <AppText tone="muted">{t('journey.safety')}</AppText>
          <AppText tone="muted">{t('journey.notMedical')}</AppText>
          {advice.data ? (
            <AppCard>
              <AppText variant="label">{t('journey.aiLabel')}</AppText>
              <AppText>{advice.data.message}</AppText>
            </AppCard>
          ) : <AppText tone="muted">{t('nutrition.aiEmpty')}</AppText>}
          {advice.isError ? <AppText accessibilityRole="alert" tone="danger">{errorText(advice.error, t('common.offline'))}</AppText> : null}
          <FormField label={t('nutrition.aiPrompt')}>
            <AppTextInput accessibilityLabel={t('nutrition.aiPrompt')} onChangeText={setQuestion} value={question} />
          </FormField>
          <AppButton label={advice.isError ? t('common.retry') : t('nutrition.ask')} loading={advice.isPending} onPress={() => advice.mutate()} />
        </ScrollView>
      </ScreenState>
    </FeatureGate>
  );
}
