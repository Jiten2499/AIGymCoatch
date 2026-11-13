import { useNavigation } from '@react-navigation/native';
import type { NativeStackNavigationProp } from '@react-navigation/native-stack';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Pressable, ScrollView } from 'react-native';
import type { AppStackParamList } from '../../../app/navigation/types';
import { useTheme } from '../../../app/providers/ThemeProvider';
import { FeatureGate } from '../../../components/access/FeatureGate';
import { ScreenState } from '../../../components/feedback/ScreenState';
import { AppButton } from '../../../components/ui/AppButton';
import { AppCard } from '../../../components/ui/AppCard';
import { AppText } from '../../../components/ui/AppText';
import { AppTextInput } from '../../../components/ui/AppTextInput';
import { featureCodes } from '../../../constants/access';
import { errorText } from '../../../services/api/problem';
import type { MembershipPackage } from '../api/memberships.api';
import { usePackageStatus, usePackages } from '../api/queries';

export function PackagesScreen() {
  return (
    <FeatureGate code={featureCodes.membershipManagement}>
      <PackagesList enabled />
    </FeatureGate>
  );
}

export function PackagesList({ enabled }: { enabled: boolean }) {
  const theme = useTheme();
  const { t } = useTranslation();
  const navigation = useNavigation<NativeStackNavigationProp<AppStackParamList>>();
  const [term, setTerm] = useState('');
  const [submitted, setSubmitted] = useState('');
  const [activeOnly, setActiveOnly] = useState(false);
  const [page, setPage] = useState(1);
  const packages = usePackages(submitted, activeOnly, page, enabled);
  const status = usePackageStatus();
  const items = packages.data?.items ?? [];
  const unauthorized = packages.isError && errorText(packages.error, '').toLowerCase().includes('permission');

  return (
    <ScreenState
      empty={packages.isFetched && items.length === 0}
      emptyBody={t('memberships.empty')}
      emptyTitle={t('memberships.title')}
      error={packages.isError ? (unauthorized ? t('memberships.unauthorized') : errorText(packages.error, t('common.offline'))) : null}
      loading={packages.isLoading}
      onRetry={() => void packages.refetch()}
    >
      <ScrollView contentContainerStyle={{ gap: theme.spacing.md, padding: theme.spacing.xl }}>
        <AppText variant="title">{t('memberships.title')}</AppText>
        <AppTextInput accessibilityLabel={t('memberships.search')} onChangeText={(value) => { setTerm(value); setPage(1); setSubmitted(value.trim()); }} placeholder={t('memberships.search')} value={term} />
        <Pressable accessibilityRole="button" onPress={() => { setPage(1); setActiveOnly((current) => !current); }}>
          <AppText tone={activeOnly ? 'accent' : 'default'}>{t('memberships.activeOnly')}</AppText>
        </Pressable>
        {items.map((item) => (
          <PackageRow key={item.id} item={item} onEdit={() => navigation.navigate('PackageForm', { packageId: item.id })} onToggle={() => void status.mutate({ id: item.id, active: !item.isActive })} />
        ))}
        {packages.data && page * packages.data.pageSize < packages.data.totalCount ? <AppButton label={t('memberships.loadMore')} onPress={() => setPage((current) => current + 1)} variant="secondary" /> : null}
        <AppButton label={t('memberships.add')} onPress={() => navigation.navigate('PackageForm')} />
      </ScrollView>
    </ScreenState>
  );
}

function PackageRow({ item, onEdit, onToggle }: { item: MembershipPackage; onEdit: () => void; onToggle: () => void }) {
  const { t } = useTranslation();
  return (
    <AppCard>
      <Pressable accessibilityRole="button" onPress={onEdit}>
        <AppText variant="label">{item.name}</AppText>
        <AppText tone="muted">{item.category} · {item.durationCount} {item.durationUnit} · {item.price} {item.currency}</AppText>
        <AppText tone="muted">{item.isActive ? t('members.active') : t('members.inactive')}</AppText>
      </Pressable>
      <AppButton label={item.isActive ? t('memberships.deactivate') : t('memberships.activate')} onPress={onToggle} variant="secondary" />
    </AppCard>
  );
}
