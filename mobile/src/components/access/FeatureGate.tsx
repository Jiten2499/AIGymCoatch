import type { ReactNode } from 'react';
import { View } from 'react-native';
import { useTranslation } from 'react-i18next';
import { useTheme } from '../../app/providers/ThemeProvider';
import { useWorkspaceFeatures } from '../../features/licensing/api/featureQuery';
import { errorText } from '../../services/api/problem';
import { ScreenState } from '../feedback/ScreenState';
import { AppText } from '../ui/AppText';

export function FeatureGate({ code, children }: { code: string; children: ReactNode }) {
  const theme = useTheme();
  const { t } = useTranslation();
  const features = useWorkspaceFeatures();
  if (features.isLoading) {
    return <ScreenState loading />;
  }
  if (features.isError) {
    return <ScreenState error={errorText(features.error, t('common.offline'))} onRetry={() => void features.refetch()} />;
  }
  if (features.data?.[code] !== true) {
    return (
      <View style={{ flex: 1, justifyContent: 'center', padding: theme.spacing.xl }}>
        <AppText>{t('journey.featureDisabled')}</AppText>
      </View>
    );
  }
  return children;
}
