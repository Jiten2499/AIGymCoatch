import { describe, expect, it } from '@jest/globals';
import { addExercise, blankExercise, planChanges, toDraft, type PlanDraft } from './planDraft';

const draft: PlanDraft = {
  name: 'AI Plan',
  description: '',
  goalType: 'Strength',
  difficulty: 'Intermediate',
  durationWeeks: 4,
  frequencyPerWeek: 3,
  days: [{
    dayNumber: 1,
    name: 'Push',
    description: '',
    restDay: false,
    exercises: [blankExercise('exercise-1')],
  }],
};

describe('plan draft editing', () => {
  it('blocks a duplicate exercise and records an added exercise', () => {
    const blocked = addExercise(draft, 0, 'exercise-1');
    expect(blocked.duplicate).toBe(true);
    expect(blocked.draft.days[0].exercises).toHaveLength(1);

    const added = addExercise(draft, 0, 'exercise-2');
    expect(added.duplicate).toBe(false);
    expect(planChanges(draft, added.draft)).toEqual([
      { dayName: 'Push', exerciseId: 'exercise-2', kind: 'added' },
    ]);
  });

  it('round-trips a program day without inventing muscles', () => {
    const program = toDraft({
      id: 'program-1',
      name: 'AI Plan',
      description: null,
      goalType: 'Strength',
      difficulty: 'Intermediate',
      durationWeeks: 4,
      frequencyPerWeek: 3,
      status: 'Active',
      isOwnedByUser: true,
      days: [{ id: 'day-1', dayNumber: 1, name: 'Push', description: null, restDay: false, exercises: [] }],
    });
    expect(program.days[0].exercises).toEqual([]);
  });
});
