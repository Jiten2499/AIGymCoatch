export const challengeTypes = ['WorkoutCount', 'ConsistencyDays', 'Custom'] as const;
export const challengeStatuses = ['Draft', 'Active', 'Completed', 'Cancelled'] as const;

export type ChallengeType = (typeof challengeTypes)[number];
export type ChallengeStatus = (typeof challengeStatuses)[number];

export type Challenge = {
  id: string;
  name: string;
  description: string | null;
  challengeType: ChallengeType;
  startDate: string;
  endDate: string;
  targetValue: number;
  status: ChallengeStatus;
  isSystem: boolean;
  currentValue: number | null;
  completedAt: string | null;
};
