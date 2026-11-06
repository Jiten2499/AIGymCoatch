import { useState } from 'react';
import { Image } from 'react-native';
import { useTheme } from '../../app/providers/ThemeProvider';
import { AppText } from './AppText';
import { nameInitials } from './initials';

export function AppAvatar({ label, uri, fallbackUri, headers, size }: { label: string; uri?: string | null; fallbackUri?: string | null; headers?: Record<string, string>; size?: number }) {
  const theme = useTheme();
  const dimension = size ?? theme.control.avatar;
  const [failed, setFailed] = useState<string | null>(null);
  const primary = uri && failed !== uri ? uri : null;
  const secondary = !primary && fallbackUri && failed !== fallbackUri ? fallbackUri : null;
  const shown = primary ?? secondary;
  const initials = nameInitials(label);
  if (!shown) {
    return (
      <AppText accessibilityLabel={label} style={{ backgroundColor: theme.colors.surfaceMuted, borderRadius: dimension / 2, height: dimension, lineHeight: dimension, overflow: 'hidden', textAlign: 'center', width: dimension }} variant="label">
        {initials}
      </AppText>
    );
  }
  return <Image accessibilityLabel={label} onError={() => setFailed(shown)} source={{ uri: shown, headers: shown === uri ? headers : undefined }} style={{ borderRadius: dimension / 2, height: dimension, width: dimension }} />;
}
