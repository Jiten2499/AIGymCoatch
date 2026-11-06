import { beforeEach, describe, expect, it, jest } from '@jest/globals';
import { fireEvent, waitFor } from '@testing-library/react-native';
import { ApiError } from '../../../services/api/problem';
import { useSessionStore } from '../../../store/sessionStore';
import { renderWithProviders } from '../../../test/render';
import { listFeatureFlags } from '../../licensing/api/licenses.api';
import { decideApprovalRequest, getApprovalRequest, listApprovalRequests } from '../api/approvals.api';
import { ApprovalDetail } from './ApprovalDetailScreen';
import { ApprovalList } from './TasksScreen';

jest.mock('../api/approvals.api', () => ({
  listApprovalRequests: jest.fn(),
  getApprovalRequest: jest.fn(),
  decideApprovalRequest: jest.fn(),
  getApprovalCount: jest.fn(),
  approvalQueryKey: jest.requireActual<typeof import('../api/approvals.api')>('../api/approvals.api').approvalQueryKey,
}));
jest.mock('../../licensing/api/licenses.api', () => ({ listFeatureFlags: jest.fn() }));
jest.mock('@react-navigation/native', () => {
  const actual = jest.requireActual<typeof import('@react-navigation/native')>('@react-navigation/native');
  return { ...actual, useNavigation: () => ({ navigate: jest.fn() }) };
});

const request = {
  id: 'request-1',
  requestType: 'MembershipFreeze' as const,
  title: 'Membership freeze',
  summary: '7 days',
  requestedByName: 'Amit',
  requestedAt: '2026-10-08T10:30:00Z',
  relatedLabel: 'Rahul Sharma',
  amount: null,
  currency: 'INR',
  days: 7,
  status: 'Pending' as const,
  reason: 'Travel',
  memberProfileId: 'member-1',
  sourceId: 'membership-1',
  originalEndDate: '2026-10-31',
  proposedEndDate: '2026-11-07',
  decisionComment: null,
  history: [{ action: 'Requested', at: '2026-10-08T10:30:00Z', actorName: 'Amit', comment: 'Travel' }],
};

describe('approval tasks', () => {
  beforeEach(() => {
    useSessionStore.setState({
      status: 'authenticated',
      accessToken: 'token',
      refreshToken: 'refresh',
      expiresAt: '2099-01-01T00:00:00Z',
      user: { id: 'user-1', email: 'ada@example.com', firstName: 'Ada', lastName: 'Lovelace' },
      currentTenant: { id: 'tenant-a', name: 'Demo Gym', type: 'Business', status: 'Active' },
    });
    jest.mocked(listFeatureFlags).mockResolvedValue([{ featureCode: 'MEMBERSHIP_MANAGEMENT', enabled: true }]);
    jest.mocked(listApprovalRequests).mockResolvedValue({ items: [request], page: 1, pageSize: 20, totalCount: 1 });
    jest.mocked(getApprovalRequest).mockResolvedValue(request);
    jest.mocked(decideApprovalRequest).mockResolvedValue({ ...request, status: 'Approved' });
  });

  it('shows pending approval requests and opens review', async () => {
    const view = await renderWithProviders(<ApprovalList enabled />);
    expect(await view.findByText('Membership freeze')).toBeTruthy();
    expect(view.getByText('Rahul Sharma')).toBeTruthy();
    expect(jest.mocked(listApprovalRequests)).toHaveBeenCalledWith('Pending', '', 1, expect.anything());
  });

  it('shows an empty inbox', async () => {
    jest.mocked(listApprovalRequests).mockResolvedValue({ items: [], page: 1, pageSize: 20, totalCount: 0 });
    const view = await renderWithProviders(<ApprovalList enabled />);
    expect(await view.findByText('No approval requests are waiting.')).toBeTruthy();
  });

  it('shows when the user cannot approve', async () => {
    jest.mocked(listApprovalRequests).mockRejectedValue(new ApiError('You do not have permission to perform this action.', 403, null));
    const view = await renderWithProviders(<ApprovalList enabled />);
    expect(await view.findByText('You do not have permission to review approval requests.')).toBeTruthy();
  });

  it('approves and rejects from the detail view', async () => {
    const view = await renderWithProviders(<ApprovalDetail id="request-1" />);
    expect(await view.findByText('Travel')).toBeTruthy();
    fireEvent.press(view.getByRole('button', { name: 'Approve' }));
    await waitFor(() => expect(jest.mocked(decideApprovalRequest)).toHaveBeenCalledWith('request-1', true, ''));
    fireEvent.press(view.getByRole('button', { name: 'Reject' }));
    expect(view.getByText('A reason is required to reject this request.')).toBeTruthy();
    fireEvent.changeText(view.getByLabelText('Comment'), 'Not this month');
    fireEvent.press(view.getByRole('button', { name: 'Reject' }));
    await waitFor(() => expect(jest.mocked(decideApprovalRequest)).toHaveBeenCalledWith('request-1', false, 'Not this month'));
  });
});
