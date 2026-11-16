// Starter exercise keys are an exact lookup only. A qualified coach must review them before production.
// An exercise name is never used to guess a muscle when that key or an exact catalog label is missing.
import { isMuscleGroupId, muscleGroupIdFromLabel, type MuscleGroupId } from './catalog';
import starter from './exercise-muscle-mapping.example.json';

export type MuscleTargetExercise = {
  id: string;
  name: string;
  muscleGroup: string | null;
};

export type ResolvedMuscleTargets = {
  primary: MuscleGroupId[];
  secondary: MuscleGroupId[];
  source: 'starter' | 'catalog-label';
};

type StarterEntry = {
  primaryMuscles: string[];
  secondaryMuscles: string[];
};

const starterExercises: Record<string, StarterEntry> = starter.exercises;

export function acceptedMuscleIds(ids: readonly string[]): MuscleGroupId[] {
  const seen = new Set<string>();
  const accepted: MuscleGroupId[] = [];
  for (const id of ids) {
    if (!isMuscleGroupId(id) || seen.has(id)) continue;
    seen.add(id);
    accepted.push(id);
  }
  return accepted;
}

export function targetsFromLists(primaryIds: readonly string[], secondaryIds: readonly string[]): Pick<ResolvedMuscleTargets, 'primary' | 'secondary'> | null {
  const primary = acceptedMuscleIds(primaryIds);
  const primarySet = new Set(primary);
  const secondary = acceptedMuscleIds(secondaryIds).filter((id) => !primarySet.has(id));
  if (primary.length === 0 && secondary.length === 0) return null;
  return { primary, secondary };
}

function exerciseSlug(value: string): string {
  return value.trim().toLowerCase().replace(/['’]/g, '').replace(/[^a-z0-9]+/g, '-').replace(/^-+|-+$/g, '');
}

function starterEntry(exercise: MuscleTargetExercise): StarterEntry | null {
  const byId = starterExercises[exercise.id];
  if (byId) return byId;
  const byName = starterExercises[exerciseSlug(exercise.name)];
  return byName ?? null;
}

export function resolveExerciseMuscles(exercise: MuscleTargetExercise): ResolvedMuscleTargets | null {
  const mapped = starterEntry(exercise);
  if (mapped) {
    const lists = targetsFromLists(mapped.primaryMuscles, mapped.secondaryMuscles);
    if (lists) return { ...lists, source: 'starter' };
  }
  const labeled = muscleGroupIdFromLabel(exercise.muscleGroup);
  if (!labeled) return null;
  return { primary: [labeled], secondary: [], source: 'catalog-label' };
}
