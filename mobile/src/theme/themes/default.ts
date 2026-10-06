import { accents, darkColors, lightColors, type AccentId, type ThemeColors } from '../colors';
import { prefersDarkText } from '../contrast';
import { control, iconSize } from '../dimensions';
import { motionDuration } from '../motion';
import { radius } from '../radius';
import { cardShadow, elevation, zIndex } from '../shadows';
import { spacing } from '../spacing';
import { fontSize, fontWeight, lineHeight, type TextVariant } from '../typography';

export type ColorScheme = 'light' | 'dark';

export type Theme = {
  scheme: ColorScheme;
  colors: ThemeColors;
  spacing: typeof spacing;
  radius: typeof radius;
  iconSize: typeof iconSize;
  control: typeof control;
  elevation: typeof elevation;
  zIndex: typeof zIndex;
  reduceMotion: boolean;
  duration: (preferred: number) => number;
  typography: Record<TextVariant, { fontSize: number; lineHeight: number; fontWeight: '400' | '600' | '700' }>;
  shadow: ReturnType<typeof cardShadow>;
};

export function resolveTheme(scheme: ColorScheme, accent: AccentId, reduceMotion: boolean): Theme {
  const base = scheme === 'dark' ? darkColors : lightColors;
  const accentColor = accents[accent][scheme];
  return {
    scheme,
    colors: {
      ...base,
      accent: accentColor,
      onAccent: prefersDarkText(accentColor) ? '#10211a' : '#f7faf7',
    },
    spacing,
    radius,
    iconSize,
    control,
    elevation,
    zIndex,
    reduceMotion,
    duration: (preferred) => motionDuration(preferred, reduceMotion),
    typography: {
      caption: { fontSize: fontSize.caption, lineHeight: lineHeight.caption, fontWeight: fontWeight.regular },
      label: { fontSize: fontSize.label, lineHeight: lineHeight.label, fontWeight: fontWeight.medium },
      body: { fontSize: fontSize.body, lineHeight: lineHeight.body, fontWeight: fontWeight.regular },
      title: { fontSize: fontSize.title, lineHeight: lineHeight.title, fontWeight: fontWeight.bold },
      display: { fontSize: fontSize.display, lineHeight: lineHeight.display, fontWeight: fontWeight.bold },
    },
    shadow: cardShadow(scheme),
  };
}
