import { describe, expect, it, jest } from '@jest/globals';
import { fireEvent } from '@testing-library/react-native';
import { renderWithProviders } from '../../test/render';
import { useSessionStore } from '../../store/sessionStore';
import { useWorkspaceStore } from '../../store/workspaceStore';
import { WorkspaceSwitcher } from './WorkspaceSwitcher';

describe('WorkspaceSwitcher', () => {
  it('does not apply another tenant logo to the current workspace row', async () => {
    useSessionStore.setState({
      currentTenant: { id: 'personal', name: 'Ada Personal', type: 'Personal', status: 'Active' },
    });
    useWorkspaceStore.setState({
      logoUrl: 'https://cdn.example/gym.png',
      workspaces: [
        { id: 'personal', name: 'Ada Personal', type: 'Personal', status: 'Active', roles: ['PersonalOwner'] },
        { id: 'gym', name: 'North Gym', type: 'Gym', status: 'Active', roles: ['GymOwner'] },
      ],
    });
    const onSelect = jest.fn();
    const view = await renderWithProviders(<WorkspaceSwitcher onSelect={onSelect} />);

    expect(view.getByRole('button', { name: 'LifeFit AI' })).toBeTruthy();
    expect(view.getByRole('button', { name: 'North Gym' })).toBeTruthy();
    fireEvent.press(view.getByRole('button', { name: 'North Gym' }));
    expect(onSelect).toHaveBeenCalledWith(expect.objectContaining({ id: 'gym' }));
  });
});
