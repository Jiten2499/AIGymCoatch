import type { PageQuery } from '../../types/api';

export function toPageParams(query: PageQuery): Record<string, string | number> {
  const page = query.page < 1 ? 1 : query.page;
  const pageSize = query.pageSize < 1 || query.pageSize > 100 ? 20 : query.pageSize;
  const params: Record<string, string | number> = { page, pageSize };
  if (query.sort) {
    params.sort = query.sort;
  }
  if (query.term) {
    params.term = query.term;
  }
  return params;
}

export function pageQueryKey(tenantId: string, resource: string, query: PageQuery): readonly unknown[] {
  return ['tenant', tenantId, resource, toPageParams(query)];
}
