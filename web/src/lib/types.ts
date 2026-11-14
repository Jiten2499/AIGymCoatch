export type Page<T> = { items: T[]; page: number; pageSize: number; totalCount: number };

export type PlatformTenant = { id: string; name: string; tenantType: string; status: string; ownerEmail: string | null };

export type PlatformUser = { id: string; email: string; firstName: string; lastName: string; status: string; personalTenantId: string; personalTenantName: string; createdAt: string; plan: string; hasPhoto?: boolean };

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
  hasPhoto?: boolean;
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

export type WorkspacePlanAssignee = {
  seatId: string;
  userId: string;
  email: string;
  firstName: string;
  lastName: string;
  assignedAt: string;
};

export type WorkspacePlanCandidate = {
  userId: string;
  email: string;
  firstName: string;
  lastName: string;
  currentPlan: string | null;
};

export type CreatedWorkspaceUser = {
  id: string;
  email: string;
  firstName: string;
  lastName: string;
  role: string;
  temporaryPassword: string | null;
  staffProfileId?: string | null;
  invitationToken?: string | null;
  status?: string;
  employeeCode?: string | null;
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

export type EmailProviderOption = { provider: string; displayName: string; implemented: boolean };

export type EmailConfiguration = {
  id: string | null;
  provider: string;
  displayName: string;
  isEnabled: boolean;
  isActive: boolean;
  smtpHost: string;
  smtpPort: number;
  encryption: string;
  authenticationEnabled: boolean;
  username: string;
  passwordConfigured: boolean;
  senderName: string;
  senderEmail: string;
  replyToEmail: string | null;
  connectionTimeoutSeconds: number;
  maxEmailsPerMinute: number;
  maxDeliveryAttempts: number;
  retryBaseDelaySeconds: number;
  configVersion: number;
  lastTestedAt: string | null;
  lastTestStatus: string | null;
  lastTestMessage: string | null;
  lastEmailSentAt: string | null;
  lastEmailStatus: string | null;
  healthStatus: string;
  updatedAt: string | null;
  updatedByName: string | null;
  availableProviders: EmailProviderOption[];
  rateLimitWarning: string | null;
};

export type EmailTestResult = { succeeded: boolean; healthStatus: string; message: string; testedAt: string };

export type EmailDeliveryLog = {
  id: string;
  tenantId: string;
  relatedTenantId: string | null;
  recipient: string;
  subject: string;
  templateKey: string;
  provider: string;
  status: string;
  classification: string;
  attemptCount: number;
  maxAttempts: number;
  providerMessageId: string | null;
  failureReason: string | null;
  queuedAt: string;
  sentAt: string | null;
  canRetry: boolean;
};

export type EmailDeliveryPage = { items: EmailDeliveryLog[]; page: number; pageSize: number; totalCount: number };

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
  assignedMembers?: number;
  employeeCode?: string | null;
  phone?: string | null;
  joiningDate?: string | null;
  commissionEligible?: boolean;
  hasPhoto?: boolean;
  staffProfileId?: string | null;
  designation?: string | null;
  dateOfBirth?: string | null;
  gender?: string | null;
  address?: string | null;
  emergencyContactName?: string | null;
  emergencyContactPhone?: string | null;
  employmentType?: string | null;
  reportingManagerUserId?: string | null;
  employmentNotes?: string | null;
  subscriptionId?: string | null;
  planName?: string | null;
};

export type CreatedCoach = {
  id: string;
  email: string;
  firstName: string;
  lastName: string;
  specialization: string | null;
  temporaryPassword: string | null;
  staffProfileId?: string | null;
};
