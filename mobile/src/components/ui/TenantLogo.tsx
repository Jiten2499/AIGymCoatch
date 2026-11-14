import { useState } from 'react';
import { ActivityIndicator, Image, View } from 'react-native';
import { useTheme } from '../../app/providers/ThemeProvider';
import { AppText } from './AppText';

export function TenantLogo({ name, uri, decorative = false }: { name: string; uri?: string | null; decorative?: boolean }) {
  const theme = useTheme();
  const [failed, setFailed] = useState(false);
  const [loading, setLoading] = useState(Boolean(uri) && !failed);
  const size = theme.control.logo;
  const a11y = decorative
    ? { accessibilityElementsHidden: true, importantForAccessibility: 'no-hide-descendants' as const }
    : { accessibilityLabel: name };
  if (!uri || failed) {
    return (
      <View {...a11y} style={{ alignItems: 'center', backgroundColor: theme.colors.accent, borderRadius: theme.radius.sm, height: size, justifyContent: 'center', width: size }}>
        <AppText style={{ color: theme.colors.onAccent }} variant="label">{name.slice(0, 1).toUpperCase()}</AppText>
      </View>
    );
  }
  return (
    <View {...a11y} style={{ height: size, width: size }}>
      {loading ? <ActivityIndicator color={theme.colors.accent} /> : null}
      <Image
        accessibilityLabel={decorative ? undefined : name}
        onError={() => {
          setFailed(true);
          setLoading(false);
        }}
        onLoadEnd={() => setLoading(false)}
        source={{ uri }}
        style={{ borderRadius: theme.radius.sm, height: size, width: size }}
      />
    </View>
  );
}
