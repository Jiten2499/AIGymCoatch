import { useQuery } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { Pressable, ScrollView, TextInput, useWindowDimensions, View } from 'react-native';
import { useTheme } from '../../../app/providers/ThemeProvider';
import { ScreenState } from '../../../components/feedback/ScreenState';
import { AppCard } from '../../../components/ui/AppCard';
import { AppText } from '../../../components/ui/AppText';
import { errorText } from '../../../services/api/problem';
import { useSessionStore } from '../../../store/sessionStore';
import { getBusinessDashboard, type BusinessDashboardOverview } from '../api/dashboard.api';
import { DashboardChartCard, type DashboardChartCardModel } from '../dashboard/DashboardChartCard';
import { DashboardInsights, toInsightPanel, type InsightPanelModel } from '../dashboard/DashboardInsights';
import { useBusinessInsights } from '../api/insights.api';
import { useBusinessAnalytics } from '../dashboard/useBusinessAnalytics';
import type { AnalyticsPreset } from '../dashboard/analyticsRange';
import { dashboardProblem, summaryHighlights, visibleWidgets, widgetMetrics, type MetricLine } from '../dashboard/widgets';

export function BusinessDashboardScreen() {
  const tenantId = useSessionStore((state) => state.currentTenant?.id);
  const dashboard = useQuery({
    queryKey: ['business-dashboard', tenantId],
    enabled: Boolean(tenantId),
    queryFn: ({ signal }) => getBusinessDashboard(signal),
  });
  const analytics = useBusinessAnalytics(dashboard.data);
  const insights = useBusinessInsights(analytics.range?.from ?? '', analytics.range?.to ?? '', analytics.range?.period ?? 'Day', Boolean(dashboard.data && analytics.range));

  return (
    <BusinessDashboardView
      charts={analytics.charts}
      insights={dashboard.data && analytics.range ? toInsightPanel(insights.query, insights.refreshInsights) : undefined}
      customFrom={analytics.customFrom}
      customTo={analytics.customTo}
      error={dashboard.isError ? errorText(dashboard.error, '') : null}
      loading={dashboard.isLoading}
      onCustomFrom={analytics.setCustomFrom}
      onCustomTo={analytics.setCustomTo}
      onRange={analytics.setPreset}
      onRetry={() => void dashboard.refetch()}
      overview={dashboard.data}
      preset={analytics.preset}
      problem={dashboardProblem(dashboard.error)}
    />
  );
}

export function BusinessDashboardView({
  loading,
  problem,
  error,
  overview,
  onRetry,
  charts = [],
  insights,
  preset = 'last30',
  onRange,
  customFrom = '',
  customTo = '',
  onCustomFrom,
  onCustomTo,
}: {
  loading?: boolean;
  problem: ReturnType<typeof dashboardProblem>;
  error: string | null;
  overview?: BusinessDashboardOverview;
  onRetry: () => void;
  charts?: DashboardChartCardModel[];
  insights?: InsightPanelModel;
  preset?: AnalyticsPreset;
  onRange?: (preset: AnalyticsPreset) => void;
  customFrom?: string;
  customTo?: string;
  onCustomFrom?: (value: string) => void;
  onCustomTo?: (value: string) => void;
}) {
  const theme = useTheme();
  const { i18n, t } = useTranslation();
  const width = useWindowDimensions().width;
  const columns = width >= 900 ? 3 : width >= 600 ? 2 : 1;

  if (loading) {
    return <ScreenState loading />;
  }

  if (problem === 'suspended') {
    return <ScreenState empty emptyBody={t('businessDashboard.suspendedBody')} emptyTitle={t('businessDashboard.suspendedTitle')} />;
  }

  if (problem === 'unauthorized' || problem === 'unavailable') {
    return <ScreenState empty emptyBody={error || t('common.forbidden')} emptyTitle={t('businessDashboard.unauthorizedTitle')} />;
  }

  if (error) {
    return <ScreenState error={error || t('common.offline')} onRetry={onRetry} />;
  }

  if (!overview) {
    return <ScreenState empty emptyBody={t('businessDashboard.emptyBody')} emptyTitle={t('businessDashboard.emptyTitle')} />;
  }

  const widgets = visibleWidgets(overview.capabilities);
  const wide = width >= 900;

  return (
    <ScrollView contentContainerStyle={{ gap: theme.spacing.lg, padding: theme.spacing.xl }}>
      <DashboardHeader date={overview.tenant.currentDate} name={overview.tenant.name} roles={overview.user.roles} timeZone={overview.tenant.timeZone} user={overview.user.displayName} />
      <RangeSelector customFrom={customFrom} customTo={customTo} onCustomFrom={onCustomFrom} onCustomTo={onCustomTo} onRange={onRange} preset={preset} />
      <SummarySection summary={overview.summary} />
      {insights ? <DashboardInsights panel={insights} /> : null}
      <View style={{ flexDirection: wide ? 'row' : 'column', flexWrap: 'wrap', gap: theme.spacing.md }}>
        {charts.map((chart) => (
          <View key={chart.id} style={{ flexGrow: 1, minWidth: wide ? 280 : '100%' }}>
            <DashboardChartCard chart={chart} locale={i18n.language} />
          </View>
        ))}
      </View>
      <DashboardWidgetArea columns={columns} summary={overview.summary} widgets={widgets} />
      <RecentActivityPlaceholder />
    </ScrollView>
  );
}

