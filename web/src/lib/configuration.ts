export type MembershipConfig = {
  allowOverlappingMemberships: boolean;
  allowEarlyRenewal: boolean;
  defaultStartOnJoinDate: boolean;
  gracePeriodDays: number;
  autoExpireEnabled: boolean;
};

export type FreezeConfig = {
  maxFreezeDays: number;
  minFreezeDays: number;
  maxFreezeOccurrences: number;
  freezeRequiresApproval: boolean;
  maxExtensionDays: number;
  extensionRequiresApproval: boolean;
};

export type AttendanceConfig = {
  duplicateCheckInMinutes: number;
  openingTime: string | null;
  closingTime: string | null;
  checkInGraceMinutes: number;
  defaultCheckInMethod: string;
};

export type CrmConfig = {
  defaultTrialDays: number;
  followUpReminderHours: number;
  defaultFollowUpDays: number;
};

export type MemberCategory = { id: string; name: string; description: string | null; isActive: boolean };
export type CrmCatalogItem = { id: string; name: string; code: string | null; isSystem: boolean; isActive: boolean };
export type DiscountRule = {
  id: string;
  name: string;
  description: string | null;
  discountType: string;
  value: number;
  appliesTo: string;
  packageId: string | null;
  packageName: string | null;
  packageCategory: string | null;
  memberCategoryId: string | null;
  memberCategoryName: string | null;
  minimumPackagePrice: number | null;
  minimumDurationDays: number | null;
  maximumDiscountAmount: number | null;
  startDate: string | null;
  endDate: string | null;
  isActive: boolean;
};

export type BusinessConfiguration = {
  membership: MembershipConfig;
  freeze: FreezeConfig;
  attendance: AttendanceConfig;
  crm: CrmConfig;
  memberCategories: MemberCategory[];
  discounts: DiscountRule[];
  leadSources: CrmCatalogItem[];
  lostReasons: CrmCatalogItem[];
};

export const configTabs = ["Membership", "Discounts", "Member Categories", "PT Packages", "Coach Rewards", "Freeze & Extensions", "Attendance", "CRM", "Chat", "Integrations", "Roles & Permissions"] as const;
export type ConfigTab = (typeof configTabs)[number];
