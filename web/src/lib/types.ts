export type Page<T> = { items: T[]; page: number; pageSize: number; totalCount: number };

export type PlatformTenant = { id: string; name: string; tenantType: string; status: string };

export type PlatformUser = { id: string; email: string; firstName: string; lastName: string; status: string; personalTenantId: string; personalTenantName: string; createdAt: string; plan: string };

export type WorkspaceUser = {
  id: string;
  email: string;
  firstName: string;
  lastName: string;
  status: string;
  role: string;
  isOwner: boolean;
  createdAt: string;
  plan: string;
};

export type WorkspacePlan = {
  subscriptionId: string;
  planCode: string;
  planName: string;
  description: string | null;
  status: string;
  startsAt: string;
  expiresAt: string | null;
  seats: number;
  assigned: number;
  available: number;
  usable: boolean;
  features: { code: string; enabled: boolean }[];
};

export type CreatedWorkspaceUser = {
  id: string;
  email: string;
  firstName: string;
  lastName: string;
  role: string;
  temporaryPassword: string | null;
};

export type LlmConnection = {
  id: string;
  name: string;
  provider: string;
  baseUrl: string;
  apiKeyConfigured: boolean;
  defaultModel: string | null;
  isActive: boolean;
  isDefault: boolean;
  timeoutSeconds: number;
  lastTestedAt: string | null;
  healthStatus: string;
  lastLatencyMs: number | null;
  modelCount: number;
  createdAt: string;
  updatedAt: string;
};

export type LlmModel = {
  id: string;
  connectionId: string;
  modelIdentifier: string;
  displayName: string;
  isActive: boolean;
  isDefault: boolean;
  capabilities: string[];
};

export type LlmTest = { succeeded: boolean; healthStatus: string; latencyMs: number | null; models: string[]; message: string | null };

export type Plan = { id: string; code: string; name: string; status: string; appliesTo: string; baseCapacity: number };

export type PlanDetail = Plan & {
  description: string | null;
  sortOrder: number;
  features: { featureCode: string; enabled: boolean }[];
};

export type FeatureRow = { code: string; name: string; category: string; scope: string; isActive: boolean };

export type TenantLicense = {
  licenseId: string;
  tenantId: string;
  tenantName: string;
  planCode: string;
  status: string;
  startsAt: string;
  expiresAt: string | null;
  baseCapacity: number;
  extraCapacity: number;
  assigned: number;
  available: number;
  usable: boolean;
  revokedAt: string | null;
};

export type Assignment = { id: string; userId: string; status: string; assignedAt: string; revokedAt: string | null };

export type MemberOption = { userId: string; email: string; firstName: string; lastName: string };

export type AuditEntry = { id: string; tenantId: string | null; action: string; entity: string; entityId: string | null; createdAt: string };

export type ExpiringLicense = { licenseId: string; tenantId: string; planCode: string; expiresAt: string | null };

export type Coach = {
  id: string;
  userId: string;
  email: string;
  firstName: string;
  lastName: string;
  bio: string | null;
  specialization: string | null;
  experienceYears: number | null;
  certification: string | null;
  profileImageUrl: string | null;
  branchId: string | null;
  status: string;
};

export type CreatedCoach = {
  id: string;
  email: string;
  firstName: string;
  lastName: string;
  specialization: string | null;
  temporaryPassword: string | null;
};
