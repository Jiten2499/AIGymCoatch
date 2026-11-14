import type { NativeStackScreenProps } from '@react-navigation/native-stack';
import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { KeyboardAvoidingView, Platform, ScrollView, View } from 'react-native';
import type { AppStackParamList } from '../../../app/navigation/types';
import { useTheme } from '../../../app/providers/ThemeProvider';
import { ChoiceGroup } from '../../../components/forms/ChoiceGroup';
import { FormField } from '../../../components/forms/FormField';
import { ScreenState } from '../../../components/feedback/ScreenState';
import { AppButton } from '../../../components/ui/AppButton';
import { AppCard } from '../../../components/ui/AppCard';
import { AppText } from '../../../components/ui/AppText';
import { AppTextInput } from '../../../components/ui/AppTextInput';
import { errorText } from '../../../services/api/problem';
import { StepIndicator } from '../../trainers/progressUi';
import { activityLevels, fitnessLevels } from '../../fitness/types';
import { MultiSelectChips } from '../MultiSelectChips';
import { useFitnessAssessment, useSaveFitnessAssessment } from '../api/assessment.queries';
import { ageYears, bodyMassIndex, emptyDraft, fromAssessment, toPayload } from '../draft';
import { assessmentGoals, weekDays, type AssessmentDraft } from '../types';

type Props = NativeStackScreenProps<AppStackParamList, 'FitnessAssessment'>;

const stepKeys = ['personal', 'goals', 'health', 'workout', 'diet', 'lifestyle', 'measurements', 'review'] as const;

