import { describe, expect, it } from '@jest/globals';
import { canAccessBusiness, isFeatureEnabled } from './access';

describe('access hints', () => {
  it('shows business navigation only for a business workspace role', () => {
    expect(canAccessBusiness('Gym', ['GymOwner'])).toBe(true);
    expect(canAccessBusiness('Personal', ['PersonalOwner'])).toBe(false);
    expect(canAccessBusiness('Gym', ['Member'])).toBe(false);
  });

  it('treats a missing feature flag as disabled', () => {
    expect(isFeatureEnabled({ AI_COACH: true }, 'AI_COACH')).toBe(true);
    expect(isFeatureEnabled(undefined, 'AI_COACH')).toBe(false);
  });
});
