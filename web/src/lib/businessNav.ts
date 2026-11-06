export const gymLinks = [
  ["Members", "/business/members"],
  ["Memberships", "/business/memberships"],
  ["Leads", "/business/leads"],
  ["Expenses", "/business/expenses"],
  ["Ledger", "/business/ledger"],
  ["Payroll", "/business/payroll"],
  ["Staff Leaves", "/business/leaves"],
  ["Circulars", "/business/communications"],
  ["Chats", "/business/chats"],
  ["Integrations", "/business/integrations"],
  ["Configuration", "/business/configuration"],
] as const;

/** Feature codes that must be enabled before a gym nav link is shown. */
export const gymLinkFeatures: Partial<Record<(typeof gymLinks)[number][1], string>> = {
  "/business/communications": "GYM_COMMUNICATIONS",
  "/business/chats": "GYM_CHAT",
  "/business/ledger": "GYM_FINANCIAL_LEDGER",
  "/business/payroll": "GYM_STAFF_PAYROLL",
  "/business/leaves": "STAFF_LEAVE",
  "/business/integrations": "GOOGLE_DRIVE_STORAGE",
};

/** Any one of these permissions is enough to show the link. */
const linkPermissions: Record<string, readonly string[]> = {
  "/business/users": ["TENANT_MEMBER_READ"],
  "/business/coaches": ["TRAINER_READ"],
  "/business/plans": ["LICENSE_READ"],
  "/business/members": ["MEMBER_READ"],
  "/business/memberships": ["MEMBERSHIP_READ"],
  "/business/leads": ["CRM_READ"],
  "/business/expenses": ["EXPENSE_READ"],
  "/business/ledger": ["LEDGER_READ"],
  "/business/payroll": ["PAYROLL_READ"],
  "/business/leaves": ["LEAVE_READ", "LEAVE_APPLY"],
  "/business/communications": ["COMMUNICATION_READ"],
  "/business/chats": ["CHAT_READ"],
  "/business/integrations": ["INTEGRATION_READ"],
  "/business/configuration": ["CONFIGURATION_READ"],
  "/business/tasks": ["MEMBERSHIP_FREEZE", "MEMBERSHIP_RENEW", "TRAINER_UPDATE", "LEAVE_APPROVE"],
};

export function canSeeBusinessLink(href: string, permissions: ReadonlySet<string> | null, features: ReadonlyMap<string, boolean>): boolean {
  const feature = gymLinkFeatures[href as keyof typeof gymLinkFeatures];
  if (feature && features.get(feature) !== true) {
    return false;
  }

  if (!permissions) {
    return false;
  }

  const required = linkPermissions[href];
  return !required || required.some((code) => permissions.has(code));
}
