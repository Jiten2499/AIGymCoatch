import { describe, expect, it } from '@jest/globals';
import { acceptedMuscleIds, resolveExerciseMuscles, targetsFromLists } from './resolveMuscles';

describe('resolveExerciseMuscles', () => {
  it('uses a canonical starter id and keeps every primary and secondary target', () => {
    const resolved = resolveExerciseMuscles({ id: 'member-exercise', name: 'Deadlift', muscleGroup: 'Legs' });
    expect(resolved).toEqual({
      primary: ['hamstrings', 'glutes', 'lower_back'],
      secondary: ['forearms', 'upper_back', 'quadriceps', 'core_abs'],
      source: 'starter',
    });
  });

  it('matches a starter key on the exercise id before the free-text muscle label', () => {
    const resolved = resolveExerciseMuscles({ id: 'dumbbell-bench-press', name: 'Press', muscleGroup: 'Legs' });
    expect(resolved?.primary).toEqual(['chest']);
    expect(resolved?.secondary).toEqual(['shoulders', 'triceps']);
  });

  it('accepts an exact catalog label and rejects an unknown label', () => {
    expect(resolveExerciseMuscles({ id: 'exercise-1', name: 'Custom press', muscleGroup: 'Chest' })?.primary).toEqual(['chest']);
    expect(resolveExerciseMuscles({ id: 'exercise-1', name: 'Custom press', muscleGroup: 'upper_back' })?.primary).toEqual(['upper_back']);
    expect(resolveExerciseMuscles({ id: 'exercise-1', name: 'Squat', muscleGroup: 'Legs' })).toBeNull();
  });

  it('does not infer a muscle from an exercise name when no mapping exists', () => {
    expect(resolveExerciseMuscles({ id: 'exercise-1', name: 'Squat', muscleGroup: null })).toBeNull();
    expect(resolveExerciseMuscles({ id: 'exercise-1', name: 'Lat pulldown variation', muscleGroup: 'Back' })).toBeNull();
  });

  it('drops unknown ids and a muscle listed as both primary and secondary', () => {
    expect(acceptedMuscleIds(['chest', 'not_a_muscle', 'chest'])).toEqual(['chest']);
    expect(targetsFromLists(['chest', 'not_a_muscle'], ['shoulders', 'chest', 'also_unknown'])).toEqual({
      primary: ['chest'],
      secondary: ['shoulders'],
    });
    expect(targetsFromLists(['not_a_muscle'], ['also_unknown'])).toBeNull();
  });
});
