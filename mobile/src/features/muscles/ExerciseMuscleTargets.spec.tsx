import { describe, expect, it } from '@jest/globals';
import { renderWithProviders } from '../../test/render';
import { ExerciseMuscleTargets } from './ExerciseMuscleTargets';

describe('ExerciseMuscleTargets', () => {
  it('shows every primary illustration before the secondary thumbnails', async () => {
    const view = await renderWithProviders(
      <ExerciseMuscleTargets exercise={{ id: 'session-exercise', name: 'Deadlift', muscleGroup: 'Legs' }} />,
    );
    const labels = view.getAllByLabelText(/Hamstrings|Glutes|Lower back|Forearms|Upper back|Quadriceps|Core \(abs\)/);
    expect(labels.map((label) => label.props.accessibilityLabel)).toEqual([
      'Hamstrings',
      'Glutes',
      'Lower back',
      'Forearms',
      'Upper back',
      'Quadriceps',
      'Core (abs)',
    ]);
  });

  it('shows a neutral fallback when the exercise has no catalog mapping', async () => {
    const view = await renderWithProviders(
      <ExerciseMuscleTargets exercise={{ id: 'exercise-1', name: 'Squat', muscleGroup: 'Legs' }} variant="compact" />,
    );
    expect(view.getByText('Muscle illustration not available for this exercise.')).toBeTruthy();
    expect(view.queryByText('Quadriceps')).toBeNull();
    expect(view.queryByText('Glutes')).toBeNull();
  });
});
