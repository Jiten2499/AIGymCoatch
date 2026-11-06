import { beforeEach, describe, expect, it, jest } from '@jest/globals';
import { fireEvent, waitFor } from '@testing-library/react-native';
import { useSessionStore } from '../../../store/sessionStore';
import { renderWithProviders } from '../../../test/render';
import { saveLead } from '../api/leads.api';
import { LeadForm } from './LeadFormScreen';

jest.mock('../api/leads.api', () => ({
  saveLead: jest.fn(),
  leadsQueryKey: jest.requireActual<typeof import('../api/leads.api')>('../api/leads.api').leadsQueryKey,
  listLeads: jest.fn(),
  getLead: jest.fn(),
  assignLead: jest.fn(),
  listFollowUps: jest.fn(),
  listFollowUpQueue: jest.fn(),
  addFollowUp: jest.fn(),
  completeFollowUp: jest.fn(),
  addTrial: jest.fn(),
  changeTrialStatus: jest.fn(),
  convertLead: jest.fn(),
  markLeadLost: jest.fn(),
}));
jest.mock('@react-navigation/native', () => {
  const actual = jest.requireActual<typeof import('@react-navigation/native')>('@react-navigation/native');
  return { ...actual, useNavigation: () => ({ navigate: jest.fn(), replace: jest.fn() }) };
});

describe('lead form', () => {
  beforeEach(() => {
    useSessionStore.setState({
      status: 'authenticated',
      accessToken: 'token',
      refreshToken: 'refresh',
      expiresAt: '2099-01-01T00:00:00Z',
      user: { id: 'user-1', email: 'owner@gym.test', firstName: 'Gym', lastName: 'Owner' },
      currentTenant: { id: 'tenant-a', name: 'North Gym', type: 'Gym', status: 'Active' },
    });
  });

  it('saves a lead', async () => {
    jest.mocked(saveLead).mockResolvedValue({
      id: 'lead-1',
      fullName: 'Ada Prospect',
      mobile: '+15551212',
      source: 'WalkIn',
      status: 'New',
      interestedPackage: null,
      assignedTo: null,
      nextFollowUpAt: null,
      trialAt: null,
      email: '',
      notes: '',
      interestedPlanId: null,
      assignedUserId: null,
      convertedMemberId: null,
      lostReason: null,
      createdAt: '2026-10-01T00:00:00Z',
    });
    const saved = jest.fn();
    const view = await renderWithProviders(<LeadForm onSaved={saved} />);
    fireEvent.changeText(view.getByLabelText('Full name'), 'Ada Prospect');
    fireEvent.changeText(view.getByLabelText('Mobile'), '+15551212');
    fireEvent.press(view.getByText('Save lead'));
    await waitFor(() => expect(saveLead).toHaveBeenCalled());
    expect(saved).toHaveBeenCalled();
  });
});
