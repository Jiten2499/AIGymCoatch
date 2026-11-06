import { beforeEach, describe, expect, it, jest } from '@jest/globals';
import { fireEvent, waitFor } from '@testing-library/react-native';
import { ApiError } from '../../../services/api/problem';
import { useSessionStore } from '../../../store/sessionStore';
import { renderWithProviders } from '../../../test/render';
import { listFeatureFlags } from '../../licensing/api/licenses.api';
import { createMember, getMember, listMembers, updateMember, uploadMemberPhoto } from '../api/members.api';
import { membersQueryKey } from '../api/members.api';
import { pickMemberPhoto } from '../photo/pickMemberPhoto';
import { MemberForm } from './MemberFormScreen';
import { MembersList } from './MembersScreen';

jest.mock('../api/members.api', () => ({
  listMembers: jest.fn(),
  createMember: jest.fn(),
  updateMember: jest.fn(),
  getMember: jest.fn(),
  uploadMemberPhoto: jest.fn(),
  removeMemberPhoto: jest.fn(),
  changeMemberStatus: jest.fn(),
  loadMemberPhoto: jest.fn(),
  membersQueryKey: jest.requireActual<typeof import('../api/members.api')>('../api/members.api').membersQueryKey,
}));
jest.mock('../photo/pickMemberPhoto', () => ({ pickMemberPhoto: jest.fn() }));
jest.mock('../../licensing/api/licenses.api', () => ({ listFeatureFlags: jest.fn() }));
jest.mock('@react-navigation/native', () => {
  const actual = jest.requireActual<typeof import('@react-navigation/native')>('@react-navigation/native');
  return { ...actual, useNavigation: () => ({ navigate: jest.fn(), replace: jest.fn() }) };
});

const member = {
  id: 'member-1',
  fullName: 'Ada Lovelace',
  mobile: '+15550001111',
  hasPhoto: false,
  status: 'Active' as const,
  joinDate: '2026-10-01',
};

