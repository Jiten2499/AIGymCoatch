export function tenantKey(tenantId: string, ...parts: readonly unknown[]): readonly unknown[] {
  return ['tenant', tenantId, ...parts];
}
