import type { NativeStackScreenProps } from '@react-navigation/native-stack';
import { useLayoutEffect, type ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { Image, ScrollView, View } from 'react-native';
import type { AppStackParamList } from '../../../app/navigation/types';
import { useTheme } from '../../../app/providers/ThemeProvider';
import { ScreenState } from '../../../components/feedback/ScreenState';
import { AppButton } from '../../../components/ui/AppButton';
import { AppText } from '../../../components/ui/AppText';
import { errorText } from '../../../services/api/problem';
import { useFitnessAssessment } from '../../assessment/api/assessment.queries';
import type { FitnessAssessment } from '../../assessment/types';
import { useProgram } from '../../fitness/api/queries';
import { CalendarIcon, ChevronIcon, ClockIcon, DumbbellIcon, FlameIcon, MuscleIcon, RunnerIcon } from '../../plan/planIcons';

const summit = require('../../../../assets/journey/plan-summit.jpg') as number;

type Props = NativeStackScreenProps<AppStackParamList, 'WorkoutPlan'>;
type FocusIcon = 'flame' | 'muscle' | 'run';

export function WorkoutPlanScreen({ navigation, route }: Props) {
  const theme = useTheme();
  const { t } = useTranslation();
  const program = useProgram(route.params.programId);
  const assessment = useFitnessAssessment();
  const data = program.data;
  const saved = assessment.data;
  const minutes = saved?.workout?.durationMinutes ?? null;
  const focuses = focusRows(saved, t);

  useLayoutEffect(() => {
    navigation.setOptions?.({ title: t('journey.planOverviewTitle') });
  }, [navigation, t]);

  return (
    <ScreenState error={program.isError ? errorText(program.error, t('common.offline')) : null} loading={program.isLoading || assessment.isLoading} onRetry={() => { void program.refetch(); void assessment.refetch(); }}>
      <ScrollView contentContainerStyle={{ gap: theme.spacing.lg, padding: theme.spacing.xl }}>
        {data ? (
          <View style={{ borderRadius: 22, height: 176, overflow: 'hidden' }}>
            <Image accessibilityIgnoresInvertColors resizeMode="cover" source={summit} style={{ bottom: 0, height: 250, position: 'absolute', width: '100%' }} />
            <View style={{ gap: theme.spacing.xs, left: 16, position: 'absolute', right: '40%', top: 16 }}>
              <View style={{ alignItems: 'center', backgroundColor: 'rgba(7,17,13,0.55)', borderRadius: 12, height: 36, justifyContent: 'center', width: 36 }}>
                <CalendarIcon />
              </View>
              <AppText variant="title">{data.name}</AppText>
              <AppText tone="muted">{t('journey.personalized')}</AppText>
            </View>
          </View>
        ) : null}
        {data ? (
          <View style={{ flexDirection: 'row', gap: theme.spacing.sm }}>
            <Stat icon={<CalendarIcon />} label={t('journey.daysLabel')} value={String(data.days.length)} />
            <Stat icon={<DumbbellIcon />} label={t('journey.daysPerWeekLabel')} value={textOrDash(saved?.workout?.daysPerWeek)} />
            <Stat icon={<ClockIcon />} label={t('journey.perSession')} value={minutes ? `~${minutes} min` : '—'} />
          </View>
        ) : null}
        {data ? (
          <View style={{ gap: theme.spacing.sm }}>
            <AppText variant="label">{t('journey.planFocus')}</AppText>
            {focuses.map((row) => (
              <View key={row.label} style={{ alignItems: 'center', backgroundColor: theme.colors.surface, borderColor: theme.colors.border, borderRadius: theme.radius.lg, borderWidth: 1, flexDirection: 'row', gap: theme.spacing.md, minHeight: theme.control.minTouch, paddingHorizontal: theme.spacing.md, paddingVertical: theme.spacing.sm }}>
                <View style={{ alignItems: 'center', backgroundColor: theme.colors.surfaceMuted, borderRadius: 16, height: 32, justifyContent: 'center', width: 32 }}>
                  <FocusGlyph name={row.icon} />
                </View>
                <AppText style={{ flex: 1 }}>{row.label}</AppText>
                <ChevronIcon />
              </View>
            ))}
          </View>
        ) : null}
        {data ? <AppButton label={t('journey.viewSchedule')} onPress={() => navigation.navigate('PlanCalendar', { programId: data.id })} /> : null}
      </ScrollView>
    </ScreenState>
  );
}

function Stat({ icon, label, value }: { icon: ReactNode; label: string; value: string }) {
  const theme = useTheme();
  return (
    <View style={{ alignItems: 'center', backgroundColor: theme.colors.surface, borderColor: theme.colors.border, borderRadius: theme.radius.lg, borderWidth: 1, flex: 1, gap: theme.spacing.xs, paddingVertical: theme.spacing.md }}>
      {icon}
      <AppText variant="title">{value}</AppText>
      <AppText tone="muted" variant="caption">{label}</AppText>
    </View>
  );
}

function FocusGlyph({ name }: { name: FocusIcon }) {
  if (name === 'flame') return <FlameIcon />;
  if (name === 'run') return <RunnerIcon />;
  return <MuscleIcon />;
}

function textOrDash(value: number | null | undefined): string {
  return value == null ? '—' : String(value);
}

function focusRows(assessment: FitnessAssessment | null | undefined, t: (key: string) => string): { icon: FocusIcon; label: string }[] {
  if (!assessment) return [];
  const rows: { icon: FocusIcon; label: string }[] = [];
  const add = (value: string | null | undefined, group: 'goals' | 'priorities' | 'styleOptions') => {
    if (!value) return;
    const label = t(`assessment.${group}.${value}`);
    if (label.startsWith('assessment.') || rows.some((row) => row.label === label)) return;
    rows.push({ icon: focusIcon(value), label });
  };
  add(assessment.primaryGoal, 'goals');
  add(assessment.secondaryGoal, 'goals');
  add(assessment.priority, 'priorities');
  for (const style of assessment.workout?.trainingStyles ?? []) add(style, 'styleOptions');
  return rows;
}

function focusIcon(value: string): FocusIcon {
  if (/weightloss|fatloss|recomp/i.test(value)) return 'flame';
  if (/endurance|cardio|hiit|athletic/i.test(value)) return 'run';
  return 'muscle';
}
