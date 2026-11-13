import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Image, Pressable, View } from 'react-native';
import { FormField } from '../../components/forms/FormField';
import { AppText } from '../../components/ui/AppText';
import { AppTextInput } from '../../components/ui/AppTextInput';
import { useTheme } from '../../app/providers/ThemeProvider';
import type { MemberProgress } from './api/workspace.api';
import { useDeleteProgressPhoto, useSaveMemberProgress, type ProgressDraftPhoto } from './api/workspace.queries';
import { parseOptionalMeasure, type OptionalNumber } from './progressForm';
import { pickProgressPhotos, progressPhotoMax } from './progressPhotos';
import { ProgressPhotoView } from './ProgressPhotoView';
import { convertLength, formatProgressDate, lengthToCentimetres, trimNumber, type LengthUnit } from './progressSeries';
import { dayKey } from './ui';
import { ProgressButton, ProgressPanel, ScoreSlider, StepIndicator } from './progressUi';

type LengthKey = 'height' | 'waist' | 'chest' | 'hip' | 'arm' | 'rightArm' | 'thigh' | 'rightThigh';
type ScoreKey = 'strength' | 'endurance' | 'flexibility' | 'core';

const lengthKeys: LengthKey[] = ['height', 'waist', 'chest', 'hip', 'arm', 'rightArm', 'thigh', 'rightThigh'];
const emptyLengths = (): Record<LengthKey, string> => ({ height: '', waist: '', chest: '', hip: '', arm: '', rightArm: '', thigh: '', rightThigh: '' });

