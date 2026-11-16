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
import { ExerciseMuscleTargets } from '../../muscles/ExerciseMuscleTargets';
import { ScenePhoto } from '../../plan/heroes';

type Props = NativeStackScreenProps<AppStackParamList, 'ExerciseDetail'>;

function instructionSteps(instructions: string): string[] {
  const lines = instructions.split(/\n+/).map((line) => line.trim()).filter((line) => line.length > 0);
  return lines.length > 0 ? lines : [instructions];
}

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
        {data ? <ScenePhoto label={`${data.name} ${data.muscleGroup}`} /> : null}
        {data ? <ExerciseMuscleTargets exercise={data} /> : null}
        <AppCard>
          <AppText tone="muted">{data?.muscleGroup}</AppText>
          {data?.equipment ? <AppText>{t('journey.equipmentLabel')}: {data.equipment}</AppText> : null}
          {data?.difficulty ? <AppText>{t('journey.level')}: {data.difficulty}</AppText> : null}
          {data?.exerciseType ? <AppText tone="muted">{data.exerciseType}</AppText> : null}
        </AppCard>
        {data?.description ? <AppText>{data.description}</AppText> : null}
        {data?.instructions ? (
          <AppCard>
            <AppText variant="label">{t('journey.guide')}</AppText>
            {instructionSteps(data.instructions).map((line, index, lines) => (
              <AppText key={line}>{lines.length > 1 ? `${index + 1}. ${line}` : line}</AppText>
            ))}
          </AppCard>
        ) : null}
        <AppText tone="muted">{t('journey.notMedical')}</AppText>
      </ScrollView>
    </ScreenState>
    </FeatureGate>
  );
}
