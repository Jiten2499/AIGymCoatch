import type { ViewStyle } from 'react-native';

export const elevation = {
  none: 0,
  card: 1,
  overlay: 8,
  toast: 12,
} as const;

export const zIndex = {
  base: 0,
  sticky: 10,
  dropdown: 20,
  modal: 30,
  toast: 40,
} as const;

export function cardShadow(scheme: 'light' | 'dark'): ViewStyle {
  if (scheme === 'dark') {
    return { elevation: elevation.card };
  }

  return {
    elevation: elevation.card,
    shadowColor: '#10211a',
    shadowOffset: { width: 0, height: 1 },
    shadowOpacity: 0.08,
    shadowRadius: 4,
  };
}
