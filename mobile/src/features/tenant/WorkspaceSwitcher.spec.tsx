import { describe, expect, it, jest } from '@jest/globals';
import { fireEvent, waitFor } from '@testing-library/react-native';
import { renderWithProviders } from '../../test/render';
import { useSessionStore } from '../../store/sessionStore';
import { useWorkspaceStore } from '../../store/workspaceStore';
import { WorkspaceSwitcher } from './WorkspaceSwitcher';

describe('WorkspaceSwitcher', () => {
  it('hides when the member has only one active workspace', async () => {
    useSessionStore.setState({
      currentTenant: { id: 'gym', name: 'North Gym', type: 'Gym', status: 'Active' },
    });
    useWorkspaceStore.setState({
      logoUrl: null,
      workspaces: [
        { id: 'gym', name: 'North Gym', type: 'Gym', status: 'Active', roles: ['Member'] },
        { id: 'gone', name: 'Old Gym', type: 'Gym', status: 'Inactive', roles: ['Member'] },
      ],
    });
    const view = await renderWithProviders(<WorkspaceSwitcher onSelect={jest.fn()} />);

    expect(view.queryByText('Switch workspace')).toBeNull();
  });

  it('shows active workspaces in a dropdown and switches when another is chosen', async () => {
    useSessionStore.setState({
      currentTenant: { id: 'personal', name: 'Ada Personal', type: 'Personal', status: 'Active' },
    });
    useWorkspaceStore.setState({
      logoUrl: 'https://cdn.example/gym.png',
      workspaces: [
        { id: 'personal', name: 'Ada Personal', type: 'Personal', status: 'Active', roles: ['PersonalOwner'] },
        { id: 'gym', name: 'North Gym', type: 'Gym', status: 'Active', roles: ['Member'] },
        { id: 'deleted', name: 'Closed Gym', type: 'Gym', status: 'Inactive', roles: ['Member'] },
      ],
    });
    const onSelect = jest.fn();
    const view = await renderWithProviders(<WorkspaceSwitcher onSelect={onSelect} />);

    expect(view.getByText('Switch workspace')).toBeTruthy();
    expect(view.queryByText('North Gym')).toBeNull();
    fireEvent.press(view.getByTestId('workspace-switcher'));
    await waitFor(() => expect(view.getByText('North Gym')).toBeTruthy());
    expect(view.queryByText('Closed Gym')).toBeNull();
    fireEvent.press(view.getByText('North Gym'));
    expect(onSelect).toHaveBeenCalledWith(expect.objectContaining({ id: 'gym' }));
  });
});
