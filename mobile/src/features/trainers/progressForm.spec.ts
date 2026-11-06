import { describe, expect, it } from '@jest/globals';
import { formatChange, formatMeasure, parseOptionalMeasure, parseOptionalPercent } from './progressForm';

describe('trainer progress form', () => {
  it('keeps a blank measurement empty and rejects zero', () => {
    expect(parseOptionalMeasure('')).toBeNull();
    expect(parseOptionalMeasure('  ')).toBeNull();
    expect(parseOptionalMeasure('0')).toBe('invalid');
    expect(parseOptionalMeasure('70.5')).toBe(70.5);
    expect(parseOptionalPercent('')).toBeNull();
    expect(parseOptionalPercent('0')).toBe(0);
    expect(parseOptionalPercent('101')).toBe('invalid');
  });

  it('does not show a change when the previous measurement is missing', () => {
    expect(formatMeasure(null, 'kg')).toBe('—');
    expect(formatChange(null, 'kg')).toBeNull();
    expect(formatChange(0.5, 'kg')).toBe('+0.5 kg');
  });
});
