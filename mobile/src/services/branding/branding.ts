import type { AccentId } from '../../theme';

export type TenantBrand = {
  tenantId: string;
  organizationName: string;
  logoUrl: string | null;
  accent: AccentId;
  isPersonal: boolean;
};

const productName = 'LifeFit AI';

export function resolveBrand(input: {
  tenantId: string;
  tenantName: string;
  tenantType: string;
  logoUrl?: string | null;
  userAccent: AccentId;
}): TenantBrand {
  const isPersonal = input.tenantType === 'Personal';
  return {
    tenantId: input.tenantId,
    organizationName: isPersonal ? productName : input.tenantName,
    logoUrl: isPersonal ? null : input.logoUrl ?? null,
    accent: input.userAccent,
    isPersonal,
  };
}
