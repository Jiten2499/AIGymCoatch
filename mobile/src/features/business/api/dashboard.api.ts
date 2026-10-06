import { apiClient } from '../../../services/api/client';

export type DashboardCapability = {
  code: string;
  enabled: boolean;
};

export type BusinessDashboardOverview = {
  tenant: {
    id: string;
    name: string;
    tenantType: string;
    status: string;
    logoUrl: string | null;
    timeZone: string;
    currentDate: string;
  };
  user: {
    displayName: string;
    roles: string[];
  };
  capabilities: DashboardCapability[];
  summary: DashboardSummary;
};

export type DashboardSummary = {
  members: { total: number; active: number; newThisMonth: number } | null;
  attendance: { checkInsToday: number } | null;
  memberships: { active: number; expiringIn7Days: number; expiringIn30Days: number; expired: number } | null;
  people: { activeTrainers: number; activeStaff: number } | null;
};

export async function getBusinessDashboard(signal?: AbortSignal): Promise<BusinessDashboardOverview> {
  const response = await apiClient.get<BusinessDashboardOverview>('/business/dashboard', { signal });
  return response.data;
}
