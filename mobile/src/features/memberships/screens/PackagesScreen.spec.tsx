import { beforeEach, describe, expect, it, jest } from '@jest/globals';
import { fireEvent, waitFor } from '@testing-library/react-native';
import { ApiError } from '../../../services/api/problem';
import { useSessionStore } from '../../../store/sessionStore';
import { renderWithProviders } from '../../../test/render';
import { listFeatureFlags } from '../../licensing/api/licenses.api';
import { listWorkspacePlans } from '../../members/api/appAccess.api';
import { changeMembership, listMemberMemberships, listPackages, packagesQueryKey, savePackage, setPackageActive } from '../api/memberships.api';
import { MemberMemberships } from './MemberMemberships';
import { PackageForm } from './PackageFormScreen';
import { PackagesList } from './PackagesScreen';

jest.mock('../api/memberships.api', () => ({
  listPackages: jest.fn(),
  getPackage: jest.fn(),
  savePackage: jest.fn(),
  setPackageActive: jest.fn(),
  listMemberMemberships: jest.fn(),
  assignMembership: jest.fn(),
  changeMembership: jest.fn(),
  packagesQueryKey: jest.requireActual<typeof import('../api/memberships.api')>('../api/memberships.api').packagesQueryKey,
  membershipsQueryKey: jest.requireActual<typeof import('../api/memberships.api')>('../api/memberships.api').membershipsQueryKey,
}));
jest.mock('../../members/api/appAccess.api', () => ({
  listWorkspacePlans: jest.fn(),
  getMemberAppAccess: jest.fn(),
  assignMemberAppAccess: jest.fn(),
  removeMemberAppAccess: jest.fn(),
  workspacePlansQueryKey: jest.requireActual<typeof import('../../members/api/appAccess.api')>('../../members/api/appAccess.api').workspacePlansQueryKey,
  memberAppAccessQueryKey: jest.requireActual<typeof import('../../members/api/appAccess.api')>('../../members/api/appAccess.api').memberAppAccessQueryKey,
}));
jest.mock('../../licensing/api/licenses.api', () => ({ listFeatureFlags: jest.fn() }));
jest.mock('@react-navigation/native', () => {
  const actual = jest.requireActual<typeof import('@react-navigation/native')>('@react-navigation/native');
  return { ...actual, useNavigation: () => ({ navigate: jest.fn(), replace: jest.fn() }) };
});

const plan = {
  id: 'plan-1',
  name: 'Annual Pro',
  description: null,
  durationDays: 12,
  membershipType: 'Yearly',
  price: 100,
  currency: 'USD',
  isActive: true,
  category: 'Standard',
  durationUnit: 'Month',
  durationCount: 12,
};

