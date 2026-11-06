import { describe, expect, it } from '@jest/globals';
import { convertLength, inProgressRange, lengthToCentimetres, progressPhotoIssue, weightSeries } from './progressSeries';

const today = '2026-10-25';

describe('progress series', () => {
  it('keeps only weighed assessments inside the selected range', () => {
    const rows = [
      { assessmentDate: '2026-10-12', weight: 72 },
      { assessmentDate: '2026-09-15', weight: null },
      { assessmentDate: '2026-04-02', weight: 76 },
      { assessmentDate: '2025-01-15', weight: 80 },
    ];

    expect(weightSeries(rows, '3M', today)).toEqual([{ date: '2026-10-12', weight: 72 }]);
    expect(weightSeries(rows, '1Y', today).map((point) => point.weight)).toEqual([76, 72]);
    expect(weightSeries(rows, 'All', today)).toHaveLength(3);
  });

  it('does not invent a point when weight was not recorded', () => {
    expect(weightSeries([{ assessmentDate: '2026-10-12', weight: null }], 'All', today)).toEqual([]);
    expect(inProgressRange('2026-10-26', 'All', today)).toBe(false);
  });

  it('converts circumference entry to centimetres and rejects photos over the limit', () => {
    expect(convertLength('10', 'in', 'cm')).toBe('25.4');
    expect(lengthToCentimetres(10, 'in')).toBe(25.4);
    expect(lengthToCentimetres(null, 'cm')).toBeNull();
    expect(progressPhotoIssue('image/gif', 100)).toBe('type');
    expect(progressPhotoIssue('image/jpeg', 2_097_153)).toBe('size');
    expect(progressPhotoIssue('image/png', 1200)).toBeNull();
  });
});
