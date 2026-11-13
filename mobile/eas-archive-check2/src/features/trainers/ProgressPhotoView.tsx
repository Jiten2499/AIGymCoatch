import { Image, Pressable, View } from 'react-native';
import { useTranslation } from 'react-i18next';
import { useTheme } from '../../app/providers/ThemeProvider';
import { AppText } from '../../components/ui/AppText';
import { useProgressPhoto } from './api/workspace.queries';

export function ProgressPhotoView({ memberId, assessmentId, photoId, onRemove }: { memberId: string; assessmentId: string; photoId: string | null; onRemove?: () => void }) {
  const { t } = useTranslation();
  const theme = useTheme();
  const photo = useProgressPhoto(memberId, assessmentId, photoId);
  return (
    <View style={{ height: 104, width: 104 }}>
      {photo.data ? (
        <Image accessibilityLabel={t('trainer.photoOnFile')} source={{ uri: photo.data }} style={{ borderRadius: theme.radius.md, height: 96, width: 96 }} />
      ) : (
        <View style={{ backgroundColor: theme.colors.surfaceMuted, borderRadius: theme.radius.md, height: 96, width: 96 }} />
      )}
      {onRemove ? (
        <Pressable accessibilityLabel={t('trainer.removePhoto')} onPress={onRemove} style={{ alignItems: 'center', backgroundColor: theme.colors.accent, borderRadius: 12, height: 24, justifyContent: 'center', position: 'absolute', right: 0, top: 0, width: 24 }}>
          <AppText style={{ color: theme.colors.onAccent }} variant="caption">×</AppText>
        </Pressable>
      ) : null}
    </View>
  );
}
