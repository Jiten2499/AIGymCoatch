import { useTranslation } from 'react-i18next';
import { Image, View } from 'react-native';
import { useTheme } from '../../app/providers/ThemeProvider';
import { AppText } from '../../components/ui/AppText';
import { muscleGroupById } from './catalog';
import { muscleGroupImage } from './images';

type IllustrationMode = 'primary' | 'secondary';
type IllustrationSize = 'compact' | 'full';

type MuscleGroupIllustrationProps = {
  muscleGroupId: string;
  mode: IllustrationMode;
  size?: IllustrationSize;
};

export function MuscleGroupIllustration({ muscleGroupId, mode, size }: MuscleGroupIllustrationProps) {
  const theme = useTheme();
  const { t } = useTranslation();
  const group = muscleGroupById(muscleGroupId);
  const source = group ? muscleGroupImage(group.id) : undefined;
  const resolvedSize = size ?? (mode === 'primary' ? 'full' : 'compact');
  const frame = resolvedSize === 'full'
    ? { width: '100%' as const, aspectRatio: 1, maxHeight: 320 }
    : { height: 64, width: 64 };

  if (!group || source == null) {
    return (
      <View accessibilityLabel={t('journey.muscleUnavailable')} accessibilityRole="text" style={{ justifyContent: 'center', minHeight: theme.control.minTouch }}>
        <AppText tone="muted">{t('journey.muscleUnavailable')}</AppText>
      </View>
    );
  }

  return (
    <View
      accessibilityLabel={group.displayName}
      accessible
      style={{
        alignItems: 'center',
        backgroundColor: theme.colors.background,
        borderColor: mode === 'primary' ? theme.colors.accent : theme.colors.border,
        borderRadius: theme.radius.md,
        borderWidth: 1,
        padding: theme.spacing.xs,
      }}
    >
      <Image accessibilityIgnoresInvertColors resizeMode="contain" source={source} style={frame} />
      <AppText tone={mode === 'primary' ? 'default' : 'muted'} variant="caption">{group.displayName}</AppText>
    </View>
  );
}
