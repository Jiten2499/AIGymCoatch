import { View } from 'react-native';
import { useTheme } from '../../app/providers/ThemeProvider';

export function AppSkeleton({ height = 16, width = '100%' as const }: { height?: number; width?: number | `${number}%` }) {
  const theme = useTheme();
  return <View accessibilityElementsHidden importantForAccessibility="no-hide-descendants" style={{ backgroundColor: theme.colors.surfaceMuted, borderRadius: theme.radius.sm, height, width }} />;
}
