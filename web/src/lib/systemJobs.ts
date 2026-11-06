export type SystemJob = {
  jobKey: string;
  name: string;
  jobType: string;
  schedule: string;
  queue: string;
  isEnabled: boolean;
  lastRunUtc: string | null;
  nextRunUtc: string | null;
  lastStatus: string | null;
};

export type SystemJobStatistics = {
  membersProcessed: number;
  tenDay: number;
  fiveDay: number;
  today: number;
  skipped: number;
  failed: number;
};

export type SystemJobDetails = {
  jobKey: string;
  name: string;
  description: string | null;
  jobType: string;
  schedule: string;
  cronExpression: string;
  queue: string;
  isEnabled: boolean;
  featureKey: string | null;
  lastRunUtc: string | null;
  lastStatus: string | null;
  lastDurationSeconds: number | null;
  nextRunUtc: string | null;
  lastError: string | null;
  statistics: SystemJobStatistics | null;
};

export function systemJobActions(enabled: boolean, canManage: boolean) {
  return {
    enable: canManage && !enabled,
    disable: canManage && enabled,
    run: canManage && enabled,
  };
}
