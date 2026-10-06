export const businessRoles = ['GymOwner', 'GymManager', 'Trainer', 'Receptionist', 'SuperAdmin'] as const;

export const permissionCodes = {
  memberRead: 'MEMBER_READ',
  businessProfileRead: 'BUSINESS_PROFILE_READ',
  attendanceRead: 'ATTENDANCE_READ',
  aiUse: 'AI_USE',
  featureRead: 'FEATURE_READ',
} as const;

export function hasAnyRole(roles: readonly string[], allowed: readonly string[]): boolean {
  return roles.some((role) => allowed.includes(role));
}

export function canAccessBusiness(type: string, roles: readonly string[]): boolean {
  return type !== 'Personal' && hasAnyRole(roles, businessRoles);
}

export function canManageLicenses(type: string, roles: readonly string[]): boolean {
  return type !== 'Personal' && hasAnyRole(roles, ['GymOwner', 'SuperAdmin']);
}

export const featureCodes = {
  workoutPlans: 'WORKOUT_PLANS',
  aiCoach: 'AI_COACH',
  aiWorkoutGeneration: 'AI_WORKOUT_GENERATION',
  aiProgressInsights: 'AI_PROGRESS_INSIGHTS',
  nutrition: 'NUTRITION',
  aiNutrition: 'AI_NUTRITION',
} as const;

export function isFeatureEnabled(flags: Readonly<Record<string, boolean>> | undefined, code: string): boolean {
  return flags?.[code] === true;
}
