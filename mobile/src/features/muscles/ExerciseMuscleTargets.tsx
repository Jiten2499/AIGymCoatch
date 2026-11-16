import { useTranslation } from 'react-i18next';
import { View } from 'react-native';
import { useTheme } from '../../app/providers/ThemeProvider';
import { AppText } from '../../components/ui/AppText';
import { MuscleGroupIllustration } from './MuscleGroupIllustration';
import { resolveExerciseMuscles, type MuscleTargetExercise } from './resolveMuscles';

type ExerciseMuscleTargetsProps = {
  exercise: MuscleTargetExercise;
  variant?: 'full' | 'compact';
};

export function ExerciseMuscleTargets({ exercise, variant = 'full' }: ExerciseMuscleTargetsProps) {
  const theme = useTheme();
  const { t } = useTranslation();
  const targets = resolveExerciseMuscles(exercise);

  if (!targets) {
    return (
      <View accessibilityLabel={t('journey.muscleUnavailable')}>
        <AppText tone="muted">{t('journey.muscleUnavailable')}</AppText>
      </View>
    );
  }

  return (
    <View style={{ gap: theme.spacing.sm }}>
      <AppText variant="label">{t('journey.muscleTargets')}</AppText>
      {targets.primary.map((id) => (
        <MuscleGroupIllustration key={id} mode="primary" muscleGroupId={id} size={variant === 'compact' ? 'compact' : 'full'} />
      ))}
      {targets.secondary.length > 0 ? (
        <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: theme.spacing.sm }}>
          {targets.secondary.map((id) => (
            <View key={id} style={{ width: variant === 'compact' ? 88 : 108 }}>
              <MuscleGroupIllustration mode="secondary" muscleGroupId={id} size="compact" />
            </View>
          ))}
        </View>
      ) : null}
    </View>
  );
}
