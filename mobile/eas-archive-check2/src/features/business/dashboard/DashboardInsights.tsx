import { ActivityIndicator, View } from 'react-native';
import { useTranslation } from 'react-i18next';
import { useTheme } from '../../../app/providers/ThemeProvider';
import { AppButton } from '../../../components/ui/AppButton';
import { AppCard } from '../../../components/ui/AppCard';
import { AppText } from '../../../components/ui/AppText';
import type { AttentionItem, BusinessDashboardInsights } from '../api/insights.api';

export type InsightPanelModel = {
  status: 'loading' | 'error' | 'ready';
  report?: BusinessDashboardInsights;
  onRetry?: () => void;
  onRefresh?: () => void;
};

export function toInsightPanel(query: { isError: boolean; isLoading: boolean; isFetching: boolean; data?: BusinessDashboardInsights; refetch: () => Promise<unknown> }, refresh: () => void): InsightPanelModel {
  const status = query.isError ? 'error' : query.isLoading || (query.isFetching && !query.data) ? 'loading' : 'ready';
  return { status, report: query.data, onRetry: () => void query.refetch(), onRefresh: refresh };
}

export function DashboardInsights({ panel }: { panel: InsightPanelModel }) {
  const theme = useTheme();
  return (
    <View style={{ gap: theme.spacing.md }}>
      <AttentionCard panel={panel} />
      <AiCard panel={panel} />
    </View>
  );
}

function AttentionCard({ panel }: { panel: InsightPanelModel }) {
  const theme = useTheme();
  const { t } = useTranslation();
  const items = panel.report?.attention ?? [];
  return (
    <AppCard accessibilityLabel={t('businessDashboard.attention')}>
      <AppText variant="title">{t('businessDashboard.attention')}</AppText>
      {panel.status === 'loading' ? <Loading label={t('businessDashboard.attentionLoading')} /> : null}
      {panel.status === 'error' ? <Failure message={t('businessDashboard.attentionError')} onRetry={panel.onRetry} /> : null}
      {panel.status === 'ready' && items.length === 0 ? <AppText tone="muted" style={{ marginTop: theme.spacing.md }}>{t('businessDashboard.attentionEmpty')}</AppText> : null}
      {panel.status === 'ready' ? items.map((item) => <AttentionRow item={item} key={`${item.type}-${item.title}`} />) : null}
    </AppCard>
  );
}

function AttentionRow({ item }: { item: AttentionItem }) {
  const theme = useTheme();
  const color = item.priority === 'High' ? theme.colors.danger : item.priority === 'Low' ? theme.colors.success : theme.colors.accent;
  return (
    <View style={{ gap: 4, marginTop: theme.spacing.md }}>
      <View style={{ alignItems: 'center', flexDirection: 'row', gap: theme.spacing.sm }}>
        <View style={{ backgroundColor: color, borderRadius: 4, height: 8, width: 8 }} />
        <AppText variant="label">{item.priority} · {item.title}</AppText>
      </View>
      <AppText>{item.description}</AppText>
    </View>
  );
}

function AiCard({ panel }: { panel: InsightPanelModel }) {
  const theme = useTheme();
  const { t } = useTranslation();
  const ai = panel.report?.ai;
  return (
    <AppCard accessibilityLabel={t('businessDashboard.aiInsights')}>
      <AppText variant="title">{t('businessDashboard.aiInsights')}</AppText>
      {panel.status === 'loading' ? <Loading label={t('businessDashboard.aiLoading')} /> : null}
      {panel.status === 'error' || (panel.status === 'ready' && ai?.status === 'Unavailable') ? <Failure message={t('businessDashboard.aiUnavailable')} onRetry={panel.onRetry} /> : null}
      {panel.status === 'ready' && ai?.status === 'Disabled' ? <AppText tone="muted" style={{ marginTop: theme.spacing.md }}>{t('businessDashboard.aiDisabled')}</AppText> : null}
      {panel.status === 'ready' && ai?.status === 'Empty' ? <AppText tone="muted" style={{ marginTop: theme.spacing.md }}>{t('businessDashboard.aiEmpty')}</AppText> : null}
      {panel.status === 'ready' && ai?.status === 'Ready' ? (
        <View style={{ gap: theme.spacing.md, marginTop: theme.spacing.md }}>
          {ai.summary ? <AppText>{ai.summary}</AppText> : null}
          {ai.insights.map((item) => (
            <View key={`${item.type}-${item.title}`}>
              <AppText variant="label">{item.type} · {item.title}</AppText>
              <AppText>{item.message}</AppText>
            </View>
          ))}
          {ai.recommendations.map((item) => (
            <View key={item.title}>
              <AppText variant="label">{t('businessDashboard.recommendation')} · {item.title}</AppText>
              <AppText>{item.description}</AppText>
            </View>
          ))}
          {panel.onRefresh ? <AppButton label={t('businessDashboard.refreshInsights')} onPress={panel.onRefresh} variant="secondary" /> : null}
        </View>
      ) : null}
    </AppCard>
  );
}

function Loading({ label }: { label: string }) {
  const theme = useTheme();
  return (
    <View accessibilityRole="progressbar" style={{ gap: theme.spacing.sm, marginTop: theme.spacing.md }}>
      <ActivityIndicator color={theme.colors.accent} />
      <AppText tone="muted">{label}</AppText>
    </View>
  );
}

function Failure({ message, onRetry }: { message: string; onRetry?: () => void }) {
  const theme = useTheme();
  const { t } = useTranslation();
  return (
    <View style={{ gap: theme.spacing.sm, marginTop: theme.spacing.md }}>
      <AppText tone="muted">{message}</AppText>
      {onRetry ? <AppButton label={t('common.retry')} onPress={onRetry} variant="secondary" /> : null}
    </View>
  );
}
