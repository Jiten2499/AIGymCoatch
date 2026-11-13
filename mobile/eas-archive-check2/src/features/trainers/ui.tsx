import type { ReactNode } from 'react';
import { Pressable, View } from 'react-native';
import { useTheme } from '../../app/providers/ThemeProvider';
import { AppText } from '../../components/ui/AppText';

export function dayKey(value: Date) {
  const month = `${value.getMonth() + 1}`.padStart(2, '0');
  const day = `${value.getDate()}`.padStart(2, '0');
  return `${value.getFullYear()}-${month}-${day}`;
}

export function monthBounds(cursor: Date) {
  const start = new Date(cursor.getFullYear(), cursor.getMonth(), 1);
  const end = new Date(cursor.getFullYear(), cursor.getMonth() + 1, 0);
  return { from: dayKey(start), to: dayKey(end) };
}

export function clock(value: string) {
  return new Date(value).toLocaleTimeString([], { hour: 'numeric', minute: '2-digit' });
}

export function localKey(value: string) {
  return dayKey(new Date(value));
}

export function formatSeconds(total: number) {
  const minutes = Math.floor(total / 60);
  const seconds = total % 60;
  return `${`${Math.floor(minutes / 60)}`.padStart(2, '0')}:${`${minutes % 60}`.padStart(2, '0')}:${`${seconds}`.padStart(2, '0')}`;
}

export function useTrainerLayout() {
  const theme = useTheme();
  return {
    content: {
      gap: theme.spacing.lg,
      padding: theme.spacing.xl,
      paddingBottom: theme.spacing.xxxl,
    },
    refresh: theme.colors.accent,
    row: { flexDirection: 'row' as const, flexWrap: 'wrap' as const, gap: theme.spacing.sm },
    warning: theme.scheme === 'dark' ? '#fbbf24' : '#9a3412',
  };
}

export function TrainerCard({ children, onPress }: { children: ReactNode; onPress?: () => void }) {
  const theme = useTheme();
  const body = (
    <View style={[{ backgroundColor: theme.colors.surface, borderColor: theme.colors.border, borderRadius: theme.radius.lg, borderWidth: 1, gap: theme.spacing.sm, padding: theme.spacing.lg }, theme.shadow]}>
      {children}
    </View>
  );
  if (!onPress) {
    return body;
  }
  return (
    <Pressable accessibilityRole="button" onPress={onPress} style={({ pressed }) => ({ opacity: pressed ? 0.86 : 1 })}>
      {body}
    </Pressable>
  );
}

export function Stat({ label, value }: { label: string; value: string }) {
  const theme = useTheme();
  return (
    <View style={{ backgroundColor: theme.colors.surface, borderColor: theme.colors.border, borderRadius: theme.radius.lg, borderWidth: 1, flexGrow: 1, flexBasis: '46%', gap: theme.spacing.xs, padding: theme.spacing.lg }}>
      <AppText variant="title">{value}</AppText>
      <AppText tone="muted" variant="caption">{label}</AppText>
    </View>
  );
}

export function ProgressBar({ percent }: { percent: number }) {
  const theme = useTheme();
  const width = `${Math.max(0, Math.min(100, percent))}%` as const;
  return (
    <View style={{ backgroundColor: theme.colors.surfaceMuted, borderRadius: theme.radius.pill, height: 8, overflow: 'hidden' }}>
      <View style={{ backgroundColor: theme.colors.accent, height: 8, width }} />
    </View>
  );
}

export function StatusPill({ status }: { status: string }) {
  const theme = useTheme();
  const color = status === 'Completed' ? theme.colors.success : status === 'Cancelled' || status === 'Missed' ? theme.colors.danger : theme.colors.accent;
  return (
    <View style={{ alignSelf: 'flex-start', backgroundColor: theme.colors.surfaceMuted, borderRadius: theme.radius.pill, paddingHorizontal: theme.spacing.md, paddingVertical: theme.spacing.xs }}>
      <AppText style={{ color }} variant="caption">{status}</AppText>
    </View>
  );
}

export function FilterChip({ label, selected, onPress }: { label: string; selected: boolean; onPress: () => void }) {
  const theme = useTheme();
  return (
    <Pressable
      accessibilityRole="button"
      accessibilityState={{ selected }}
      onPress={onPress}
      style={{ backgroundColor: selected ? theme.colors.accent : theme.colors.surfaceMuted, borderRadius: theme.radius.pill, justifyContent: 'center', minHeight: 36, paddingHorizontal: theme.spacing.md }}
    >
      <AppText style={{ color: selected ? theme.colors.onAccent : theme.colors.text }} variant="label">{label}</AppText>
    </Pressable>
  );
}
