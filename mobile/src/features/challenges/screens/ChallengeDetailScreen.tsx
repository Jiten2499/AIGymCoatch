import type { NativeStackScreenProps } from '@react-navigation/native-stack';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { ScrollView, View } from 'react-native';
import type { AppStackParamList } from '../../../app/navigation/types';
import { useTheme } from '../../../app/providers/ThemeProvider';
import { FeatureGate } from '../../../components/access/FeatureGate';
import { FormField } from '../../../components/forms/FormField';
import { ScreenState } from '../../../components/feedback/ScreenState';
import { AppButton } from '../../../components/ui/AppButton';
import { AppCard } from '../../../components/ui/AppCard';
import { AppText } from '../../../components/ui/AppText';
import { AppTextInput } from '../../../components/ui/AppTextInput';
import { featureCodes } from '../../../constants/access';
import { formatDate, formatNumber } from '../../../services/localization/format';
import { errorText } from '../../../services/api/problem';
import { useChallenges, useJoinChallenge, useRecordChallengeProgress } from '../api/queries';

type Props = NativeStackScreenProps<AppStackParamList, 'ChallengeDetail'>;

export function ChallengeDetailScreen({ route }: Props) {
  const theme = useTheme();
  const { t, i18n } = useTranslation();
  const challenges = useChallenges(route.params.page);
  const challenge = challenges.data?.items.find((item) => item.id === route.params.challengeId) ?? null;
  const join = useJoinChallenge();
  const progress = useRecordChallengeProgress();
  const [value, setValue] = useState('');
  const [formError, setFormError] = useState<string | null>(null);
  const missing = challenges.isSuccess && !challenge;

  const saveProgress = () => {
    const parsed = Number(value);
    if (!value.trim() || !Number.isFinite(parsed) || parsed < 0) {
      setFormError(t('journey.invalidNumber'));
      return;
    }
    setFormError(null);
    progress.mutate({ id: route.params.challengeId, value: parsed });
  };

  return (
    <FeatureGate code={featureCodes.workoutPlans}>
      <ScreenState empty={missing} emptyBody={t('challenge.missing')} emptyTitle={t('nav.challenges')} error={challenges.isError ? errorText(challenges.error, t('common.offline')) : null} loading={challenges.isLoading} onRetry={() => void challenges.refetch()}>
        {challenge ? (
          <ScrollView contentContainerStyle={{ gap: theme.spacing.lg, padding: theme.spacing.xl }}>
            <AppText variant="title">{challenge.name}</AppText>
            {challenge.description ? <AppText>{challenge.description}</AppText> : null}
            <AppCard>
              <AppText>{t(`challenge.status.${challenge.status}`)}</AppText>
              <AppText tone="muted">{t(`challenge.type.${challenge.challengeType}`)}</AppText>
              <AppText tone="muted">{formatDate(new Date(`${challenge.startDate}T00:00:00`), i18n.language)} – {formatDate(new Date(`${challenge.endDate}T00:00:00`), i18n.language)}</AppText>
              <AppText>{t('challenge.target')}: {formatNumber(challenge.targetValue, i18n.language)}</AppText>
              {challenge.currentValue === null ? <AppText>{t('challenge.notJoined')}</AppText> : (
                <View>
                  <AppText>{t('challenge.progress', { current: formatNumber(challenge.currentValue, i18n.language), target: formatNumber(challenge.targetValue, i18n.language) })}</AppText>
                  <View accessibilityLabel={t('challenge.progress', { current: formatNumber(challenge.currentValue, i18n.language), target: formatNumber(challenge.targetValue, i18n.language) })} style={{ backgroundColor: theme.colors.accent, borderRadius: theme.radius.sm, height: 8, marginTop: theme.spacing.sm, width: `${Math.min(100, Math.max(0, (challenge.currentValue / Math.max(challenge.targetValue, 1)) * 100))}%` }} />
                </View>
              )}
              {challenge.completedAt ? <AppText>{t('challenge.completed')}</AppText> : null}
            </AppCard>
            {join.isError ? <AppText accessibilityRole="alert" tone="danger">{errorText(join.error, t('common.offline'))}</AppText> : null}
            {progress.isError ? <AppText accessibilityRole="alert" tone="danger">{errorText(progress.error, t('common.offline'))}</AppText> : null}
            {challenge.currentValue === null && challenge.status === 'Active' ? (
              <AppButton label={t('challenge.join')} loading={join.isPending} onPress={() => join.mutate(challenge.id)} />
            ) : null}
            {challenge.currentValue !== null && challenge.completedAt === null && challenge.status === 'Active' ? (
              <AppCard>
                <FormField error={formError ?? undefined} label={t('challenge.record')}>
                  <AppTextInput accessibilityLabel={t('challenge.record')} keyboardType="decimal-pad" onChangeText={setValue} value={value} />
                </FormField>
                <AppButton label={t('challenge.saveProgress')} loading={progress.isPending} onPress={saveProgress} />
              </AppCard>
            ) : null}
          </ScrollView>
        ) : null}
      </ScreenState>
    </FeatureGate>
  );
}
