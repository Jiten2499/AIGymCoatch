import { useState } from 'react';
import { Pressable, ScrollView, View } from 'react-native';
import { useTheme } from '../../../app/providers/ThemeProvider';
import { AppText } from '../../../components/ui/AppText';
import { formatNumber } from '../../../services/localization/format';
import type { ChartSeries } from './analyticsRange';

export function TrendChart({ series, locale }: { series: ChartSeries[]; locale: string }) {
  const theme = useTheme();
  const colors = [theme.colors.accent, theme.colors.success, theme.colors.danger];
  const max = Math.max(1, ...series.flatMap((item) => item.points.map((point) => point.value)));
  const [selected, setSelected] = useState(series[0]?.points.length ? series[0].points.length - 1 : 0);
  const caption = series.map((item) => `${item.label}: ${formatNumber(item.points[selected]?.value ?? 0, locale)}`).join(' · ');

  return (
    <View style={{ gap: theme.spacing.sm }}>
      {series.length > 1 ? (
        <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: theme.spacing.md }}>
          {series.map((item, index) => (
            <View key={item.key} style={{ alignItems: 'center', flexDirection: 'row', gap: 6 }}>
              <View style={{ backgroundColor: colors[index % colors.length], borderRadius: 4, height: 8, width: 8 }} />
              <AppText variant="label">{item.label}</AppText>
            </View>
          ))}
        </View>
      ) : null}
      <ScrollView horizontal showsHorizontalScrollIndicator={false}>
        <View style={{ alignItems: 'flex-end', flexDirection: 'row', gap: 4, minHeight: 140 }}>
          {series[0]?.points.map((point, pointIndex) => (
            <Pressable
              accessibilityLabel={`${point.detail} ${series.map((item) => `${item.label} ${formatNumber(item.points[pointIndex]?.value ?? 0, locale)}`).join(', ')}`}
              accessibilityRole="button"
              key={point.detail}
              onPress={() => setSelected(pointIndex)}
              style={{ alignItems: 'center', gap: 4, minWidth: 28 }}
            >
              <View style={{ alignItems: 'flex-end', flexDirection: 'row', gap: 2, height: 112 }}>
                {series.map((item, seriesIndex) => {
                  const value = item.points[pointIndex]?.value ?? 0;
                  return (
                    <View
                      key={item.key}
                      style={{
                        backgroundColor: colors[seriesIndex % colors.length],
                        borderRadius: 3,
                        height: Math.max(value === 0 ? 2 : 8, Math.round((value / max) * 112)),
                        opacity: value === 0 ? 0.35 : 1,
                        width: series.length > 1 ? 6 : 14,
                      }}
                    />
                  );
                })}
              </View>
              <AppText tone="muted" variant="label" style={{ fontSize: 10 }}>
                {pointIndex % labelStep(series[0].points.length) === 0 ? point.label : ''}
              </AppText>
            </Pressable>
          ))}
        </View>
      </ScrollView>
      <AppText>{series[0]?.points[selected]?.detail}. {caption}</AppText>
    </View>
  );
}

function labelStep(count: number): number {
  if (count <= 8) {
    return 1;
  }
  if (count <= 16) {
    return 2;
  }
  return Math.ceil(count / 6);
}
