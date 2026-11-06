import { beforeEach, describe, expect, it, jest } from '@jest/globals';
import { fireEvent, waitFor } from '@testing-library/react-native';
import { ApiError } from '../../../services/api/problem';
import { useSessionStore } from '../../../store/sessionStore';
import { renderWithProviders } from '../../../test/render';
import { listFeatureFlags } from '../../licensing/api/licenses.api';
import { addFollowUp, addTrial, assignLead, completeFollowUp, convertLead, getLead, leadsQueryKey, listFollowUpQueue, listFollowUps, listLeads, markLeadLost } from '../api/leads.api';
import { LeadDetail } from './LeadDetailScreen';
import { LeadList } from './LeadsScreen';
import { LeadsScreen } from './LeadsScreen';

jest.mock('../api/leads.api', () => ({
  listLeads: jest.fn(),
  getLead: jest.fn(),
  saveLead: jest.fn(),
  assignLead: jest.fn(),
  listFollowUps: jest.fn(),
  listFollowUpQueue: jest.fn(),
  addFollowUp: jest.fn(),
  completeFollowUp: jest.fn(),
  addTrial: jest.fn(),
  changeTrialStatus: jest.fn(),
  convertLead: jest.fn(),
  markLeadLost: jest.fn(),
  leadsQueryKey: jest.requireActual<typeof import('../api/leads.api')>('../api/leads.api').leadsQueryKey,
}));
jest.mock('../../licensing/api/licenses.api', () => ({ listFeatureFlags: jest.fn() }));
jest.mock('@react-navigation/native', () => {
  const actual = jest.requireActual<typeof import('@react-navigation/native')>('@react-navigation/native');
  return { ...actual, useNavigation: () => ({ navigate: jest.fn(), replace: jest.fn() }) };
});

const lead = {
  id: 'lead-1',
  fullName: 'Ada Prospect',
  mobile: '+15551212',
  source: 'WalkIn',
  status: 'New',
  interestedPackage: 'Annual',
  assignedTo: 'Sam Sales',
  nextFollowUpAt: '2026-10-07T09:00:00Z',
  trialAt: null,
  email: 'ada@gym.test',
  notes: 'Asked about annual',
  interestedPlanId: null,
  assignedUserId: null,
  convertedMemberId: null,
  lostReason: null,
  createdAt: '2026-10-01T00:00:00Z',
};

