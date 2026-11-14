import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { imageDataUri } from '../../../services/api/image';
import { useSessionStore } from '../../../store/sessionStore';
import {
  addTrainerExercise,
  cancelTrainerSession,
  completeTrainerSession,
  getTrainerHome,
  getTrainerMember,
  getTrainerPerformance,
  getTrainerProfile,
  getTrainerSchedule,
  getTrainerSession,
  deleteMemberProgressPhoto,
  getMemberProgram,
  listMemberPrograms,
  listMemberProgress,
  listTrainerAlerts,
  listTrainerMembers,
  loadMemberProgressPhoto,
  saveMemberProgress,
  saveTrainerProgress,
  updateMemberProgram,
  uploadMemberProgressPhoto,
  scheduleTrainerSession,
  startTrainerSession,
  toggleTrainerExercise,
  trainerAlertsKey,
  trainerHomeKey,
  trainerMemberKey,
  trainerMemberProgramKey,
  trainerMemberProgramsKey,
  trainerMembersKey,
  trainerPerformanceKey,
  trainerProfileKey,
  trainerScheduleKey,
  trainerSessionKey,
  type MemberProgramUpdate,
} from './workspace.api';

function useTenantId() {
  return useSessionStore((state) => state.currentTenant?.id ?? '');
}

export function useTrainerHome() {
  const tenantId = useTenantId();
  return useQuery({ queryKey: trainerHomeKey(tenantId), queryFn: ({ signal }) => getTrainerHome(signal), enabled: tenantId.length > 0 });
}

export function useTrainerSchedule(from: string, to: string) {
  const tenantId = useTenantId();
  return useQuery({ queryKey: trainerScheduleKey(tenantId, from, to), queryFn: ({ signal }) => getTrainerSchedule(from, to, signal), enabled: tenantId.length > 0 });
}

export function useTrainerMembers(search: string) {
  const tenantId = useTenantId();
  return useQuery({ queryKey: trainerMembersKey(tenantId, search), queryFn: ({ signal }) => listTrainerMembers(search, signal), enabled: tenantId.length > 0 });
}

export function useTrainerMember(memberId: string) {
  const tenantId = useTenantId();
  return useQuery({ queryKey: trainerMemberKey(tenantId, memberId), queryFn: ({ signal }) => getTrainerMember(memberId, signal), enabled: tenantId.length > 0 });
}

export function useTrainerSession(sessionId: string) {
  const tenantId = useTenantId();
  return useQuery({ queryKey: trainerSessionKey(tenantId, sessionId), queryFn: ({ signal }) => getTrainerSession(sessionId, signal), enabled: tenantId.length > 0 });
}

export function useTrainerPerformance(from: string, to: string) {
  const tenantId = useTenantId();
  return useQuery({ queryKey: trainerPerformanceKey(tenantId, from, to), queryFn: ({ signal }) => getTrainerPerformance(from, to, signal), enabled: tenantId.length > 0 });
}

export function useTrainerAlerts() {
  const tenantId = useTenantId();
  return useQuery({ queryKey: trainerAlertsKey(tenantId), queryFn: ({ signal }) => listTrainerAlerts(signal), enabled: tenantId.length > 0 });
}

export function useMyTrainerPhoto(enabled = true) {
  const tenantId = useTenantId();
  return useQuery({
    queryKey: ['trainer-self-photo', tenantId],
    enabled: enabled && tenantId.length > 0,
    retry: false,
    queryFn: ({ signal }) => imageDataUri('/trainers/me/profile-photo', signal),
  });
}

export function useTrainerProfile() {
  const tenantId = useTenantId();
  return useQuery({ queryKey: trainerProfileKey(tenantId), queryFn: ({ signal }) => getTrainerProfile(signal), enabled: tenantId.length > 0 });
}

export function useMemberProgress(memberId: string) {
  const tenantId = useTenantId();
  return useQuery({
    queryKey: ['trainer-progress', tenantId, memberId],
    queryFn: ({ signal }) => listMemberProgress(memberId, signal),
    enabled: tenantId.length > 0 && memberId.length > 0,
  });
}

export function useMemberPrograms(memberId: string) {
  const tenantId = useTenantId();
  return useQuery({
    queryKey: trainerMemberProgramsKey(tenantId, memberId),
    queryFn: ({ signal }) => listMemberPrograms(memberId, signal),
    enabled: tenantId.length > 0 && memberId.length > 0,
  });
}

export function useMemberProgram(memberId: string, programId: string) {
  const tenantId = useTenantId();
  return useQuery({
    queryKey: trainerMemberProgramKey(tenantId, memberId, programId),
    queryFn: ({ signal }) => getMemberProgram(memberId, programId, signal),
    enabled: tenantId.length > 0 && memberId.length > 0 && programId.length > 0,
  });
}

