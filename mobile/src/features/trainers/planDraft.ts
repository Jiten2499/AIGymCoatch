import type { ProgramDay, ProgramExercise, WorkoutProgram } from '../fitness/types';
import type { MemberProgramUpdate } from './api/workspace.api';

export type DraftExercise = {
  exerciseId: string;
  sets: string;
  targetReps: string;
  targetDurationSeconds: string;
  targetDistance: string;
  targetWeight: string;
  restSeconds: string;
  notes: string;
};

export type DraftDay = {
  dayNumber: number;
  name: string;
  description: string;
  restDay: boolean;
  exercises: DraftExercise[];
};

export type PlanDraft = {
  name: string;
  description: string;
  goalType: string;
  difficulty: string;
  durationWeeks: number;
  frequencyPerWeek: number;
  days: DraftDay[];
};

export type PlanChange = {
  dayName: string;
  exerciseId: string;
  kind: 'added' | 'removed' | 'modified';
};

export function toDraft(program: WorkoutProgram): PlanDraft {
  return {
    name: program.name,
    description: program.description ?? '',
    goalType: program.goalType,
    difficulty: program.difficulty,
    durationWeeks: program.durationWeeks,
    frequencyPerWeek: program.frequencyPerWeek,
    days: program.days.map((day) => toDraftDay(day)),
  };
}

export function fromDraft(draft: PlanDraft): MemberProgramUpdate {
  return {
    name: draft.name,
    description: draft.description.trim() || null,
    goalType: draft.goalType,
    difficulty: draft.difficulty,
    durationWeeks: draft.durationWeeks,
    frequencyPerWeek: draft.frequencyPerWeek,
    days: draft.days.map((day) => ({
      dayNumber: day.dayNumber,
      name: day.name,
      description: day.description.trim() || null,
      restDay: day.restDay,
      exercises: day.restDay ? [] : day.exercises.map((exercise) => ({
        exerciseId: exercise.exerciseId,
        sets: parseOptionalInt(exercise.sets),
        targetReps: parseOptionalInt(exercise.targetReps),
        targetDurationSeconds: parseOptionalInt(exercise.targetDurationSeconds),
        targetDistance: parseOptionalNumber(exercise.targetDistance),
        targetWeight: parseOptionalNumber(exercise.targetWeight),
        restSeconds: parseOptionalInt(exercise.restSeconds),
        notes: exercise.notes.trim() || null,
      })),
    })),
  };
}

export function weeksOf(draft: PlanDraft) {
  const maxDay = draft.days.reduce((max, day) => Math.max(max, day.dayNumber), 1);
  const count = Math.max(draft.durationWeeks, Math.ceil(maxDay / 7));
  return Array.from({ length: count }, (_, weekIndex) => {
    const start = weekIndex * 7 + 1;
    return {
      week: weekIndex + 1,
      days: draft.days
        .map((day, index) => ({ day, index }))
        .filter((item) => item.day.dayNumber >= start && item.day.dayNumber <= start + 6),
    };
  });
}

export function blankExercise(exerciseId: string): DraftExercise {
  return {
    exerciseId,
    sets: '3',
    targetReps: '10',
    targetDurationSeconds: '',
    targetDistance: '',
    targetWeight: '',
    restSeconds: '60',
    notes: '',
  };
}

export function moveExercise(draft: PlanDraft, dayIndex: number, from: number, to: number): PlanDraft {
  const exercises = [...draft.days[dayIndex].exercises];
  if (to < 0 || to >= exercises.length || from === to) {
    return draft;
  }
  const [item] = exercises.splice(from, 1);
  exercises.splice(to, 0, item);
  return updateDay(draft, dayIndex, { exercises });
}

export function addExercise(draft: PlanDraft, dayIndex: number, exerciseId: string): { draft: PlanDraft; duplicate: boolean } {
  const exercises = draft.days[dayIndex].exercises;
  if (exercises.some((item) => item.exerciseId === exerciseId)) {
    return { draft, duplicate: true };
  }
  return { draft: updateDay(draft, dayIndex, { exercises: [...exercises, blankExercise(exerciseId)] }), duplicate: false };
}

export function replaceExercise(draft: PlanDraft, dayIndex: number, exerciseIndex: number, exerciseId: string): { draft: PlanDraft; duplicate: boolean } {
  const exercises = draft.days[dayIndex].exercises;
  if (exercises.some((item, index) => index !== exerciseIndex && item.exerciseId === exerciseId)) {
    return { draft, duplicate: true };
  }
  return {
    draft: updateDay(draft, dayIndex, {
      exercises: exercises.map((item, index) => (index === exerciseIndex ? { ...item, exerciseId } : item)),
    }),
    duplicate: false,
  };
}

