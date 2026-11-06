import { useState, type ReactNode } from 'react';
import { ActivityIndicator, Pressable, View } from 'react-native';
import { useTheme } from '../../app/providers/ThemeProvider';
import { AppText } from '../../components/ui/AppText';

export function ProgressButton({ label, onPress, loading = false, disabled = false, variant = 'primary' }: { label: string; onPress: () => void; loading?: boolean; disabled?: boolean; variant?: 'primary' | 'secondary' }) {
  const theme = useTheme();
  const filled = variant === 'primary';
  const isDisabled = disabled || loading;
  return (
    <Pressable
      accessibilityLabel={label}
      accessibilityRole="button"
      accessibilityState={{ busy: loading, disabled: isDisabled }}
      disabled={isDisabled}
      onPress={onPress}
      style={{ alignItems: 'center', backgroundColor: filled ? theme.colors.accent : theme.colors.surfaceMuted, borderRadius: theme.radius.pill, flexGrow: 1, justifyContent: 'center', minHeight: 48, opacity: isDisabled ? 0.55 : 1, paddingHorizontal: theme.spacing.lg }}
    >
      {loading ? <ActivityIndicator color={filled ? theme.colors.onAccent : theme.colors.text} /> : <AppText style={{ color: filled ? theme.colors.onAccent : theme.colors.text }} variant="label">{label}</AppText>}
    </Pressable>
  );
}

export function StepIndicator({ labels, step, onStep }: { labels: string[]; step: number; onStep: (step: number) => void }) {
  const theme = useTheme();
  return (
    <View style={{ flexDirection: 'row', gap: theme.spacing.xs }}>
      {labels.map((label, index) => {
        const active = index <= step;
        return (
          <Pressable accessibilityRole="button" accessibilityState={{ selected: index === step }} key={label} onPress={() => onStep(index)} style={{ alignItems: 'center', flex: 1, gap: theme.spacing.xs }}>
            <View style={{ alignItems: 'center', backgroundColor: active ? theme.colors.accent : theme.colors.surfaceMuted, borderRadius: 14, height: 28, justifyContent: 'center', width: 28 }}>
              <AppText style={{ color: active ? theme.colors.onAccent : theme.colors.muted }} variant="caption">{index + 1}</AppText>
            </View>
            <AppText numberOfLines={1} style={{ textAlign: 'center' }} variant="caption">{label}</AppText>
          </Pressable>
        );
      })}
    </View>
  );
}

export function MetricCard({ label, value, detail }: { label: string; value: string; detail?: string | null }) {
  const theme = useTheme();
  return (
    <View style={[{ backgroundColor: theme.colors.surface, borderRadius: theme.radius.lg, flexBasis: '47%', flexGrow: 1, gap: theme.spacing.xs, padding: theme.spacing.lg }, theme.shadow]}>
      <AppText tone="muted" variant="caption">{label}</AppText>
      <AppText variant="title">{value}</AppText>
      {detail ? <AppText tone="accent" variant="caption">{detail}</AppText> : null}
    </View>
  );
}

export function Segmented({ options, value, onChange }: { options: { id: string; label: string }[]; value: string; onChange: (id: string) => void }) {
  const theme = useTheme();
  return (
    <View style={{ backgroundColor: theme.colors.surfaceMuted, borderRadius: theme.radius.pill, flexDirection: 'row', padding: 4 }}>
      {options.map((option) => {
        const selected = option.id === value;
        return (
          <Pressable accessibilityRole="button" accessibilityState={{ selected }} key={option.id} onPress={() => onChange(option.id)} style={{ alignItems: 'center', backgroundColor: selected ? theme.colors.accent : 'transparent', borderRadius: theme.radius.pill, flex: 1, justifyContent: 'center', minHeight: 36 }}>
            <AppText style={{ color: selected ? theme.colors.onAccent : theme.colors.text }} variant="label">{option.label}</AppText>
          </Pressable>
        );
      })}
    </View>
  );
}

export function ScoreSlider({ label, clearLabel, value, min = 1, max, onChange }: { label: string; clearLabel: string; value: number | null; min?: number; max: number; onChange: (value: number | null) => void }) {
  const theme = useTheme();
  const [width, setWidth] = useState(0);
  const percent = value == null ? '0%' : `${Math.max(0, Math.min(100, (value / max) * 100))}%` as const;
  const choose = (location: number) => {
    if (width <= 0) {
      return;
    }
    const ratio = Math.min(1, Math.max(0, location / width));
    onChange(Math.max(min, Math.min(max, Math.round(min + ratio * (max - min)))));
  };
  return (
    <View style={{ gap: theme.spacing.sm }}>
      <View style={{ flexDirection: 'row', justifyContent: 'space-between' }}>
        <AppText variant="label">{label}</AppText>
        <AppText variant="label">{value == null ? '—' : `${value}/${max}`}</AppText>
      </View>
      <View
        accessibilityLabel={label}
        accessibilityRole="adjustable"
        onLayout={(event) => setWidth(event.nativeEvent.layout.width)}
        onMoveShouldSetResponder={() => true}
        onResponderMove={(event) => choose(event.nativeEvent.locationX)}
        onResponderRelease={(event) => choose(event.nativeEvent.locationX)}
        onStartShouldSetResponder={() => true}
        style={{ height: 28, justifyContent: 'center' }}
      >
        <SliderTrack percent={percent} />
      </View>
      {value != null ? <Pressable accessibilityRole="button" onPress={() => onChange(null)}><AppText tone="muted" variant="caption">{clearLabel}</AppText></Pressable> : null}
    </View>
  );
}

function SliderTrack({ percent }: { percent: `${number}%` | '0%' }) {
  const theme = useTheme();
  return (
    <View style={{ backgroundColor: theme.colors.surfaceMuted, borderRadius: theme.radius.pill, height: 8, overflow: 'hidden' }}>
      <View style={{ backgroundColor: theme.colors.accent, height: 8, width: percent }} />
    </View>
  );
}

export function ProgressPanel({ children }: { children: ReactNode }) {
  const theme = useTheme();
  return <View style={[{ backgroundColor: theme.colors.surface, borderRadius: theme.radius.lg, gap: theme.spacing.md, padding: theme.spacing.lg }, theme.shadow]}>{children}</View>;
}
