import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { ScrollView, View } from 'react-native';
import { useTheme } from '../../../app/providers/ThemeProvider';
import { FeatureGate } from '../../../components/access/FeatureGate';
import { FormField } from '../../../components/forms/FormField';
import { ScreenState } from '../../../components/feedback/ScreenState';
import { AppButton } from '../../../components/ui/AppButton';
import { AppCard } from '../../../components/ui/AppCard';
import { AppText } from '../../../components/ui/AppText';
import { AppTextInput } from '../../../components/ui/AppTextInput';
import { featureCodes } from '../../../constants/access';
import { formatDate } from '../../../services/localization/format';
import { errorText } from '../../../services/api/problem';
import { useFitnessProfile, useMeasurements, useRecordMeasurement } from '../../fitness/api/queries';
import type { Measurement } from '../../fitness/types';

export function MeasurementsScreen() {
  const theme = useTheme();
  const { t, i18n } = useTranslation();
  const measurements = useMeasurements();
  const profile = useFitnessProfile();
  const save = useRecordMeasurement();
  const [weight, setWeight] = useState('');
  const [formError, setFormError] = useState<string | null>(null);
  const items = measurements.data?.items ?? [];
  const latest = items[0] ?? null;

  const submit = () => {
    const parsed = Number(weight);
    if (!weight.trim() || !Number.isFinite(parsed) || parsed <= 0 || parsed > 500) {
      setFormError(t('journey.invalidNumber'));
      return;
    }
    setFormError(null);
    const today = new Date().toISOString().slice(0, 10);
    save.mutate({
      unitSystem: profile.data?.unitSystem ?? 'Metric',
      measurementDate: today,
      weight: parsed,
      bodyFatPercentage: null,
      chest: null,
      waist: null,
      hip: null,
      arm: null,
      thigh: null,
      notes: null,
    });
  };

  return (
    <FeatureGate code={featureCodes.workoutPlans}>
      <ScreenState error={measurements.isError ? errorText(measurements.error, t('common.offline')) : null} loading={measurements.isLoading} onRetry={() => void measurements.refetch()}>
        <ScrollView contentContainerStyle={{ gap: theme.spacing.lg, padding: theme.spacing.xl }}>
          <AppText variant="title">{t('journey.measurements')}</AppText>
          {latest ? <MeasurementCard item={latest} locale={i18n.language} title={t('journey.latest')} /> : <AppText tone="muted">{t('journey.measurementsEmpty')}</AppText>}
          <AppCard>
            <FormField label={t('journey.weight')}>
              <AppTextInput accessibilityLabel={t('journey.weight')} keyboardType="decimal-pad" onChangeText={setWeight} value={weight} />
            </FormField>
            {formError ? <AppText accessibilityRole="alert" tone="danger">{formError}</AppText> : null}
            {save.isError ? <AppText accessibilityRole="alert" tone="danger">{errorText(save.error, t('common.offline'))}</AppText> : null}
            <AppButton label={t('journey.saveMeasurement')} loading={save.isPending} onPress={submit} />
          </AppCard>
          <View style={{ gap: theme.spacing.sm }}>
            {items.map((item) => <MeasurementCard item={item} key={item.id} locale={i18n.language} />)}
          </View>
        </ScrollView>
      </ScreenState>
    </FeatureGate>
  );
}

function MeasurementCard({ item, locale, title }: { item: Measurement; locale: string; title?: string }) {
  const { t } = useTranslation();
  const fields = [
    item.weight !== null ? `${t('journey.weight')}: ${item.weight}` : null,
    item.bodyFatPercentage !== null ? `${t('journey.bodyFat')}: ${item.bodyFatPercentage}` : null,
    item.chest !== null ? `${t('journey.chest')}: ${item.chest}` : null,
    item.waist !== null ? `${t('journey.waist')}: ${item.waist}` : null,
    item.hip !== null ? `${t('journey.hip')}: ${item.hip}` : null,
    item.arm !== null ? `${t('journey.arm')}: ${item.arm}` : null,
    item.thigh !== null ? `${t('journey.thigh')}: ${item.thigh}` : null,
  ].filter(Boolean);
  return (
    <AppCard>
      {title ? <AppText variant="label">{title}</AppText> : null}
      <AppText>{formatDate(new Date(`${item.measurementDate}T00:00:00`), locale)}</AppText>
      <AppText tone="muted">{item.unitSystem}</AppText>
      {fields.map((field) => <AppText key={field}>{field}</AppText>)}
      {item.notes ? <AppText tone="muted">{item.notes}</AppText> : null}
    </AppCard>
  );
}
