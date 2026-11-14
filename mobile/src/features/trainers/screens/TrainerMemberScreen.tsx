import type { NativeStackScreenProps } from '@react-navigation/native-stack';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { RefreshControl, ScrollView, View } from 'react-native';
import type { AppStackParamList } from '../../../app/navigation/types';
import { ScreenState } from '../../../components/feedback/ScreenState';
import { AppText } from '../../../components/ui/AppText';
import { MemberPhoto } from '../../../components/ui/MemberPhoto';
import { errorText } from '../../../services/api/problem';
import { useMemberPrograms, useMemberProgress, useTrainerMember } from '../api/workspace.queries';
import { formatChange, formatMeasure, weightUnit } from '../progressForm';
import { formatProgressDate } from '../progressSeries';
import { MetricCard, ProgressButton } from '../progressUi';
import { clock, FilterChip, StatusPill, TrainerCard, useTrainerLayout } from '../ui';

type Props = NativeStackScreenProps<AppStackParamList, 'TrainerMember'>;
type Tab = 'details' | 'sessions' | 'packages' | 'notes' | 'progress' | 'plans';

const tabs: Tab[] = ['details', 'sessions', 'packages', 'notes', 'progress', 'plans'];

export function TrainerMemberScreen({ route, navigation }: Props) {
  const { t } = useTranslation();
  const layout = useTrainerLayout();
  const member = useTrainerMember(route.params.memberId);
  const progress = useMemberProgress(route.params.memberId);
  const programs = useMemberPrograms(route.params.memberId);
  const [tab, setTab] = useState<Tab>('details');
  const data = member.data;

  return (
    <ScreenState error={member.isError ? errorText(member.error, t('common.offline')) : null} loading={member.isLoading} onRetry={() => void member.refetch()}>
      <ScrollView contentContainerStyle={layout.content} refreshControl={<RefreshControl onRefresh={() => { void member.refetch(); void progress.refetch(); void programs.refetch(); }} refreshing={member.isRefetching || progress.isRefetching || programs.isRefetching} tintColor={layout.refresh} />}>
        <View style={{ alignItems: 'center', flexDirection: 'row', gap: 12 }}>
          <MemberPhoto label={data?.name ?? ''} path={data ? `/trainers/me/members/${route.params.memberId}/profile-photo` : null} size={72} />
          <AppText variant="display">{data?.name}</AppText>
        </View>
        <View style={layout.row}>
          {tabs.map((item) => (
            <FilterChip key={item} label={t(`trainer.${item}`)} onPress={() => setTab(item)} selected={tab === item} />
          ))}
        </View>
        {tab === 'details' ? (
          <>
            <AppText tone="muted">{data?.goal ?? t('trainer.noGoal')}</AppText>
            {data ? <StatusPill status={data.status} /> : null}
            {data?.phone ? <AppText>{data.phone}</AppText> : null}
            {data?.joinDate ? <AppText tone="muted">{t('trainer.joined', { date: data.joinDate })}</AppText> : null}
          </>
        ) : null}
        {tab === 'packages' ? (
          (data?.packages.length ?? 0) === 0 ? <AppText tone="muted">{t('trainer.noPackage')}</AppText> : data?.packages.map((item) => (
            <TrainerCard key={`${item.name}-${item.endDate}`}>
              <AppText variant="label">{item.name}</AppText>
              <AppText>{t('trainer.packageUse', { used: item.sessionCount - item.sessionsRemaining, total: item.sessionCount })}</AppText>
              <AppText tone="muted">{item.endDate} · {item.price} {item.currency}</AppText>
              {item.expiring ? <AppText style={{ color: layout.warning }}>{t('trainer.expiring')}</AppText> : null}
            </TrainerCard>
          ))
        ) : null}
        {tab === 'sessions' ? (
          (data?.sessions.length ?? 0) === 0 ? <AppText tone="muted">{t('trainer.noSessions')}</AppText> : data?.sessions.map((item) => (
            <TrainerCard key={item.id} onPress={() => navigation.navigate('TrainerSession', { sessionId: item.id })}>
              <AppText>{clock(item.scheduledStartUtc)} · {item.sessionType ?? t('trainer.session')}</AppText>
              <StatusPill status={item.status} />
            </TrainerCard>
          ))
        ) : null}
        {tab === 'notes' ? (
          (data?.notes.length ?? 0) === 0 ? <AppText tone="muted">{t('trainer.noNotes')}</AppText> : data?.notes.map((item) => (
            <TrainerCard key={item.sessionId}><AppText>{item.text}</AppText></TrainerCard>
          ))
        ) : null}
        {tab === 'progress' ? (
          <>
            {progress.isError ? <AppText tone="danger">{errorText(progress.error, t('common.offline'))}</AppText> : null}
            {progress.isLoading ? <AppText tone="muted">{t('common.loading')}</AppText> : null}
            {(progress.data?.length ?? 0) === 0 && !progress.isLoading ? <AppText tone="muted">{t('trainer.noProgress')}</AppText> : null}
            {progress.data?.[0] ? (
              <>
                <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: 12 }}>
                  <MetricCard detail={formatChange(progress.data[0].weight.change, weightUnit(progress.data[0].unitSystem))} label={t('trainer.currentWeight')} value={formatMeasure(progress.data[0].weight.value, weightUnit(progress.data[0].unitSystem))} />
                  <MetricCard detail={formatChange(progress.data[0].bodyFatPercentage.change, '%')} label={t('trainer.bodyFat')} value={formatMeasure(progress.data[0].bodyFatPercentage.value, '%')} />
                </View>
                <TrainerCard>
                  <AppText variant="label">{t('trainer.latestProgress')}</AppText>
                  <AppText>{formatProgressDate(progress.data[0].assessmentDate)}</AppText>
                  {progress.data[0].notes ? <AppText>{progress.data[0].notes}</AppText> : null}
                </TrainerCard>
              </>
            ) : null}
            <ProgressButton label={t('trainer.recordProgress')} onPress={() => navigation.navigate('TrainerMemberProgress', { memberId: route.params.memberId, start: 'record' })} />
            <ProgressButton label={t('trainer.viewProgress')} onPress={() => navigation.navigate('TrainerMemberProgress', { memberId: route.params.memberId })} variant="secondary" />
          </>
        ) : null}
        {tab === 'plans' ? (
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
      </ScrollView>
    </ScreenState>
  );
}
