import type { NativeStackScreenProps } from '@react-navigation/native-stack';
import { useTranslation } from 'react-i18next';
import { ScrollView } from 'react-native';
import type { AppStackParamList } from '../../../app/navigation/types';
import { ScreenState } from '../../../components/feedback/ScreenState';
import { AppText } from '../../../components/ui/AppText';
import { errorText } from '../../../services/api/problem';
import { useMemberProgram } from '../api/workspace.queries';
import { TrainerCard, useTrainerLayout } from '../ui';

type Props = NativeStackScreenProps<AppStackParamList, 'TrainerPlanHistory'>;

export function TrainerPlanHistoryScreen({ route }: Props) {
  const { t } = useTranslation();
  const layout = useTrainerLayout();
  const program = useMemberProgram(route.params.memberId, route.params.programId);
  const data = program.data;

  return (
    <ScreenState error={program.isError ? errorText(program.error, t('common.offline')) : null} loading={program.isLoading} onRetry={() => void program.refetch()}>
      <ScrollView contentContainerStyle={layout.content}>
        <AppText variant="title">{t('trainer.history')}</AppText>
        <AppText tone="muted">{t('trainer.historyBody')}</AppText>
        {data ? (
          <TrainerCard>
            <AppText variant="label">{data.name}</AppText>
            <AppText>{t('trainer.historyStatus')}</AppText>
            <AppText tone="muted">{t('journey.weeks', { count: data.durationWeeks })} · {t('journey.exercisesDone', { count: data.days.filter((day) => !day.restDay).length })}</AppText>
            <AppText tone="muted">{data.difficulty}</AppText>
          </TrainerCard>
        ) : null}
      </ScrollView>
    </ScreenState>
  );
}
