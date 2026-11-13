import * as ImagePicker from 'expo-image-picker';
import type { PickedMemberPhoto } from '../members/photo/pickMemberPhoto';
import { progressPhotoIssue } from './progressSeries';

export const progressPhotoMax = 6;

export type ProgressPhotoPick = PickedMemberPhoto | { error: 'type' | 'size' | 'permission' };

export async function pickProgressPhotos(source: 'camera' | 'library', remaining: number): Promise<ProgressPhotoPick[]> {
  if (remaining <= 0) {
    return [];
  }
  const permission = source === 'camera'
    ? await ImagePicker.requestCameraPermissionsAsync()
    : await ImagePicker.requestMediaLibraryPermissionsAsync();
  if (!permission.granted) {
    return [{ error: 'permission' }];
  }
  const result = source === 'camera'
    ? await ImagePicker.launchCameraAsync({ mediaTypes: ['images'], quality: 0.7 })
    : await ImagePicker.launchImageLibraryAsync({ allowsMultipleSelection: true, mediaTypes: ['images'], quality: 0.7, selectionLimit: remaining });
  if (result.canceled) {
    return [];
  }
  return result.assets.slice(0, remaining).map((asset) => {
    const mimeType = asset.mimeType ?? 'image/jpeg';
    const issue = progressPhotoIssue(mimeType, asset.fileSize);
    if (issue) {
      return { error: issue };
    }
    const extension = mimeType === 'image/png' ? 'png' : mimeType === 'image/webp' ? 'webp' : 'jpg';
    return { uri: asset.uri, mimeType, fileName: asset.fileName ?? `progress.${extension}` };
  });
}
