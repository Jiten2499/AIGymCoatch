import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useSessionStore } from '../../../store/sessionStore';
import { isNotFound } from '../../../services/api/problem';
import { tenantKey } from './keys';
import { addSet, completeSession, createGoal, getDashboard, getExercise, getProfile, getProgram, getSession, listGoals, listMeasurements, listPrograms, listRecords, listSessions, recordMeasurement, saveProfile, startProgram } from './fitness.api';
import type { FitnessProfile } from '../types';

export function useTenantId(): string {
  return useSessionStore((state) => state.currentTenant?.id ?? '');
}

function skipMissing(failureCount: number, error: unknown): boolean {
  return !isNotFound(error) && failureCount < 1;
}

export function useFitnessProfile() {
  const tenantId = useTenantId();
  return useQuery({
    queryKey: tenantKey(tenantId, 'fitnessProfile'),
    enabled: Boolean(tenantId),
    retry: skipMissing,
    queryFn: ({ signal }) => getProfile(signal),
  });
}

export function useSaveProfile() {
  const tenantId = useTenantId();
  const client = useQueryClient();
  return useMutation({
    mutationFn: (body: Omit<FitnessProfile, 'id'>) => saveProfile(body),
    onSuccess: async (profile) => {
      client.setQueryData(tenantKey(tenantId, 'fitnessProfile'), profile);
      await client.invalidateQueries({ queryKey: tenantKey(tenantId, 'dashboard') });
    },
  });
}

export function useGoals() {
  const tenantId = useTenantId();
  return useQuery({
    queryKey: tenantKey(tenantId, 'goals'),
    enabled: Boolean(tenantId),
    queryFn: ({ signal }) => listGoals(signal),
  });
}

export function useCreateGoal() {
  const tenantId = useTenantId();
  const client = useQueryClient();
  return useMutation({
    mutationFn: (body: Parameters<typeof createGoal>[0]) => createGoal(body),
    onSuccess: async () => {
      await client.invalidateQueries({ queryKey: tenantKey(tenantId, 'goals') });
      await client.invalidateQueries({ queryKey: tenantKey(tenantId, 'dashboard') });
    },
  });
}

export function useDashboard() {
  const tenantId = useTenantId();
  return useQuery({
    queryKey: tenantKey(tenantId, 'dashboard'),
    enabled: Boolean(tenantId),
    queryFn: ({ signal }) => getDashboard(signal),
  });
}

export function usePrograms(page = 1) {
  const tenantId = useTenantId();
  return useQuery({
    queryKey: tenantKey(tenantId, 'programs', page),
    enabled: Boolean(tenantId),
    queryFn: ({ signal }) => listPrograms(page, signal),
  });
}

export function useProgram(programId: string) {
  const tenantId = useTenantId();
  return useQuery({
    queryKey: tenantKey(tenantId, 'program', programId),
    enabled: Boolean(tenantId && programId),
    queryFn: ({ signal }) => getProgram(programId, signal),
  });
}

export function useExercise(exerciseId: string) {
  const tenantId = useTenantId();
  return useQuery({
    queryKey: tenantKey(tenantId, 'exercise', exerciseId),
    enabled: Boolean(tenantId && exerciseId),
    queryFn: ({ signal }) => getExercise(exerciseId, signal),
  });
}

export function useSession(sessionId: string) {
  const tenantId = useTenantId();
  return useQuery({
    queryKey: tenantKey(tenantId, 'session', sessionId),
    enabled: Boolean(tenantId && sessionId),
    queryFn: ({ signal }) => getSession(sessionId, signal),
  });
}

export function useStartProgram() {
  const tenantId = useTenantId();
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ programId, dayId }: { programId: string; dayId: string | null }) => startProgram(programId, dayId),
    onSuccess: async (session) => {
      client.setQueryData(tenantKey(tenantId, 'session', session.id), session);
      await client.invalidateQueries({ queryKey: tenantKey(tenantId, 'dashboard') });
    },
  });
}

export function useAddSet(sessionId: string) {
  const tenantId = useTenantId();
  const client = useQueryClient();
  return useMutation({
    mutationFn: (input: { sessionExerciseId: string; body: Parameters<typeof addSet>[1] }) => addSet(input.sessionExerciseId, input.body),
    onSuccess: async () => {
      await client.invalidateQueries({ queryKey: tenantKey(tenantId, 'session', sessionId) });
    },
  });
}

export function useCompleteSession() {
  const tenantId = useTenantId();
  const client = useQueryClient();
  return useMutation({
    mutationFn: (sessionId: string) => completeSession(sessionId),
    onSuccess: async (session) => {
      client.setQueryData(tenantKey(tenantId, 'session', session.id), session);
      await client.invalidateQueries({ queryKey: tenantKey(tenantId, 'dashboard') });
      await client.invalidateQueries({ queryKey: tenantKey(tenantId, 'records') });
      await client.invalidateQueries({ queryKey: tenantKey(tenantId, 'sessions') });
    },
  });
}

export function useMeasurements() {
  const tenantId = useTenantId();
  return useQuery({
    queryKey: tenantKey(tenantId, 'measurements'),
    enabled: Boolean(tenantId),
    queryFn: ({ signal }) => listMeasurements(signal),
  });
}

export function useRecords() {
  const tenantId = useTenantId();
  return useQuery({
    queryKey: tenantKey(tenantId, 'records'),
    enabled: Boolean(tenantId),
    queryFn: ({ signal }) => listRecords(signal),
  });
}

export function useSessions(page: number) {
  const tenantId = useTenantId();
  return useQuery({
    queryKey: tenantKey(tenantId, 'sessions', page),
    enabled: Boolean(tenantId),
    queryFn: ({ signal }) => listSessions(page, signal),
  });
}

export function useRecordMeasurement() {
  const tenantId = useTenantId();
  const client = useQueryClient();
  return useMutation({
    mutationFn: (body: Parameters<typeof recordMeasurement>[0]) => recordMeasurement(body),
    onSuccess: async () => {
      await client.invalidateQueries({ queryKey: tenantKey(tenantId, 'measurements') });
      await client.invalidateQueries({ queryKey: tenantKey(tenantId, 'dashboard') });
    },
  });
}
