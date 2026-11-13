import { toApiError } from '../../../services/api/problem';
import type { DashboardCapability, DashboardSummary } from '../api/dashboard.api';

export type DashboardWidget = {
  code: string;
  titleKey: string;
  bodyKey: string;
};

export const dashboardWidgets: DashboardWidget[] = [
  { code: 'Members', titleKey: 'businessDashboard.members', bodyKey: 'businessDashboard.membersBody' },
  { code: 'Attendance', titleKey: 'businessDashboard.attendance', bodyKey: 'businessDashboard.attendanceBody' },
  { code: 'Memberships', titleKey: 'businessDashboard.memberships', bodyKey: 'businessDashboard.membershipsBody' },
  { code: 'Fitness', titleKey: 'businessDashboard.fitness', bodyKey: 'businessDashboard.fitnessBody' },
  { code: 'Trainers', titleKey: 'businessDashboard.trainers', bodyKey: 'businessDashboard.trainersBody' },
  { code: 'AI', titleKey: 'businessDashboard.ai', bodyKey: 'businessDashboard.aiBody' },
  { code: 'Reports', titleKey: 'businessDashboard.reports', bodyKey: 'businessDashboard.reportsBody' },
  { code: 'Challenges', titleKey: 'businessDashboard.challenges', bodyKey: 'businessDashboard.challengesBody' },
];

export type DashboardProblem = 'suspended' | 'unauthorized' | 'unavailable' | 'error';

export type MetricLine = {
  labelKey: string;
  value: number;
};

export function widgetMetrics(code: string, summary: DashboardSummary): MetricLine[] | 'placeholder' | 'unavailable' {
  if (code === 'Members') {
    return summary.members
      ? [
          { labelKey: 'businessDashboard.total', value: summary.members.total },
          { labelKey: 'businessDashboard.active', value: summary.members.active },
          { labelKey: 'businessDashboard.newThisMonth', value: summary.members.newThisMonth },
        ]
      : 'unavailable';
  }

  if (code === 'Attendance') {
    return summary.attendance ? [{ labelKey: 'businessDashboard.checkInsToday', value: summary.attendance.checkInsToday }] : 'unavailable';
  }

  if (code === 'Memberships') {
    return summary.memberships
      ? [
          { labelKey: 'businessDashboard.active', value: summary.memberships.active },
          { labelKey: 'businessDashboard.expiring7', value: summary.memberships.expiringIn7Days },
          { labelKey: 'businessDashboard.expiring30', value: summary.memberships.expiringIn30Days },
          { labelKey: 'businessDashboard.expired', value: summary.memberships.expired },
        ]
      : 'unavailable';
  }

  if (code === 'Trainers') {
    return summary.people
      ? [
          { labelKey: 'businessDashboard.activeTrainers', value: summary.people.activeTrainers },
          { labelKey: 'businessDashboard.activeStaff', value: summary.people.activeStaff },
        ]
      : 'unavailable';
  }

  return 'placeholder';
}

export function summaryHighlights(summary: DashboardSummary): MetricLine[] {
  const lines: MetricLine[] = [];
  if (summary.members) {
    lines.push({ labelKey: 'businessDashboard.activeMembers', value: summary.members.active });
  }

  if (summary.attendance) {
    lines.push({ labelKey: 'businessDashboard.checkInsToday', value: summary.attendance.checkInsToday });
  }

  if (summary.memberships) {
    lines.push({ labelKey: 'businessDashboard.activeMemberships', value: summary.memberships.active });
  }

  if (summary.people) {
    lines.push({ labelKey: 'businessDashboard.activeTrainers', value: summary.people.activeTrainers }, { labelKey: 'businessDashboard.activeStaff', value: summary.people.activeStaff });
  }

  return lines;
}

export function visibleWidgets(capabilities: DashboardCapability[]): DashboardWidget[] {
  const enabled = new Set(capabilities.filter((capability) => capability.enabled).map((capability) => capability.code));
  return dashboardWidgets.filter((widget) => enabled.has(widget.code));
}

export function dashboardProblem(error: unknown): DashboardProblem | null {
  if (!error) {
    return null;
  }

  const apiError = toApiError(error);
  const message = apiError.message.toLowerCase();
  if (message.includes('suspended') || message.includes('not active')) {
    return 'suspended';
  }

  if (message.includes('business workspace')) {
    return 'unavailable';
  }

  if (apiError.status === 403) {
    return 'unauthorized';
  }

  return 'error';
}