export function duplicateExercise(draft: PlanDraft, dayIndex: number, exerciseIndex: number): PlanDraft {
  const exercises = [...draft.days[dayIndex].exercises];
  exercises.splice(exerciseIndex + 1, 0, { ...exercises[exerciseIndex] });
  return updateDay(draft, dayIndex, { exercises });
}

export function removeExercise(draft: PlanDraft, dayIndex: number, exerciseIndex: number): PlanDraft {
  return updateDay(draft, dayIndex, {
    exercises: draft.days[dayIndex].exercises.filter((_, index) => index !== exerciseIndex),
  });
}

export function updateExercise(draft: PlanDraft, dayIndex: number, exerciseIndex: number, patch: Partial<DraftExercise>): PlanDraft {
  return updateDay(draft, dayIndex, {
    exercises: draft.days[dayIndex].exercises.map((exercise, index) => (index === exerciseIndex ? { ...exercise, ...patch } : exercise)),
  });
}

export function positiveField(value: string): boolean {
  const trimmed = value.trim();
  if (!trimmed) {
    return true;
  }
  return /^\d+$/.test(trimmed) && Number(trimmed) >= 1;
}

export function decimalField(value: string): boolean {
  const trimmed = value.trim();
  if (!trimmed) {
    return true;
  }
  const parsed = Number(trimmed);
  return Number.isFinite(parsed) && parsed > 0;
}

export function exerciseIsValid(exercise: DraftExercise): boolean {
  return positiveField(exercise.sets) && positiveField(exercise.targetReps) && positiveField(exercise.restSeconds) && decimalField(exercise.targetWeight);
}

export function planChanges(baseline: PlanDraft, draft: PlanDraft): PlanChange[] {
  const changes: PlanChange[] = [];
  const count = Math.max(baseline.days.length, draft.days.length);
  for (let index = 0; index < count; index += 1) {
    const before = baseline.days[index];
    const after = draft.days[index];
    const dayName = after?.name ?? before?.name ?? '';
    const beforeIds = new Set((before?.exercises ?? []).map((item) => item.exerciseId));
    const afterIds = new Set((after?.exercises ?? []).map((item) => item.exerciseId));
    for (const exercise of after?.exercises ?? []) {
      if (!beforeIds.has(exercise.exerciseId)) {
        changes.push({ dayName, exerciseId: exercise.exerciseId, kind: 'added' });
      }
    }
    for (const exercise of before?.exercises ?? []) {
      if (!afterIds.has(exercise.exerciseId)) {
        changes.push({ dayName, exerciseId: exercise.exerciseId, kind: 'removed' });
      }
    }
    for (const exercise of after?.exercises ?? []) {
      const previous = before?.exercises.find((item) => item.exerciseId === exercise.exerciseId);
      if (previous && signature(previous) !== signature(exercise)) {
        changes.push({ dayName, exerciseId: exercise.exerciseId, kind: 'modified' });
      }
    }
  }
  return changes;
}

export function updateDay(draft: PlanDraft, index: number, patch: Partial<DraftDay>): PlanDraft {
  return { ...draft, days: draft.days.map((day, dayIndex) => (dayIndex === index ? { ...day, ...patch } : day)) };
}

function toDraftDay(day: ProgramDay): DraftDay {
  return {
    dayNumber: day.dayNumber,
    name: day.name,
    description: day.description ?? '',
    restDay: day.restDay,
    exercises: day.exercises.map((exercise) => toDraftExercise(exercise)),
  };
}

function toDraftExercise(exercise: ProgramExercise): DraftExercise {
  return {
    exerciseId: exercise.exerciseId,
    sets: numberText(exercise.sets),
    targetReps: numberText(exercise.targetReps),
    targetDurationSeconds: numberText(exercise.targetDurationSeconds),
    targetDistance: numberText(exercise.targetDistance),
    targetWeight: numberText(exercise.targetWeight),
    restSeconds: numberText(exercise.restSeconds),
    notes: exercise.notes ?? '',
  };
}

function signature(exercise: DraftExercise) {
  return [exercise.sets, exercise.targetReps, exercise.restSeconds, exercise.targetWeight, exercise.notes].join('|');
}

function numberText(value: number | null) {
  return value === null || value === undefined ? '' : String(value);
}

function parseOptionalInt(value: string) {
  const trimmed = value.trim();
  if (!trimmed) {
    return null;
  }
  const parsed = Number.parseInt(trimmed, 10);
  return Number.isFinite(parsed) ? parsed : null;
}

function parseOptionalNumber(value: string) {
  const trimmed = value.trim();
  if (!trimmed) {
    return null;
  }
  const parsed = Number.parseFloat(trimmed);
  return Number.isFinite(parsed) ? parsed : null;
}
