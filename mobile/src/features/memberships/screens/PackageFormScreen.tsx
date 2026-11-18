import type { NativeStackScreenProps } from '@react-navigation/native-stack';
import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { ScrollView } from 'react-native';
import type { AppStackParamList } from '../../../app/navigation/types';
import { useTheme } from '../../../app/providers/ThemeProvider';
import { FeatureGate } from '../../../components/access/FeatureGate';
import { ScreenState } from '../../../components/feedback/ScreenState';
import { AppButton } from '../../../components/ui/AppButton';
import { AppText } from '../../../components/ui/AppText';
import { AppTextInput } from '../../../components/ui/AppTextInput';
import { featureCodes } from '../../../constants/access';
import { apiClient } from '../../../services/api/client';
import { errorText } from '../../../services/api/problem';
import { usePackage, useSavePackage } from '../api/queries';

type Props = NativeStackScreenProps<AppStackParamList, 'PackageForm'>;

export function PackageFormScreen({ navigation, route }: Props) {
  return (
    <FeatureGate code={featureCodes.membershipManagement}>
      <PackageForm packageId={route.params?.packageId} onSaved={() => navigation.replace('Memberships')} />
    </FeatureGate>
  );
}

export function PackageForm({ packageId, onSaved }: { packageId?: string; onSaved: () => void }) {
  const theme = useTheme();
  const { t } = useTranslation();
  const existing = usePackage(packageId ?? '');
  const save = useSavePackage(packageId);
  const [name, setName] = useState('');
  const [price, setPrice] = useState('');
  const [count, setCount] = useState('1');
  const [unit, setUnit] = useState('Month');
  const [category, setCategory] = useState('Standard');
  const [currency, setCurrency] = useState('USD');
  useEffect(() => {
    let active = true;
    void loadWorkspaceCurrency().then((code) => {
      if (active) {
        setCurrency(code);
      }
    });
    return () => {
      active = false;
    };
  }, []);
  useEffect(() => {
    if (!existing.data) {
      return;
    }
    setName(existing.data.name);
    setPrice(String(existing.data.price));
    setCount(String(existing.data.durationCount || existing.data.durationDays));
    setUnit(existing.data.durationUnit);
    setCategory(existing.data.category);
  }, [existing.data]);

  return (
    <ScreenState error={existing.isError ? errorText(existing.error, t('common.offline')) : null} loading={Boolean(packageId) && existing.isLoading} onRetry={() => void existing.refetch()}>
      <ScrollView contentContainerStyle={{ gap: theme.spacing.md, padding: theme.spacing.xl }}>
        <AppText variant="title">{packageId ? t('memberships.edit') : t('memberships.add')}</AppText>
        <Field label={t('memberships.name')} onChangeText={setName} value={name} />
        <Field keyboardType="phone-pad" label={`${t('memberships.price')} (${currency})`} onChangeText={setPrice} value={price} />
        <Field keyboardType="phone-pad" label={t('memberships.duration')} onChangeText={setCount} value={count} />
        <Field label={unit === 'Month' ? t('memberships.months') : t('memberships.days')} onChangeText={setUnit} value={unit} />
        <Field label={t('memberships.category')} onChangeText={setCategory} value={category} />
        {save.isError ? <AppText tone="danger">{errorText(save.error, t('common.offline'))}</AppText> : null}
        <AppButton
          label={t('memberships.save')}
          loading={save.isPending}
          onPress={() => void save.mutateAsync({
            name,
            durationDays: Number(count) || 1,
            membershipType: unit === 'Month' ? 'Monthly' : 'Custom',
            price: Number(price) || 0,
            currency,
            category,
            durationUnit: unit,
            durationCount: Number(count) || 1,
          }).then(onSaved)}
        />
      </ScrollView>
    </ScreenState>
  );
}

async function loadWorkspaceCurrency() {
  try {
    const configuration = await apiClient.get<{ currency?: string }>('/configuration');
    if (configuration.data.currency) {
      return configuration.data.currency;
    }
  } catch {
    // The business profile is the same currency when configuration is not readable.
  }
  try {
    const profile = await apiClient.get<{ currency?: string }>('/business/profile');
    if (profile.data.currency) {
      return profile.data.currency;
    }
  } catch {
    // Saving still stores the currency configured for the business.
  }
  return 'USD';
}

function Field(props: { label: string; value: string; onChangeText: (value: string) => void; keyboardType?: 'default' | 'phone-pad' }) {
  return (
    <>
      <AppText variant="label">{props.label}</AppText>
      <AppTextInput accessibilityLabel={props.label} keyboardType={props.keyboardType} onChangeText={props.onChangeText} value={props.value} />
    </>
  );
}
