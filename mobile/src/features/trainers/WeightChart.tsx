import { useState } from 'react';
import { View } from 'react-native';
import Svg, { Circle, Line, Polyline, Text as SvgText } from 'react-native-svg';
import { useTranslation } from 'react-i18next';
import { useTheme } from '../../app/providers/ThemeProvider';
import { AppText } from '../../components/ui/AppText';
import { formatMonth, formatProgressDate } from './progressSeries';
import { ProgressPanel } from './progressUi';

type Point = { date: string; weight: number };

export function WeightChart({ points, unit }: { points: Point[]; unit: string }) {
  const { t } = useTranslation();
  const theme = useTheme();
  const [width, setWidth] = useState(0);
  const [selected, setSelected] = useState<number | null>(null);
  const height = 196;
  const active = selected != null ? points[selected] : null;

  return (
    <ProgressPanel>
      <AppText variant="label">{t('trainer.weight')} ({unit})</AppText>
      <View accessibilityLabel={t('trainer.weightChart', { count: points.length })} onLayout={(event) => setWidth(event.nativeEvent.layout.width)}>
        {width > 0 && points.length > 0 ? <ChartSvg height={height} points={points} selected={selected} themeColor={theme.colors.text} grid={theme.colors.border} accent={theme.colors.accent} muted={theme.colors.muted} width={width} onSelect={setSelected} /> : null}
        {points.length < 2 ? <AppText tone="muted">{points.length === 0 ? t('trainer.chartEmpty') : t('trainer.chartSingle')}</AppText> : null}
        {active ? <AppText variant="label">{formatProgressDate(active.date)} · {active.weight} {unit}</AppText> : null}
      </View>
    </ProgressPanel>
  );
}

function ChartSvg({ points, width, height, selected, themeColor, grid, accent, muted, onSelect }: { points: Point[]; width: number; height: number; selected: number | null; themeColor: string; grid: string; accent: string; muted: string; onSelect: (index: number) => void }) {
  const padLeft = 36;
  const padRight = 8;
  const padTop = 16;
  const padBottom = 28;
  const innerWidth = Math.max(width - padLeft - padRight, 1);
  const innerHeight = height - padTop - padBottom;
  const weights = points.map((point) => point.weight);
  const min = Math.min(...weights);
  const max = Math.max(...weights);
  const span = max - min || 1;
  const xAt = (index: number) => padLeft + (points.length === 1 ? innerWidth / 2 : (index / (points.length - 1)) * innerWidth);
  const yAt = (weight: number) => padTop + (1 - (weight - min) / span) * innerHeight;
  const coordinates = points.map((point, index) => `${xAt(index)},${yAt(point.weight)}`).join(' ');
  const ticks = [max, (max + min) / 2, min];
  const labelIndexes = points.length <= 5 ? points.map((_, index) => index) : [0, Math.round((points.length - 1) / 2), points.length - 1];

  return (
    <Svg height={height} width={width}>
      {ticks.map((tick, index) => (
        <Line key={`grid-${index}`} stroke={grid} strokeWidth={1} x1={padLeft} x2={width - padRight} y1={yAt(tick)} y2={yAt(tick)} />
      ))}
      {ticks.map((tick, index) => (
        <SvgText key={`label-${index}`} fill={muted} fontSize={11} x={0} y={yAt(tick) + 4}>{String(Math.round(tick))}</SvgText>
      ))}
      {points.length > 1 ? <Polyline fill="none" points={coordinates} stroke={accent} strokeLinejoin="round" strokeWidth={3} /> : null}
      {points.map((point, index) => (
        <Circle key={`${point.date}-${index}`} cx={xAt(index)} cy={yAt(point.weight)} fill={selected === index ? themeColor : accent} onPress={() => onSelect(index)} r={selected === index ? 6 : 4} />
      ))}
      {labelIndexes.map((index) => (
        <SvgText key={`month-${index}`} fill={muted} fontSize={11} textAnchor="middle" x={xAt(index)} y={height - 6}>{formatMonth(points[index].date)}</SvgText>
      ))}
    </Svg>
  );
}
