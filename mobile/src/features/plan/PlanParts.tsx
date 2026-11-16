import { View } from 'react-native';
import { useTheme } from '../../app/providers/ThemeProvider';
import { AppText } from '../../components/ui/AppText';

export function ProgressRing({ percent }: { percent: number }) {
  const theme = useTheme();
  const value = Math.max(0, Math.min(100, Math.round(percent)));
  return (
    <View
      accessibilityLabel={`${value}%`}
      style={{
        alignItems: 'center',
        borderColor: theme.colors.accent,
        borderRadius: 48,
        borderWidth: 8,
        height: 96,
        justifyContent: 'center',
        width: 96,
      }}
    >
      <AppText variant="title">{value}%</AppText>
    </View>
  );
}

export function StatTile({ label, value }: { label: string; value: string }) {
  const theme = useTheme();
  return (
    <View style={{ backgroundColor: theme.colors.surfaceMuted, borderColor: theme.colors.border, borderRadius: theme.radius.md, borderWidth: 1, flex: 1, gap: theme.spacing.xs, minHeight: theme.control.minTouch, padding: theme.spacing.md }}>
      <AppText variant="label">{value}</AppText>
      <AppText tone="muted">{label}</AppText>
    </View>
  );
}

export function SectionTitle({ title }: { title: string }) {
  return <AppText variant="label">{title}</AppText>;
}
