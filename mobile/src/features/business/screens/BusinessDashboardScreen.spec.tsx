import { afterEach, describe, expect, it, jest } from '@jest/globals';
import { cleanup, fireEvent } from '@testing-library/react-native';
import { renderWithProviders } from '../../../test/render';
import { usePreferencesStore } from '../../../store/preferencesStore';
import type { BusinessDashboardOverview } from '../api/dashboard.api';
import { BusinessDashboardView } from './BusinessDashboardScreen';
import type { DashboardChartCardModel } from '../dashboard/DashboardChartCard';
import type { ChartSeries } from '../dashboard/analyticsRange';

const overview: BusinessDashboardOverview = {
  tenant: {
    id: 'gym',
    name: 'North Athletic',
    tenantType: 'Gym',
    status: 'Active',
    logoUrl: null,
    timeZone: 'UTC',
    currentDate: '2026-10-05T08:30:00',
  },
  user: { displayName: 'Ada Lovelace', roles: ['GymOwner'] },
  capabilities: [
    { code: 'Members', enabled: true },
    { code: 'Reports', enabled: false },
  ],
  summary: {
    members: { total: 0, active: 0, newThisMonth: 0 },
    attendance: null,
    memberships: null,
    people: null,
  },
};

function ready(id: string, title: string, series: ChartSeries[]): DashboardChartCardModel {
  return { id, title, status: 'ready', series };
}

