import { useState } from 'react';
import { ActivityIndicator, Image, View } from 'react-native';
import { useTheme } from '../../app/providers/ThemeProvider';
import { AppText } from './AppText';

export function TenantLogo({ name, uri }: { name: string; uri?: string | null }) {
  const theme = useTheme();
  const [failed, setFailed] = useState(false);
  const [loading, setLoading] = useState(Boolean(uri) && !failed);
  const size = theme.control.logo;
  if (!uri || failed) {
    return (
      <View accessibilityLabel={name} style={{ alignItems: 'center', backgroundColor: theme.colors.accent, borderRadius: theme.radius.sm, height: size, justifyContent: 'center', width: size }}>
        <AppText style={{ color: theme.colors.onAccent }} variant="label">{name.slice(0, 1).toUpperCase()}</AppText>
      </View>
    );
  }
  return (
    <View style={{ height: size, width: size }}>
      {loading ? <ActivityIndicator color={theme.colors.accent} /> : null}
      <Image
        accessibilityLabel={name}
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
