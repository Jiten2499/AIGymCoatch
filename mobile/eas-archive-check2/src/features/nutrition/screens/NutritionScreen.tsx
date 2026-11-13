import type { CompositeNavigationProp } from '@react-navigation/native';
import type { BottomTabNavigationProp } from '@react-navigation/bottom-tabs';
import { useNavigation } from '@react-navigation/native';
import type { NativeStackNavigationProp } from '@react-navigation/native-stack';
import { useTranslation } from 'react-i18next';
import { RefreshControl, ScrollView } from 'react-native';
import type { AppStackParamList, MemberTabParamList } from '../../../app/navigation/types';
import { useTheme } from '../../../app/providers/ThemeProvider';
import { FeatureGate } from '../../../components/access/FeatureGate';
import { ScreenState } from '../../../components/feedback/ScreenState';
import { AppButton } from '../../../components/ui/AppButton';
import { AppCard } from '../../../components/ui/AppCard';
import { AppText } from '../../../components/ui/AppText';
import { featureCodes } from '../../../constants/access';
import { formatDate, formatNumber } from '../../../services/localization/format';
import { errorText } from '../../../services/api/problem';
import { useWorkspaceFeatures } from '../../licensing/api/featureQuery';
import { useNutritionLogs, useNutritionSummary } from '../api/queries';
import { localDate } from '../dates';
import type { NutritionSummary } from '../types';

type NutritionNavigation = CompositeNavigationProp<
  BottomTabNavigationProp<MemberTabParamList, 'Nutrition'>,
  NativeStackNavigationProp<AppStackParamList>
>;

export function NutritionScreen() {
  const theme = useTheme();
  const { t, i18n } = useTranslation();
  const navigation = useNavigation<NutritionNavigation>();
  const features = useWorkspaceFeatures();
  const today = localDate();
  const summary = useNutritionSummary(today);
  const logs = useNutritionLogs(1, today);
  const loading = summary.isLoading || logs.isLoading;
  const error = summary.error ?? logs.error;

  return (
    <FeatureGate code={featureCodes.nutrition}>
      <ScreenState error={error ? errorText(error, t('common.offline')) : null} loading={loading} onRetry={() => { void summary.refetch(); void logs.refetch(); }}>
        <ScrollView
          contentContainerStyle={{ gap: theme.spacing.lg, padding: theme.spacing.xl }}
          refreshControl={<RefreshControl refreshing={summary.isRefetching || logs.isRefetching} onRefresh={() => { void summary.refetch(); void logs.refetch(); }} />}
        >
          <AppText variant="title">{t('nav.nutrition')}</AppText>
          <AppText tone="muted">{formatDate(new Date(`${today}T00:00:00`), i18n.language)}</AppText>
          {summary.data ? <MacroCard locale={i18n.language} summary={summary.data} /> : null}
          <AppCard>
            <AppText variant="label">{t('nutrition.mealsToday')}</AppText>
            <AppText tone="muted">{formatNumber(logs.data?.totalCount ?? 0, i18n.language)}</AppText>
            {(logs.data?.items.length ?? 0) === 0 ? <AppText tone="muted">{t('nutrition.noMeals')}</AppText> : logs.data?.items.map((log) => (
              <AppText key={log.id}>{t(`nutrition.mealTypeValue.${log.mealType}`)} · {log.foodName}</AppText>
            ))}
          </AppCard>
          <AppCard>
            <AppText variant="label">{t('nutrition.trendsTitle')}</AppText>
            <AppText tone="muted">{t('nutrition.trendsUnavailable')}</AppText>
          </AppCard>
          <AppButton label={t('nutrition.logMeal')} onPress={() => navigation.navigate('LogMeal')} />
          <AppButton label={t('nutrition.history')} onPress={() => navigation.navigate('NutritionHistory')} variant="secondary" />
          <AppButton label={t('nutrition.profile')} onPress={() => navigation.navigate('NutritionProfile')} variant="secondary" />
          {features.data?.[featureCodes.aiCoach] === true ? (
            <AppButton label={t('coach.ask')} onPress={() => navigation.navigate('AiCoach')} variant="secondary" />
          ) : null}
          {features.data?.[featureCodes.aiNutrition] === true ? (
            <AppButton label={t('nutrition.aiTitle')} onPress={() => navigation.navigate('AiNutrition')} variant="secondary" />
          ) : null}
        </ScrollView>
      </ScreenState>
    </FeatureGate>
  );
}

function MacroCard({ summary, locale }: { summary: NutritionSummary; locale: string }) {
  const { t } = useTranslation();
  const hasTarget = summary.calorieTarget > 0;
  const rows = [
    [t('nutrition.calories'), summary.calories, summary.calorieTarget],
    [t('nutrition.protein'), summary.protein, summary.proteinTarget],
    [t('nutrition.carbohydrates'), summary.carbohydrates, summary.carbTarget],
    [t('nutrition.fat'), summary.fat, summary.fatTarget],
  ] as const;
  return (
    <AppCard>
      {rows.map(([label, consumed, target]) => (
        <AppText key={label}>
          {label}: {hasTarget
            ? t('nutrition.consumedOfTarget', { consumed: formatNumber(consumed, locale), target: formatNumber(target, locale) })
            : formatNumber(consumed, locale)}
        </AppText>
      ))}
      {hasTarget ? null : <AppText tone="muted">{t('nutrition.noTarget')}</AppText>}
    </AppCard>
  );
}
