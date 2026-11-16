import type { CompositeNavigationProp } from '@react-navigation/native';
import type { BottomTabNavigationProp } from '@react-navigation/bottom-tabs';
import type { NativeStackNavigationProp } from '@react-navigation/native-stack';
import { useNavigation } from '@react-navigation/native';
import { useTranslation } from 'react-i18next';
import { Pressable, RefreshControl, ScrollView, View } from 'react-native';
import { NotificationBellButton } from '../../../app/navigation/NotificationBellButton';
import type { AppStackParamList, TrainerTabParamList } from '../../../app/navigation/types';
import { useTheme } from '../../../app/providers/ThemeProvider';
import { ScreenState } from '../../../components/feedback/ScreenState';
import { AppAvatar } from '../../../components/ui/AppAvatar';
import { AppText } from '../../../components/ui/AppText';
import { useMyProfilePhoto } from '../../profile/photo';
import { errorText } from '../../../services/api/problem';
import { useSessionStore } from '../../../store/sessionStore';
import { useMyTrainerPhoto, useTrainerHome } from '../api/workspace.queries';
import { TrainerIcon, type TrainerIconName } from '../icons';
import { useTrainerLayout } from '../ui';

type Navigation = CompositeNavigationProp<BottomTabNavigationProp<TrainerTabParamList, 'Home'>, NativeStackNavigationProp<AppStackParamList>>;

export function TrainerHomeScreen() {
  const theme = useTheme();
  const { t } = useTranslation();
  const layout = useTrainerLayout({ topInset: true });
  const navigation = useNavigation<Navigation>();
  const user = useSessionStore((state) => state.user);
  const photo = useMyProfilePhoto();
  const home = useTrainerHome();
  const trainerPhoto = useMyTrainerPhoto();
  const name = home.data?.firstName || user?.firstName || '';
  const avatar = trainerPhoto.data ?? photo.data;

  return (
    <ScreenState error={home.isError ? errorText(home.error, t('common.offline')) : null} loading={home.isLoading} onRetry={() => void home.refetch()}>
      <ScrollView contentContainerStyle={layout.content} refreshControl={<RefreshControl onRefresh={() => void home.refetch()} refreshing={home.isRefetching} tintColor={layout.refresh} />}>
        <View style={{ alignItems: 'center', flexDirection: 'row', justifyContent: 'space-between' }}>
          <View>
            <AppText tone="accent" variant="title">Growfit</AppText>
            <AppText tone="muted" variant="caption">{t('trainer.roleLabel')}</AppText>
          </View>
          <View style={{ alignItems: 'center', flexDirection: 'row', gap: 8 }}>
            <NotificationBellButton />
            <AppAvatar label={`${user?.firstName ?? ''} ${user?.lastName ?? ''}`} size={40} uri={avatar} />
          </View>
        </View>
        <View style={{ alignItems: 'center', flexDirection: 'row', gap: 12 }}>
          <AppAvatar label={name} size={72} uri={avatar} />
          <View style={{ flex: 1, gap: 4 }}>
            <AppText variant="title">{t('trainer.hi', { name })}</AppText>
            <AppText tone="muted">{t('trainer.ready')}</AppText>
          </View>
        </View>
        <View style={layout.row}>
          <Metric icon="users" label={t('trainer.myMembers')} value={`${home.data?.activeMembers ?? 0}`} />
          <Metric icon="clipboard" label={t('trainer.sessionsToday')} value={`${home.data?.sessionsToday ?? 0}`} />
          <Metric icon="pencil" label={t('trainer.pendingNotes')} value={`${home.data?.pendingNotes ?? 0}`} />
        </View>
        <AppText variant="title">{t('trainer.tasks')}</AppText>
        <TaskRow count={home.data?.pendingNotes ?? 0} icon="clipboard" label={t('trainer.planReviews')} onPress={() => navigation.navigate('TrainerAlerts')} />
        <TaskRow count={home.data?.newAssignments ?? 0} icon="calendar" label={t('trainer.planRequests')} onPress={() => navigation.navigate('Members')} />
        <TaskRow count={home.data?.sessionsToday ?? 0} icon="userCheck" label={t('trainer.checkIns')} onPress={() => navigation.navigate('TrainerSchedule')} />
        <AppText variant="title">{t('trainer.quickActions')}</AppText>
        <View style={{ flexDirection: 'row', gap: theme.spacing.sm }}>
          <QuickAction highlighted icon="users" label={t('trainer.myMembers')} onPress={() => navigation.navigate('Members')} />
          <QuickAction icon="dumbbell" label={t('trainer.library')} onPress={() => navigation.navigate('TrainerExerciseLibrary', {})} />
          <QuickAction icon="clipboard" label={t('trainer.plans')} onPress={() => navigation.navigate('Plans')} />
        </View>
      </ScrollView>
    </ScreenState>
  );
}

function Metric({ icon, label, value }: { icon: TrainerIconName; label: string; value: string }) {
  const theme = useTheme();
  return (
    <View style={{ backgroundColor: theme.colors.surface, borderColor: theme.colors.border, borderRadius: theme.radius.lg, borderWidth: 1, flexBasis: '30%', flexGrow: 1, gap: theme.spacing.xs, padding: theme.spacing.md }}>
      <TrainerIcon color={theme.colors.accent} name={icon} size={18} />
      <AppText variant="title">{value}</AppText>
      <AppText tone="muted" variant="caption">{label}</AppText>
    </View>
  );
}

function TaskRow({ icon, label, count, onPress }: { icon: TrainerIconName; label: string; count: number; onPress: () => void }) {
  const theme = useTheme();
  return (
    <Pressable accessibilityRole="button" onPress={onPress} style={({ pressed }) => ({ opacity: pressed ? 0.86 : 1 })}>
      <View style={{ alignItems: 'center', backgroundColor: theme.colors.surface, borderColor: theme.colors.border, borderRadius: theme.radius.lg, borderWidth: 1, flexDirection: 'row', gap: theme.spacing.md, minHeight: theme.control.minTouch, padding: theme.spacing.lg }}>
        <View style={{ backgroundColor: theme.colors.surfaceMuted, borderRadius: theme.radius.md, padding: theme.spacing.sm }}>
          <TrainerIcon color={theme.colors.accent} name={icon} size={18} />
        </View>
        <AppText style={{ flex: 1 }} variant="label">{label}</AppText>
        <AppText tone="accent">{count}</AppText>
        <TrainerIcon color={theme.colors.muted} name="chevron" size={18} />
      </View>
    </Pressable>
  );
}

function QuickAction({ icon, label, onPress, highlighted = false }: { icon: TrainerIconName; label: string; onPress: () => void; highlighted?: boolean }) {
  const theme = useTheme();
  const color = highlighted ? theme.colors.onAccent : theme.colors.accent;
  return (
    <Pressable accessibilityRole="button" onPress={onPress} style={({ pressed }) => ({ flex: 1, opacity: pressed ? 0.86 : 1 })}>
      <View style={{ alignItems: 'center', aspectRatio: 1, backgroundColor: highlighted ? theme.colors.accent : theme.colors.surface, borderColor: highlighted ? theme.colors.accent : theme.colors.border, borderRadius: theme.radius.lg, borderWidth: 1, gap: theme.spacing.sm, justifyContent: 'center', padding: theme.spacing.sm }}>
        <TrainerIcon color={color} name={icon} size={22} />
        <AppText style={{ color: highlighted ? theme.colors.onAccent : theme.colors.text, textAlign: 'center' }} variant="caption">{label}</AppText>
      </View>
    </Pressable>
  );
}
