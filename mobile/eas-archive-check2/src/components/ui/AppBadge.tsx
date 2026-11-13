import { View } from 'react-native';
import { useTheme } from '../../app/providers/ThemeProvider';
import { AppText } from './AppText';

export function AppBadge({ label }: { label: string }) {
  const theme = useTheme();
  return (
    <View accessibilityRole="text" style={{ alignSelf: 'flex-start', backgroundColor: theme.colors.surfaceMuted, borderRadius: theme.radius.pill, paddingHorizontal: theme.spacing.md, paddingVertical: theme.spacing.xs }}>
      <AppText variant="caption">{label}</AppText>
    </View>
  );
}
