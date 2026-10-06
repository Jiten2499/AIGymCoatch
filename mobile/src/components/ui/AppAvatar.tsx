import { Image } from 'react-native';
import { useTheme } from '../../app/providers/ThemeProvider';
import { AppText } from './AppText';

export function AppAvatar({ label, uri }: { label: string; uri?: string | null }) {
  const theme = useTheme();
  const size = theme.control.avatar;
  const initials = label.slice(0, 2).toUpperCase();
  if (!uri) {
    return (
      <AppText accessibilityLabel={label} style={{ backgroundColor: theme.colors.surfaceMuted, borderRadius: size / 2, height: size, lineHeight: size, overflow: 'hidden', textAlign: 'center', width: size }} variant="label">
        {initials}
      </AppText>
    );
  }
  return <Image accessibilityLabel={label} source={{ uri }} style={{ borderRadius: size / 2, height: size, width: size }} />;
}
