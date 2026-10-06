import { describe, expect, it } from '@jest/globals';
import { resolveBrand } from './branding';

describe('resolveBrand', () => {
  it('keeps a business logo on that tenant and uses LifeFit branding for a personal workspace', () => {
    const business = resolveBrand({
      tenantId: 'gym',
      tenantName: 'North Gym',
      tenantType: 'Gym',
      logoUrl: 'https://cdn.example/north.png',
      userAccent: 'violet',
    });
    const personal = resolveBrand({
      tenantId: 'home',
      tenantName: 'Ada Personal',
      tenantType: 'Personal',
      logoUrl: 'https://cdn.example/should-not-show.png',
      userAccent: 'lime',
    });

    expect(business.organizationName).toBe('North Gym');
    expect(business.logoUrl).toBe('https://cdn.example/north.png');
    expect(personal.organizationName).toBe('LifeFit AI');
    expect(personal.logoUrl).toBeNull();
    expect(business.tenantId).not.toBe(personal.tenantId);
  });
});
