import type { CompositeNavigationProp } from '@react-navigation/native';
import type { BottomTabNavigationProp } from '@react-navigation/bottom-tabs';
import type { NativeStackNavigationProp } from '@react-navigation/native-stack';
import { useNavigation } from '@react-navigation/native';
import { useTranslation } from 'react-i18next';
import { RefreshControl, ScrollView, View } from 'react-native';
import type { AppStackParamList, TrainerTabParamList } from '../../../app/navigation/types';
import { ScreenState } from '../../../components/feedback/ScreenState';
import { AppAvatar } from '../../../components/ui/AppAvatar';
import { AppButton } from '../../../components/ui/AppButton';
import { AppText } from '../../../components/ui/AppText';
import { useMyProfilePhoto } from '../../profile/photo';
import { errorText } from '../../../services/api/problem';
import { useSessionStore } from '../../../store/sessionStore';
import { useMyTrainerPhoto, useTrainerHome } from '../api/workspace.queries';
import { clock, Stat, StatusPill, TrainerCard, useTrainerLayout } from '../ui';

type Navigation = CompositeNavigationProp<BottomTabNavigationProp<TrainerTabParamList, 'Home'>, NativeStackNavigationProp<AppStackParamList>>;

export function TrainerHomeScreen() {
  const { t } = useTranslation();
  const layout = useTrainerLayout();
  const navigation = useNavigation<Navigation>();
  const user = useSessionStore((state) => state.user);
  const photo = useMyProfilePhoto();
  const home = useTrainerHome();
  const trainerPhoto = useMyTrainerPhoto();
  const name = home.data?.firstName || user?.firstName || '';

  return (
    <ScreenState error={home.isError ? errorText(home.error, t('common.offline')) : null} loading={home.isLoading} onRetry={() => void home.refetch()}>
      <ScrollView contentContainerStyle={layout.content} refreshControl={<RefreshControl onRefresh={() => void home.refetch()} refreshing={home.isRefetching} tintColor={layout.refresh} />}>
        <View style={{ alignItems: 'center', flexDirection: 'row', gap: 12 }}>
          <AppAvatar label={`${user?.firstName ?? ''} ${user?.lastName ?? ''}`} uri={trainerPhoto.data ?? photo.data} />
          <View style={{ flex: 1, gap: 4 }}>
            <AppText variant="display">{t('trainer.greeting', { name })}</AppText>
            <AppText tone="muted">{t('trainer.greetingBody')}</AppText>
          </View>
        </View>
        <View style={layout.row}>
          <Stat label={t('trainer.sessionsToday')} value={`${home.data?.sessionsToday ?? 0}`} />
          <Stat label={t('trainer.activeMembers')} value={`${home.data?.activeMembers ?? 0}`} />
          <Stat label={t('trainer.pendingNotes')} value={`${home.data?.pendingNotes ?? 0}`} />
          <Stat label={t('trainer.newAssignments')} value={`${home.data?.newAssignments ?? 0}`} />
        </View>
        <TrainerCard>
          <AppText variant="label">{t('trainer.nextSession')}</AppText>
          {home.data?.nextSession ? (
            <>
              <AppText variant="title">{clock(home.data.nextSession.scheduledStartUtc)} · {home.data.nextSession.memberName}</AppText>
              <AppText tone="muted">{home.data.nextSession.sessionType ?? t('trainer.session')}{home.data.nextSession.location ? ` · ${home.data.nextSession.location}` : ''}</AppText>
              <AppButton label={t('trainer.startSession')} onPress={() => navigation.navigate('TrainerSession', { sessionId: home.data!.nextSession!.id })} />
            </>
          ) : <AppText tone="muted">{t('trainer.noNext')}</AppText>}
        </TrainerCard>
        <View style={{ flexDirection: 'row', justifyContent: 'space-between' }}>
          <AppText variant="title">{t('trainer.today')}</AppText>
          <AppText onPress={() => navigation.navigate('TrainerAlerts')} tone="accent">{t('trainer.alerts')}</AppText>
        </View>
        {(home.data?.today.length ?? 0) === 0 ? <AppText tone="muted">{t('trainer.noSessions')}</AppText> : home.data?.today.map((item) => (
          <TrainerCard key={item.id} onPress={() => navigation.navigate('TrainerSession', { sessionId: item.id })}>
            <AppText variant="label">{clock(item.scheduledStartUtc)}</AppText>
            <AppText>{item.memberName}</AppText>
            <StatusPill status={item.status} />
          </TrainerCard>
        ))}
      </ScrollView>
    </ScreenState>
  );
}