describe('BusinessDashboardView', () => {
  afterEach(async () => {
    await cleanup();
    usePreferencesStore.setState({ appearance: 'system', accent: 'lime' });
  });

  it('shows a loading state', async () => {
    const view = await renderWithProviders(<BusinessDashboardView error={null} loading onRetry={jest.fn()} problem={null} />);
    expect(view.getByText('Loading')).toBeTruthy();
  });

  it('shows an error state', async () => {
    const view = await renderWithProviders(<BusinessDashboardView error="The request failed." onRetry={jest.fn()} problem="error" />);
    expect(view.getByText('The request failed.')).toBeTruthy();
    expect(view.getByText('Try again')).toBeTruthy();
  });

  it('shows the workspace and only enabled areas', async () => {
    const view = await renderWithProviders(<BusinessDashboardView error={null} onRetry={jest.fn()} overview={overview} problem={null} />);
    expect(view.getByText('North Athletic')).toBeTruthy();
    expect(view.getByText('Members')).toBeTruthy();
    expect(view.queryByText('Reports')).toBeNull();
    expect(view.getByText('Total 0')).toBeTruthy();
    expect(view.getByText('Active 0')).toBeTruthy();
    expect(view.getByText('New this month 0')).toBeTruthy();
    expect(view.getByText('Recent activity will appear here.')).toBeTruthy();
  });

  it('shows loaded counts and hides a metric the workspace cannot use', async () => {
    const loaded = {
      ...overview,
      capabilities: [
        { code: 'Members', enabled: true },
        { code: 'Attendance', enabled: true },
        { code: 'Reports', enabled: false },
      ],
      summary: {
        members: { total: 5, active: 4, newThisMonth: 1 },
        attendance: null,
        memberships: null,
        people: null,
      },
    };
    const view = await renderWithProviders(<BusinessDashboardView error={null} onRetry={jest.fn()} overview={loaded} problem={null} />);
    expect(view.getByText('Active 4')).toBeTruthy();
    expect(view.queryByText('Check-ins today 0')).toBeNull();
    expect(view.getByText('This metric is not available for this workspace.')).toBeTruthy();
  });

  it('renders growth, attendance, and membership charts, including a zero period', async () => {
    const view = await renderWithProviders(
      <BusinessDashboardView
        charts={[
          ready('members', 'Member growth', [{ key: 'newMembers', label: 'New members', points: [
            { label: 'Oct 01', value: 4, detail: 'Oct 01 · New members' },
            { label: 'Oct 02', value: 0, detail: 'Oct 02 · New members' },
          ] }]),
          ready('attendance', 'Attendance', [{ key: 'checkIns', label: 'Check-ins', points: [{ label: 'Oct 01', value: 43, detail: 'Oct 01 · Check-ins' }] }]),
          ready('memberships', 'Memberships', [{ key: 'newMemberships', label: 'New memberships', points: [{ label: 'Oct 01', value: 2, detail: 'Oct 01 · New memberships' }] }]),
        ]}
        error={null}
        onRetry={jest.fn()}
        overview={overview}
        problem={null}
      />,
    );
    expect(view.getByText('Member growth')).toBeTruthy();
    expect(view.getByText('Attendance')).toBeTruthy();
    expect(view.getByText('Memberships')).toBeTruthy();
    expect(view.getByLabelText('Oct 02 · New members New members 0')).toBeTruthy();
  });

  it('shows an empty chart, an error with retry, and a range change', async () => {
    const onRange = jest.fn();
    const onRetry = jest.fn();
    const view = await renderWithProviders(
      <>
        <BusinessDashboardView
          charts={[
            { id: 'members', title: 'Member growth', status: 'empty', message: 'Not enough member activity yet.', series: [] },
            { id: 'attendance', title: 'Attendance', status: 'error', message: 'Unable to load attendance.', onRetry, series: [] },
          ]}
          error={null}
          onRange={onRange}
          onRetry={jest.fn()}
          overview={overview}
          problem={null}
        />
      </>,
    );
    expect(view.getByText('Not enough member activity yet.')).toBeTruthy();
    expect(view.getByText('Unable to load attendance.')).toBeTruthy();
    await fireEvent.press(view.getByText('Try again'));
    expect(onRetry).toHaveBeenCalled();
    await fireEvent.press(view.getByText('Last 7 days'));
    expect(onRange).toHaveBeenCalledWith('last7');
  });

  it('uses the dark accent color for the selected range', async () => {
    usePreferencesStore.setState({ appearance: 'dark', accent: 'lime' });
    const dark = await renderWithProviders(<BusinessDashboardView error={null} onRetry={jest.fn()} overview={overview} problem={null} />);
    expect(dark.getByRole('button', { selected: true }).props.style).toEqual(expect.objectContaining({ backgroundColor: '#c8f54e' }));
    expect(dark.getByText('Last 30 days').props.style).toEqual(expect.arrayContaining([expect.objectContaining({ color: '#10211a' })]));
  });

  it('uses the light accent color for the selected range', async () => {
    usePreferencesStore.setState({ appearance: 'light', accent: 'lime' });
    const light = await renderWithProviders(<BusinessDashboardView error={null} onRetry={jest.fn()} overview={overview} problem={null} />);
    expect(light.getByRole('button', { selected: true }).props.style).toEqual(expect.objectContaining({ backgroundColor: '#4f6b10' }));
    expect(light.getByText('Last 30 days').props.style).toEqual(expect.arrayContaining([expect.objectContaining({ color: '#f7faf7' })]));
  });

  it('renders attention items and structured AI insights', async () => {
    const onRefresh = jest.fn();
    const view = await renderWithProviders(
      <BusinessDashboardView
        error={null}
        insights={{
          onRefresh,
          report: {
            attention: [{ type: 'MembershipExpiry', priority: 'High', title: 'Memberships expiring soon', description: '18 memberships expire in the next 7 days.', value: 18, action: 'Review memberships' }],
            ai: {
              status: 'Ready',
              summary: 'New members increased.',
              insights: [{ type: 'Positive', title: 'Growth', message: 'New members changed by 7.', priority: 'Low' }],
              recommendations: [{ title: 'Review renewals', description: 'Look at upcoming renewals.' }],
            },
          },
          status: 'ready',
        }}
        onRetry={jest.fn()}
        overview={overview}
        problem={null}
      />,
    );
    expect(view.getByText('18 memberships expire in the next 7 days.')).toBeTruthy();
    expect(view.getByText('New members increased.')).toBeTruthy();
    expect(view.getByText('New members changed by 7.')).toBeTruthy();
    await fireEvent.press(view.getByText('Refresh insights'));
    expect(onRefresh).toHaveBeenCalled();
  });

  it('shows AI loading, empty, unavailable, and plan states without blocking the workspace', async () => {
    const loading = await renderWithProviders(<BusinessDashboardView error={null} insights={{ status: 'loading' }} onRetry={jest.fn()} overview={overview} problem={null} />);
    expect(loading.getByText('North Athletic')).toBeTruthy();
    expect(loading.getByText('Analyzing your business data...')).toBeTruthy();
    await loading.unmount();

    const empty = await renderWithProviders(<BusinessDashboardView error={null} insights={{ report: { attention: [], ai: { status: 'Empty', summary: null, insights: [], recommendations: [] } }, status: 'ready' }} onRetry={jest.fn()} overview={overview} problem={null} />);
    expect(empty.getByText('Not enough business activity to generate meaningful AI insights yet.')).toBeTruthy();
    expect(empty.getByText('No attention items for this period.')).toBeTruthy();
    await empty.unmount();

    const unavailable = await renderWithProviders(<BusinessDashboardView error={null} insights={{ onRetry: jest.fn(), report: { attention: [], ai: { status: 'Unavailable', summary: null, insights: [], recommendations: [] } }, status: 'ready' }} onRetry={jest.fn()} overview={overview} problem={null} />);
    expect(unavailable.getByText('AI insights are temporarily unavailable.')).toBeTruthy();
    expect(unavailable.getByText('Total 0')).toBeTruthy();
    await unavailable.unmount();

    const disabled = await renderWithProviders(<BusinessDashboardView error={null} insights={{ report: { attention: [], ai: { status: 'Disabled', summary: null, insights: [], recommendations: [] } }, status: 'ready' }} onRetry={jest.fn()} overview={overview} problem={null} />);
    expect(disabled.getByText('AI insights are not included in your current plan.')).toBeTruthy();
  });

  it('shows a suspended workspace without metrics', async () => {
    const view = await renderWithProviders(<BusinessDashboardView error="This workspace is suspended." onRetry={jest.fn()} problem="suspended" />);
    expect(view.getByText('This workspace is unavailable')).toBeTruthy();
    expect(view.queryByText('Members')).toBeNull();
  });
});
