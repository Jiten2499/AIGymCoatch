import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { errorText } from '../../../services/api/problem';
import { useDashboardAnalytics } from '../api/analytics.api';
import type { BusinessDashboardOverview } from '../api/dashboard.api';
import { analyticsIsEmpty, customRange, mapSeries, presetRange, tenantDate, type AnalyticsPreset } from './analyticsRange';
import type { DashboardChartCardModel } from './DashboardChartCard';

const areas = [
  { id: 'members', capability: 'Members', titleKey: 'businessDashboard.memberGrowth', emptyKey: 'businessDashboard.memberGrowthEmpty' },
  { id: 'attendance', capability: 'Attendance', titleKey: 'businessDashboard.attendanceTrend', emptyKey: 'businessDashboard.attendanceEmpty' },
  { id: 'memberships', capability: 'Memberships', titleKey: 'businessDashboard.membershipTrend', emptyKey: 'businessDashboard.membershipEmpty' },
] as const;

export function useBusinessAnalytics(overview: BusinessDashboardOverview | undefined) {
  const { i18n, t } = useTranslation();
  const [preset, setPreset] = useState<AnalyticsPreset>('last30');
  const [customFrom, setCustomFrom] = useState('');
  const [customTo, setCustomTo] = useState('');
  const today = overview ? tenantDate(overview.tenant.currentDate) : '';
  const range = preset === 'custom' ? customRange(customFrom, customTo) : today ? presetRange(preset, today) : null;
  const members = useDashboardAnalytics('members', range?.from ?? '', range?.to ?? '', range?.period ?? 'Day', enabled(overview, 'Members', range));
  const attendance = useDashboardAnalytics('attendance', range?.from ?? '', range?.to ?? '', range?.period ?? 'Day', enabled(overview, 'Attendance', range));
  const memberships = useDashboardAnalytics('memberships', range?.from ?? '', range?.to ?? '', range?.period ?? 'Day', enabled(overview, 'Memberships', range));
  const queries = { members, attendance, memberships };

  const charts: DashboardChartCardModel[] = areas.flatMap((area): DashboardChartCardModel[] => {
    if (!overview?.capabilities.some((capability) => capability.code === area.capability && capability.enabled)) {
      return [];
    }
    const query = queries[area.id];
    if (!range) {
      return [{ id: area.id, title: t(area.titleKey), status: 'empty' as const, message: t('businessDashboard.rangeInvalid'), series: [] }];
    }
    if (query.isLoading || (query.isFetching && !query.data)) {
      return [{ id: area.id, title: t(area.titleKey), status: 'loading' as const, series: [] }];
    }
    if (query.isError) {
      return [{ id: area.id, title: t(area.titleKey), status: 'error' as const, message: errorText(query.error, t('common.offline')), onRetry: () => void query.refetch(), series: [] }];
    }
    const report = query.data;
    if (!report || analyticsIsEmpty(report)) {
      return [{ id: area.id, title: t(area.titleKey), status: 'empty' as const, message: t(area.emptyKey), series: [] }];
    }
    return [{
      id: area.id,
      title: t(area.titleKey),
      status: 'ready' as const,
      series: report.series.map((series) => mapSeries(series, report.period, i18n.language)),
    }];
  });

  return { preset, setPreset, customFrom, setCustomFrom, customTo, setCustomTo, charts, range };
}

function enabled(overview: BusinessDashboardOverview | undefined, capability: string, range: ReturnType<typeof customRange>): boolean {
  return Boolean(range && overview?.capabilities.some((item) => item.code === capability && item.enabled));
}
