import type { ReactNode } from 'react';
import { View } from 'react-native';
import { useTheme } from '../../app/providers/ThemeProvider';
import { AppText } from '../ui/AppText';

type FormFieldProps = {
  label: string;
  error?: string;
  hint?: string;
  required?: boolean;
  children: ReactNode;
};

export function FormField({ label, error, hint, required = false, children }: FormFieldProps) {
  const theme = useTheme();
  return (
    <View style={{ gap: theme.spacing.sm }}>
      <AppText accessibilityLabel={required ? `${label}, required` : undefined} variant="label">
        {label}
        {required ? <AppText tone="danger" variant="label"> *</AppText> : null}
      </AppText>
      {children}
      {error ? (
        <AppText accessibilityRole="alert" tone="danger" variant="caption">{error}</AppText>
      ) : hint ? (
        <AppText tone="muted" variant="caption">{hint}</AppText>
      ) : null}
    </View>
  );
}
