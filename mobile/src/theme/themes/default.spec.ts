import { describe, expect, it } from '@jest/globals';
import { prefersDarkText } from '../contrast';
import { resolveTheme } from './default';

describe('resolveTheme', () => {
  it('uses the dark palette and a dark-on-lime label', () => {
    const theme = resolveTheme('dark', 'lime', false);

    expect(theme.colors.background).toBe('#07110d');
    expect(theme.colors.accent).toBe('#c8f54e');
    expect(theme.colors.onAccent).toBe('#10211a');
  });

  it('uses the light palette for an explicit light appearance', () => {
    const theme = resolveTheme('light', 'blue', false);

    expect(theme.scheme).toBe('light');
    expect(theme.colors.accent).toBe('#1d4ed8');
    expect(prefersDarkText(theme.colors.accent)).toBe(false);
  });

  it('drops motion duration when reduced motion is requested', () => {
    expect(resolveTheme('dark', 'lime', true).duration(200)).toBe(0);
  });
});