export function FitnessAssessmentScreen({ navigation }: Props) {
  const theme = useTheme();
  const { t } = useTranslation();
  const assessment = useFitnessAssessment();
  const save = useSaveFitnessAssessment();
  const [draft, setDraft] = useState<AssessmentDraft>(emptyDraft);
  const [step, setStep] = useState(0);
  const [message, setMessage] = useState<string | null>(null);
  const [hydrated, setHydrated] = useState(false);
  const create = !assessment.data;
  const bmi = bodyMassIndex(draft.heightCm, draft.currentWeightKg);
  const age = ageYears(draft.dateOfBirth);
  const labels = stepKeys.map((key) => t(`assessment.steps.${key}`));

  useEffect(() => {
    if (hydrated || assessment.isLoading) {
      return;
    }
    if (assessment.data) {
      const next = fromAssessment(assessment.data);
      setDraft(next);
      setStep(Math.max(0, Math.min(7, next.currentStep - 1)));
    }
    setHydrated(true);
  }, [assessment.data, assessment.isLoading, hydrated]);

  const patch = (update: Partial<AssessmentDraft>) => setDraft((current) => ({ ...current, ...update }));
  const persist = (submit: boolean, nextStep = draft.currentStep) => {
    setMessage(null);
    const body = toPayload({ ...draft, currentStep: nextStep }, submit);
    save.mutate(
      { body, create: create && !assessment.data },
      {
        onSuccess: (data) => {
          setDraft(fromAssessment(data));
          setMessage(submit ? t('assessment.submitted') : t('assessment.draftSaved'));
          if (submit) {
            navigation.goBack();
          }
        },
      },
    );
  };

  const continueStep = () => {
    const next = Math.min(7, step + 1);
    setStep(next);
    persist(false, next + 1);
  };

  return (
    <ScreenState error={assessment.isError ? errorText(assessment.error, t('common.offline')) : null} loading={assessment.isLoading && !hydrated} onRetry={() => void assessment.refetch()}>
      <KeyboardAvoidingView behavior={Platform.OS === 'ios' ? 'padding' : undefined} style={{ flex: 1 }}>
        <ScrollView contentContainerStyle={{ gap: theme.spacing.lg, padding: theme.spacing.xl, paddingBottom: theme.spacing.xxxl }}>
          <AppText variant="display">{t('assessment.title')}</AppText>
          <AppText tone="muted">{t('assessment.subtitle')}</AppText>
          <AppText variant="label">{t('assessment.progress', { percent: draft.currentStep === 8 && assessment.data?.isCompleted ? 100 : Math.round(((step + 1) / 8) * 100) })}</AppText>
          <StepIndicator labels={labels} onStep={setStep} step={step} />
          {save.isError ? <AppText tone="danger">{errorText(save.error, t('common.offline'))}</AppText> : null}
          {message ? <AppText>{message}</AppText> : null}

          {step === 0 ? (
            <AppCard>
              <ChoiceGroup label={t('assessment.units')} onChange={(value) => patch({ measurementUnit: value as AssessmentDraft['measurementUnit'] })} options={[{ value: 'Metric', label: t('assessment.metric') }, { value: 'Imperial', label: t('assessment.imperial') }]} required value={draft.measurementUnit} />
              <FormField label={t('assessment.dateOfBirth')} required><AppTextInput accessibilityLabel={t('assessment.dateOfBirth')} onChangeText={(value) => patch({ dateOfBirth: value })} placeholder="YYYY-MM-DD" value={draft.dateOfBirth} /></FormField>
              {age != null ? <AppText tone="muted">{t('assessment.age', { count: age })}</AppText> : null}
              <ChoiceGroup label={t('assessment.gender')} onChange={(value) => patch({ gender: value })} options={[{ value: 'Male', label: t('assessment.genders.Male') }, { value: 'Female', label: t('assessment.genders.Female') }, { value: 'Other', label: t('assessment.genders.Other') }, { value: 'PreferNotToSay', label: t('assessment.genders.PreferNotToSay') }]} value={draft.gender} />
              <FormField label={t('assessment.heightCm')} required><AppTextInput keyboardType="decimal-pad" onChangeText={(value) => patch({ heightCm: value })} value={draft.heightCm} /></FormField>
              <FormField label={t('assessment.currentWeightKg')} required><AppTextInput keyboardType="decimal-pad" onChangeText={(value) => patch({ currentWeightKg: value })} value={draft.currentWeightKg} /></FormField>
              <FormField label={t('assessment.targetWeightKg')}><AppTextInput keyboardType="decimal-pad" onChangeText={(value) => patch({ targetWeightKg: value })} value={draft.targetWeightKg} /></FormField>
              <FormField label={t('assessment.waistCm')}><AppTextInput keyboardType="decimal-pad" onChangeText={(value) => patch({ waistCircumferenceCm: value })} value={draft.waistCircumferenceCm} /></FormField>
              <FormField label={t('assessment.bodyFat')}><AppTextInput keyboardType="decimal-pad" onChangeText={(value) => patch({ bodyFatPercentage: value })} value={draft.bodyFatPercentage} /></FormField>
              <FormField label={t('assessment.country')}><AppTextInput onChangeText={(value) => patch({ country: value })} value={draft.country} /></FormField>
              {bmi != null ? <AppText tone="muted">{t('assessment.bmi', { value: bmi })}</AppText> : null}
              <AppText tone="muted">{t('assessment.bmiNote')}</AppText>
            </AppCard>
          ) : null}

          {step === 1 ? (
            <AppCard>
              <ChoiceGroup label={t('assessment.primaryGoal')} onChange={(value) => patch({ primaryGoal: value })} options={assessmentGoals.map((value) => ({ value, label: t(`assessment.goals.${value}`) }))} required value={draft.primaryGoal} />
              <ChoiceGroup label={t('assessment.secondaryGoal')} onChange={(value) => patch({ secondaryGoal: value })} options={[{ value: '', label: t('assessment.none') }, ...assessmentGoals.map((value) => ({ value, label: t(`assessment.goals.${value}`) }))]} value={draft.secondaryGoal} />
              <ChoiceGroup label={t('assessment.goalTimeline')} onChange={(value) => patch({ goalTimeline: value })} options={[{ value: 'OneMonth', label: t('assessment.timelines.OneMonth') }, { value: 'ThreeMonths', label: t('assessment.timelines.ThreeMonths') }, { value: 'SixMonths', label: t('assessment.timelines.SixMonths') }, { value: 'TwelveMonths', label: t('assessment.timelines.TwelveMonths') }, { value: 'Custom', label: t('assessment.timelines.Custom') }]} value={draft.goalTimeline} />
              {draft.goalTimeline === 'Custom' ? <FormField label={t('assessment.customDays')}><AppTextInput keyboardType="number-pad" onChangeText={(value) => patch({ customGoalTimelineDays: value })} value={draft.customGoalTimelineDays} /></FormField> : null}
              <ChoiceGroup label={t('assessment.fitnessLevel')} onChange={(value) => patch({ fitnessLevel: value as AssessmentDraft['fitnessLevel'] })} options={fitnessLevels.map((value) => ({ value, label: t(`journey.fitnessLevel.${value}`) }))} required value={draft.fitnessLevel} />
              <ChoiceGroup label={t('assessment.priority')} onChange={(value) => patch({ priority: value })} options={[{ value: 'FatLoss', label: t('assessment.priorities.FatLoss') }, { value: 'MuscleDevelopment', label: t('assessment.priorities.MuscleDevelopment') }, { value: 'Strength', label: t('assessment.priorities.Strength') }, { value: 'OverallFitness', label: t('assessment.priorities.OverallFitness') }]} value={draft.priority} />
              <FormField label={t('assessment.goalDescription')}><AppTextInput multiline onChangeText={(value) => patch({ goalDescription: value })} value={draft.goalDescription} /></FormField>
            </AppCard>
          ) : null}

          {step === 2 ? (
            <AppCard>
              <AppText tone="muted">{t('assessment.healthPrivacy')}</AppText>
              <ChoiceGroup label={t('assessment.hasConditions')} onChange={(value) => patch({ health: { ...draft.health, hasMedicalConditions: value === 'Yes' } })} options={[{ value: 'Yes', label: t('assessment.yes') }, { value: 'No', label: t('assessment.no') }]} value={draft.health.hasMedicalConditions == null ? '' : draft.health.hasMedicalConditions ? 'Yes' : 'No'} />
              {draft.health.hasMedicalConditions ? <FormField label={t('assessment.conditions')}><AppTextInput multiline onChangeText={(value) => patch({ health: { ...draft.health, medicalConditions: value.split(',').map((item) => item.trim()).filter(Boolean) } })} value={draft.health.medicalConditions.join(', ')} /></FormField> : null}
              <ChoiceGroup label={t('assessment.hasInjuries')} onChange={(value) => patch({ health: { ...draft.health, hasInjuries: value === 'Yes' } })} options={[{ value: 'Yes', label: t('assessment.yes') }, { value: 'No', label: t('assessment.no') }]} value={draft.health.hasInjuries == null ? '' : draft.health.hasInjuries ? 'Yes' : 'No'} />
              {draft.health.hasInjuries ? <FormField label={t('assessment.injuryDetails')}><AppTextInput multiline onChangeText={(value) => patch({ health: { ...draft.health, injuryDetails: value } })} value={draft.health.injuryDetails} /></FormField> : null}
              <FormField label={t('assessment.limitations')}><AppTextInput multiline onChangeText={(value) => patch({ health: { ...draft.health, physicalLimitations: value } })} value={draft.health.physicalLimitations} /></FormField>
              <FormField label={t('assessment.allergies')}><AppTextInput multiline onChangeText={(value) => patch({ health: { ...draft.health, foodAllergies: value.split(',').map((item) => item.trim()).filter(Boolean) } })} value={draft.health.foodAllergies.join(', ')} /></FormField>
              <FormField label={t('assessment.intolerances')}><AppTextInput multiline onChangeText={(value) => patch({ health: { ...draft.health, foodIntolerances: value } })} value={draft.health.foodIntolerances} /></FormField>
              <FormField label={t('assessment.medications')}><AppTextInput multiline onChangeText={(value) => patch({ health: { ...draft.health, medications: value } })} value={draft.health.medications} /></FormField>
              <FormField label={t('assessment.doctorRestrictions')}><AppTextInput multiline onChangeText={(value) => patch({ health: { ...draft.health, doctorRestrictions: value } })} value={draft.health.doctorRestrictions} /></FormField>
              <FormField label={t('assessment.healthNotes')}><AppTextInput multiline onChangeText={(value) => patch({ health: { ...draft.health, additionalNotes: value } })} value={draft.health.additionalNotes} /></FormField>
            </AppCard>
          ) : null}

          {step === 3 ? (
            <AppCard>
              <ChoiceGroup label={t('assessment.location')} onChange={(value) => patch({ workout: { ...draft.workout, location: value } })} options={[{ value: 'Gym', label: t('assessment.locations.Gym') }, { value: 'Home', label: t('assessment.locations.Home') }, { value: 'Outdoor', label: t('assessment.locations.Outdoor') }, { value: 'Mixed', label: t('assessment.locations.Mixed') }]} value={draft.workout.location} />
              <FormField label={t('assessment.daysPerWeek')}><AppTextInput keyboardType="number-pad" onChangeText={(value) => patch({ workout: { ...draft.workout, daysPerWeek: value ? Number.parseInt(value, 10) : null } })} value={draft.workout.daysPerWeek?.toString() ?? ''} /></FormField>
              <MultiSelectChips label={t('assessment.preferredDays')} onChange={(values) => patch({ workout: { ...draft.workout, preferredDays: values } })} options={weekDays.map((value) => ({ value, label: t(`assessment.days.${value}`) }))} values={draft.workout.preferredDays} />
              <ChoiceGroup label={t('assessment.duration')} onChange={(value) => patch({ workout: { ...draft.workout, durationMinutes: Number.parseInt(value, 10) } })} options={[20, 30, 45, 60, 90].map((value) => ({ value: String(value), label: t('assessment.minutes', { count: value }) }))} value={draft.workout.durationMinutes?.toString() ?? ''} />
              <ChoiceGroup label={t('assessment.preferredTime')} onChange={(value) => patch({ workout: { ...draft.workout, preferredTime: value } })} options={['Morning', 'Afternoon', 'Evening', 'Flexible'].map((value) => ({ value, label: t(`assessment.times.${value}`) }))} value={draft.workout.preferredTime} />
              <MultiSelectChips label={t('assessment.equipment')} onChange={(values) => patch({ workout: { ...draft.workout, equipment: values } })} options={['FullGym', 'Dumbbells', 'Barbells', 'ResistanceBands', 'Treadmill', 'ExerciseBike', 'PullUpBar', 'BodyweightOnly', 'Other'].map((value) => ({ value, label: t(`assessment.equipmentOptions.${value}`) }))} values={draft.workout.equipment} />
              <MultiSelectChips label={t('assessment.styles')} onChange={(values) => patch({ workout: { ...draft.workout, trainingStyles: values } })} options={['Strength', 'Cardio', 'HIIT', 'Yoga', 'Functional', 'Bodyweight'].map((value) => ({ value, label: t(`assessment.styleOptions.${value}`) }))} values={draft.workout.trainingStyles} />
              <FormField label={t('assessment.avoidExercises')}><AppTextInput multiline onChangeText={(value) => patch({ workout: { ...draft.workout, exercisesToAvoid: value } })} value={draft.workout.exercisesToAvoid} /></FormField>
              <FormField label={t('assessment.cardio')}><AppTextInput onChangeText={(value) => patch({ workout: { ...draft.workout, cardioPreference: value } })} value={draft.workout.cardioPreference} /></FormField>
              <FormField label={t('assessment.restDays')}><AppTextInput onChangeText={(value) => patch({ workout: { ...draft.workout, restDayPreference: value } })} value={draft.workout.restDayPreference} /></FormField>
            </AppCard>
          ) : null}

          {step === 4 ? (
            <AppCard>
              <ChoiceGroup label={t('assessment.dietType')} onChange={(value) => patch({ diet: { ...draft.diet, dietType: value, eggsAllowed: value === 'Vegan' ? false : draft.diet.eggsAllowed, dairyConsumption: value === 'Vegan' ? 'No' : draft.diet.dairyConsumption } })} options={['Vegetarian', 'Vegan', 'Eggetarian', 'NonVegetarian', 'Pescatarian'].map((value) => ({ value, label: t(`assessment.diets.${value}`) }))} value={draft.diet.dietType} />
              <ChoiceGroup label={t('assessment.eggs')} onChange={(value) => patch({ diet: { ...draft.diet, eggsAllowed: value === 'Yes' } })} options={[{ value: 'Yes', label: t('assessment.yes') }, { value: 'No', label: t('assessment.no') }]} value={draft.diet.eggsAllowed == null ? '' : draft.diet.eggsAllowed ? 'Yes' : 'No'} />
              <ChoiceGroup label={t('assessment.dairy')} onChange={(value) => patch({ diet: { ...draft.diet, dairyConsumption: value } })} options={['Yes', 'No', 'Limited'].map((value) => ({ value, label: t(`assessment.dairyOptions.${value}`) }))} value={draft.diet.dairyConsumption} />
              <MultiSelectChips label={t('assessment.cuisines')} onChange={(values) => patch({ diet: { ...draft.diet, preferredCuisines: values } })} options={['Nepali', 'Indian', 'Continental', 'Mediterranean', 'Asian', 'Other'].map((value) => ({ value, label: t(`assessment.cuisineOptions.${value}`) }))} values={draft.diet.preferredCuisines} />
              <FormField label={t('assessment.mealsPerDay')}><AppTextInput keyboardType="number-pad" onChangeText={(value) => patch({ diet: { ...draft.diet, mealsPerDay: value ? Number.parseInt(value, 10) : null } })} value={draft.diet.mealsPerDay?.toString() ?? ''} /></FormField>
              <FormField label={t('assessment.mealTimings')}><AppTextInput onChangeText={(value) => patch({ diet: { ...draft.diet, preferredMealTimings: value } })} value={draft.diet.preferredMealTimings} /></FormField>
              <FormField label={t('assessment.foodsLiked')}><AppTextInput multiline onChangeText={(value) => patch({ diet: { ...draft.diet, foodsLiked: value } })} value={draft.diet.foodsLiked} /></FormField>
              <FormField label={t('assessment.foodsDisliked')}><AppTextInput multiline onChangeText={(value) => patch({ diet: { ...draft.diet, foodsDisliked: value } })} value={draft.diet.foodsDisliked} /></FormField>
              <FormField label={t('assessment.foodsAvoid')}><AppTextInput multiline onChangeText={(value) => patch({ diet: { ...draft.diet, foodsToAvoid: value } })} value={draft.diet.foodsToAvoid} /></FormField>
              <ChoiceGroup label={t('assessment.cooking')} onChange={(value) => patch({ diet: { ...draft.diet, cookingAvailability: value } })} options={['HomeCooking', 'EatingOutside', 'Both'].map((value) => ({ value, label: t(`assessment.cookingOptions.${value}`) }))} value={draft.diet.cookingAvailability} />
              <ChoiceGroup label={t('assessment.budget')} onChange={(value) => patch({ diet: { ...draft.diet, dailyFoodBudget: value } })} options={['Low', 'Medium', 'High', 'Custom'].map((value) => ({ value, label: t(`assessment.budgetOptions.${value}`) }))} value={draft.diet.dailyFoodBudget} />
              <FormField label={t('assessment.supplements')}><AppTextInput multiline onChangeText={(value) => patch({ diet: { ...draft.diet, supplements: value } })} value={draft.diet.supplements} /></FormField>
              <FormField label={t('assessment.proteinSources')}><AppTextInput multiline onChangeText={(value) => patch({ diet: { ...draft.diet, preferredProteinSources: value } })} value={draft.diet.preferredProteinSources} /></FormField>
            </AppCard>
          ) : null}

          {step === 5 ? (
            <AppCard>
              <FormField label={t('assessment.occupation')}><AppTextInput onChangeText={(value) => patch({ lifestyle: { ...draft.lifestyle, occupation: value } })} value={draft.lifestyle.occupation} /></FormField>
              <ChoiceGroup label={t('assessment.workType')} onChange={(value) => patch({ lifestyle: { ...draft.lifestyle, workType: value } })} options={['Sitting', 'Standing', 'PhysicallyActive', 'Mixed'].map((value) => ({ value, label: t(`assessment.workTypes.${value}`) }))} value={draft.lifestyle.workType} />
              <ChoiceGroup label={t('assessment.activityLevel')} onChange={(value) => patch({ lifestyle: { ...draft.lifestyle, activityLevel: value as AssessmentDraft['lifestyle']['activityLevel'] } })} options={activityLevels.map((value) => ({ value, label: t(`journey.activityLevel.${value}`) }))} value={draft.lifestyle.activityLevel} />
              <FormField label={t('assessment.steps')}><AppTextInput keyboardType="number-pad" onChangeText={(value) => patch({ lifestyle: { ...draft.lifestyle, averageDailySteps: value ? Number.parseInt(value, 10) : null } })} value={draft.lifestyle.averageDailySteps?.toString() ?? ''} /></FormField>
              <FormField label={t('assessment.sleepHours')}><AppTextInput keyboardType="decimal-pad" onChangeText={(value) => patch({ lifestyle: { ...draft.lifestyle, averageSleepHours: value ? Number.parseFloat(value) : null } })} value={draft.lifestyle.averageSleepHours?.toString() ?? ''} /></FormField>
              <ChoiceGroup label={t('assessment.sleepQuality')} onChange={(value) => patch({ lifestyle: { ...draft.lifestyle, sleepQuality: value } })} options={['Poor', 'Average', 'Good', 'Excellent'].map((value) => ({ value, label: t(`assessment.sleepOptions.${value}`) }))} value={draft.lifestyle.sleepQuality} />
              <ChoiceGroup label={t('assessment.stress')} onChange={(value) => patch({ lifestyle: { ...draft.lifestyle, stressLevel: value } })} options={['Low', 'Moderate', 'High'].map((value) => ({ value, label: t(`assessment.stressOptions.${value}`) }))} value={draft.lifestyle.stressLevel} />
              <FormField label={t('assessment.water')}><AppTextInput keyboardType="decimal-pad" onChangeText={(value) => patch({ lifestyle: { ...draft.lifestyle, dailyWaterLitres: value ? Number.parseFloat(value) : null } })} value={draft.lifestyle.dailyWaterLitres?.toString() ?? ''} /></FormField>
              <FormField label={t('assessment.schedule')}><AppTextInput onChangeText={(value) => patch({ lifestyle: { ...draft.lifestyle, preferredWorkoutSchedule: value } })} value={draft.lifestyle.preferredWorkoutSchedule} /></FormField>
            </AppCard>
          ) : null}

          {step === 6 ? (
            <AppCard>
              <AppText tone="muted">{t('assessment.measurementsOptional')}</AppText>
              {(['chestCm', 'waistCm', 'hipCm', 'leftArmCm', 'rightArmCm', 'thighCm', 'bodyFatPercentage'] as const).map((key) => (
                <FormField key={key} label={t(`assessment.measure.${key}`)}>
                  <AppTextInput
                    keyboardType="decimal-pad"
                    onChangeText={(value) => patch({ measurements: { ...draft.measurements, [key]: value ? Number.parseFloat(value) : null } })}
                    value={draft.measurements[key]?.toString() ?? ''}
                  />
                </FormField>
              ))}
              <FormField label={t('assessment.fitnessNotes')}><AppTextInput multiline onChangeText={(value) => patch({ measurements: { ...draft.measurements, fitnessNotes: value } })} value={draft.measurements.fitnessNotes} /></FormField>
            </AppCard>
          ) : null}

          {step === 7 ? (
            <AppCard>
              <AppText variant="label">{t('assessment.reviewTitle')}</AppText>
              <AppText>{t('assessment.reviewPersonal', { age: age ?? '—', height: draft.heightCm || '—', weight: draft.currentWeightKg || '—' })}</AppText>
              <AppText>{t('assessment.reviewGoal', { goal: draft.primaryGoal ? t(`assessment.goals.${draft.primaryGoal}`) : '—', level: draft.fitnessLevel || '—' })}</AppText>
              <AppText>{t('assessment.reviewDiet', { diet: draft.diet.dietType ? t(`assessment.diets.${draft.diet.dietType}`) : '—' })}</AppText>
              <AppText>{t('assessment.reviewWorkout', { days: draft.workout.daysPerWeek ?? '—', location: draft.workout.location || '—' })}</AppText>
              <AppText tone="muted">{t('assessment.reviewHint')}</AppText>
            </AppCard>
          ) : null}

          <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: theme.spacing.sm }}>
            {step > 0 ? <AppButton label={t('assessment.previous')} onPress={() => setStep((value) => value - 1)} variant="secondary" /> : null}
            <AppButton label={t('assessment.saveDraft')} loading={save.isPending} onPress={() => persist(false, step + 1)} variant="secondary" />
            {step < 7 ? <AppButton label={t('assessment.continue')} loading={save.isPending} onPress={continueStep} /> : <AppButton label={t('assessment.submit')} loading={save.isPending} onPress={() => persist(true, 8)} />}
          </View>
        </ScrollView>
      </KeyboardAvoidingView>
    </ScreenState>
  );
}
