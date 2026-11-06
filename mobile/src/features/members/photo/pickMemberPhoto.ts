import * as ImagePicker from 'expo-image-picker';

export type PickedMemberPhoto = {
  uri: string;
  mimeType: string;
  fileName: string;
};

export async function pickMemberPhoto(): Promise<PickedMemberPhoto | null> {
  const permission = await ImagePicker.requestMediaLibraryPermissionsAsync();
  if (!permission.granted) {
    return null;
  }

  const result = await ImagePicker.launchImageLibraryAsync({
    mediaTypes: ['images'],
    allowsEditing: true,
    quality: 0.8,
  });
  if (result.canceled || result.assets.length === 0) {
    return null;
  }

  const asset = result.assets[0];
  return {
    uri: asset.uri,
    mimeType: asset.mimeType ?? 'image/jpeg',
    fileName: asset.fileName ?? 'profile.jpg',
  };
}
