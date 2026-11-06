export type AccessAction = { code: string; action: string; label: string };
export type AccessModule = { key: string; name: string; scoped: boolean; actions: AccessAction[] };
export type AccessScope = { resource: string; scope: "All" | "Branch" | "Assigned" | "Own" };
export type AccessRole = {
  id: string;
  name: string;
  label: string;
  description: string | null;
  isSystem: boolean;
  isProtected: boolean;
  isActive: boolean;
  assignedUsers: number;
  permissionSummary: string;
  permissionCodes: string[];
  scopes: AccessScope[];
};
export type AccessRoleList = { canManage: boolean; roles: AccessRole[] };
export type AccessAssignment = {
  userId: string;
  displayName: string;
  email: string;
  roleId: string | null;
  roleName: string;
  roleLabel: string;
  hasPhoto: boolean;
  accountStatus: string;
  seatStatus: string;
  membershipStatus: string;
};
export type AccessOverview = {
  userId: string;
  displayName: string;
  roleLabel: string;
  permissions: string[];
  excessive: string[];
  restrictedModules: string[];
  memberScope: string;
};
export type EffectiveAccess = {
  userId: string;
  displayName: string;
  roleLabel: string;
  permissions: string[];
  scopes: AccessScope[];
};
