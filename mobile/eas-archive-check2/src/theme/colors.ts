export const palette = {
  ink: '#10211a',
  mist: '#f4f7f4',
  white: '#ffffff',
  lime: '#c8f54e',
  limeDeep: '#4f6b10',
  teal: '#14b8a6',
  tealDeep: '#0f766e',
  blue: '#3b82f6',
  blueDeep: '#1d4ed8',
  violet: '#8b5cf6',
  violetDeep: '#6d28d9',
  coral: '#f43f5e',
  coralDeep: '#be123c',
} as const;

export const accents = {
  lime: { dark: palette.lime, light: palette.limeDeep },
  teal: { dark: palette.teal, light: palette.tealDeep },
  blue: { dark: palette.blue, light: palette.blueDeep },
  violet: { dark: palette.violet, light: palette.violetDeep },
  coral: { dark: palette.coral, light: palette.coralDeep },
} as const;

export type AccentId = keyof typeof accents;

export const darkColors = {
  background: '#07110d',
  surface: '#10211a',
  surfaceMuted: '#173024',
  text: '#f3f7f2',
  muted: '#9aab9f',
  border: '#1e3328',
  danger: '#fb7185',
  success: '#4ade80',
  overlay: 'rgba(7, 17, 13, 0.72)',
} as const;

export const lightColors = {
  background: palette.mist,
  surface: palette.white,
  surfaceMuted: '#e7f0ea',
  text: palette.ink,
  muted: '#4d6358',
  border: '#d5e2da',
  danger: '#be123c',
  success: '#15803d',
  overlay: 'rgba(16, 33, 26, 0.4)',
} as const;

export type ThemeColors = {
  background: string;
  surface: string;
  surfaceMuted: string;
  text: string;
  muted: string;
  border: string;
  accent: string;
  onAccent: string;
  danger: string;
  success: string;
  overlay: string;
};
