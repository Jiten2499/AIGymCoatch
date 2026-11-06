import { describe, expect, it } from '@jest/globals';
import { advanceCursor, mergeById } from './polling';

describe('notification polling helpers', () => {
  it('deduplicates by id while preferring incoming rows', () => {
    const merged = mergeById(
      [
        { id: 'a', title: 'old-a' },
        { id: 'b', title: 'old-b' },
      ],
      [
        { id: 'b', title: 'new-b' },
        { id: 'c', title: 'new-c' },
      ],
    );
    expect(merged).toEqual([
      { id: 'a', title: 'old-a' },
      { id: 'b', title: 'new-b' },
      { id: 'c', title: 'new-c' },
    ]);
  });

  it('advances the sync cursor using server time', () => {
    expect(advanceCursor(null, '2026-10-07T12:00:00Z')).toBe('2026-10-07T12:00:00Z');
    expect(advanceCursor('2026-10-07T12:00:00Z', '2026-10-07T11:00:00Z')).toBe('2026-10-07T12:00:00Z');
    expect(advanceCursor('2026-10-07T12:00:00Z', '2026-10-07T13:00:00Z')).toBe('2026-10-07T13:00:00Z');
  });
});
