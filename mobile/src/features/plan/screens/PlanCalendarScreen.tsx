import type { NativeStackScreenProps } from '@react-navigation/native-stack';
import { useTranslation } from 'react-i18next';
import { Pressable, ScrollView, View } from 'react-native';
import type { AppStackParamList } from '../../../app/navigation/types';
import { useTheme } from '../../../app/providers/ThemeProvider';
import { ScreenState } from '../../../components/feedback/ScreenState';
import { AppText } from '../../../components/ui/AppText';
import { errorText } from '../../../services/api/problem';
import { useProgram, useSessions } from '../../fitness/api/queries';
import { completedDayIds, dayDate, nextTrainingDay } from '../planModel';
import { ThumbPhoto } from '../heroes';
import { ProgressRing } from '../PlanParts';

type Props = NativeStackScreenProps<AppStackParamList, 'PlanCalendar'>;

export function PlanCalendarScreen({ navigation, route }: Props) {
  const theme = useTheme();
  const { t } = useTranslation();
  const program = useProgram(route.params.programId);
  const sessions = useSessions(1);
  const data = program.data;
  const done = data ? completedDayIds(data, sessions.data?.items ?? []) : new Set<string>();
  const current = data ? nextTrainingDay(data, done) : null;
  const percent = data && data.days.length > 0 ? (done.size / data.days.length) * 100 : 0;
  const loading = program.isLoading || sessions.isLoading;
  const error = program.error ?? sessions.error;

  return (
    <ScreenState error={error ? errorText(error, t('common.offline')) : null} loading={loading} onRetry={() => { void program.refetch(); void sessions.refetch(); }}>
      <ScrollView contentContainerStyle={{ gap: theme.spacing.lg, padding: theme.spacing.xl }}>
        <View style={{ alignItems: 'center', flexDirection: 'row', justifyContent: 'space-between' }}>
          <AppText variant="title">{t('journey.calendarTitle')}</AppText>
          <ProgressRing percent={percent} />
        </View>
        <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: theme.spacing.sm }}>
          <Legend color={theme.colors.success} label={t('journey.legendDone')} />
          <Legend color={theme.colors.accent} label={t('journey.legendToday')} />
          <Legend color={theme.colors.muted} label={t('journey.legendNext')} />
          <Legend color={theme.colors.border} label={t('journey.legendRest')} />
        </View>
        <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: theme.spacing.sm }}>
          {data?.days.map((day) => {
            const state = done.has(day.id) ? 'done' : day.id === current?.id ? 'today' : day.restDay ? 'rest' : 'next';
            const background = state === 'done' ? theme.colors.success : state === 'today' ? theme.colors.accent : theme.colors.surface;
            const color = state === 'today' ? theme.colors.onAccent : theme.colors.text;
            return (
              <Pressable
                accessibilityLabel={day.name}
                accessibilityRole="button"
                key={day.id}
                onPress={() => navigation.navigate('WorkoutDay', { programId: route.params.programId, dayId: day.id })}
                style={{ backgroundColor: background, borderColor: theme.colors.border, borderRadius: theme.radius.md, borderWidth: 1, minHeight: theme.control.minTouch, padding: theme.spacing.sm, width: '22%' }}
              >
                <AppText style={{ color }} variant="label">{dayDate(day)?.slice(8) ?? day.dayNumber}</AppText>
                <AppText numberOfLines={2} style={{ color }} tone={state === 'today' ? 'default' : 'muted'}>{day.restDay ? t('journey.restDay') : day.name}</AppText>
              </Pressable>
            );
          })}
        </View>
        {current ? (
          <Pressable accessibilityRole="button" onPress={() => navigation.navigate('WorkoutDay', { programId: route.params.programId, dayId: current.id })}>
            <View style={{ alignItems: 'center', backgroundColor: theme.colors.surface, borderColor: theme.colors.border, borderRadius: theme.radius.lg, borderWidth: 1, flexDirection: 'row', gap: theme.spacing.md, padding: theme.spacing.md }}>
              <ThumbPhoto label={current.name} />
              <View style={{ flex: 1, gap: theme.spacing.xs }}>
                <AppText variant="label">{t('journey.planDay', { current: current.dayNumber, total: data?.days.length ?? current.dayNumber })}</AppText>
                <AppText>{current.name}</AppText>
              </View>
            </View>
          </Pressable>
        ) : null}
      </ScrollView>
    </ScreenState>
  );
}

function Legend({ color, label }: { color: string; label: string }) {
  const theme = useTheme();
  return (
    <View style={{ alignItems: 'center', flexDirection: 'row', gap: theme.spacing.xs }}>
      <View style={{ backgroundColor: color, borderRadius: 6, height: 12, width: 12 }} />
      <AppText tone="muted">{label}</AppText>
    </View>
  );
}
