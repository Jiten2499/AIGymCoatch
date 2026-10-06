import { describe, expect, it } from '@jest/globals';
import { pageQueryKey, toPageParams } from './paging';

describe('paging', () => {
  it('keeps page parameters inside the backend contract and scopes the cache key by tenant', () => {
    const params = toPageParams({ page: 0, pageSize: 500, sort: 'name', term: 'ada' });

    expect(params).toEqual({ page: 1, pageSize: 20, sort: 'name', term: 'ada' });
    expect(pageQueryKey('tenant-a', 'members', { page: 2, pageSize: 20 })).not.toEqual(
      pageQueryKey('tenant-b', 'members', { page: 2, pageSize: 20 }),
    );
  });
});
