import type { ReactNode } from 'react';
import { ActivityIndicator, View } from 'react-native';
import { useTranslation } from 'react-i18next';
import { useTheme } from '../../app/providers/ThemeProvider';
import { AppButton } from '../ui/AppButton';
import { AppText } from '../ui/AppText';

type ScreenStateProps = {
  loading?: boolean;
  error?: string | null;
  empty?: boolean;
  emptyTitle?: string;
  emptyBody?: string;
  onRetry?: () => void;
  children?: ReactNode;
};

export function ScreenState({ loading, error, empty, emptyTitle, emptyBody, onRetry, children }: ScreenStateProps) {
  const theme = useTheme();
  const { t } = useTranslation();
  if (loading) {
    return (
      <View accessibilityRole="progressbar" style={{ alignItems: 'center', flex: 1, gap: theme.spacing.md, justifyContent: 'center' }}>
        <ActivityIndicator color={theme.colors.accent} />
        <AppText tone="muted">{t('common.loading')}</AppText>
      </View>
    );
  }
  if (error) {
    return (
      <View style={{ flex: 1, gap: theme.spacing.md, justifyContent: 'center', padding: theme.spacing.xl }}>
        <AppText variant="title">{t('common.errorTitle')}</AppText>
        <AppText tone="muted">{error}</AppText>
        {onRetry ? <AppButton label={t('common.retry')} onPress={onRetry} /> : null}
      </View>
    );
  }
  if (empty) {
    return (
      <View style={{ flex: 1, gap: theme.spacing.md, justifyContent: 'center', padding: theme.spacing.xl }}>
        <AppText variant="title">{emptyTitle ?? t('common.emptyTitle')}</AppText>
        <AppText tone="muted">{emptyBody ?? t('common.emptyBody')}</AppText>
      </View>
    );
  }
  return children;
}
