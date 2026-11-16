import type { ProgramDay, WorkoutProgram, WorkoutSession } from '../fitness/types';

export function dayDate(day: ProgramDay): string | null {
  const match = day.description?.match(/^(\d{4}-\d{2}-\d{2})/);
  return match?.[1] ?? null;
}

export function completedDayIds(program: WorkoutProgram, sessions: WorkoutSession[]): Set<string> {
  const ids = new Set(program.days.map((day) => day.id));
  return new Set(sessions.filter((session) => session.status === 'Completed' && session.workoutProgramDayId && ids.has(session.workoutProgramDayId)).map((session) => session.workoutProgramDayId as string));
}

export function nextTrainingDay(program: WorkoutProgram, done: Set<string>): ProgramDay | null {
  return program.days.find((day) => !day.restDay && !done.has(day.id)) ?? program.days.find((day) => !day.restDay) ?? null;
}

export function averageMinutes(program: WorkoutProgram): number | null {
  const timed = program.days.filter((day) => !day.restDay);
  if (timed.length === 0) {
    return null;
  }
  const total = timed.reduce((sum, day) => sum + day.exercises.reduce((inner, exercise) => inner + (exercise.targetDurationSeconds ?? 0), 0), 0);
  if (total <= 0) {
    return null;
  }
  return Math.round(total / timed.length / 60);
}

export function splitDay(day: ProgramDay): { warmup: ProgramDay['exercises']; main: ProgramDay['exercises']; cooldown: ProgramDay['exercises'] } {
  const warmup = day.exercises.filter((exercise) => /warm/i.test(`${exercise.notes ?? ''}`));
  const cooldown = day.exercises.filter((exercise) => /cool|stretch/i.test(`${exercise.notes ?? ''}`));
  const used = new Set([...warmup, ...cooldown].map((exercise) => exercise.id));
  return { warmup, cooldown, main: day.exercises.filter((exercise) => !used.has(exercise.id)) };
}
