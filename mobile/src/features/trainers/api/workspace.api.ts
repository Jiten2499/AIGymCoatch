import { apiClient } from '../../../services/api/client';
import type { PageResult } from '../../../types/api';
import type { WorkoutProgram } from '../../fitness/types';

export type TrainerSession = {
  id: string;
  memberId: string;
  memberName: string;
  status: string;
  scheduledStartUtc: string;
  scheduledEndUtc: string;
  sessionType: string | null;
  location: string | null;
  focus: string | null;
  notes: string | null;
  elapsedSeconds: number;
  startedAtUtc: string | null;
  packageName: string | null;
  sessionsRemaining: number | null;
  sessionCount: number | null;
};

export type TrainerExercise = {
  id: string;
  name: string;
  muscleGroup: string | null;
  sets: number;
  reps: string;
  completed: boolean;
  sortOrder: number;
};

export type TrainerHome = {
  firstName: string;
  sessionsToday: number;
  activeMembers: number;
  pendingNotes: number;
  newAssignments: number;
  nextSession: TrainerSession | null;
  today: TrainerSession[];
  hasPhoto?: boolean;
};

export type TrainerMember = {
  id: string;
  name: string;
  goal: string | null;
  status: string;
  packageName: string | null;
  sessionsUsed: number | null;
  sessionCount: number | null;
  packageEnd: string | null;
  expiring: boolean;
  hasPhoto?: boolean;
};

export type ProgressValue = { value: number | null; previous: number | null; change: number | null };

export type MemberProgress = {
  id: string;
  assessmentDate: string;
  unitSystem: string;
  weight: ProgressValue;
  waist: ProgressValue;
  chest: ProgressValue;
  hip: ProgressValue;
  arm: ProgressValue;
  thigh: ProgressValue;
  bodyFatPercentage: ProgressValue;
  fitnessScore: ProgressValue;
  trainingPerformance: string | null;
  notes: string | null;
  achievedGoals: string | null;
  nextGoals: string | null;
  photoConsent: boolean;
  hasPhoto: boolean;
  canEdit: boolean;
  height: ProgressValue;
  rightArm: ProgressValue;
  rightThigh: ProgressValue;
  visceralFat: ProgressValue;
  strengthScore: ProgressValue;
  enduranceScore: ProgressValue;
  flexibilityScore: ProgressValue;
  coreScore: ProgressValue;
  photoIds: string[];
};

export type MemberProgressInput = {
  assessmentDate: string;
  weight: number | null;
  waist: number | null;
  chest: number | null;
  hip: number | null;
  arm: number | null;
  thigh: number | null;
  bodyFatPercentage: number | null;
  fitnessScore: number | null;
  trainingPerformance: string | null;
  notes: string | null;
  achievedGoals: string | null;
  nextGoals: string | null;
  photoConsent: boolean;
  height: number | null;
  rightArm: number | null;
  rightThigh: number | null;
  visceralFat: number | null;
  strengthScore: number | null;
  enduranceScore: number | null;
  flexibilityScore: number | null;
  coreScore: number | null;
};

export type TrainerMemberDetail = {
  id: string;
  name: string;
  phone: string | null;
  goal: string | null;
  status: string;
  joinDate: string;
  packages: { name: string; status: string; sessionsRemaining: number; sessionCount: number; endDate: string; price: number; currency: string; expiring: boolean }[];
  sessions: TrainerSession[];
  notes: { sessionId: string; atUtc: string; text: string }[];
  measurements: { date: string; weight: number | null; bodyFatPercentage: number | null; waist: number | null }[];
  hasPhoto?: boolean;
};

export type TrainerPerformance = {
  creditBalance: number;
  overallPercent: number;
  sessionsCompleted: number;
  packageValue: number;
  currency: string | null;
  packagesSold: number;
  newClients: number;
  metrics: { id: string; name: string; metric: string; goal: number; actual: number; percent: number; status: string }[];
  credits: { id: string; points: number; type: string; status: string; reason: string | null; createdAt: string }[];
};

