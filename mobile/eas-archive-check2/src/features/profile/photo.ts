import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import * as ImagePicker from 'expo-image-picker';
import { apiClient } from '../../services/api/client';
import { useSessionStore } from '../../store/sessionStore';
import type { UserProfile } from '../../types/api';

export const profilePhotoKey = ['my-profile-photo'] as const;
const maxBytes = 2_097_152;

export type PickedProfilePhoto = { uri: string; mimeType: string; fileName: string };
export type ProfilePhotoIssue = { error: 'permission' | 'type' | 'size' };

export function useMyProfilePhoto() {
  const hasPhoto = useSessionStore((state) => state.user?.hasPhoto === true);
  return useQuery({
    queryKey: profilePhotoKey,
    enabled: hasPhoto,
    retry: false,
    queryFn: ({ signal }) => loadMyProfilePhoto(signal),
  });
}

export function useUploadProfilePhoto() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (photo: PickedProfilePhoto & { onProgress?: (ratio: number) => void }) =>
      uploadMyProfilePhoto(photo.uri, photo.mimeType, photo.fileName, photo.onProgress),
    onSuccess: async (profile) => {
      remember(profile.hasPhoto === true);
      await client.invalidateQueries({ queryKey: profilePhotoKey });
    },
  });
}

export function useRemoveProfilePhoto() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: removeMyProfilePhoto,
    onSuccess: async (profile) => {
      remember(profile.hasPhoto === true);
      await client.invalidateQueries({ queryKey: profilePhotoKey });
    },
  });
}

export async function pickProfilePhoto(source: 'camera' | 'library'): Promise<PickedProfilePhoto | ProfilePhotoIssue | null> {
  const permission = source === 'camera'
    ? await ImagePicker.requestCameraPermissionsAsync()
    : await ImagePicker.requestMediaLibraryPermissionsAsync();
  if (!permission.granted) {
    return { error: 'permission' };
  }

  const result = source === 'camera'
    ? await ImagePicker.launchCameraAsync({ mediaTypes: ['images'], allowsEditing: true, quality: 0.8 })
    : await ImagePicker.launchImageLibraryAsync({ mediaTypes: ['images'], allowsEditing: true, quality: 0.8 });
  if (result.canceled || result.assets.length === 0) {
    return null;
  }

  const asset = result.assets[0];
  const mimeType = asset.mimeType ?? 'image/jpeg';
  if (!['image/jpeg', 'image/png', 'image/webp'].includes(mimeType)) {
    return { error: 'type' };
  }
  if ((asset.fileSize ?? 0) > maxBytes) {
    return { error: 'size' };
  }
  return { uri: asset.uri, mimeType, fileName: asset.fileName ?? 'profile.jpg' };
}

async function uploadMyProfilePhoto(uri: string, mimeType: string, fileName: string, onProgress?: (ratio: number) => void) {
  const body = new FormData();
  body.append('file', { uri, name: fileName, type: mimeType } as unknown as Blob);
  const response = await apiClient.post<UserProfile>('/users/me/photo', body, {
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

async function removeMyProfilePhoto() {
  const response = await apiClient.delete<UserProfile>('/users/me/photo');
  return response.data;
}

async function loadMyProfilePhoto(signal?: AbortSignal): Promise<string | null> {
  try {
    const response = await apiClient.get<ArrayBuffer>('/users/me/photo', { responseType: 'arraybuffer', signal });
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

function remember(hasPhoto: boolean) {
  const user = useSessionStore.getState().user;
  if (user) {
    useSessionStore.setState({ user: { ...user, hasPhoto } });
  }
}
