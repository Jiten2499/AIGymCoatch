import { apiClient } from '../../../services/api/client';
import type { PageResult } from '../../../types/api';
import type { ExerciseDetail, FitnessGoal, FitnessProfile, Measurement, MemberDashboard, PersonalRecord, WorkoutProgram, WorkoutSession, WorkoutSet } from '../types';

export async function getProfile(signal?: AbortSignal): Promise<FitnessProfile> {
  const response = await apiClient.get<FitnessProfile>('/fitness/profile', { signal });
  return response.data;
}

export async function saveProfile(body: Omit<FitnessProfile, 'id'>, signal?: AbortSignal): Promise<FitnessProfile> {
  const response = await apiClient.put<FitnessProfile>('/fitness/profile', body, { signal });
  return response.data;
}

export async function listGoals(signal?: AbortSignal): Promise<FitnessGoal[]> {
  const response = await apiClient.get<FitnessGoal[]>('/fitness/goals', { signal });
  return response.data;
}

export async function createGoal(body: { goalType: string; targetValue: number | null; currentValue: number | null; startDate: string; targetDate: string | null }, signal?: AbortSignal): Promise<FitnessGoal> {
  const response = await apiClient.post<FitnessGoal>('/fitness/goals', body, { signal });
  return response.data;
}

export async function getDashboard(signal?: AbortSignal): Promise<MemberDashboard> {
  const response = await apiClient.get<MemberDashboard>('/member/dashboard', { signal });
  return response.data;
}

export async function listMeasurements(signal?: AbortSignal): Promise<PageResult<Measurement>> {
  const response = await apiClient.get<PageResult<Measurement>>('/measurements', { params: { page: 1, pageSize: 20 }, signal });
  return response.data;
}

export async function listSessions(page: number, signal?: AbortSignal): Promise<PageResult<WorkoutSession>> {
  const response = await apiClient.get<PageResult<WorkoutSession>>('/workout-sessions', { params: { page, pageSize: 20 }, signal });
  return response.data;
}

export async function recordMeasurement(body: Omit<Measurement, 'id'>, signal?: AbortSignal): Promise<Measurement> {
  const response = await apiClient.post<Measurement>('/measurements', body, { signal });
  return response.data;
}

export async function listPrograms(page = 1, signal?: AbortSignal): Promise<PageResult<WorkoutProgram>> {
  const response = await apiClient.get<PageResult<WorkoutProgram>>('/workouts', { params: { page, pageSize: 20 }, signal });
  return response.data;
}

export async function getProgram(id: string, signal?: AbortSignal): Promise<WorkoutProgram> {
  const response = await apiClient.get<WorkoutProgram>(`/workouts/${id}`, { signal });
  return response.data;
}

export async function startProgram(id: string, dayId: string | null, signal?: AbortSignal): Promise<WorkoutSession> {
  const response = await apiClient.post<WorkoutSession>(`/workouts/${id}/start`, null, { params: dayId ? { dayId } : undefined, signal });
  return response.data;
}

export async function getSession(id: string, signal?: AbortSignal): Promise<WorkoutSession> {
  const response = await apiClient.get<WorkoutSession>(`/workout-sessions/${id}`, { signal });
  return response.data;
}

export async function completeSession(id: string, signal?: AbortSignal): Promise<WorkoutSession> {
  const response = await apiClient.post<WorkoutSession>(`/workout-sessions/${id}/complete`, null, { signal });
  return response.data;
}

export async function addSet(sessionExerciseId: string, body: { reps: number | null; weight: number | null; durationSeconds: number | null; distance: number | null; rpe: number | null; completed: boolean }, signal?: AbortSignal): Promise<WorkoutSet> {
  const response = await apiClient.post<WorkoutSet>(`/workout-sessions/exercises/${sessionExerciseId}/sets`, body, { signal });
  return response.data;
}

export async function getExercise(id: string, signal?: AbortSignal): Promise<ExerciseDetail> {
  const response = await apiClient.get<ExerciseDetail>(`/exercises/${id}`, { signal });
  return response.data;
}

export async function listRecords(signal?: AbortSignal): Promise<PersonalRecord[]> {
  const response = await apiClient.get<PersonalRecord[]>('/progress/records', { signal });
  return response.data;
}
