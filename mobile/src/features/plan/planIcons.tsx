import Svg, { Circle, Path, Rect } from 'react-native-svg';

const lime = '#c8f54e';

export function CalendarIcon({ color = lime, size = 22 }: { color?: string; size?: number }) {
  return (
    <Svg height={size} viewBox="0 0 24 24" width={size}>
      <Rect fill="none" height="16" rx="2" stroke={color} strokeWidth="1.8" width="18" x="3" y="5" />
      <Path d="M3 10h18" stroke={color} strokeWidth="1.8" />
      <Path d="M8 3v4M16 3v4" stroke={color} strokeLinecap="round" strokeWidth="1.8" />
    </Svg>
  );
}

export function DumbbellIcon({ color = lime, size = 22 }: { color?: string; size?: number }) {
  return (
    <Svg height={size} viewBox="0 0 24 24" width={size}>
      <Rect fill={color} height="8" rx="1" width="3" x="3" y="8" />
      <Rect fill={color} height="8" rx="1" width="3" x="18" y="8" />
      <Path d="M6 12h12" stroke={color} strokeLinecap="round" strokeWidth="2.4" />
    </Svg>
  );
}

export function ClockIcon({ color = lime, size = 22 }: { color?: string; size?: number }) {
  return (
    <Svg height={size} viewBox="0 0 24 24" width={size}>
      <Circle cx="12" cy="12" fill="none" r="8" stroke={color} strokeWidth="1.8" />
      <Path d="M12 8v5l3 2" fill="none" stroke={color} strokeLinecap="round" strokeWidth="1.8" />
    </Svg>
  );
}

export function FlameIcon({ color = lime, size = 20 }: { color?: string; size?: number }) {
  return (
    <Svg height={size} viewBox="0 0 24 24" width={size}>
      <Path d="M12 2c1 4 5 5 5 10a5 5 0 1 1-10 0c0-2.5 1.6-3.6 2.6-5.2.2 2 1.2 2.8 2.6 2.8C11.4 7.4 11 5 12 2z" fill={color} />
    </Svg>
  );
}

export function MuscleIcon({ color = lime, size = 20 }: { color?: string; size?: number }) {
  return (
    <Svg height={size} viewBox="0 0 24 24" width={size}>
      <Path d="M7 20c.4-3.2 1.4-5.2 3.2-6.4 1.6.6 3.2-.2 4.4-1.8 1.2-1.6 2.2-1.8 3.2-.8-1.2 2.6-3.2 4.2-5.8 5-1.2 1.8-2.2 3.4-2.6 5.6H7z" fill={color} />
      <Circle cx="16.5" cy="7" fill={color} r="2.1" />
    </Svg>
  );
}

export function RunnerIcon({ color = lime, size = 20 }: { color?: string; size?: number }) {
  return (
    <Svg height={size} viewBox="0 0 24 24" width={size}>
      <Circle cx="15" cy="5" fill={color} r="2" />
      <Path d="M7 20l3.2-5.2 2.2 1.6 2.6-1.2-1.6-3.4 3.6.8 1.6 2.6M6.5 14.5l3.2.8" fill="none" stroke={color} strokeLinecap="round" strokeLinejoin="round" strokeWidth="1.8" />
    </Svg>
  );
}

export function ChevronIcon({ color = '#9aab9f', size = 18 }: { color?: string; size?: number }) {
  return (
    <Svg height={size} viewBox="0 0 24 24" width={size}>
      <Path d="M9 6l6 6-6 6" fill="none" stroke={color} strokeLinecap="round" strokeLinejoin="round" strokeWidth="2" />
    </Svg>
  );
}
