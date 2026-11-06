import { leaveDuration } from './leaves.api';

describe('leaveDuration', () => {
  it('counts inclusive calendar days', () => {
    expect(leaveDuration('2026-10-01', '2026-10-03')).toBe(3);
    expect(leaveDuration('2026-10-01', '2026-10-01')).toBe(1);
    expect(leaveDuration('2026-10-03', '2026-10-01')).toBe(0);
  });
});