export function ProgressWizard({ memberId, assessmentId, existing, onDone, onCancel }: { memberId: string; assessmentId?: string; existing?: MemberProgress; onDone: () => void; onCancel: () => void }) {
  const { t } = useTranslation();
  const theme = useTheme();
  const save = useSaveMemberProgress(memberId);
  const removePhoto = useDeleteProgressPhoto(memberId);
  const [step, setStep] = useState(0);
  const [hydratedId, setHydratedId] = useState<string | null>(null);
  const [date, setDate] = useState(dayKey(new Date()));
  const [unit, setUnit] = useState<LengthUnit>('cm');
  const [weight, setWeight] = useState('');
  const [lengths, setLengths] = useState(emptyLengths);
  const [scores, setScores] = useState<Record<ScoreKey, number | null>>({ strength: null, endurance: null, flexibility: null, core: null });
  const [bodyFat, setBodyFat] = useState<number | null>(null);
  const [visceral, setVisceral] = useState<number | null>(null);
  const [fitness, setFitness] = useState<number | null>(null);
  const [performance, setPerformance] = useState('');
  const [notes, setNotes] = useState('');
  const [achieved, setAchieved] = useState('');
  const [nextGoals, setNextGoals] = useState('');
  const [consent, setConsent] = useState(false);
  const [drafts, setDrafts] = useState<ProgressDraftPhoto[]>([]);
  const [uploadProgress, setUploadProgress] = useState<Record<string, number>>({});
  const [error, setError] = useState<string | null>(null);

  if (existing && hydratedId !== existing.id) {
    setHydratedId(existing.id);
    setDate(existing.assessmentDate.slice(0, 10));
    setWeight(existing.weight.value?.toString() ?? '');
    setLengths({
      height: text(existing.height.value), waist: text(existing.waist.value), chest: text(existing.chest.value), hip: text(existing.hip.value),
      arm: text(existing.arm.value), rightArm: text(existing.rightArm.value), thigh: text(existing.thigh.value), rightThigh: text(existing.rightThigh.value),
    });
    setScores({ strength: existing.strengthScore.value, endurance: existing.enduranceScore.value, flexibility: existing.flexibilityScore.value, core: existing.coreScore.value });
    setBodyFat(existing.bodyFatPercentage.value);
    setVisceral(existing.visceralFat.value);
    setFitness(existing.fitnessScore.value);
    setPerformance(existing.trainingPerformance ?? '');
    setNotes(existing.notes ?? '');
    setAchieved(existing.achievedGoals ?? '');
    setNextGoals(existing.nextGoals ?? '');
    setConsent(existing.photoConsent);
  }

  if (assessmentId && !existing) {
    return <AppText tone="muted">{t('trainer.noProgress')}</AppText>;
  }

  const labels = [t('trainer.measurements'), t('trainer.assessments'), t('trainer.photos'), t('trainer.review')];
  const existingPhotos = existing?.photoIds ?? [];
  const slots = progressPhotoMax - existingPhotos.length - drafts.length;

  const changeUnit = (next: LengthUnit) => {
    setLengths((current) => Object.fromEntries(lengthKeys.map((key) => [key, convertLength(current[key], unit, next)])) as Record<LengthKey, string>);
    setUnit(next);
  };

  const validateMeasures = () => {
    const today = dayKey(new Date());
    const values = [parseOptionalMeasure(weight), ...lengthKeys.map((key) => lengthToCentimetres(parseOptionalMeasure(lengths[key]), unit))];
    if (values.includes('invalid') || !/^\d{4}-\d{2}-\d{2}$/.test(date) || date > today) {
      setError(t('trainer.progressInvalid'));
      return false;
    }
    setError(null);
    return true;
  };

  const submit = () => {
    if (!validateMeasures()) {
      setStep(0);
      return;
    }
    if ((drafts.length > 0 || existingPhotos.length > 0) && !consent) {
      setError(t('trainer.consentOff'));
      setStep(2);
      return;
    }
    const centimetres = (key: LengthKey) => optional(lengthToCentimetres(parseOptionalMeasure(lengths[key]), unit));
    save.mutate({
      assessmentId,
      photos: drafts,
      onProgress: (key, ratio) => setUploadProgress((current) => ({ ...current, [key]: ratio })),
      body: {
        assessmentDate: date,
        weight: optional(parseOptionalMeasure(weight)),
        height: centimetres('height'),
        waist: centimetres('waist'),
        chest: centimetres('chest'),
        hip: centimetres('hip'),
        arm: centimetres('arm'),
        rightArm: centimetres('rightArm'),
        thigh: centimetres('thigh'),
        rightThigh: centimetres('rightThigh'),
        bodyFatPercentage: bodyFat,
        visceralFat: visceral,
        fitnessScore: fitness,
        strengthScore: scores.strength,
        enduranceScore: scores.endurance,
        flexibilityScore: scores.flexibility,
        coreScore: scores.core,
        trainingPerformance: blank(performance),
        notes: blank(notes),
        achievedGoals: blank(achieved),
        nextGoals: blank(nextGoals),
        photoConsent: consent,
      },
    }, {
      onSuccess: (result) => {
        if (result.failed.length > 0) {
          setDrafts((current) => current.filter((photo) => result.failed.includes(photo.key)));
          setError(result.failure ? `${t('trainer.photoFailed')} ${result.failure}` : t('trainer.photoFailed'));
          setStep(2);
          return;
        }
        onDone();
      },
    });
  };

  const addPhotos = async (source: 'camera' | 'library') => {
    if (!consent) {
      setError(t('trainer.consentOff'));
      return;
    }
    const picked = await pickProgressPhotos(source, slots);
    const problem = picked.find((item) => 'error' in item);
    if (problem && 'error' in problem) {
      setError(t(problem.error === 'size' ? 'trainer.photoSize' : problem.error === 'type' ? 'trainer.photoType' : 'trainer.photoPermission'));
    }
    setDrafts((current) => [...current, ...picked.flatMap((item) => ('uri' in item ? [{ key: `${item.fileName}-${item.uri}`, uri: item.uri, mimeType: item.mimeType, fileName: item.fileName }] : []))]);
  };

  return (
    <View style={{ gap: theme.spacing.lg }}>
      <StepIndicator labels={labels} onStep={(next) => { if (next < step || validateMeasures()) setStep(next); }} step={step} />
      {step === 0 ? <Measurements date={date} lengths={lengths} unit={unit} weight={weight} onDate={setDate} onLength={(key, value) => setLengths((current) => ({ ...current, [key]: value }))} onUnit={changeUnit} onWeight={setWeight} /> : null}
      {step === 1 ? (
        <View style={{ gap: theme.spacing.lg }}>
          <AppText variant="title">{t('trainer.fitnessAssessment')}</AppText>
          <ScoreSlider clearLabel={t('trainer.clearValue')} label={t('trainer.strength')} max={10} onChange={(value) => setScores((current) => ({ ...current, strength: value }))} value={scores.strength} />
          <ScoreSlider clearLabel={t('trainer.clearValue')} label={t('trainer.endurance')} max={10} onChange={(value) => setScores((current) => ({ ...current, endurance: value }))} value={scores.endurance} />
          <ScoreSlider clearLabel={t('trainer.clearValue')} label={t('trainer.flexibility')} max={10} onChange={(value) => setScores((current) => ({ ...current, flexibility: value }))} value={scores.flexibility} />
          <ScoreSlider clearLabel={t('trainer.clearValue')} label={t('trainer.coreStrength')} max={10} onChange={(value) => setScores((current) => ({ ...current, core: value }))} value={scores.core} />
          <AppText variant="title">{t('trainer.bodyComposition')}</AppText>
          <ScoreSlider clearLabel={t('trainer.clearValue')} label={t('trainer.bodyFat')} max={100} min={0} onChange={setBodyFat} value={bodyFat} />
          <ScoreSlider clearLabel={t('trainer.clearValue')} label={t('trainer.visceralFat')} max={60} onChange={setVisceral} value={visceral} />
        </View>
      ) : null}
      {step === 2 ? (
        <View style={{ gap: theme.spacing.md }}>
          <Pressable accessibilityRole="checkbox" accessibilityState={{ checked: consent }} onPress={() => setConsent((value) => !value)}>
            <AppText>{consent ? t('trainer.consentOn') : t('trainer.consentOff')}</AppText>
          </Pressable>
          <AppText tone="muted">{t('trainer.photoConsentNote')}</AppText>
          <View style={{ flexDirection: 'row', gap: theme.spacing.sm }}>
            <ProgressButton disabled={!consent || slots <= 0} label={t('trainer.takePhoto')} onPress={() => { void addPhotos('camera'); }} variant="secondary" />
            <ProgressButton disabled={!consent || slots <= 0} label={t('trainer.choosePhoto')} onPress={() => { void addPhotos('library'); }} variant="secondary" />
          </View>
          <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: theme.spacing.sm }}>
            {assessmentId ? existingPhotos.map((photoId) => (
              <ProgressPhotoView assessmentId={assessmentId} key={photoId} memberId={memberId} onRemove={() => { void removePhoto.mutateAsync({ assessmentId, photoId }).catch(() => setError(t('common.errorTitle'))); }} photoId={photoId} />
            )) : null}
            {drafts.map((photo) => (
              <View key={photo.key} style={{ height: 104, width: 104 }}>
                <Image source={{ uri: photo.uri }} style={{ borderRadius: theme.radius.md, height: 96, width: 96 }} />
                <Pressable accessibilityLabel={t('trainer.removePhoto')} onPress={() => setDrafts((current) => current.filter((item) => item.key !== photo.key))} style={{ position: 'absolute', right: 0, top: 0 }}>
                  <AppText variant="label">×</AppText>
                </Pressable>
                {uploadProgress[photo.key] != null ? <AppText variant="caption">{Math.round(uploadProgress[photo.key] * 100)}%</AppText> : null}
              </View>
            ))}
          </View>
          <AppText tone="muted">{t('trainer.photoLimit')}</AppText>
        </View>
      ) : null}
      {step === 3 ? (
        <ProgressPanel>
          <AppText variant="title">{t('trainer.reviewDetails')}</AppText>
          <AppText>{formatProgressDate(date)} · {optional(parseOptionalMeasure(weight)) ?? '—'} kg · {optional(lengthToCentimetres(parseOptionalMeasure(lengths.height), unit)) ?? '—'} cm</AppText>
          <AppText>{t('trainer.waist')} {optional(lengthToCentimetres(parseOptionalMeasure(lengths.waist), unit)) ?? '—'} · {t('trainer.chest')} {optional(lengthToCentimetres(parseOptionalMeasure(lengths.chest), unit)) ?? '—'}</AppText>
          <AppText>{t('trainer.strength')} {scores.strength ?? '—'} · {t('trainer.endurance')} {scores.endurance ?? '—'} · {t('trainer.flexibility')} {scores.flexibility ?? '—'} · {t('trainer.coreStrength')} {scores.core ?? '—'}</AppText>
          <AppText>{t('trainer.bodyFat')} {bodyFat ?? '—'}% · {t('trainer.visceralFat')} {visceral ?? '—'}</AppText>
          <AppText>{t('trainer.photosCount', { count: existingPhotos.length + drafts.length })}</AppText>
          <FormField label={t('trainer.notes')}><AppTextInput multiline onChangeText={setNotes} value={notes} /></FormField>
          <FormField label={t('trainer.trainingPerformance')}><AppTextInput multiline onChangeText={setPerformance} value={performance} /></FormField>
          <FormField label={t('trainer.achievedGoals')}><AppTextInput onChangeText={setAchieved} value={achieved} /></FormField>
          <FormField label={t('trainer.nextGoals')}><AppTextInput onChangeText={setNextGoals} value={nextGoals} /></FormField>
          <Pressable accessibilityRole="button" onPress={() => setStep(0)}><AppText variant="label">{t('trainer.editDetails')}</AppText></Pressable>
        </ProgressPanel>
      ) : null}
      {error ? <AppText accessibilityRole="alert" tone="danger">{error}</AppText> : null}
      {save.isError ? <AppText accessibilityRole="alert" tone="danger">{t('common.errorTitle')}</AppText> : null}
      {save.isPending ? <AppText tone="muted">{t('trainer.uploading')}</AppText> : null}
      <View style={{ flexDirection: 'row', gap: theme.spacing.sm }}>
        <ProgressButton label={t('trainer.back')} onPress={() => { if (step === 0) onCancel(); else setStep((value) => value - 1); }} variant="secondary" />
        {step < 3 ? <ProgressButton label={t(step === 0 ? 'trainer.nextAssessments' : step === 1 ? 'trainer.nextPhotos' : 'trainer.nextReview')} onPress={() => { if (step > 0 || validateMeasures()) setStep((value) => value + 1); }} /> : <ProgressButton label={t('trainer.saveProgress')} loading={save.isPending} onPress={submit} />}
      </View>
    </View>
  );
}

