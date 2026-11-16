import Svg, { Circle, Path, Rect } from 'react-native-svg';

export type TrainerIconName =
  | 'home'
  | 'users'
  | 'bell'
  | 'search'
  | 'sliders'
  | 'dumbbell'
  | 'clipboard'
  | 'pencil'
  | 'plus'
  | 'trash'
  | 'grip'
  | 'ellipsis'
  | 'calendar'
  | 'chart'
  | 'clock'
  | 'check'
  | 'save'
  | 'chevron'
  | 'send'
  | 'history'
  | 'target'
  | 'activity'
  | 'close'
  | 'userCheck'
  | 'library';

type IconProps = { name: TrainerIconName; color: string; size?: number };

export function TrainerIcon({ name, color, size = 22 }: IconProps) {
  const common = { fill: 'none' as const, stroke: color, strokeLinecap: 'round' as const, strokeLinejoin: 'round' as const, strokeWidth: 1.8 };
  return (
    <Svg height={size} viewBox="0 0 24 24" width={size}>
      {name === 'home' ? <Path {...common} d="M4 10.5 12 4l8 6.5V20a1 1 0 0 1-1 1h-5v-6H10v6H5a1 1 0 0 1-1-1z" /> : null}
      {name === 'users' ? <Path {...common} d="M16 20v-1a3 3 0 0 0-3-3H7a3 3 0 0 0-3 3v1M10 12a3 3 0 1 0 0-6 3 3 0 0 0 0 6M20 20v-1a3 3 0 0 0-2.2-2.9M16 4.1a3 3 0 0 1 0 5.8" /> : null}
      {name === 'bell' ? <Path {...common} d="M6 9a6 6 0 1 1 12 0c0 7 2 7 2 7H4s2 0 2-7M10 19a2 2 0 0 0 4 0" /> : null}
      {name === 'search' ? <Path {...common} d="M11 18a7 7 0 1 0 0-14 7 7 0 0 0 0 14M20 20l-3.5-3.5" /> : null}
      {name === 'sliders' ? <Path {...common} d="M4 7h16M4 17h16M8 7v4M16 13v4" /> : null}
      {name === 'dumbbell' ? <Path {...common} d="M6 8v8M18 8v8M4 10v4M20 10v4M6 12h12" /> : null}
      {name === 'clipboard' ? <Path {...common} d="M9 4h6a1 1 0 0 1 1 1v1H8V5a1 1 0 0 1 1-1M8 6H6v14h12V6h-2M9 12h6M9 16h4" /> : null}
      {name === 'pencil' ? <Path {...common} d="M12 20h8M16.5 3.5a2 2 0 0 1 3 3L8 18l-4 1 1-4z" /> : null}
      {name === 'plus' ? <Path {...common} d="M12 5v14M5 12h14" /> : null}
      {name === 'trash' ? <Path {...common} d="M4 7h16M9 7V5h6v2M8 7l1 12h6l1-12" /> : null}
      {name === 'grip' ? <Path {...common} d="M9 6h.01M9 12h.01M9 18h.01M15 6h.01M15 12h.01M15 18h.01" /> : null}
      {name === 'ellipsis' ? <Path {...common} d="M6 12h.01M12 12h.01M18 12h.01" /> : null}
      {name === 'calendar' ? <Path {...common} d="M4 6h16v14H4zM8 4v4M16 4v4M4 10h16" /> : null}
      {name === 'chart' ? <Path {...common} d="M4 19V5M4 19h16M8 16v-5M12 16V8M16 16v-3" /> : null}
      {name === 'clock' ? <><Circle {...common} cx="12" cy="12" r="8" /><Path {...common} d="M12 8v5l3 2" /></> : null}
      {name === 'check' ? <Path {...common} d="M12 21a9 9 0 1 0 0-18 9 9 0 0 0 0 18M8 12l3 3 5-6" /> : null}
      {name === 'save' ? <Path {...common} d="M5 4h11l3 3v13H5zM8 4v5h7M8 20v-6h8v6" /> : null}
      {name === 'chevron' ? <Path {...common} d="M9 6l6 6-6 6" /> : null}
      {name === 'send' ? <Path {...common} d="M4 12 20 4l-6 16-2-7z" /> : null}
      {name === 'history' ? <Path {...common} d="M4 12a8 8 0 1 0 2-5M4 5v4h4M12 8v5l3 2" /> : null}
      {name === 'target' ? <><Circle {...common} cx="12" cy="12" r="7" /><Circle {...common} cx="12" cy="12" r="3" /></> : null}
      {name === 'activity' ? <Path {...common} d="M3 12h4l2-6 4 12 2-6h6" /> : null}
      {name === 'close' ? <Path {...common} d="M6 6l12 12M18 6 6 18" /> : null}
      {name === 'userCheck' ? <Path {...common} d="M16 20v-1a3 3 0 0 0-3-3H7a3 3 0 0 0-3 3v1M10 12a3 3 0 1 0 0-6 3 3 0 0 0 0 6M17 11l2 2 3-3" /> : null}
      {name === 'library' ? <><Rect {...common} height="14" rx="1" width="6" x="4" y="5" /><Path {...common} d="M14 5h6v14h-6M14 9h6" /></> : null}
    </Svg>
  );
}
