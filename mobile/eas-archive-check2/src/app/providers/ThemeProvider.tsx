import { createContext, useContext, useEffect, useMemo, useState, type ReactNode } from 'react';
import { AccessibilityInfo, useColorScheme } from 'react-native';
import { usePreferencesStore } from '../../store/preferencesStore';
import { resolveTheme, type ColorScheme, type Theme } from '../../theme';

const ThemeContext = createContext<Theme | null>(null);

export function ThemeProvider({ children }: { children: ReactNode }) {
  const appearance = usePreferencesStore((state) => state.appearance);
  const accent = usePreferencesStore((state) => state.accent);
  const system = useColorScheme();
  const [reduceMotion, setReduceMotion] = useState(false);

  useEffect(() => {
    const subscription = AccessibilityInfo.addEventListener('reduceMotionChanged', setReduceMotion);
    void AccessibilityInfo.isReduceMotionEnabled().then(setReduceMotion);
    return () => subscription.remove();
  }, []);

  const scheme: ColorScheme = appearance === 'system' ? (system === 'light' ? 'light' : 'dark') : appearance;
  const theme = useMemo(() => resolveTheme(scheme, accent, reduceMotion), [scheme, accent, reduceMotion]);

  return <ThemeContext.Provider value={theme}>{children}</ThemeContext.Provider>;
}

export function useTheme(): Theme {
  const theme = useContext(ThemeContext);
  if (!theme) {
    throw new Error('useTheme must be used within ThemeProvider');
  }
  return theme;
}

export function useResolvedScheme(): ColorScheme {
  return useTheme().scheme;
}