function Measurements({ date, weight, lengths, unit, onDate, onWeight, onLength, onUnit }: { date: string; weight: string; lengths: Record<LengthKey, string>; unit: LengthUnit; onDate: (value: string) => void; onWeight: (value: string) => void; onLength: (key: LengthKey, value: string) => void; onUnit: (unit: LengthUnit) => void }) {
  const { t } = useTranslation();
  const theme = useTheme();
  const fields: { key: LengthKey; label: string }[] = [
    { key: 'waist', label: t('trainer.waist') }, { key: 'chest', label: t('trainer.chest') }, { key: 'hip', label: t('trainer.hips') },
    { key: 'arm', label: t('trainer.leftArm') }, { key: 'rightArm', label: t('trainer.rightArm') },
    { key: 'thigh', label: t('trainer.leftThigh') }, { key: 'rightThigh', label: t('trainer.rightThigh') },
  ];
  return (
    <View style={{ gap: theme.spacing.md }}>
      <AppText variant="title">{t('trainer.basicInformation')}</AppText>
      <FormField label={t('trainer.assessmentDate')} required><AppTextInput accessibilityLabel={t('trainer.assessmentDate')} onChangeText={onDate} value={date} /></FormField>
      <View style={{ flexDirection: 'row', gap: theme.spacing.sm }}>
        <View style={{ flex: 1 }}><Field label={`${t('trainer.weight')} (kg)`} onChange={onWeight} value={weight} /></View>
        <View style={{ flex: 1 }}><Field label={`${t('trainer.height')} (${unit})`} onChange={(value) => onLength('height', value)} value={lengths.height} /></View>
      </View>
      <View style={{ alignItems: 'center', flexDirection: 'row', justifyContent: 'space-between' }}>
        <AppText variant="title">{t('trainer.bodyMeasurements')}</AppText>
        <View style={{ flexDirection: 'row', gap: theme.spacing.xs }}>
          <ProgressButton label={t('trainer.centimetres')} onPress={() => onUnit('cm')} variant={unit === 'cm' ? 'primary' : 'secondary'} />
          <ProgressButton label={t('trainer.inches')} onPress={() => onUnit('in')} variant={unit === 'in' ? 'primary' : 'secondary'} />
        </View>
      </View>
      <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: theme.spacing.sm }}>
        {fields.map((field) => (
          <View key={field.key} style={{ width: '48%' }}>
            <Field label={`${field.label} (${unit})`} onChange={(value) => onLength(field.key, value)} value={lengths[field.key]} />
          </View>
        ))}
      </View>
    </View>
  );
}

function Field({ label, value, onChange }: { label: string; value: string; onChange: (value: string) => void }) {
  return <FormField label={label}><AppTextInput accessibilityLabel={label} keyboardType="decimal-pad" onChangeText={onChange} value={value} /></FormField>;
}

function text(value: number | null) {
  return value == null ? '' : trimNumber(value);
}

function blank(value: string) {
  const trimmed = value.trim();
  return trimmed.length === 0 ? null : trimmed;
}

function optional(value: OptionalNumber) {
  return value === 'invalid' ? null : value;
}
