import { describe, expect, it } from '@jest/globals';
import { Text } from 'react-native';
import { renderWithProviders } from '../../test/render';
import { RequireAccess } from './RequireAccess';

describe('RequireAccess', () => {
  it('hides children when the UX gate is closed', async () => {
    const view = await renderWithProviders(
      <RequireAccess allowed={false}>
        <Text>Members</Text>
      </RequireAccess>,
    );

    expect(view.queryByText('Members')).toBeNull();
    expect(view.getByText('You do not have access to this area.')).toBeTruthy();
  });
});
