import type { NativeStackScreenProps } from '@react-navigation/native-stack';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { RefreshControl, ScrollView, View } from 'react-native';
import type { AppStackParamList } from '../../../app/navigation/types';
import { useTheme } from '../../../app/providers/ThemeProvider';
import { ScreenState } from '../../../components/feedback/ScreenState';
import { AppButton } from '../../../components/ui/AppButton';
import { AppText } from '../../../components/ui/AppText';
import { MemberPhoto } from '../../../components/ui/MemberPhoto';
import { errorText } from '../../../services/api/problem';
import { useSessionStore } from '../../../store/sessionStore';
import { useMemberPrograms, useMemberProgress, useTrainerMember } from '../api/workspace.queries';
import { formatChange, formatMeasure, weightUnit } from '../progressForm';
import { formatProgressDate } from '../progressSeries';
import { MetricCard, ProgressButton } from '../progressUi';
import { clock, FilterChip, StatusPill, TrainerCard, useTrainerLayout } from '../ui';

type Props = NativeStackScreenProps<AppStackParamList, 'TrainerMember'>;
type Tab = 'overview' | 'plan' | 'progress';

export function TrainerMemberScreen({ route, navigation }: Props) {
  const theme = useTheme();
  const { t } = useTranslation();
  const layout = useTrainerLayout();
  const trainer = useSessionStore((state) => state.user);
  const member = useTrainerMember(route.params.memberId);
  const progress = useMemberProgress(route.params.memberId);
  const programs = useMemberPrograms(route.params.memberId);
  const [tab, setTab] = useState<Tab>(route.params.tab ?? 'overview');
  const data = member.data;
  const plan = programs.data?.items[0] ?? null;

  return (
    <ScreenState error={member.isError ? errorText(member.error, t('common.offline')) : null} loading={member.isLoading} onRetry={() => void member.refetch()}>
      <ScrollView contentContainerStyle={layout.content} refreshControl={<RefreshControl onRefresh={() => { void member.refetch(); void progress.refetch(); void programs.refetch(); }} refreshing={member.isRefetching || progress.isRefetching || programs.isRefetching} tintColor={layout.refresh} />}>
        <View style={{ alignItems: 'center', flexDirection: 'row', gap: 12 }}>
          <View style={{ borderColor: theme.colors.accent, borderRadius: 36, borderWidth: 1 }}>
            <MemberPhoto label={data?.name ?? ''} path={data ? `/trainers/me/members/${route.params.memberId}/profile-photo` : null} size={72} />
          </View>
          <View style={{ flex: 1, gap: 4 }}>
            <AppText variant="title">{data?.name}</AppText>
            <AppText tone="muted">{data?.id}</AppText>
            {data ? <StatusPill status={data.status} /> : null}
            {data?.joinDate ? <AppText tone="muted">{t('trainer.joined', { date: data.joinDate })}</AppText> : null}
          </View>
        </View>
        <View style={layout.row}>
          {(['overview', 'plan', 'progress'] as Tab[]).map((item) => (
            <FilterChip key={item} label={t(item === 'plan' ? 'trainer.workoutPlan' : `trainer.${item}`)} onPress={() => setTab(item)} selected={tab === item} />
          ))}
        </View>
        {tab === 'overview' && data ? (
          <>
            <Info label={t('trainer.goal')} value={data.goal ?? t('trainer.noGoal')} />
            <Info label={t('trainer.membership')} value={data.packages[0]?.name ?? t('trainer.noPackage')} />
            <Info label={t('trainer.assignedTrainer')} value={trainer ? `${trainer.firstName} ${trainer.lastName}` : t('trainer.coach')} />
            {data.phone ? <Info label={t('trainer.phone')} value={data.phone} /> : null}
            <AppText variant="label">{t('trainer.packages')}</AppText>
            {data.packages.length === 0 ? <AppText tone="muted">{t('trainer.noPackage')}</AppText> : data.packages.map((item) => (
              <TrainerCard key={`${item.name}-${item.endDate}`}>
                <AppText variant="label">{item.name}</AppText>
                <AppText>{t('trainer.packageUse', { used: item.sessionCount - item.sessionsRemaining, total: item.sessionCount })}</AppText>
                <AppText tone="muted">{item.endDate}</AppText>
              </TrainerCard>
            ))}
            <AppText variant="label">{t('trainer.sessions')}</AppText>
            {data.sessions.length === 0 ? <AppText tone="muted">{t('trainer.noSessions')}</AppText> : data.sessions.slice(0, 3).map((item) => (
              <TrainerCard key={item.id} onPress={() => navigation.navigate('TrainerSession', { sessionId: item.id })}>
                <AppText>{clock(item.scheduledStartUtc)} · {item.sessionType ?? t('trainer.session')}</AppText>
                <StatusPill status={item.status} />
              </TrainerCard>
            ))}
            <AppText variant="label">{t('trainer.notes')}</AppText>
            {data.notes.length === 0 ? <AppText tone="muted">{t('trainer.noNotes')}</AppText> : data.notes.map((item) => (
              <TrainerCard key={item.sessionId}><AppText>{item.text}</AppText></TrainerCard>
            ))}
            <TrainerCard>
              <AppText variant="label">{t('trainer.currentPlan')}</AppText>
              {plan ? (
                <>
                  <AppText variant="title">{plan.name}</AppText>
                  <AppText tone="muted">{t(`journey.goalType.${plan.goalType}`)} · {t('journey.weeks', { count: plan.durationWeeks })}</AppText>
                  <AppText tone="muted">{t('journey.perWeek', { count: plan.frequencyPerWeek })}</AppText>
                </>
              ) : <AppText tone="muted">{t('trainer.planEmpty')}</AppText>}
            </TrainerCard>
            {plan ? <AppButton label={t('trainer.viewPlan')} onPress={() => navigation.navigate('TrainerMemberPlan', { memberId: route.params.memberId, programId: plan.id })} /> : null}
          </>
        ) : null}
        {tab === 'plan' ? (
          <>
            {programs.isError ? <AppText tone="danger">{errorText(programs.error, t('common.offline'))}</AppText> : null}
            {programs.isLoading ? <AppText tone="muted">{t('common.loading')}</AppText> : null}
            {(programs.data?.items.length ?? 0) === 0 && !programs.isLoading ? <AppText tone="muted">{t('trainer.planEmpty')}</AppText> : null}
            {programs.data?.items.map((item) => (
              <TrainerCard key={item.id} onPress={() => navigation.navigate('TrainerMemberPlan', { memberId: route.params.memberId, programId: item.id })}>
                <AppText variant="label">{item.name}</AppText>
                <AppText tone="muted">{t(`journey.goalType.${item.goalType}`)} · {t('journey.weeks', { count: item.durationWeeks })}</AppText>
              </TrainerCard>
            ))}
          </>
        ) : null}
        {tab === 'progress' ? (
          <>
            {progress.isError ? <AppText tone="danger">{errorText(progress.error, t('common.offline'))}</AppText> : null}
            {progress.isLoading ? <AppText tone="muted">{t('common.loading')}</AppText> : null}
            {(progress.data?.length ?? 0) === 0 && !progress.isLoading ? <AppText tone="muted">{t('trainer.noProgress')}</AppText> : null}
            {progress.data?.[0] ? (
              <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: 12 }}>
                <MetricCard detail={formatChange(progress.data[0].weight.change, weightUnit(progress.data[0].unitSystem))} label={t('trainer.currentWeight')} value={formatMeasure(progress.data[0].weight.value, weightUnit(progress.data[0].unitSystem))} />
                <MetricCard detail={formatChange(progress.data[0].bodyFatPercentage.change, '%')} label={t('trainer.bodyFat')} value={formatMeasure(progress.data[0].bodyFatPercentage.value, '%')} />
                <TrainerCard>
                  <AppText variant="label">{t('trainer.latestProgress')}</AppText>
                  <AppText>{formatProgressDate(progress.data[0].assessmentDate)}</AppText>
                </TrainerCard>
              </View>
            ) : null}
            <ProgressButton label={t('trainer.viewProgress')} onPress={() => navigation.navigate('TrainerMemberProgress', { memberId: route.params.memberId })} variant="secondary" />
          </>
        ) : null}
      </ScrollView>
    </ScreenState>
  );
}

function Info({ label, value }: { label: string; value: string }) {
  return (
    <TrainerCard>
      <AppText tone="muted" variant="caption">{label}</AppText>
      <AppText>{value}</AppText>
    </TrainerCard>
  );
}
