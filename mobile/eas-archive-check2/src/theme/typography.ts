export const fontFamily = {
  regular: undefined,
  medium: undefined,
  bold: undefined,
} as const;

export const fontSize = {
  caption: 12,
  label: 13,
  body: 16,
  title: 22,
  display: 32,
} as const;

export const fontWeight = {
  regular: '400',
  medium: '600',
  bold: '700',
} as const;

export const lineHeight = {
  caption: 16,
  label: 18,
  body: 24,
  title: 28,
  display: 38,
} as const;

export type TextVariant = 'caption' | 'label' | 'body' | 'title' | 'display';
