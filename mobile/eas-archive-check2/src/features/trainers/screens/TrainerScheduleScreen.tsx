import type { CompositeNavigationProp } from '@react-navigation/native';
import type { BottomTabNavigationProp } from '@react-navigation/bottom-tabs';
import type { NativeStackNavigationProp } from '@react-navigation/native-stack';
import { useNavigation } from '@react-navigation/native';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Pressable, RefreshControl, ScrollView, View } from 'react-native';
import type { AppStackParamList, TrainerTabParamList } from '../../../app/navigation/types';
import { useTheme } from '../../../app/providers/ThemeProvider';
import { ScreenState } from '../../../components/feedback/ScreenState';
import { AppButton } from '../../../components/ui/AppButton';
import { FormField } from '../../../components/forms/FormField';
import { AppText } from '../../../components/ui/AppText';
import { AppTextInput } from '../../../components/ui/AppTextInput';
import { errorText } from '../../../services/api/problem';
import { useTrainerMembers, useTrainerMutations, useTrainerSchedule } from '../api/workspace.queries';
import { Chevron } from '../../../app/navigation/TabGlyph';
import { clock, dayKey, FilterChip, localKey, monthBounds, StatusPill, TrainerCard, useTrainerLayout } from '../ui';

type Navigation = CompositeNavigationProp<BottomTabNavigationProp<TrainerTabParamList, 'Schedule'>, NativeStackNavigationProp<AppStackParamList>>;
type Span = 'day' | 'week' | 'month';

export function TrainerScheduleScreen() {
  const { t } = useTranslation();
  const layout = useTrainerLayout();
  const navigation = useNavigation<Navigation>();
  const [cursor, setCursor] = useState(() => new Date());
  const [span, setSpan] = useState<Span>('day');
  const [open, setOpen] = useState(false);
  const bounds = monthBounds(cursor);
  const schedule = useTrainerSchedule(bounds.from, bounds.to);
  const visible = (schedule.data ?? []).filter((item) => inSpan(item.scheduledStartUtc, cursor, span));

  return (
    <ScreenState error={schedule.isError ? errorText(schedule.error, t('common.offline')) : null} loading={schedule.isLoading} onRetry={() => void schedule.refetch()}>
      <ScrollView contentContainerStyle={layout.content} refreshControl={<RefreshControl onRefresh={() => void schedule.refetch()} refreshing={schedule.isRefetching} tintColor={layout.refresh} />}>
        <View style={{ alignItems: 'center', flexDirection: 'row', justifyContent: 'space-between' }}>
          <DateStep direction="left" onPress={() => setCursor(shift(cursor, span, -1))} />
          <AppText variant="title">{cursor.toLocaleDateString(undefined, span === 'month' ? { month: 'long', year: 'numeric' } : { day: 'numeric', month: 'short' })}</AppText>
          <DateStep direction="right" onPress={() => setCursor(shift(cursor, span, 1))} />
        </View>
        <View style={{ flexDirection: 'row', gap: 8 }}>
          {(['day', 'week', 'month'] as Span[]).map((item) => (
            <FilterChip key={item} label={t(`trainer.${item}`)} onPress={() => setSpan(item)} selected={span === item} />
          ))}
        </View>
        <AppButton label={t('trainer.assignSession')} onPress={() => setOpen((value) => !value)} />
        {open ? <ScheduleForm onDone={() => setOpen(false)} /> : null}
        {visible.length === 0 ? <AppText tone="muted">{t('trainer.noSessions')}</AppText> : visible.map((item) => (
          <TrainerCard key={item.id} onPress={() => navigation.navigate('TrainerSession', { sessionId: item.id })}>
            <AppText variant="label">{new Date(item.scheduledStartUtc).toLocaleDateString()} · {clock(item.scheduledStartUtc)}</AppText>
            <AppText>{item.memberName}</AppText>
            <AppText tone="muted">{item.sessionType ?? t('trainer.session')}{item.location ? ` · ${item.location}` : ''}</AppText>
            <StatusPill status={item.status} />
          </TrainerCard>
        ))}
      </ScrollView>
    </ScreenState>
  );
}

