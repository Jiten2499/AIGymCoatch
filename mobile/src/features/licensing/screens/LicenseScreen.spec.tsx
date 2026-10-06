import { beforeEach, describe, expect, it, jest } from '@jest/globals';
import { useSessionStore } from '../../../store/sessionStore';
import { renderWithProviders } from '../../../test/render';
import { getLicensePool, listAssignments } from '../api/licenses.api';
import { LicenseScreen } from './LicenseScreen';

jest.mock('../api/licenses.api', () => ({
  getLicensePool: jest.fn(),
  listAssignments: jest.fn(),
  assignLicense: jest.fn(),
  revokeAssignment: jest.fn(),
}));

describe('LicenseScreen', () => {
  beforeEach(() => {
    useSessionStore.setState({
      status: 'authenticated',
      accessToken: 'token',
      refreshToken: 'refresh',
      expiresAt: '2099-01-01T00:00:00Z',
      user: { id: 'user-1', email: 'ada@example.com', firstName: 'Ada', lastName: 'Lovelace' },
      currentTenant: { id: 'tenant-a', name: 'North Gym', type: 'Gym', status: 'Active' },
    });
  });

  it('shows capacity from the license pool', async () => {
    jest.mocked(getLicensePool).mockResolvedValue({
      licenseId: 'lic-1',
      planCode: 'PRO',
      status: 'Active',
      startsAt: '2026-01-01T00:00:00Z',
      expiresAt: '2026-12-31T00:00:00Z',
      baseCapacity: 100,
      extraCapacity: 25,
      assigned: 72,
      available: 53,
      usable: true,
    });
    jest.mocked(listAssignments).mockResolvedValue([]);
    const view = await renderWithProviders(<LicenseScreen />);
    expect(await view.findByText('PRO')).toBeTruthy();
    expect(view.getByText('Total: 125')).toBeTruthy();
    expect(view.getByText('Assigned: 72')).toBeTruthy();
    expect(view.getByText('Available: 53')).toBeTruthy();
  });
});