describe('membership packages', () => {
  beforeEach(() => {
    useSessionStore.setState({
      status: 'authenticated',
      accessToken: 'token',
      refreshToken: 'refresh',
      expiresAt: '2099-01-01T00:00:00Z',
      user: { id: 'user-1', email: 'owner@gym.test', firstName: 'Gym', lastName: 'Owner' },
      currentTenant: { id: 'tenant-a', name: 'North Gym', type: 'Gym', status: 'Active' },
    });
    jest.mocked(listFeatureFlags).mockResolvedValue([{ featureCode: 'MEMBERSHIP_MANAGEMENT', enabled: true }]);
    jest.mocked(listPackages).mockResolvedValue({ items: [plan], page: 1, pageSize: 20, totalCount: 1 });
    jest.mocked(listWorkspacePlans).mockResolvedValue([{
      subscriptionId: 'sub-1',
      planCode: 'PREMIUM',
      planName: 'LifeFit Premium',
      description: null,
      status: 'Active',
      seats: 50,
      assigned: 30,
      available: 20,
      usable: true,
      features: [],
    }]);
    jest.mocked(listMemberMemberships).mockResolvedValue({
      items: [{ id: 'membership-1', memberProfileId: 'member-1', branchId: 'branch-1', membershipPlanId: 'plan-1', membershipNumber: 'G1', startDate: '2026-01-01', endDate: '2026-12-31', status: 'Active', autoRenew: false, notes: null, memberName: 'Ada Lovelace', planName: 'Annual Pro' }],
      page: 1,
      pageSize: 20,
      totalCount: 1,
    });
  });

  it('keeps the workspace in the package query key', () => {
    expect(packagesQueryKey('tenant-a', 'Pro', true, 1)).not.toEqual(packagesQueryKey('tenant-b', 'Pro', true, 1));
  });

  it('loads packages and filters active ones', async () => {
    const view = await renderWithProviders(<PackagesList enabled />);
    expect(await view.findByText('Annual Pro')).toBeTruthy();
    fireEvent.changeText(view.getByLabelText('Search packages'), 'Pro');
    await waitFor(() => expect(listPackages).toHaveBeenCalledWith('Pro', false, 1, expect.anything()));
    fireEvent.press(view.getByText('Active packages'));
    await waitFor(() => expect(listPackages).toHaveBeenCalledWith('Pro', true, 1, expect.anything()));
  }, 15000);

  it('shows an empty list, an error, and an unauthorized state', async () => {
    jest.mocked(listPackages).mockResolvedValue({ items: [], page: 1, pageSize: 20, totalCount: 0 });
    const empty = await renderWithProviders(<PackagesList enabled />);
    expect(await empty.findByText('No packages in this workspace.')).toBeTruthy();

    jest.mocked(listPackages).mockReset();
    jest.mocked(listPackages).mockRejectedValue(new ApiError('The request failed.', 500, { detail: 'The request failed.' }));
    const failed = await renderWithProviders(<PackagesList enabled />);
    expect(await failed.findByText('The request failed.')).toBeTruthy();

    jest.mocked(listPackages).mockReset();
    jest.mocked(listPackages).mockRejectedValue(new ApiError('You do not have permission to perform this action.', 403, { detail: 'You do not have permission to perform this action.' }));
    const denied = await renderWithProviders(<PackagesList enabled />);
    expect(await denied.findByText('You do not have permission to manage memberships in this workspace.')).toBeTruthy();
  });

  it('does not query packages when the feature is disabled', async () => {
    jest.mocked(listPackages).mockClear();
    const view = await renderWithProviders(<PackagesList enabled={false} />);
    expect(view.queryByText('Annual Pro')).toBeNull();
    expect(listPackages).not.toHaveBeenCalled();
  });

  it('creates a package and deactivates one', async () => {
    jest.mocked(savePackage).mockResolvedValue(plan);
    jest.mocked(setPackageActive).mockResolvedValue(undefined);
    const onSaved = jest.fn();
    const form = await renderWithProviders(<PackageForm onSaved={onSaved} />);
    expect(await form.findByText('Add package')).toBeTruthy();
    fireEvent.changeText(form.getByLabelText('Package name'), 'Annual Pro');
    await waitFor(() => expect(form.getByLabelText('Package name').props.value).toBe('Annual Pro'));
    fireEvent.press(form.getByLabelText('Save package'));
    await waitFor(() => expect(savePackage).toHaveBeenCalledWith(undefined, expect.objectContaining({ name: 'Annual Pro', durationUnit: 'Month' })));

    const list = await renderWithProviders(<PackagesList enabled />);
    fireEvent.press(await list.findByText('Deactivate'));
    await waitFor(() => expect(setPackageActive).toHaveBeenCalledWith('plan-1', false));
  }, 15000);

  it('shows membership history and renews, freezes, or extends it', async () => {
    jest.mocked(changeMembership).mockResolvedValue({ id: 'membership-2', memberProfileId: 'member-1', branchId: 'branch-1', membershipPlanId: 'plan-1', membershipNumber: 'G2', startDate: '2027-01-01', endDate: '2027-12-31', status: 'Pending', autoRenew: false, notes: null, memberName: 'Ada Lovelace', planName: 'Annual Pro' });
    const view = await renderWithProviders(<MemberMemberships enabled memberId="member-1" />);
    expect(await view.findByText('Annual Pro · Active · 2026-01-01 → 2026-12-31')).toBeTruthy();
    fireEvent.press(view.getByText('Renew'));
    fireEvent.press(view.getByText('Freeze'));
    fireEvent.press(view.getByText('Extend 7 days'));
    await waitFor(() => expect(changeMembership).toHaveBeenCalledWith('membership-1', 'renew', 0));
    expect(changeMembership).toHaveBeenCalledWith('membership-1', 'freeze', 0);
    expect(changeMembership).toHaveBeenCalledWith('membership-1', 'extend', 7);
  });
});
