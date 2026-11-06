import { zodResolver } from '@hookform/resolvers/zod';
import { useMemo } from 'react';
import { Controller, useForm } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { ScrollView } from 'react-native';
import { z } from 'zod';
import { useTheme } from '../../../app/providers/ThemeProvider';
import { FeatureGate } from '../../../components/access/FeatureGate';
import { FormField } from '../../../components/forms/FormField';
import { AppButton } from '../../../components/ui/AppButton';
import { AppText } from '../../../components/ui/AppText';
import { AppTextInput } from '../../../components/ui/AppTextInput';
import { featureCodes } from '../../../constants/access';
import { errorText } from '../../../services/api/problem';
import { ChoiceList } from '../components/ChoiceList';
import { useCreateNutritionLog } from '../api/queries';
import { isCalendarDate, localDate } from '../dates';
import { mealTypes, type MealType } from '../types';

type LogValues = {
  logDate: string;
  mealType: string;
  foodName: string;
  quantity: string;
  unit: string;
  calories: string;
  protein: string;
  carbohydrates: string;
  fat: string;
};

function nonNegative(value: string): boolean {
  const parsed = Number(value);
  return value.trim() !== '' && Number.isFinite(parsed) && parsed >= 0;
}

export function LogMealScreen() {
  const theme = useTheme();
  const { t } = useTranslation();
  const save = useCreateNutritionLog();
  const schema = useMemo(() => z.object({
    logDate: z.string().refine(isCalendarDate, t('nutrition.dateInvalid')),
    mealType: z.string().refine((value): value is MealType => mealTypes.includes(value as MealType), t('journey.required')),
    foodName: z.string().trim().min(1, t('nutrition.nameRequired')).max(200, t('nutrition.nameRequired')),
    quantity: z.string().refine((value) => nonNegative(value) && Number(value) > 0, t('nutrition.quantityRange')),
    unit: z.string().trim().min(1, t('nutrition.unitRequired')).max(32, t('nutrition.unitRequired')),
    calories: z.string().refine(nonNegative, t('journey.invalidNumber')),
    protein: z.string().refine(nonNegative, t('journey.invalidNumber')),
    carbohydrates: z.string().refine(nonNegative, t('journey.invalidNumber')),
    fat: z.string().refine(nonNegative, t('journey.invalidNumber')),
  }), [t]);
  const form = useForm<LogValues>({
    resolver: zodResolver(schema),
    defaultValues: { logDate: localDate(), mealType: '', foodName: '', quantity: '', unit: '', calories: '', protein: '', carbohydrates: '', fat: '' },
  });
  const submit = form.handleSubmit((values) => {
    save.mutate({
      logDate: values.logDate,
      mealType: values.mealType as MealType,
      foodName: values.foodName.trim(),
      quantity: Number(values.quantity),
      unit: values.unit.trim(),
      calories: Number(values.calories),
      protein: Number(values.protein),
      carbohydrates: Number(values.carbohydrates),
      fat: Number(values.fat),
    });
  });

  return (
    <FeatureGate code={featureCodes.nutrition}>
      <ScrollView contentContainerStyle={{ gap: theme.spacing.lg, padding: theme.spacing.xl }} keyboardShouldPersistTaps="handled">
        <AppText variant="title">{t('nutrition.logMeal')}</AppText>
        <TextField form={form} label={t('nutrition.date')} name="logDate" />
        <FormField error={form.formState.errors.mealType?.message} label={t('nutrition.mealType')} required>
          <ChoiceList
            onChange={(value) => form.setValue('mealType', value, { shouldValidate: true })}
            options={mealTypes.map((value) => ({ value, label: t(`nutrition.mealTypeValue.${value}`) }))}
            value={form.watch('mealType')}
          />
        </FormField>
        <TextField form={form} label={t('nutrition.foodName')} name="foodName" />
        <TextField form={form} keyboard="decimal-pad" label={t('nutrition.quantity')} name="quantity" />
        <TextField form={form} label={t('nutrition.unit')} name="unit" />
        <TextField form={form} keyboard="decimal-pad" label={t('nutrition.calories')} name="calories" />
        <TextField form={form} keyboard="decimal-pad" label={t('nutrition.protein')} name="protein" />
        <TextField form={form} keyboard="decimal-pad" label={t('nutrition.carbohydrates')} name="carbohydrates" />
        <TextField form={form} keyboard="decimal-pad" label={t('nutrition.fat')} name="fat" />
        {save.isError ? <AppText accessibilityRole="alert" tone="danger">{errorText(save.error, t('common.offline'))}</AppText> : null}
        {save.isSuccess ? <AppText tone="accent">{t('nutrition.saved')}</AppText> : null}
        <AppButton label={t('nutrition.saveMeal')} loading={save.isPending} onPress={() => { void submit(); }} />
      </ScrollView>
    </FeatureGate>
  );
}

function TextField({ form, name, label, keyboard = 'default' }: { form: ReturnType<typeof useForm<LogValues>>; name: Exclude<keyof LogValues, 'mealType'>; label: string; keyboard?: 'default' | 'decimal-pad' }) {
  return (
    <Controller control={form.control} name={name} render={({ field, fieldState }) => (
      <FormField error={fieldState.error?.message} label={label} required>
        <AppTextInput accessibilityLabel={label} keyboardType={keyboard} onChangeText={field.onChange} value={field.value} />
      </FormField>
    )} />
  );
}
