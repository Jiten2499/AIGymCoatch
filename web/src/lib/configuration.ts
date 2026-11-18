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

export type CurrencyOption = { code: string; name: string };

export const currencyChoices: CurrencyOption[] = [
  { code: "USD", name: "US Dollar" },
  { code: "INR", name: "Indian Rupee" },
];

export function currencyOptions(currency: string, currencies: CurrencyOption[] = []): CurrencyOption[] {
  const merged = [...currencyChoices, ...currencies];
  if (currency.length === 3 && !merged.some((item) => item.code === currency)) {
    merged.unshift({ code: currency, name: currency });
  }
  const seen = new Set<string>();
  return merged.filter((item) => {
    if (seen.has(item.code)) {
      return false;
    }
    seen.add(item.code);
    return true;
  });
}

export type BusinessConfiguration = {
  membership: MembershipConfig;
  freeze: FreezeConfig;
  attendance: AttendanceConfig;
  crm: CrmConfig;
  memberCategories: MemberCategory[];
  discounts: DiscountRule[];
  leadSources: CrmCatalogItem[];
  lostReasons: CrmCatalogItem[];
  currency?: string;
  currencies?: CurrencyOption[];
};

export const configTabs = ["General", "Membership", "Discounts", "Member Categories", "PT Packages", "Coach Rewards", "Freeze & Extensions", "Attendance", "CRM", "Chat", "Roles & Permissions"] as const;
export type ConfigTab = (typeof configTabs)[number];
