import type { NativeStackScreenProps } from '@react-navigation/native-stack';
import { useTranslation } from 'react-i18next';
import { RefreshControl, ScrollView } from 'react-native';
import type { AppStackParamList } from '../../../app/navigation/types';
import { ScreenState } from '../../../components/feedback/ScreenState';
import { AppText } from '../../../components/ui/AppText';
import { errorText } from '../../../services/api/problem';
import { useTrainerAlerts } from '../api/workspace.queries';
import { TrainerCard, useTrainerLayout } from '../ui';

type Props = NativeStackScreenProps<AppStackParamList, 'TrainerAlerts'>;

export function TrainerAlertsScreen({ navigation }: Props) {
  const { t } = useTranslation();
  const layout = useTrainerLayout();
  const alerts = useTrainerAlerts();

  return (
    <ScreenState empty={(alerts.data?.length ?? 0) === 0} emptyBody={t('trainer.noAlerts')} emptyTitle={t('trainer.alerts')} error={alerts.isError ? errorText(alerts.error, t('common.offline')) : null} loading={alerts.isLoading} onRetry={() => void alerts.refetch()}>
      <ScrollView contentContainerStyle={layout.content} refreshControl={<RefreshControl onRefresh={() => void alerts.refetch()} refreshing={alerts.isRefetching} tintColor={layout.refresh} />}>
        <AppText variant="display">{t('trainer.alerts')}</AppText>
        {alerts.data?.map((item, index) => (
          <TrainerCard key={`${item.kind}-${item.atUtc}-${index}`} onPress={() => { if (item.sessionId) { navigation.navigate('TrainerSession', { sessionId: item.sessionId }); } else if (item.memberId) { navigation.navigate('TrainerMember', { memberId: item.memberId }); } }}>
            <AppText variant="label">{t(`trainer.alert.${item.kind}`, { defaultValue: item.kind })}</AppText>
            <AppText>{item.title}</AppText>
            <AppText tone="muted">{item.body}</AppText>
          </TrainerCard>
        ))}
      </ScrollView>
    </ScreenState>
  );
}
