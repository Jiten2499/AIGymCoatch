import { describe, expect, it } from '@jest/globals';
import { composerBottomPadding } from './composerInset';

describe('composerBottomPadding', () => {
  it('clears the navigation bar when the keyboard is closed', () => {
    expect(composerBottomPadding(12, 48, 0, 0)).toBe(60);
  });

  it('does not add keyboard padding when the window already resized', () => {
    expect(composerBottomPadding(12, 0, 320, 320)).toBe(12);
  });

  it('lifts the composer when a Play build leaves the keyboard covering it', () => {
    expect(composerBottomPadding(12, 48, 320, 0)).toBe(332);
  });
});
