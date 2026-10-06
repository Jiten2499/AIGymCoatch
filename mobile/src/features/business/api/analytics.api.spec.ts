import { describe, expect, it } from '@jest/globals';
import { analyticsPath, dashboardAnalyticsQueryKey } from './analytics.api';

describe('dashboard analytics client', () => {
  it('keeps the workspace in the query key and sends the chart range', () => {
    expect(dashboardAnalyticsQueryKey('gym', 'memberships', '2026-09-01', '2026-10-01', 'Month')).toEqual([
      'business-dashboard-analytics',
      'gym',
      'memberships',
      '2026-09-01',
      '2026-10-01',
      'Month',
    ]);
    expect(analyticsPath('members', '2026-10-01', '2026-10-07', 'Day')).toBe(
      '/business/dashboard/analytics/members?from=2026-10-01&to=2026-10-07&period=Day',
    );
  });
});
