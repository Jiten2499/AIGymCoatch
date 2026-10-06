import { StyleSheet, TextInput, type TextInputProps } from 'react-native';
import { useTheme } from '../../app/providers/ThemeProvider';

type AppTextInputProps = TextInputProps & {
  invalid?: boolean;
};

export function AppTextInput({ invalid = false, style, ...rest }: AppTextInputProps) {
  const theme = useTheme();
  return (
    <TextInput
      placeholderTextColor={theme.colors.muted}
      style={[
        styles.input,
        {
          color: theme.colors.text,
          backgroundColor: theme.colors.surface,
          borderColor: invalid ? theme.colors.danger : theme.colors.border,
          borderRadius: theme.radius.md,
          minHeight: theme.control.inputHeight,
          paddingHorizontal: theme.spacing.lg,
        },
        style,
      ]}
      {...rest}
    />
  );
}

const styles = StyleSheet.create({
  input: {
    borderWidth: 1,
    fontSize: 16,
  },
});
