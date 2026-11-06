import { zodResolver } from '@hookform/resolvers/zod';
import { useEffect, useMemo } from 'react';
import { Controller, useForm } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { ScrollView } from 'react-native';
import { z } from 'zod';
import { useTheme } from '../../../app/providers/ThemeProvider';
import { FeatureGate } from '../../../components/access/FeatureGate';
import { FormField } from '../../../components/forms/FormField';
import { ScreenState } from '../../../components/feedback/ScreenState';
import { AppButton } from '../../../components/ui/AppButton';
import { AppText } from '../../../components/ui/AppText';
import { AppTextInput } from '../../../components/ui/AppTextInput';
import { featureCodes } from '../../../constants/access';
import { errorText, isNotFound } from '../../../services/api/problem';
import { ChoiceList } from '../components/ChoiceList';
import { useNutritionProfile, useSaveNutritionProfile } from '../api/queries';
import { dietaryPreferences, type DietaryPreference } from '../types';

type ProfileValues = {
  dailyCalorieTarget: string;
  proteinTarget: string;
  carbTarget: string;
  fatTarget: string;
  mealFrequency: string;
  dietaryPreference: string;
};

const emptyValues: ProfileValues = {
  dailyCalorieTarget: '',
  proteinTarget: '',
  carbTarget: '',
  fatTarget: '',
  mealFrequency: '',
  dietaryPreference: '',
};

function inRange(value: string, min: number, max: number | null): boolean {
  const parsed = Number(value);
  return value.trim() !== '' && Number.isFinite(parsed) && parsed >= min && (max === null || parsed <= max);
}

export function NutritionProfileScreen() {
  const theme = useTheme();
  const { t } = useTranslation();
  const profile = useNutritionProfile();
  const save = useSaveNutritionProfile();
  const schema = useMemo(() => z.object({
    dailyCalorieTarget: z.string().refine((value) => inRange(value, 800, 6000), t('nutrition.rangeCalories')),
    proteinTarget: z.string().refine((value) => inRange(value, 0, null), t('journey.invalidNumber')),
    carbTarget: z.string().refine((value) => inRange(value, 0, null), t('journey.invalidNumber')),
    fatTarget: z.string().refine((value) => inRange(value, 0, null), t('journey.invalidNumber')),
    mealFrequency: z.string().refine((value) => inRange(value, 1, 8) && Number.isInteger(Number(value)), t('nutrition.mealsRange')),
    dietaryPreference: z.string().refine((value): value is DietaryPreference => dietaryPreferences.includes(value as DietaryPreference), t('nutrition.preferenceRequired')),
  }), [t]);
  const form = useForm<ProfileValues>({ resolver: zodResolver(schema), defaultValues: emptyValues });
  const missing = profile.isError && isNotFound(profile.error);

  useEffect(() => {
    if (!profile.data) {
      return;
    }
    form.reset({
      dailyCalorieTarget: String(profile.data.dailyCalorieTarget),
      proteinTarget: String(profile.data.proteinTarget),
      carbTarget: String(profile.data.carbTarget),
      fatTarget: String(profile.data.fatTarget),
      mealFrequency: String(profile.data.mealFrequency),
      dietaryPreference: profile.data.dietaryPreference,
    });
  }, [form, profile.data]);

  const submit = form.handleSubmit((values) => {
    save.mutate({
      dailyCalorieTarget: Number(values.dailyCalorieTarget),
      proteinTarget: Number(values.proteinTarget),
      carbTarget: Number(values.carbTarget),
      fatTarget: Number(values.fatTarget),
      mealFrequency: Number(values.mealFrequency),
      dietaryPreference: values.dietaryPreference as DietaryPreference,
    });
  });

  return (
    <FeatureGate code={featureCodes.nutrition}>
      <ScreenState error={profile.isError && !missing ? errorText(profile.error, t('common.offline')) : null} loading={profile.isLoading} onRetry={() => void profile.refetch()}>
        <ScrollView contentContainerStyle={{ gap: theme.spacing.lg, padding: theme.spacing.xl }}>
          <AppText variant="title">{t('nutrition.profile')}</AppText>
          <NumberField form={form} label={t('nutrition.calorieTarget')} name="dailyCalorieTarget" />
          <NumberField form={form} label={t('nutrition.proteinTarget')} name="proteinTarget" />
          <NumberField form={form} label={t('nutrition.carbTarget')} name="carbTarget" />
          <NumberField form={form} label={t('nutrition.fatTarget')} name="fatTarget" />
          <NumberField form={form} label={t('nutrition.mealsPerDay')} name="mealFrequency" />
          <FormField error={form.formState.errors.dietaryPreference?.message} label={t('nutrition.preference')} required>
            <ChoiceList
              onChange={(value) => form.setValue('dietaryPreference', value, { shouldValidate: true })}
              options={dietaryPreferences.map((value) => ({ value, label: t(`nutrition.preferenceValue.${value}`) }))}
              value={form.watch('dietaryPreference')}
            />
          </FormField>
          {save.isError ? <AppText accessibilityRole="alert" tone="danger">{errorText(save.error, t('common.offline'))}</AppText> : null}
          {save.isSuccess ? <AppText tone="accent">{t('nutrition.saved')}</AppText> : null}
          <AppButton label={t('nutrition.saveProfile')} loading={save.isPending} onPress={() => { void submit(); }} />
        </ScrollView>
      </ScreenState>
    </FeatureGate>
  );
}

function NumberField({ form, name, label }: { form: ReturnType<typeof useForm<ProfileValues>>; name: Exclude<keyof ProfileValues, 'dietaryPreference'>; label: string }) {
  return (
    <Controller control={form.control} name={name} render={({ field, fieldState }) => (
      <FormField error={fieldState.error?.message} label={label} required>
        <AppTextInput accessibilityLabel={label} keyboardType="decimal-pad" onChangeText={field.onChange} value={field.value} />
      </FormField>
    )} />
  );
}
