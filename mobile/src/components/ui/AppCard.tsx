import type { ReactNode } from 'react';
import { View, type ViewProps } from 'react-native';
import { useTheme } from '../../app/providers/ThemeProvider';

export function AppCard({ children, style, ...rest }: ViewProps & { children: ReactNode }) {
  const theme = useTheme();
  return (
    <View
      style={[{ backgroundColor: theme.colors.surface, borderColor: theme.colors.border, borderRadius: theme.radius.lg, borderWidth: 1, gap: theme.spacing.md, padding: theme.spacing.lg }, theme.shadow, style]}
      {...rest}
    >
      {children}
    </View>
  );
}