function RangeSelector({
  preset,
  onRange,
  customFrom,
  customTo,
  onCustomFrom,
  onCustomTo,
}: {
  preset: AnalyticsPreset;
  onRange?: (preset: AnalyticsPreset) => void;
  customFrom: string;
  customTo: string;
  onCustomFrom?: (value: string) => void;
  onCustomTo?: (value: string) => void;
}) {
  const theme = useTheme();
  const { t } = useTranslation();
  const options: { id: AnalyticsPreset; label: string }[] = [
    { id: 'last7', label: t('businessDashboard.last7') },
    { id: 'last30', label: t('businessDashboard.last30') },
    { id: 'last90', label: t('businessDashboard.last90') },
    { id: 'thisMonth', label: t('businessDashboard.thisMonth') },
    { id: 'custom', label: t('businessDashboard.customRange') },
  ];
  return (
    <View style={{ gap: theme.spacing.sm }}>
      <ScrollView horizontal showsHorizontalScrollIndicator={false}>
        <View style={{ flexDirection: 'row', gap: theme.spacing.sm }}>
          {options.map((option) => {
            const selected = option.id === preset;
            return (
              <Pressable
                accessibilityRole="button"
                accessibilityState={{ selected }}
                key={option.id}
                onPress={() => onRange?.(option.id)}
                style={{
                  backgroundColor: selected ? theme.colors.accent : theme.colors.surface,
                  borderColor: theme.colors.border,
                  borderRadius: theme.radius.pill,
                  borderWidth: 1,
                  justifyContent: 'center',
                  minHeight: theme.control.minTouch,
                  paddingHorizontal: theme.spacing.md,
                }}
              >
                <AppText variant="label" style={{ color: selected ? theme.colors.onAccent : theme.colors.text }}>{option.label}</AppText>
              </Pressable>
            );
          })}
        </View>
      </ScrollView>
      {preset === 'custom' ? (
        <View style={{ flexDirection: 'row', gap: theme.spacing.sm }}>
          <TextInput accessibilityLabel={t('businessDashboard.from')} onChangeText={onCustomFrom} placeholder="YYYY-MM-DD" placeholderTextColor={theme.colors.muted} style={{ borderColor: theme.colors.border, borderRadius: theme.radius.md, borderWidth: 1, color: theme.colors.text, flex: 1, minHeight: theme.control.minTouch, paddingHorizontal: theme.spacing.sm }} value={customFrom} />
          <TextInput accessibilityLabel={t('businessDashboard.to')} onChangeText={onCustomTo} placeholder="YYYY-MM-DD" placeholderTextColor={theme.colors.muted} style={{ borderColor: theme.colors.border, borderRadius: theme.radius.md, borderWidth: 1, color: theme.colors.text, flex: 1, minHeight: theme.control.minTouch, paddingHorizontal: theme.spacing.sm }} value={customTo} />
        </View>
      ) : null}
    </View>
  );
}

function DashboardHeader({ name, user, roles, date, timeZone }: { name: string; user: string; roles: string[]; date: string; timeZone: string }) {
  const { t } = useTranslation();
  return (
    <View style={{ gap: 4 }}>
      <AppText variant="display">{name}</AppText>
      <AppText tone="muted">{user} · {roles.join(', ') || t('businessDashboard.member')}</AppText>
      <AppText tone="muted">{date} · {timeZone}</AppText>
    </View>
  );
}

function SummarySection({ summary }: { summary: BusinessDashboardOverview['summary'] }) {
  const theme = useTheme();
  const { t } = useTranslation();
  const highlights = summaryHighlights(summary);
  return (
    <AppCard>
      <AppText variant="title">{t('businessDashboard.summary')}</AppText>
      {highlights.length === 0 ? <AppText tone="muted" style={{ marginTop: theme.spacing.xs }}>{t('businessDashboard.summaryBody')}</AppText> : <MetricLines lines={highlights} />}
    </AppCard>
  );
}

function DashboardWidgetArea({ columns, summary, widgets }: { columns: number; summary: BusinessDashboardOverview['summary']; widgets: ReturnType<typeof visibleWidgets> }) {
  const theme = useTheme();
  const { t } = useTranslation();
  const width = columns === 1 ? '100%' : columns === 2 ? '48%' : '31%';
  return (
    <View style={{ gap: theme.spacing.md }}>
      <AppText variant="title">{t('businessDashboard.areas')}</AppText>
      {widgets.length === 0 ? <AppText tone="muted">{t('businessDashboard.emptyBody')}</AppText> : null}
      <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: theme.spacing.md }}>
        {widgets.map((widget) => (
          <AppCard key={widget.code} style={{ width }}>
            <AppText variant="label">{t(widget.titleKey)}</AppText>
            <WidgetMetrics bodyKey={widget.bodyKey} code={widget.code} summary={summary} />
          </AppCard>
        ))}
      </View>
    </View>
  );
}

function WidgetMetrics({ code, summary, bodyKey }: { code: string; summary: BusinessDashboardOverview['summary']; bodyKey: string }) {
  const { t } = useTranslation();
  const metrics = widgetMetrics(code, summary);
  if (metrics === 'placeholder') {
    return <AppText tone="muted">{t(bodyKey)}</AppText>;
  }

  if (metrics === 'unavailable') {
    return <AppText tone="muted">{t('businessDashboard.unavailable')}</AppText>;
  }

  return <MetricLines lines={metrics} />;
}

function MetricLines({ lines }: { lines: MetricLine[] }) {
  const { t } = useTranslation();
  return (
    <View style={{ gap: 4 }}>
      {lines.map((line) => (
        <AppText key={line.labelKey}>{t(line.labelKey)} {line.value}</AppText>
      ))}
    </View>
  );
}

function RecentActivityPlaceholder() {
  const theme = useTheme();
  const { t } = useTranslation();
  return (
    <AppCard>
      <AppText variant="title">{t('businessDashboard.activity')}</AppText>
      <AppText tone="muted" style={{ marginTop: theme.spacing.xs }}>{t('businessDashboard.activityBody')}</AppText>
    </AppCard>
  );
}
