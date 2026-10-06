import { Text, type TextProps, type TextStyle } from 'react-native';
import { useTheme } from '../../app/providers/ThemeProvider';
import type { TextVariant } from '../../theme';

type AppTextProps = TextProps & {
  variant?: TextVariant;
  tone?: 'default' | 'muted' | 'accent' | 'danger';
};

export function AppText({ variant = 'body', tone = 'default', style, ...rest }: AppTextProps) {
  const theme = useTheme();
  const color = tone === 'muted' ? theme.colors.muted : tone === 'accent' ? theme.colors.accent : tone === 'danger' ? theme.colors.danger : theme.colors.text;
  const type = theme.typography[variant];
  const textStyle: TextStyle = { color, fontSize: type.fontSize, lineHeight: type.lineHeight, fontWeight: type.fontWeight };
  return <Text allowFontScaling maxFontSizeMultiplier={1.4} style={[textStyle, style]} {...rest} />;
}
