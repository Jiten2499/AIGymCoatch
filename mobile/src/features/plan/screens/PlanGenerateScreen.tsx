import type { NativeStackScreenProps } from '@react-navigation/native-stack';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { ActivityIndicator, ScrollView, View } from 'react-native';
import type { AppStackParamList } from '../../../app/navigation/types';
import { useTheme } from '../../../app/providers/ThemeProvider';
import { AppButton } from '../../../components/ui/AppButton';
import { AppCard } from '../../../components/ui/AppCard';
import { AppText } from '../../../components/ui/AppText';
import { errorText } from '../../../services/api/problem';
import { generateWorkout } from '../../aiCoach/api/ai.api';
import { AiOrb } from '../heroes';
import { PlanLengthField } from '../PlanLengthField';
import { tenantKey } from '../../fitness/api/keys';
import { useTenantId } from '../../fitness/api/queries';

type Props = NativeStackScreenProps<AppStackParamList, 'PlanGenerate'>;

const stages = ['stageProfile', 'stageWorkouts', 'stageSchedule'] as const;

export function PlanGenerateScreen({ navigation }: Props) {
  const theme = useTheme();
  const { t } = useTranslation();
  const client = useQueryClient();
  const tenantId = useTenantId();
  const [stage, setStage] = useState(0);
  const [planDays, setPlanDays] = useState(30);
  const generate = useMutation({
    mutationFn: () => generateWorkout(null, planDays),
    onSuccess: async (program) => {
      client.setQueryData(tenantKey(tenantId, 'program', program.id), program);
      await client.invalidateQueries({ queryKey: tenantKey(tenantId, 'programs') });
      navigation.replace('WorkoutPlan', { programId: program.id });
    },
  });

  useEffect(() => {
    if (!generate.isPending || theme.reduceMotion) {
      return;
    }
    const timer = setInterval(() => setStage((current) => (current + 1) % stages.length), theme.duration(1600));
    return () => clearInterval(timer);
  }, [generate.isPending, theme]);

  return (
    <ScrollView contentContainerStyle={{ flexGrow: 1, gap: theme.spacing.lg, justifyContent: 'center', padding: theme.spacing.xl }}>
      <AiOrb />
      <AppText variant="display">{t('journey.generateTitle')}</AppText>
      <AppText tone="muted">{t('journey.generateBody')}</AppText>
      <AppCard>
        {generate.isPending ? <ActivityIndicator color={theme.colors.accent} /> : null}
        <View style={{ gap: theme.spacing.sm }}>
          {stages.map((key, index) => (
            <AppText key={key} tone={generate.isPending && index === stage ? 'accent' : 'muted'}>{t(`journey.${key}`)}</AppText>
          ))}
        </View>
      </AppCard>
      <PlanLengthField disabled={generate.isPending} onChange={setPlanDays} value={planDays} />
      {generate.isError ? <AppText accessibilityRole="alert" tone="danger">{errorText(generate.error, t('common.offline'))}</AppText> : null}
      <AppButton disabled={generate.isPending} label={generate.isError ? t('journey.generateRetry') : t('journey.generate')} loading={generate.isPending} onPress={() => generate.mutate()} />
    </ScrollView>
  );
}
