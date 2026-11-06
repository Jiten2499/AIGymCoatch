export type PtPackageOption = {
  id: string;
  name: string;
  description: string | null;
  sessionCount: number;
  validityDays: number;
  price: number;
  currency: string;
  isActive: boolean;
  status?: string;
};

export const noPtPackagesMessage = "No personal-training packages have been configured. Add one from Configuration, under PT Packages.";

export function ptPackageLabel(item: Pick<PtPackageOption, "name" | "sessionCount" | "validityDays" | "price" | "currency">): string {
  return `${item.name} · ${item.sessionCount} sessions · ${item.validityDays} days · ${item.price} ${item.currency}`;
}
