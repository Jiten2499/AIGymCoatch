import { describe, expect, it, jest } from '@jest/globals';
import { renderWithProviders } from '../../test/render';
import { MuscleGroupIllustration } from './MuscleGroupIllustration';

jest.mock('./images', () => ({
  muscleGroupImage: (id: string) => (id === 'calves' ? undefined : 1),
}));

describe('MuscleGroupIllustration', () => {
  it('renders the catalog illustration for a valid id', async () => {
    const view = await renderWithProviders(<MuscleGroupIllustration mode="primary" muscleGroupId="chest" />);
    expect(view.getByLabelText('Chest')).toBeTruthy();
    expect(view.getByText('Chest')).toBeTruthy();
  });

  it('shows a neutral fallback for an unknown id', async () => {
    const view = await renderWithProviders(<MuscleGroupIllustration mode="secondary" muscleGroupId="not_a_muscle" />);
    expect(view.getByText('Muscle illustration not available for this exercise.')).toBeTruthy();
    expect(view.queryByText('Chest')).toBeNull();
  });

  it('shows a neutral fallback when the catalog image is missing', async () => {
    const view = await renderWithProviders(<MuscleGroupIllustration mode="primary" muscleGroupId="calves" size="full" />);
    expect(view.getByLabelText('Muscle illustration not available for this exercise.')).toBeTruthy();
    expect(view.queryByText('Calves')).toBeNull();
  });
});
