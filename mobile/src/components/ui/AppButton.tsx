import { ActivityIndicator, Pressable, StyleSheet, type PressableProps } from 'react-native';
import { useTheme } from '../../app/providers/ThemeProvider';
import { AppText } from './AppText';

type AppButtonProps = Omit<PressableProps, 'children'> & {
  label: string;
  loading?: boolean;
  variant?: 'primary' | 'secondary';
};

export function AppButton({ label, loading = false, variant = 'primary', disabled, style, ...rest }: AppButtonProps) {
  const theme = useTheme();
  const isDisabled = disabled || loading;
  const background = variant === 'primary' ? theme.colors.accent : theme.colors.surfaceMuted;
  const labelColor = variant === 'primary' ? theme.colors.onAccent : theme.colors.text;
  return (
    <Pressable
      accessibilityLabel={label}
      accessibilityRole="button"
      accessibilityState={{ disabled: isDisabled, busy: loading }}
      disabled={isDisabled}
      style={(state) => [
        styles.button,
        { backgroundColor: background, borderRadius: theme.radius.pill, minHeight: theme.control.minTouch, opacity: isDisabled ? 0.6 : 1 },
        typeof style === 'function' ? style(state) : style,
      ]}
      {...rest}
    >
      {loading ? <ActivityIndicator color={labelColor} /> : <AppText variant="label" style={{ color: labelColor }}>{label}</AppText>}
    </Pressable>
  );
}

const styles = StyleSheet.create({
  button: {
    alignItems: 'center',
    justifyContent: 'center',
    paddingHorizontal: 18,
  },
});