function DateStep({ direction, onPress }: { direction: 'left' | 'right'; onPress: () => void }) {
  const theme = useTheme();
  return (
    <Pressable accessibilityRole="button" onPress={onPress} style={{ alignItems: 'center', backgroundColor: theme.colors.surfaceMuted, borderRadius: theme.radius.pill, height: 44, justifyContent: 'center', width: 44 }}>
      <Chevron color={theme.colors.text} direction={direction} />
    </Pressable>
  );
}

function ScheduleForm({ onDone }: { onDone: () => void }) {
  const { t } = useTranslation();
  const members = useTrainerMembers('');
  const mutations = useTrainerMutations();
  const [memberId, setMemberId] = useState(members.data?.[0]?.id ?? '');
  const [date, setDate] = useState(dayKey(new Date()));
  const [start, setStart] = useState('10:00');
  const [end, setEnd] = useState('11:00');
  const [sessionType, setSessionType] = useState('Personal Training');
  const [location, setLocation] = useState('');
  const selected = memberId || members.data?.[0]?.id || '';

  return (
    <TrainerCard>
      <AppText variant="label">{t('trainer.member')}</AppText>
      {(members.data ?? []).map((member) => (
        <Pressable accessibilityRole="button" key={member.id} onPress={() => setMemberId(member.id)}>
          <AppText tone={selected === member.id ? 'accent' : 'default'}>{member.name}</AppText>
        </Pressable>
      ))}
      <FormField label={t('trainer.date')}><AppTextInput accessibilityLabel={t('trainer.date')} onChangeText={setDate} value={date} /></FormField>
      <FormField label={t('trainer.start')}><AppTextInput accessibilityLabel={t('trainer.start')} onChangeText={setStart} value={start} /></FormField>
      <FormField label={t('trainer.endTime')}><AppTextInput accessibilityLabel={t('trainer.endTime')} onChangeText={setEnd} value={end} /></FormField>
      <FormField label={t('trainer.type')}><AppTextInput accessibilityLabel={t('trainer.type')} onChangeText={setSessionType} value={sessionType} /></FormField>
      <FormField label={t('trainer.location')}><AppTextInput accessibilityLabel={t('trainer.location')} onChangeText={setLocation} value={location} /></FormField>
      {mutations.schedule.isError ? <AppText tone="danger">{errorText(mutations.schedule.error, t('common.offline'))}</AppText> : null}
      <AppButton label={t('common.save')} loading={mutations.schedule.isPending} onPress={() => { const startUtc = combine(date, start); const endUtc = combine(date, end); if (!selected || !startUtc || !endUtc) { return; } mutations.schedule.mutate({ memberId: selected, startUtc, endUtc, sessionType, location }, { onSuccess: onDone }); }} />
    </TrainerCard>
  );
}

function combine(date: string, time: string) {
  const [year, month, day] = date.split('-').map(Number);
  const [hour, minute] = time.split(':').map(Number);
  if (!year || !month || !day || Number.isNaN(hour) || Number.isNaN(minute)) {
    return null;
  }
  return new Date(year, month - 1, day, hour, minute).toISOString();
}

function shift(cursor: Date, span: Span, direction: number) {
  const next = new Date(cursor);
  next.setDate(cursor.getDate() + direction * (span === 'month' ? 30 : span === 'week' ? 7 : 1));
  return next;
}

function inSpan(iso: string, cursor: Date, span: Span) {
  const key = localKey(iso);
  if (span === 'day') {
    return key === dayKey(cursor);
  }
  if (span === 'month') {
    return key.slice(0, 7) === dayKey(cursor).slice(0, 7);
  }
  const start = new Date(cursor);
  const weekday = start.getDay();
  start.setDate(start.getDate() + (weekday === 0 ? -6 : 1 - weekday));
  const end = new Date(start);
  end.setDate(start.getDate() + 6);
  return key >= dayKey(start) && key <= dayKey(end);
}
