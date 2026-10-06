import type { NativeStackNavigationProp } from '@react-navigation/native-stack';
import { useNavigation } from '@react-navigation/native';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Pressable, RefreshControl, ScrollView } from 'react-native';
import type { AppStackParamList } from '../../../app/navigation/types';
import { useTheme } from '../../../app/providers/ThemeProvider';
import { FeatureGate } from '../../../components/access/FeatureGate';
import { ScreenState } from '../../../components/feedback/ScreenState';
import { AppButton } from '../../../components/ui/AppButton';
import { AppCard } from '../../../components/ui/AppCard';
import { AppText } from '../../../components/ui/AppText';
import { featureCodes } from '../../../constants/access';
import { errorText } from '../../../services/api/problem';
import { usePrograms } from '../../fitness/api/queries';

export function WorkoutsScreen() {
  const theme = useTheme();
  const { t } = useTranslation();
  const navigation = useNavigation<NativeStackNavigationProp<AppStackParamList>>();
  const [page, setPage] = useState(1);
  const programs = usePrograms(page);
  const items = programs.data?.items ?? [];
  const totalPages = programs.data && programs.data.pageSize > 0 ? Math.ceil(programs.data.totalCount / programs.data.pageSize) : 1;

  return (
    <FeatureGate code={featureCodes.workoutPlans}>
      <ScreenState empty={programs.isSuccess && items.length === 0} emptyBody={t('journey.plansEmpty')} emptyTitle={t('journey.plansTitle')} error={programs.isError ? errorText(programs.error, t('common.offline')) : null} loading={programs.isLoading} onRetry={() => void programs.refetch()}>
        <ScrollView contentContainerStyle={{ gap: theme.spacing.md, padding: theme.spacing.xl }} refreshControl={<RefreshControl refreshing={programs.isRefetching} onRefresh={() => void programs.refetch()} />}>
          <AppText variant="title">{t('journey.plansTitle')}</AppText>
          <AppButton label={t('journey.historyTitle')} onPress={() => navigation.navigate('WorkoutHistory')} variant="secondary" />
          {items.map((program) => (
            <Pressable accessibilityRole="button" key={program.id} onPress={() => navigation.navigate('WorkoutPlan', { programId: program.id })}>
              <AppCard>
                <AppText variant="label">{program.name}</AppText>
                <AppText tone="muted">{t(`journey.goalType.${program.goalType}`)} · {program.difficulty}</AppText>
                <AppText tone="muted">{t('journey.weeks', { count: program.durationWeeks })} · {t('journey.perWeek', { count: program.frequencyPerWeek })}</AppText>
                <AppText tone="muted">{program.status}</AppText>
              </AppCard>
            </Pressable>
          ))}
          {totalPages > 1 ? <AppText tone="muted">{t('journey.pageOf', { page, total: totalPages })}</AppText> : null}
          {page > 1 ? <AppButton label={t('journey.previousPage')} onPress={() => setPage(page - 1)} variant="secondary" /> : null}
          {page < totalPages ? <AppButton label={t('journey.nextPage')} onPress={() => setPage(page + 1)} variant="secondary" /> : null}
        </ScrollView>
      </ScreenState>
    </FeatureGate>
  );
}
