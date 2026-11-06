import { zodResolver } from '@hookform/resolvers/zod';
import { useEffect, useRef, useState } from 'react';
import { Controller, useForm } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { KeyboardAvoidingView, Platform, Pressable, ScrollView, View } from 'react-native';
import { z } from 'zod';
import { useTheme } from '../../../app/providers/ThemeProvider';
import { ChoiceGroup } from '../../../components/forms/ChoiceGroup';
import { FormField } from '../../../components/forms/FormField';
import { ScreenState } from '../../../components/feedback/ScreenState';
import { AppButton } from '../../../components/ui/AppButton';
import { AppCard } from '../../../components/ui/AppCard';
import { AppText } from '../../../components/ui/AppText';
import { AppTextInput } from '../../../components/ui/AppTextInput';
import { isNotFound, namedFieldErrors, unmappedValidation } from '../../../services/api/problem';
import { useCreateGoal, useFitnessProfile, useGoals, useSaveProfile } from '../api/queries';
import { activityLevels, fitnessLevels, goalTypes, type ActivityLevel, type FitnessGoalType, type FitnessLevel, type UnitSystem } from '../types';

const schema = z.object({
  unitSystem: z.enum(['Metric', 'Imperial']),
  height: z.string(),
  weight: z.string(),
  fitnessLevel: z.enum(['Beginner', 'Intermediate', 'Advanced']),
  primaryGoal: z.enum(['FatLoss', 'MuscleGain', 'Strength', 'Endurance', 'GeneralFitness', 'Mobility', 'WeightMaintenance']),
  activityLevel: z.enum(['Sedentary', 'Light', 'Moderate', 'Active', 'VeryActive']),
  trainingExperience: z.string().max(500),
  preferredWorkoutDays: z.string(),
  preferredWorkoutDuration: z.string(),
  preferredTrainingLocation: z.string().max(100),
});

type ProfileValues = z.infer<typeof schema>;

const emptyValues: ProfileValues = {
  unitSystem: 'Metric',
  height: '',
  weight: '',
  fitnessLevel: 'Beginner',
  primaryGoal: 'GeneralFitness',
  activityLevel: 'Moderate',
  trainingExperience: '',
  preferredWorkoutDays: '3',
  preferredWorkoutDuration: '45',
  preferredTrainingLocation: '',
};

