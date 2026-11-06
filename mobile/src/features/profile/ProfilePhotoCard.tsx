import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { View } from 'react-native';
import { useTheme } from '../../app/providers/ThemeProvider';
import { AppAvatar } from '../../components/ui/AppAvatar';
import { AppButton } from '../../components/ui/AppButton';
import { AppText } from '../../components/ui/AppText';
import { useSessionStore } from '../../store/sessionStore';
import { useWorkspaceStore } from '../../store/workspaceStore';
import { useMyMemberPhoto } from '../members/myPhoto';
import { useMyTrainerPhoto } from '../trainers/api/workspace.queries';
import { pickProfilePhoto, useMyProfilePhoto, useRemoveProfilePhoto, useUploadProfilePhoto } from './photo';

export function ProfilePhotoCard() {
  const theme = useTheme();
  const { t } = useTranslation();
  const user = useSessionStore((state) => state.user);
  const currentId = useSessionStore((state) => state.currentTenant?.id);
  const isTrainer = useWorkspaceStore((state) => state.workspaces.some((workspace) => workspace.id === currentId && workspace.roles.includes('Trainer')));
  const photo = useMyProfilePhoto();
  const memberPhoto = useMyMemberPhoto();
  const trainerPhoto = useMyTrainerPhoto(isTrainer);
  const upload = useUploadProfilePhoto();
  const remove = useRemoveProfilePhoto();
  const [progress, setProgress] = useState<number | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const label = user ? `${user.firstName} ${user.lastName}` : '';
  const busy = upload.isPending || remove.isPending;

  const choose = async (source: 'camera' | 'library') => {
    setMessage(null);
    const picked = await pickProfilePhoto(source);
    if (!picked) {
      return;
    }
    if ('error' in picked) {
      setError(t(picked.error === 'size' ? 'profile.photoSize' : picked.error === 'type' ? 'profile.photoType' : 'profile.photoPermission'));
      return;
    }
    setError(null);
    setProgress(0);
    try {
      await upload.mutateAsync({ ...picked, onProgress: setProgress });
      setMessage(t('profile.photoSaved'));
    } catch (caught) {
      setError(caught instanceof Error && caught.message ? caught.message : t('profile.photoFailed'));
    } finally {
      setProgress(null);
    }
  };

  return (
    <View style={{ alignItems: 'flex-start', gap: theme.spacing.md }}>
      <AppAvatar fallbackUri={memberPhoto ?? photo.data} label={label} uri={trainerPhoto.data} />
      <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: theme.spacing.sm }}>
        <AppButton disabled={busy} label={t('profile.takePhoto')} onPress={() => { void choose('camera'); }} />
        <AppButton disabled={busy} label={t('profile.choosePhoto')} onPress={() => { void choose('library'); }} />
        <AppButton disabled={busy || (!photo.data && user?.hasPhoto !== true)} label={t('profile.removePhoto')} onPress={() => { void remove.mutateAsync().then(() => setMessage(t('profile.photoRemoved'))).catch((caught: unknown) => setError(caught instanceof Error && caught.message ? caught.message : t('profile.photoFailed'))); }} />
      </View>
      {progress !== null ? <AppText tone="muted">{t('profile.uploading', { percent: Math.round(progress * 100) })}</AppText> : null}
      {message ? <AppText accessibilityRole="alert">{message}</AppText> : null}
      {error ? <AppText accessibilityRole="alert" tone="danger">{error}</AppText> : null}
    </View>
  );
}