export type TrainerAlert = { kind: string; title: string; body: string; atUtc: string; sessionId: string | null; memberId: string | null };

export type TrainerProfile = {
  firstName: string;
  lastName: string;
  email: string;
  specialization: string | null;
  certification: string | null;
  bio: string | null;
  experienceYears: number | null;
  joiningDate: string | null;
  hasPhoto?: boolean;
};

const root = '/trainers/me';

export const trainerHomeKey = (tenantId: string) => ['trainer-home', tenantId] as const;
export const trainerScheduleKey = (tenantId: string, from: string, to: string) => ['trainer-schedule', tenantId, from, to] as const;
export const trainerMembersKey = (tenantId: string, search: string) => ['trainer-members', tenantId, search] as const;
export const trainerMemberKey = (tenantId: string, memberId: string) => ['trainer-member', tenantId, memberId] as const;
export const trainerSessionKey = (tenantId: string, sessionId: string) => ['trainer-session', tenantId, sessionId] as const;
export const trainerPerformanceKey = (tenantId: string, from: string, to: string) => ['trainer-performance', tenantId, from, to] as const;
export const trainerAlertsKey = (tenantId: string) => ['trainer-alerts', tenantId] as const;
export const trainerProfileKey = (tenantId: string) => ['trainer-profile', tenantId] as const;
export const trainerMemberProgramsKey = (tenantId: string, memberId: string) => ['trainer-member-programs', tenantId, memberId] as const;
export const trainerMemberProgramKey = (tenantId: string, memberId: string, programId: string) => ['trainer-member-program', tenantId, memberId, programId] as const;

export type MemberProgramUpdate = {
  name: string;
  description: string | null;
  goalType: string;
  difficulty: string;
  durationWeeks: number;
  frequencyPerWeek: number;
  days: {
    dayNumber: number;
    name: string;
    description: string | null;
    restDay: boolean;
    exercises: {
      exerciseId: string;
      sets: number | null;
      targetReps: number | null;
      targetDurationSeconds: number | null;
      targetDistance: number | null;
      targetWeight: number | null;
      restSeconds: number | null;
      notes: string | null;
    }[];
  }[];
};

export async function getTrainerHome(signal?: AbortSignal) {
  const response = await apiClient.get<TrainerHome>(`${root}/home`, { signal });
  return response.data;
}

export async function getTrainerSchedule(from: string, to: string, signal?: AbortSignal) {
  const response = await apiClient.get<TrainerSession[]>(`${root}/schedule`, { params: { from, to }, signal });
  return response.data;
}

export async function getTrainerSession(sessionId: string, signal?: AbortSignal) {
  const response = await apiClient.get<{ session: TrainerSession; exercises: TrainerExercise[] }>(`${root}/sessions/${sessionId}`, { signal });
  return response.data;
}

export async function listTrainerMembers(search: string, signal?: AbortSignal) {
  const response = await apiClient.get<TrainerMember[]>(`${root}/members`, { params: { search }, signal });
  return response.data;
}

export async function getTrainerMember(memberId: string, signal?: AbortSignal) {
  const response = await apiClient.get<TrainerMemberDetail>(`${root}/members/${memberId}`, { signal });
  return response.data;
}

export async function getTrainerPerformance(from: string, to: string, signal?: AbortSignal) {
  const response = await apiClient.get<TrainerPerformance>(`${root}/performance`, { params: { from, to }, signal });
  return response.data;
}

export async function listTrainerAlerts(signal?: AbortSignal) {
  const response = await apiClient.get<TrainerAlert[]>(`${root}/alerts`, { signal });
  return response.data;
}

export async function getTrainerProfile(signal?: AbortSignal) {
  const response = await apiClient.get<TrainerProfile>(`${root}/profile`, { signal });
  return response.data;
}

export async function scheduleTrainerSession(body: { memberId: string; startUtc: string; endUtc: string; sessionType?: string; location?: string; focus?: string }) {
  const response = await apiClient.post<string>(`${root}/sessions`, body);
  return response.data;
}