describe('gym crm leads', () => {
  beforeEach(() => {
    useSessionStore.setState({
      status: 'authenticated',
      accessToken: 'token',
      refreshToken: 'refresh',
      expiresAt: '2099-01-01T00:00:00Z',
      user: { id: 'user-1', email: 'owner@gym.test', firstName: 'Gym', lastName: 'Owner' },
      currentTenant: { id: 'tenant-a', name: 'North Gym', type: 'Gym', status: 'Active' },
    });
    jest.mocked(listFeatureFlags).mockResolvedValue([{ featureCode: 'GYM_CRM', enabled: true }]);
    jest.mocked(listLeads).mockResolvedValue({ items: [lead], page: 1, pageSize: 20, totalCount: 1 });
    jest.mocked(listFollowUpQueue).mockResolvedValue({ items: [], page: 1, pageSize: 20, totalCount: 0 });
    jest.mocked(getLead).mockResolvedValue(lead);
    jest.mocked(listFollowUps).mockResolvedValue([{ id: 'follow-1', leadId: 'lead-1', scheduledAt: '2026-10-07T09:00:00Z', completedAt: null, notes: 'Called lead', outcome: null, leadName: 'Ada Prospect' }]);
  });

  it('keeps the workspace in the lead query key', () => {
    expect(leadsQueryKey('tenant-a', 'Ada', 'New', 'WalkIn', 'today', 1)).not.toEqual(leadsQueryKey('tenant-b', 'Ada', 'New', 'WalkIn', 'today', 1));
  });

  it('searches and filters the lead list', async () => {
    const view = await renderWithProviders(<LeadList enabled />);
    expect(await view.findByText('Ada Prospect')).toBeTruthy();
    fireEvent.changeText(view.getByLabelText('Search leads'), 'Ada');
    await waitFor(() => expect(listLeads).toHaveBeenCalledWith('Ada', '', '', '', 1, expect.anything()));
    fireEvent.press(view.getByText('Status'));
    await waitFor(() => expect(listLeads).toHaveBeenCalledWith('Ada', 'New', '', '', 1, expect.anything()));
    fireEvent.press(view.getByLabelText('Source'));
    await waitFor(() => expect(listLeads).toHaveBeenCalledWith('Ada', 'New', 'WalkIn', '', 1, expect.anything()));
    fireEvent.press(view.getByText('Today'));
    await waitFor(() => expect(listLeads).toHaveBeenCalledWith('Ada', 'New', 'WalkIn', 'today', 1, expect.anything()));
  }, 15000);

  it('shows empty, error, and unauthorized states', async () => {
    jest.mocked(listLeads).mockResolvedValue({ items: [], page: 1, pageSize: 20, totalCount: 0 });
    const empty = await renderWithProviders(<LeadList enabled />);
    expect(await empty.findByText('No leads in this workspace.')).toBeTruthy();
    jest.mocked(listLeads).mockReset();
    jest.mocked(listLeads).mockRejectedValue(new ApiError('The request failed.', 500, { detail: 'The request failed.' }));
    const failed = await renderWithProviders(<LeadList enabled />);
    expect(await failed.findByText('The request failed.')).toBeTruthy();
    jest.mocked(listLeads).mockReset();
    jest.mocked(listLeads).mockRejectedValue(new ApiError('You do not have permission to perform this action.', 403, { detail: 'You do not have permission to perform this action.' }));
    const denied = await renderWithProviders(<LeadList enabled />);
    expect(await denied.findByText('You do not have permission to use CRM in this workspace.')).toBeTruthy();
  });

  it('does not query when the feature is off', async () => {
    jest.mocked(listLeads).mockClear();
    const hidden = await renderWithProviders(<LeadList enabled={false} />);
    expect(hidden.queryByText('Ada Prospect')).toBeNull();
    expect(listLeads).not.toHaveBeenCalled();
    jest.mocked(listFeatureFlags).mockResolvedValue([]);
    const gated = await renderWithProviders(<LeadsScreen />);
    expect(await gated.findByText('This feature is not enabled for the current workspace.')).toBeTruthy();
  });

  it('assigns, follows up, schedules a trial, converts, and marks lost', async () => {
    jest.mocked(assignLead).mockResolvedValue(lead);
    jest.mocked(addFollowUp).mockResolvedValue({ id: 'follow-1', leadId: 'lead-1', scheduledAt: '2026-10-08T00:00:00Z', completedAt: null, notes: 'Follow-up', outcome: null, leadName: 'Ada Prospect' });
    jest.mocked(completeFollowUp).mockResolvedValue({ id: 'follow-1', leadId: 'lead-1', scheduledAt: '2026-10-08T00:00:00Z', completedAt: '2026-10-08T00:00:00Z', notes: 'Follow-up', outcome: 'Completed', leadName: 'Ada Prospect' });
    jest.mocked(addTrial).mockResolvedValue({ id: 'trial-1', leadId: 'lead-1', scheduledAt: '2026-10-08T00:00:00Z', status: 'Scheduled', notes: 'Trial', outcome: null });
    jest.mocked(convertLead).mockResolvedValue({ ...lead, status: 'Converted' });
    jest.mocked(markLeadLost).mockResolvedValue({ ...lead, status: 'Lost', lostReason: 'NotInterested' });
    const view = await renderWithProviders(<LeadDetail enabled leadId="lead-1" />);
    expect(await view.findByText('Ada Prospect')).toBeTruthy();
    expect(await view.findByText(/Called lead/)).toBeTruthy();
    fireEvent.press(view.getByText('Assign'));
    fireEvent.press(view.getByText('Follow-up'));
    fireEvent.press(view.getByText('Complete follow-up'));
    fireEvent.press(view.getByText('Schedule trial'));
    fireEvent.press(view.getByText('Convert to member'));
    fireEvent.press(view.getByText('Mark lost'));
    await waitFor(() => expect(markLeadLost).toHaveBeenCalled());
    expect(assignLead).toHaveBeenCalled();
    expect(addFollowUp).toHaveBeenCalled();
    expect(completeFollowUp).toHaveBeenCalled();
    expect(addTrial).toHaveBeenCalled();
    expect(convertLead).toHaveBeenCalled();
  }, 15000);
});
