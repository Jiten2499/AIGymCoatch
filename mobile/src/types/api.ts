export type UserProfile = {
  id: string;
  email: string;
  firstName: string;
  lastName: string;
};

export type TenantSummary = {
  id: string;
  name: string;
  type: string;
  status: string;
};

export type WorkspaceSummary = TenantSummary & {
  roles: string[];
};

export type AuthResponse = {
  accessToken: string;
  refreshToken: string;
  expiresAt: string;
  user: UserProfile;
  currentTenant: TenantSummary;
};

export type ProblemDetails = {
  title?: string;
  status?: number;
  detail?: string;
  type?: string;
  errors?: Record<string, string[]>;
};

export type PageQuery = {
  page: number;
  pageSize: number;
  sort?: string;
  term?: string;
};

export type PageResult<T> = {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
};

export type BusinessProfile = {
  id: string;
  tenantId: string;
  businessName: string;
  logoUrl: string | null;
  timeZone: string;
  currency: string;
};
