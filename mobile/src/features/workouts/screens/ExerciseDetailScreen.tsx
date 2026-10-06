import type { NativeStackScreenProps } from '@react-navigation/native-stack';
import { useTranslation } from 'react-i18next';
import { ScrollView } from 'react-native';
import type { AppStackParamList } from '../../../app/navigation/types';
import { useTheme } from '../../../app/providers/ThemeProvider';
import { FeatureGate } from '../../../components/access/FeatureGate';
import { ScreenState } from '../../../components/feedback/ScreenState';
import { AppCard } from '../../../components/ui/AppCard';
import { AppText } from '../../../components/ui/AppText';
import { featureCodes } from '../../../constants/access';
import { errorText } from '../../../services/api/problem';
import { useExercise } from '../../fitness/api/queries';

type Props = NativeStackScreenProps<AppStackParamList, 'ExerciseDetail'>;

export function ExerciseDetailScreen({ route }: Props) {
  const theme = useTheme();
  const { t } = useTranslation();
  const exercise = useExercise(route.params.exerciseId);
  const data = exercise.data;

  return (
    <FeatureGate code={featureCodes.workoutPlans}>
    <ScreenState error={exercise.isError ? errorText(exercise.error, t('common.offline')) : null} loading={exercise.isLoading} onRetry={() => void exercise.refetch()}>
      <ScrollView contentContainerStyle={{ gap: theme.spacing.lg, padding: theme.spacing.xl }}>
        <AppText variant="title">{data?.name}</AppText>
        <AppCard>
          <AppText tone="muted">{data?.muscleGroup}</AppText>
          {data?.equipment ? <AppText>{t('journey.equipmentLabel')}: {data.equipment}</AppText> : null}
          {data?.difficulty ? <AppText>{t('journey.level')}: {data.difficulty}</AppText> : null}
          {data?.exerciseType ? <AppText tone="muted">{data.exerciseType}</AppText> : null}
        </AppCard>
        {data?.description ? <AppText>{data.description}</AppText> : null}
        {data?.instructions ? (
          <AppCard>
            <AppText variant="label">{t('journey.instructions')}</AppText>
            <AppText>{data.instructions}</AppText>
          </AppCard>
        ) : null}
        <AppText tone="muted">{t('journey.notMedical')}</AppText>
      </ScrollView>
    </ScreenState>
    </FeatureGate>
  );
}