export function useUpdateMemberProgram(memberId: string, programId: string) {
  const client = useQueryClient();
  const tenantId = useTenantId();
  return useMutation({
    mutationFn: (body: MemberProgramUpdate) => updateMemberProgram(memberId, programId, body),
    onSuccess: async (program) => {
      client.setQueryData(trainerMemberProgramKey(tenantId, memberId, programId), program);
      await client.invalidateQueries({ queryKey: trainerMemberProgramsKey(tenantId, memberId) });
      await client.invalidateQueries({ queryKey: trainerMemberProgramKey(tenantId, memberId, programId) });
    },
  });
}

export type ProgressDraftPhoto = { key: string; uri: string; mimeType: string; fileName: string };

export function useSaveMemberProgress(memberId: string) {
  const client = useQueryClient();
  const tenantId = useTenantId();
  return useMutation({
    mutationFn: async ({ assessmentId, body, photos, onProgress }: { assessmentId?: string; body: Parameters<typeof saveMemberProgress>[2]; photos: ProgressDraftPhoto[]; onProgress?: (key: string, ratio: number) => void }) => {
      const id = await saveMemberProgress(memberId, assessmentId, body);
      const failed: string[] = [];
      let failure: string | null = null;
      if (body.photoConsent) {
        for (const photo of photos) {
          try {
            await uploadMemberProgressPhoto(memberId, id, photo.uri, photo.mimeType, photo.fileName, (ratio) => onProgress?.(photo.key, ratio));
          } catch (error) {
            failed.push(photo.key);
            failure ??= error instanceof Error && error.message.length > 0 ? error.message : null;
          }
        }
      }
      await client.invalidateQueries({ queryKey: ['trainer-progress', tenantId, memberId] });
      return { id, failed, failure };
    },
  });
}

export function useProgressPhoto(memberId: string, assessmentId: string, photoId: string | null) {
  return useQuery({
    queryKey: ['trainer-progress-photo', memberId, assessmentId, photoId],
    queryFn: ({ signal }) => loadMemberProgressPhoto(memberId, assessmentId, photoId, signal),
    enabled: memberId.length > 0 && assessmentId.length > 0,
  });
}

export function useDeleteProgressPhoto(memberId: string) {
  const client = useQueryClient();
  const tenantId = useTenantId();
  return useMutation({
    mutationFn: ({ assessmentId, photoId }: { assessmentId: string; photoId: string }) => deleteMemberProgressPhoto(memberId, assessmentId, photoId),
    onSuccess: async () => {
      await client.invalidateQueries({ queryKey: ['trainer-progress', tenantId, memberId] });
    },
  });
}

export function useTrainerMutations() {
  const client = useQueryClient();
  const tenantId = useTenantId();
  const refresh = async () => {
    await client.invalidateQueries({ queryKey: ['trainer-home', tenantId] });
    await client.invalidateQueries({ queryKey: ['trainer-schedule', tenantId] });
    await client.invalidateQueries({ queryKey: ['trainer-members', tenantId] });
    await client.invalidateQueries({ queryKey: ['trainer-member', tenantId] });
    await client.invalidateQueries({ queryKey: ['trainer-session', tenantId] });
    await client.invalidateQueries({ queryKey: ['trainer-performance', tenantId] });
    await client.invalidateQueries({ queryKey: ['trainer-alerts', tenantId] });
  };
  return {
    schedule: useMutation({ mutationFn: scheduleTrainerSession, onSuccess: refresh }),
    start: useMutation({ mutationFn: startTrainerSession, onSuccess: refresh }),
    progress: useMutation({ mutationFn: ({ sessionId, elapsedSeconds }: { sessionId: string; elapsedSeconds: number }) => saveTrainerProgress(sessionId, elapsedSeconds), onSuccess: refresh }),
    exercise: useMutation({ mutationFn: ({ sessionId, name, muscleGroup, sets, reps }: { sessionId: string; name: string; muscleGroup?: string; sets: number; reps: string }) => addTrainerExercise(sessionId, { name, muscleGroup, sets, reps }), onSuccess: refresh }),
    toggle: useMutation({ mutationFn: ({ sessionId, exerciseId }: { sessionId: string; exerciseId: string }) => toggleTrainerExercise(sessionId, exerciseId), onSuccess: refresh }),
    complete: useMutation({ mutationFn: ({ sessionId, notes, elapsedSeconds }: { sessionId: string; notes: string; elapsedSeconds: number }) => completeTrainerSession(sessionId, notes, elapsedSeconds), onSuccess: refresh }),
    cancel: useMutation({ mutationFn: cancelTrainerSession, onSuccess: refresh }),
  };
}
