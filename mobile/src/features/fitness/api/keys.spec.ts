import { describe, expect, it } from '@jest/globals';
import { tenantKey } from './keys';

describe('tenantKey', () => {
  it('keeps personal and business workspace caches apart', () => {
    expect(tenantKey('personal-a', 'fitnessProfile')).not.toEqual(tenantKey('business-b', 'fitnessProfile'));
    expect(tenantKey('personal-a', 'session', 'session-1')).not.toEqual(tenantKey('personal-a', 'session', 'session-2'));
  });
});
