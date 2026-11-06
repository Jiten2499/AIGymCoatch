import { describe, expect, it } from '@jest/globals';
import { acceptCircularEvent } from './realtime';

describe('circular realtime', () => {
  const tenantId = 'tenant-1';

  it('accepts a published notice for the current tenant only', () => {
    expect(acceptCircularEvent({ communicationId: 'circular-1', tenantId, audience: 'AllMembers' }, tenantId)).toBe(true);
    expect(acceptCircularEvent({ communicationId: 'circular-1', tenantId: 'other' }, tenantId)).toBe(false);
    expect(acceptCircularEvent({ tenantId }, tenantId)).toBe(false);
    expect(acceptCircularEvent(null, tenantId)).toBe(false);
  });
});
