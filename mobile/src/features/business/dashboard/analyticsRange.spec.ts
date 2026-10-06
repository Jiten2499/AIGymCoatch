import { describe, expect, it } from '@jest/globals';
import { analyticsIsEmpty, customRange, formatAnalyticsLabel, inclusiveDays, mapSeries, presetRange, tenantDate } from './analyticsRange';
import type { AnalyticsReport } from '../api/analytics.api';

describe('dashboard analytics range', () => {
  it('uses the tenant calendar date and the last 30 days by default', () => {
    expect(tenantDate('2026-10-05T08:30:00')).toBe('2026-10-05');
    expect(presetRange('last30', '2026-10-05')).toEqual({ from: '2026-09-06', to: '2026-10-05', period: 'Day' });
    expect(presetRange('last7', '2026-10-05').period).toBe('Day');
    expect(presetRange('last90', '2026-10-05')).toEqual({ from: '2026-07-08', to: '2026-10-05', period: 'Week' });
    expect(presetRange('thisMonth', '2026-10-05')).toEqual({ from: '2026-10-01', to: '2026-10-05', period: 'Day' });
  });

  it('rejects a range the backend will not accept', () => {
    expect(inclusiveDays('2026-01-01', '2026-04-02')).toBe(92);
    expect(customRange('2026-01-01', '2030-01-01')).toBeNull();
    expect(customRange('2026-10-05', '2026-10-01')).toBeNull();
    expect(customRange('2026-08-01', '2026-10-05')?.period).toBe('Week');
  });

  it('keeps zero points and formats weekly labels', () => {
    const report: AnalyticsReport = {
      from: '2026-10-01',
      to: '2026-10-03',
      period: 'Day',
      series: [{ key: 'newMembers', label: 'New members', points: [
        { periodStart: '2026-10-01', periodEnd: '2026-10-01', label: '2026-10-01', value: 4 },
        { periodStart: '2026-10-02', periodEnd: '2026-10-02', label: '2026-10-02', value: 0 },
        { periodStart: '2026-10-03', periodEnd: '2026-10-03', label: '2026-10-03', value: 7 },
      ] }],
    };
    const mapped = mapSeries(report.series[0], 'Day', 'en');
    expect(mapped.points.map((point) => point.value)).toEqual([4, 0, 7]);
    expect(formatAnalyticsLabel('2026-10-05', '2026-10-11', 'Week', 'en')).toContain('Oct');
    expect(analyticsIsEmpty({ ...report, series: [{ ...report.series[0], points: report.series[0].points.map((point) => ({ ...point, value: 0 })) }] })).toBe(true);
  });
});