export function FitnessScreen() {
  const theme = useTheme();
  const { t } = useTranslation();
  const profile = useFitnessProfile();
  const goals = useGoals();
  const save = useSaveProfile();
  const createGoal = useCreateGoal();
  const [saved, setSaved] = useState(false);
  const [goalType, setGoalType] = useState<FitnessGoalType>('GeneralFitness');
  const [target, setTarget] = useState('');
  const targetRef = useRef('');
  const [targetError, setTargetError] = useState<string | null>(null);
  const missing = profile.isError && isNotFound(profile.error);
  const form = useForm<ProfileValues>({ resolver: zodResolver(schema), defaultValues: emptyValues });

  useEffect(() => {
    if (!profile.data) return;
    form.reset({
      unitSystem: profile.data.unitSystem,
      height: profile.data.height?.toString() ?? '',
      weight: profile.data.weight?.toString() ?? '',
      fitnessLevel: profile.data.fitnessLevel,
      primaryGoal: profile.data.primaryGoal,
      activityLevel: profile.data.activityLevel,
      trainingExperience: profile.data.trainingExperience ?? '',
      preferredWorkoutDays: String(profile.data.preferredWorkoutDays),
      preferredWorkoutDuration: String(profile.data.preferredWorkoutDuration),
      preferredTrainingLocation: profile.data.preferredTrainingLocation ?? '',
    });
    setGoalType(profile.data.primaryGoal);
  }, [profile.data, form]);

  const submit = form.handleSubmit((values) => {
    form.clearErrors();
    const height = readNumber(values.height, 1, 300);
    const weight = readNumber(values.weight, 1, 500);
    const days = readNumber(values.preferredWorkoutDays, 0, 7);
    const duration = readNumber(values.preferredWorkoutDuration, 0, 300);
    let invalid = false;
    if (height === 'invalid') {
      form.setError('height', { message: t('journey.heightRange') });
      invalid = true;
    }
    if (weight === 'invalid') {
      form.setError('weight', { message: t('journey.weightRange') });
      invalid = true;
    }
    if (days === null) {
      form.setError('preferredWorkoutDays', { message: t('journey.required') });
      invalid = true;
    } else if (days === 'invalid') {
      form.setError('preferredWorkoutDays', { message: t('journey.daysRange') });
      invalid = true;
    }
    if (duration === null) {
      form.setError('preferredWorkoutDuration', { message: t('journey.required') });
      invalid = true;
    } else if (duration === 'invalid') {
      form.setError('preferredWorkoutDuration', { message: t('journey.durationRange') });
      invalid = true;
    }
    if (invalid || height === 'invalid' || weight === 'invalid' || days === null || days === 'invalid' || duration === null || duration === 'invalid') {
      return;
    }
    setSaved(false);
    save.mutate({
      unitSystem: values.unitSystem,
      height,
      weight,
      fitnessLevel: values.fitnessLevel,
      primaryGoal: values.primaryGoal,
      activityLevel: values.activityLevel,
      trainingExperience: values.trainingExperience.trim() || null,
      preferredWorkoutDays: days,
      preferredWorkoutDuration: duration,
      preferredTrainingLocation: values.preferredTrainingLocation.trim() || null,
    }, { onSuccess: () => setSaved(true) });
  });

  const active = goals.data?.find((goal) => goal.status === 'Active') ?? null;
  const profileError = profile.isError && !missing ? unmappedValidation(profile.error, [], t('common.offline')) : null;
  const profileFields = save.isError ? namedFieldErrors(save.error) : {};
  const goalFields = createGoal.isError ? namedFieldErrors(createGoal.error) : {};
  const profileAttached = ['unitSystem', 'height', 'weight', 'fitnessLevel', 'primaryGoal', 'activityLevel', 'preferredWorkoutDays', 'preferredWorkoutDuration', 'preferredTrainingLocation', 'trainingExperience'];
  const goalAttached = ['goalType', 'targetValue', 'currentValue', 'startDate', 'targetDate'];
  const profileBanner = save.isError ? unmappedValidation(save.error, profileAttached, t('common.offline')) : null;
  const goalBanner = createGoal.isError ? unmappedValidation(createGoal.error, goalAttached, t('common.offline')) : null;

  return (
    <ScreenState error={profileError} loading={profile.isLoading} onRetry={() => void profile.refetch()}>
      <KeyboardAvoidingView behavior={Platform.OS === 'ios' ? 'padding' : undefined} style={{ flex: 1 }}>
        <ScrollView contentContainerStyle={{ gap: theme.spacing.lg, padding: theme.spacing.xl }} keyboardShouldPersistTaps="handled">
          <AppText variant="title">{t('journey.profileTitle')}</AppText>
          <AppText tone="muted">{t('journey.notMedical')}</AppText>
          {missing ? <AppText tone="muted">{t('journey.profileEmpty')}</AppText> : null}
          <ChoiceGroup error={profileFields.unitSystem} label={t('journey.unit')} onChange={(value) => form.setValue('unitSystem', value as UnitSystem)} options={[{ value: 'Metric', label: t('journey.metric') }, { value: 'Imperial', label: t('journey.imperial') }]} required value={form.watch('unitSystem')} />
          <Controller control={form.control} name="height" render={({ field, fieldState }) => (
            <FormField error={fieldState.error?.message ?? profileFields.height} label={t('journey.height')}>
              <AppTextInput accessibilityLabel={t('journey.height')} invalid={Boolean(fieldState.error ?? profileFields.height)} keyboardType="decimal-pad" onChangeText={field.onChange} value={field.value} />
            </FormField>
          )} />
          <Controller control={form.control} name="weight" render={({ field, fieldState }) => (
            <FormField error={fieldState.error?.message ?? profileFields.weight} label={t('journey.weight')}>
              <AppTextInput accessibilityLabel={t('journey.weight')} invalid={Boolean(fieldState.error ?? profileFields.weight)} keyboardType="decimal-pad" onChangeText={field.onChange} value={field.value} />
            </FormField>
          )} />
          <ChoiceGroup error={profileFields.fitnessLevel} label={t('journey.level')} onChange={(value) => form.setValue('fitnessLevel', value as FitnessLevel)} options={fitnessLevels.map((value) => ({ value, label: t(`journey.fitnessLevel.${value}`) }))} required value={form.watch('fitnessLevel')} />
          <ChoiceGroup error={profileFields.activityLevel} label={t('journey.activity')} onChange={(value) => form.setValue('activityLevel', value as ActivityLevel)} options={activityLevels.map((value) => ({ value, label: t(`journey.activityLevel.${value}`) }))} required value={form.watch('activityLevel')} />
          <ChoiceGroup error={profileFields.primaryGoal} label={t('journey.goal')} onChange={(value) => form.setValue('primaryGoal', value as FitnessGoalType)} options={goalTypes.map((value) => ({ value, label: t(`journey.goalType.${value}`) }))} required value={form.watch('primaryGoal')} />
          <Controller control={form.control} name="preferredWorkoutDays" render={({ field, fieldState }) => (
            <FormField error={fieldState.error?.message ?? profileFields.preferredWorkoutDays} label={t('journey.days')} required>
              <AppTextInput accessibilityLabel={t('journey.days')} invalid={Boolean(fieldState.error ?? profileFields.preferredWorkoutDays)} keyboardType="number-pad" onChangeText={field.onChange} value={field.value} />
            </FormField>
          )} />
          <Controller control={form.control} name="preferredWorkoutDuration" render={({ field, fieldState }) => (
            <FormField error={fieldState.error?.message ?? profileFields.preferredWorkoutDuration} label={t('journey.duration')} required>
              <AppTextInput accessibilityLabel={t('journey.duration')} invalid={Boolean(fieldState.error ?? profileFields.preferredWorkoutDuration)} keyboardType="number-pad" onChangeText={field.onChange} value={field.value} />
            </FormField>
          )} />
          <Controller control={form.control} name="preferredTrainingLocation" render={({ field, fieldState }) => (
            <FormField error={fieldState.error?.message ?? profileFields.preferredTrainingLocation} label={t('journey.location')}>
              <AppTextInput accessibilityLabel={t('journey.location')} invalid={Boolean(fieldState.error ?? profileFields.preferredTrainingLocation)} onChangeText={field.onChange} value={field.value} />
            </FormField>
          )} />
          <Controller control={form.control} name="trainingExperience" render={({ field, fieldState }) => (
            <FormField error={fieldState.error?.message ?? profileFields.trainingExperience} label={t('journey.experience')}>
              <AppTextInput accessibilityLabel={t('journey.experience')} invalid={Boolean(fieldState.error ?? profileFields.trainingExperience)} onChangeText={field.onChange} value={field.value} />
            </FormField>
          )} />
          {profileBanner ? <AppText accessibilityRole="alert" tone="danger">{profileBanner}</AppText> : null}
          {saved ? <AppText tone="accent">{t('journey.saved')}</AppText> : null}
          <AppButton label={t('common.save')} loading={save.isPending} onPress={() => { void submit(); }} />
          <AppText variant="title">{t('journey.goalTitle')}</AppText>
          {active ? <AppCard><AppText variant="label">{t('journey.activeGoal')}</AppText><AppText>{t(`journey.goalType.${active.goalType}`)}</AppText>{active.targetValue !== null ? <AppText tone="muted">{t('journey.target')}: {active.targetValue}</AppText> : null}</AppCard> : <AppText tone="muted">{t('journey.noGoal')}</AppText>}
          <AppText accessibilityLabel={`${t('journey.goalChoice')}, required`} variant="label">
            {t('journey.goalChoice')}
            <AppText tone="danger" variant="label"> *</AppText>
          </AppText>
          <View accessibilityRole="radiogroup" style={{ gap: theme.spacing.sm }}>
            {goalTypes.map((type) => (
              <Pressable accessibilityRole="radio" accessibilityState={{ selected: goalType === type }} key={type} onPress={() => setGoalType(type)} style={{ minHeight: theme.control.minTouch, justifyContent: 'center' }} testID={`goal-${type}`}>
                <AppText tone={goalType === type ? 'accent' : 'default'}>{t(`journey.goalType.${type}`)}</AppText>
              </Pressable>
            ))}
          </View>
          {goalFields.goalType ? <AppText accessibilityRole="alert" tone="danger" variant="caption">{goalFields.goalType}</AppText> : null}
          <FormField error={targetError ?? goalFields.targetValue} label={t('journey.target')}>
            <AppTextInput accessibilityLabel={t('journey.target')} invalid={Boolean(targetError ?? goalFields.targetValue)} keyboardType="decimal-pad" onChangeText={(value) => { targetRef.current = value; setTarget(value); setTargetError(null); }} testID="goal-target" value={target} />
          </FormField>
          {goalBanner ? <AppText accessibilityRole="alert" tone="danger">{goalBanner}</AppText> : null}
          <AppButton label={t('journey.setGoal')} loading={createGoal.isPending} onPress={() => {
            const parsed = readNumber(targetRef.current, 0, 100000);
            if (parsed === 'invalid') {
              setTargetError(t('journey.invalidNumber'));
              return;
            }
            setTargetError(null);
            createGoal.mutate({ goalType, targetValue: parsed, currentValue: null, startDate: new Date().toISOString().slice(0, 10), targetDate: null });
          }} variant="secondary" />
          {goals.isError ? <AppButton label={t('common.retry')} onPress={() => void goals.refetch()} variant="secondary" /> : null}
        </ScrollView>
      </KeyboardAvoidingView>
    </ScreenState>
  );
}

function readNumber(value: string, min: number, max: number): number | null | 'invalid' {
  const trimmed = value.trim();
  if (!trimmed) return null;
  const parsed = Number(trimmed);
  if (!Number.isFinite(parsed) || parsed < min || parsed > max) return 'invalid';
  return parsed;
}