describe('member management', () => {
  beforeEach(() => {
    useSessionStore.setState({
      status: 'authenticated',
      accessToken: 'token',
      refreshToken: 'refresh',
      expiresAt: '2099-01-01T00:00:00Z',
      user: { id: 'user-1', email: 'owner@gym.test', firstName: 'Gym', lastName: 'Owner' },
      currentTenant: { id: 'tenant-a', name: 'North Gym', type: 'Gym', status: 'Active' },
    });
    jest.mocked(listFeatureFlags).mockResolvedValue([{ featureCode: 'MEMBER_MANAGEMENT', enabled: true }]);
    jest.mocked(listMembers).mockResolvedValue({ items: [member], page: 1, pageSize: 20, totalCount: 1 });
    jest.mocked(getMember).mockResolvedValue({ ...member, email: 'ada@gym.test', firstName: 'Ada', lastName: 'Lovelace', phone: '+15550001111', userId: 'user-2', memberCode: 'M1', dateOfBirth: null, gender: null, emergencyContactName: null, emergencyContactPhone: null, emergencyContactRelationship: null, address: null, city: null, state: null, postalCode: null, fitnessGoal: null, assignedTrainerId: null, assignedTrainerName: null });
  });

  it('updates an existing member', async () => {
    jest.mocked(updateMember).mockResolvedValue({ ...member, email: 'ada@gym.test', firstName: 'Ada', lastName: 'Byron', phone: '+15550001111', userId: 'user-2', memberCode: 'M1', dateOfBirth: null, gender: null, emergencyContactName: null, emergencyContactPhone: null, emergencyContactRelationship: null, address: null, city: null, state: null, postalCode: null, fitnessGoal: null, assignedTrainerId: null, assignedTrainerName: null });
    const onSaved = jest.fn();
    const view = await renderWithProviders(<MemberForm memberId="member-1" onSaved={onSaved} />);
    expect(await view.findByText('Edit', {}, { timeout: 8000 })).toBeTruthy();
    await waitFor(() => expect(view.getByLabelText('First name').props.value).toBe('Ada'));
    fireEvent.changeText(view.getByLabelText('Last name'), 'Byron');
    await waitFor(() => expect(view.getByLabelText('Last name').props.value).toBe('Byron'));
    fireEvent.press(view.getByLabelText('Save member'));
    await waitFor(() => expect(updateMember).toHaveBeenCalledWith('member-1', expect.objectContaining({ firstName: 'Ada', lastName: 'Byron' })));
    expect(onSaved).toHaveBeenCalledWith('member-1');
  }, 15000);

  it('keeps the workspace in the member query key', () => {
    expect(membersQueryKey('tenant-a', 'Ada', 'Active', 1)).not.toEqual(membersQueryKey('tenant-b', 'Ada', 'Active', 1));
  });

  it('loads members and searches within the current workspace', async () => {
    const view = await renderWithProviders(<MembersList enabled />);
    expect(await view.findByText('Ada Lovelace')).toBeTruthy();
    fireEvent.changeText(view.getByLabelText('Search name or mobile'), 'Ada');
    await waitFor(() => expect(listMembers).toHaveBeenCalledWith('Ada', '', 1, expect.anything()));
    fireEvent.press(view.getByText('Inactive'));
    await waitFor(() => expect(listMembers).toHaveBeenCalledWith('Ada', 'Inactive', 1, expect.anything()));
  }, 15000);

  it('shows an empty list', async () => {
    jest.mocked(listMembers).mockResolvedValue({ items: [], page: 1, pageSize: 20, totalCount: 0 });
    const view = await renderWithProviders(<MembersList enabled />);
    expect(await view.findByText('No members match this workspace.')).toBeTruthy();
  });

  it('shows an error and an unauthorized state', async () => {
    jest.mocked(listMembers).mockReset();
    jest.mocked(listMembers).mockRejectedValue(new ApiError('The request failed.', 500, { detail: 'The request failed.' }));
    const failed = await renderWithProviders(<MembersList enabled />);
    expect(await failed.findByText('The request failed.')).toBeTruthy();

    jest.mocked(listMembers).mockReset();
    jest.mocked(listMembers).mockRejectedValue(new ApiError('You do not have permission to perform this action.', 403, { detail: 'You do not have permission to perform this action.' }));
    const denied = await renderWithProviders(<MembersList enabled />);
    expect(await denied.findByText('You do not have permission to manage members in this workspace.')).toBeTruthy();
  });

  it('does not query members when the feature is disabled', async () => {
    jest.mocked(listMembers).mockClear();
    const view = await renderWithProviders(<MembersList enabled={false} />);
    expect(view.queryByText('Ada Lovelace')).toBeNull();
    expect(listMembers).not.toHaveBeenCalled();
  });

  it('adds a member and uploads the selected photo', async () => {
    jest.mocked(pickMemberPhoto).mockResolvedValue({ uri: 'file://profile.jpg', mimeType: 'image/jpeg', fileName: 'profile.jpg' });
    jest.mocked(createMember).mockResolvedValue({ ...member, email: 'ada@gym.test', firstName: 'Ada', lastName: 'Lovelace', phone: '+15550001111', userId: 'user-2', memberCode: 'M1', dateOfBirth: null, gender: null, emergencyContactName: null, emergencyContactPhone: null, emergencyContactRelationship: null, address: null, city: null, state: null, postalCode: null, fitnessGoal: 'Strength', assignedTrainerId: null, assignedTrainerName: null });
    const onSaved = jest.fn();
    const view = await renderWithProviders(<MemberForm onSaved={onSaved} />);
    expect(await view.findByText('Add member')).toBeTruthy();
    fireEvent.press(view.getByText('Choose photo'));
    expect(await view.findByLabelText('Profile photo')).toBeTruthy();
    fireEvent.changeText(view.getByLabelText('First name'), 'Ada');
    fireEvent.changeText(view.getByLabelText('Last name'), 'Lovelace');
    fireEvent.changeText(view.getByLabelText('Email'), 'ada@gym.test');
    fireEvent.changeText(view.getByLabelText('Password'), 'Secret-pass-1');
    fireEvent.press(view.getByLabelText('Save member'));
    await waitFor(() => expect(createMember).toHaveBeenCalled());
    await waitFor(() => expect(uploadMemberPhoto).toHaveBeenCalledWith('member-1', 'file://profile.jpg', 'image/jpeg', 'profile.jpg'));
    expect(onSaved).toHaveBeenCalledWith('member-1');
  }, 15000);
});
