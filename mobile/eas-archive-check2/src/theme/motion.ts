export const duration = {
  fast: 120,
  normal: 200,
  slow: 320,
} as const;

export function motionDuration(preferred: number, reduceMotion: boolean): number {
  return reduceMotion ? 0 : preferred;
}
