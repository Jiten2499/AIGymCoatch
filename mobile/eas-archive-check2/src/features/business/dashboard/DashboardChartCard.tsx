import { ActivityIndicator, View } from 'react-native';
import { useTranslation } from 'react-i18next';
import { useTheme } from '../../../app/providers/ThemeProvider';
import { AppButton } from '../../../components/ui/AppButton';
import { AppCard } from '../../../components/ui/AppCard';
import { AppText } from '../../../components/ui/AppText';
import { TrendChart } from './TrendChart';
import type { ChartSeries } from './analyticsRange';

export type DashboardChartCardModel = {
  id: string;
  title: string;
  status: 'loading' | 'error' | 'empty' | 'ready';
  message?: string;
  series: ChartSeries[];
  onRetry?: () => void;
};

export function DashboardChartCard({ chart, locale }: { chart: DashboardChartCardModel; locale: string }) {
  const theme = useTheme();
  const { t } = useTranslation();
  return (
    <AppCard accessibilityLabel={chart.title}>
      <AppText variant="title">{chart.title}</AppText>
      {chart.status === 'loading' ? (
        <View accessibilityRole="progressbar" style={{ gap: theme.spacing.sm, marginTop: theme.spacing.md }}>
          <ActivityIndicator color={theme.colors.accent} />
          <View style={{ backgroundColor: theme.colors.surfaceMuted, borderRadius: theme.radius.sm, height: 96 }} />
        </View>
      ) : null}
      {chart.status === 'error' ? (
        <View style={{ gap: theme.spacing.sm, marginTop: theme.spacing.md }}>
          <AppText tone="muted">{chart.message}</AppText>
          {chart.onRetry ? <AppButton label={t('common.retry')} onPress={chart.onRetry} variant="secondary" /> : null}
        </View>
      ) : null}
      {chart.status === 'empty' ? <AppText tone="muted" style={{ marginTop: theme.spacing.md }}>{chart.message}</AppText> : null}
      {chart.status === 'ready' ? (
        <View style={{ marginTop: theme.spacing.md }}>
          <TrendChart locale={locale} series={chart.series} />
        </View>
      ) : null}
    </AppCard>
  );
}
