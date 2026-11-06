import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { View } from 'react-native';
import { AppText } from '../../components/ui/AppText';
import type { MemberProgress } from './api/workspace.api';
import { formatChange, formatMeasure, lengthUnit, weightUnit } from './progressForm';
import { ProgressPhotoView } from './ProgressPhotoView';
import { formatProgressDate, inProgressRange, weightSeries, type ProgressRange } from './progressSeries';
import { MetricCard, ProgressPanel, Segmented } from './progressUi';
import { FilterChip } from './ui';
import { WeightChart } from './WeightChart';

const ranges: ProgressRange[] = ['3M', '6M', '1Y', 'All'];

export function ProgressOverview({ memberId, records, today, onEdit }: { memberId: string; records: MemberProgress[]; today: string; onEdit: (assessmentId: string) => void }) {
  const { t } = useTranslation();
  const [tab, setTab] = useState('measurements');
  const [range, setRange] = useState<ProgressRange>('3M');
  const visible = records.filter((item) => inProgressRange(item.assessmentDate, range, today));
  const latest = visible[0];
  const weight = weightUnit(latest?.unitSystem ?? 'Metric');
  const length = lengthUnit(latest?.unitSystem ?? 'Metric');
  const points = weightSeries(records.map((item) => ({ assessmentDate: item.assessmentDate, weight: item.weight.value })), range, today);

  return (
    <View style={{ gap: 16 }}>
      <Segmented
        onChange={setTab}
        options={[
          { id: 'measurements', label: t('trainer.measurements') },
          { id: 'assessments', label: t('trainer.assessments') },
          { id: 'photos', label: t('trainer.photos') },
        ]}
        value={tab}
      />
      <View style={{ flexDirection: 'row', gap: 8 }}>
        {ranges.map((item) => <FilterChip key={item} label={t(`trainer.range${item}`)} onPress={() => setRange(item)} selected={range === item} />)}
      </View>
      {tab === 'measurements' ? (
        <>
          <WeightChart points={points} unit={weight} />
          {latest ? (
            <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: 12 }}>
              <MetricCard detail={signed(latest.weight.change, weight)} label={t('trainer.currentWeight')} value={formatMeasure(latest.weight.value, weight)} />
              <MetricCard detail={signed(latest.bodyFatPercentage.change, '%')} label={t('trainer.bodyFat')} value={formatMeasure(latest.bodyFatPercentage.value, '%')} />
              <MetricCard detail={signed(latest.waist.change, length)} label={t('trainer.waist')} value={formatMeasure(latest.waist.value, length)} />
              <MetricCard detail={signed(latest.chest.change, length)} label={t('trainer.chest')} value={formatMeasure(latest.chest.value, length)} />
            </View>
          ) : <AppText tone="muted">{t('trainer.noProgress')}</AppText>}
          <AppText variant="title">{t('trainer.progressHistory')}</AppText>
          {visible.map((item, index) => <HistoryCard key={item.id} item={item} latest={index === 0} onEdit={item.canEdit ? () => onEdit(item.id) : undefined} />)}
        </>
      ) : null}
      {tab === 'assessments' ? <AssessmentList records={visible} /> : null}
      {tab === 'photos' ? <PhotoList memberId={memberId} records={visible} /> : null}
    </View>
  );
}

function signed(change: number | null, unit: string) {
  const text = formatChange(change, unit);
  return text ? `${text}` : null;
}

function HistoryCard({ item, latest, onEdit }: { item: MemberProgress; latest: boolean; onEdit?: () => void }) {
  const { t } = useTranslation();
  const weight = weightUnit(item.unitSystem);
  const length = lengthUnit(item.unitSystem);
  return (
    <ProgressPanel>
      <View style={{ flexDirection: 'row', justifyContent: 'space-between' }}>
        <AppText variant="label">{formatProgressDate(item.assessmentDate)}</AppText>
        {latest ? <AppText tone="accent" variant="caption">{t('trainer.latest')}</AppText> : null}
      </View>
      <AppText>{formatMeasure(item.weight.value, weight)} · {formatMeasure(item.bodyFatPercentage.value, '%')} · {t('trainer.waist')} {formatMeasure(item.waist.value, length)}</AppText>
      {item.notes ? <AppText tone="muted">{item.notes}</AppText> : null}
      {onEdit ? <AppText accessibilityRole="button" onPress={onEdit} variant="label">{t('trainer.editProgress')}</AppText> : null}
    </ProgressPanel>
  );
}

function AssessmentList({ records }: { records: MemberProgress[] }) {
  const { t } = useTranslation();
  if (records.length === 0) {
    return <AppText tone="muted">{t('trainer.noAssessments')}</AppText>;
  }
  return (
    <>
      {records.map((item) => (
        <ProgressPanel key={item.id}>
          <AppText variant="label">{formatProgressDate(item.assessmentDate)}</AppText>
          <AppText>{t('trainer.strength')} {score(item.strengthScore.value)} · {t('trainer.endurance')} {score(item.enduranceScore.value)}</AppText>
          <AppText>{t('trainer.flexibility')} {score(item.flexibilityScore.value)} · {t('trainer.coreStrength')} {score(item.coreScore.value)}</AppText>
          <AppText>{t('trainer.bodyFat')} {formatMeasure(item.bodyFatPercentage.value, '%')} · {t('trainer.visceralFat')} {score(item.visceralFat.value)}</AppText>
        </ProgressPanel>
      ))}
    </>
  );
}

function PhotoList({ memberId, records }: { memberId: string; records: MemberProgress[] }) {
  const { t } = useTranslation();
  const withPhotos = records.filter((item) => item.photoIds.length > 0 || item.hasPhoto);
  if (withPhotos.length === 0) {
    return <AppText tone="muted">{t('trainer.noPhotos')}</AppText>;
  }
  return (
    <>
      {withPhotos.map((item) => (
        <ProgressPanel key={item.id}>
          <AppText variant="label">{formatProgressDate(item.assessmentDate)}</AppText>
          <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: 8 }}>
            {item.photoIds.length > 0
              ? item.photoIds.map((photoId) => <ProgressPhotoView assessmentId={item.id} key={photoId} memberId={memberId} photoId={photoId} />)
              : <ProgressPhotoView assessmentId={item.id} memberId={memberId} photoId={null} />}
          </View>
        </ProgressPanel>
      ))}
    </>
  );
}

function score(value: number | null) {
  return value == null ? '—' : String(value);
}