export async function startTrainerSession(sessionId: string) {
  await apiClient.post(`${root}/sessions/${sessionId}/start`);
}

export async function saveTrainerProgress(sessionId: string, elapsedSeconds: number) {
  await apiClient.post(`${root}/sessions/${sessionId}/progress`, { elapsedSeconds });
}

export async function addTrainerExercise(sessionId: string, body: { name: string; muscleGroup?: string; sets: number; reps: string }) {
  await apiClient.post(`${root}/sessions/${sessionId}/exercises`, body);
}

export async function toggleTrainerExercise(sessionId: string, exerciseId: string) {
  await apiClient.post(`${root}/sessions/${sessionId}/exercises/${exerciseId}/toggle`);
}

export async function completeTrainerSession(sessionId: string, notes: string, elapsedSeconds: number) {
  await apiClient.post(`${root}/sessions/${sessionId}/complete`, { notes, elapsedSeconds });
}

export async function cancelTrainerSession(sessionId: string) {
  await apiClient.post(`${root}/sessions/${sessionId}/cancel`);
}

export async function listMemberPrograms(memberId: string, signal?: AbortSignal) {
  const response = await apiClient.get<PageResult<WorkoutProgram>>(`${root}/members/${memberId}/workouts`, { params: { page: 1, pageSize: 50 }, signal });
  return response.data;
}

export async function getMemberProgram(memberId: string, programId: string, signal?: AbortSignal) {
  const response = await apiClient.get<WorkoutProgram>(`${root}/members/${memberId}/workouts/${programId}`, { signal });
  return response.data;
}

export async function updateMemberProgram(memberId: string, programId: string, body: MemberProgramUpdate) {
  const response = await apiClient.put<WorkoutProgram>(`${root}/members/${memberId}/workouts/${programId}`, body);
  return response.data;
}

export async function listMemberProgress(memberId: string, signal?: AbortSignal) {
  const response = await apiClient.get<MemberProgress[]>(`${root}/members/${memberId}/progress`, { signal });
  return response.data;
}

export async function saveMemberProgress(memberId: string, assessmentId: string | undefined, body: MemberProgressInput) {
  const path = assessmentId ? `${root}/members/${memberId}/progress/${assessmentId}` : `${root}/members/${memberId}/progress`;
  const response = assessmentId ? await apiClient.put<string>(path, body) : await apiClient.post<string>(path, body);
  return response.data;
}

export async function uploadMemberProgressPhoto(memberId: string, assessmentId: string, uri: string, mimeType: string, fileName: string, onProgress?: (ratio: number) => void) {
  const body = new FormData();
  body.append('file', { uri, name: fileName, type: mimeType } as unknown as Blob);
  const response = await apiClient.post<string>(`${root}/members/${memberId}/progress/${assessmentId}/photo`, body, {
    headers: { 'Content-Type': 'multipart/form-data' },
    onUploadProgress: (event) => {
      if (onProgress && event.total) {
        onProgress(event.loaded / event.total);
      }
    },
  });
  onProgress?.(1);
  return response.data;
}

export async function deleteMemberProgressPhoto(memberId: string, assessmentId: string, photoId: string) {
  await apiClient.delete(`${root}/members/${memberId}/progress/${assessmentId}/photos/${photoId}`);
}

export async function loadMemberProgressPhoto(memberId: string, assessmentId: string, photoId: string | null, signal?: AbortSignal) {
  const path = photoId
    ? `${root}/members/${memberId}/progress/${assessmentId}/photos/${photoId}`
    : `${root}/members/${memberId}/progress/${assessmentId}/photo`;
  try {
    const response = await apiClient.get<ArrayBuffer>(path, { responseType: 'arraybuffer', signal });
    const bytes = new Uint8Array(response.data);
    let binary = '';
    bytes.forEach((value) => {
      binary += String.fromCharCode(value);
    });
    const type = String(response.headers['content-type'] ?? 'image/jpeg');
    return `data:${type};base64,${globalThis.btoa(binary)}`;
  } catch {
    return null;
  }
}
