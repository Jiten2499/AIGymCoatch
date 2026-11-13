import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { ScrollView } from 'react-native';
import { useTheme } from '../../../app/providers/ThemeProvider';
import { ScreenState } from '../../../components/feedback/ScreenState';
import { FormField } from '../../../components/forms/FormField';
import { AppButton } from '../../../components/ui/AppButton';
import { AppCard } from '../../../components/ui/AppCard';
import { AppText } from '../../../components/ui/AppText';
import { AppTextInput } from '../../../components/ui/AppTextInput';
import { errorText } from '../../../services/api/problem';
import { useSessionStore } from '../../../store/sessionStore';
import { assignLicense, getLicensePool, listAssignments, revokeAssignment } from '../api/licenses.api';

export function LicenseScreen() {
  const theme = useTheme();
  const { t } = useTranslation();
  const tenantId = useSessionStore((state) => state.currentTenant?.id ?? '');
  const client = useQueryClient();
  const [userId, setUserId] = useState('');
  const pool = useQuery({ queryKey: ['tenant', tenantId, 'licensePool'], queryFn: ({ signal }) => getLicensePool(signal), enabled: Boolean(tenantId) });
  const assignments = useQuery({ queryKey: ['tenant', tenantId, 'licenseAssignments'], queryFn: ({ signal }) => listAssignments(signal), enabled: Boolean(tenantId) });
  const assign = useMutation({
    mutationFn: assignLicense,
    onSuccess: async () => {
      setUserId('');
      await client.invalidateQueries({ queryKey: ['tenant', tenantId, 'licensePool'] });
      await client.invalidateQueries({ queryKey: ['tenant', tenantId, 'licenseAssignments'] });
    },
  });
  const revoke = useMutation({
    mutationFn: revokeAssignment,
    onSuccess: async () => {
      await client.invalidateQueries({ queryKey: ['tenant', tenantId, 'licensePool'] });
      await client.invalidateQueries({ queryKey: ['tenant', tenantId, 'licenseAssignments'] });
    },
  });
  const error = pool.isError ? errorText(pool.error, t('common.offline')) : null;

  return (
    <ScreenState error={error} loading={pool.isLoading} onRetry={() => void pool.refetch()}>
      <ScrollView contentContainerStyle={{ gap: theme.spacing.lg, padding: theme.spacing.xl }}>
        <AppText variant="title">{t('licensing.title')}</AppText>
        {pool.data ? (
          <AppCard>
            <AppText variant="label">{pool.data.planCode}</AppText>
            <AppText tone="muted">{t('licensing.expires')}: {pool.data.expiresAt ? pool.data.expiresAt.slice(0, 10) : t('licensing.noExpiry')}</AppText>
            <AppText>{t('licensing.total')}: {pool.data.baseCapacity + pool.data.extraCapacity}</AppText>
            <AppText>{t('licensing.assigned')}: {pool.data.assigned}</AppText>
            <AppText>{t('licensing.available')}: {pool.data.available}</AppText>
          </AppCard>
        ) : null}
        <FormField label={t('licensing.member')}>
          <AppTextInput accessibilityLabel={t('licensing.member')} autoCapitalize="none" onChangeText={setUserId} value={userId} />
        </FormField>
        {assign.isError ? <AppText accessibilityRole="alert" tone="danger">{errorText(assign.error, t('common.offline'))}</AppText> : null}
        <AppButton label={t('licensing.assign')} loading={assign.isPending} onPress={() => { if (userId.trim()) assign.mutate(userId.trim()); }} />
        {(assignments.data ?? []).map((item) => (
          <AppCard key={item.id}>
            <AppText>{item.userId}</AppText>
            <AppText tone="muted">{item.status}</AppText>
            {item.status === 'Active' ? <AppButton label={t('licensing.revoke')} onPress={() => revoke.mutate(item.id)} variant="secondary" /> : null}
          </AppCard>
        ))}
      </ScrollView>
    </ScreenState>
  );
}
