import type { NativeStackNavigationProp } from '@react-navigation/native-stack';
import { useNavigation } from '@react-navigation/native';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { KeyboardAvoidingView, Platform, ScrollView, View } from 'react-native';
import type { AppStackParamList } from '../../../app/navigation/types';
import { useTheme } from '../../../app/providers/ThemeProvider';
import { FeatureGate } from '../../../components/access/FeatureGate';
import { ScreenState } from '../../../components/feedback/ScreenState';
import { FormField } from '../../../components/forms/FormField';
import { AppButton } from '../../../components/ui/AppButton';
import { AppCard } from '../../../components/ui/AppCard';
import { AppText } from '../../../components/ui/AppText';
import { AppTextInput } from '../../../components/ui/AppTextInput';
import { featureCodes, isFeatureEnabled } from '../../../constants/access';
import { useWorkspaceFeatures } from '../../licensing/api/featureQuery';
import { errorText, isNotFound } from '../../../services/api/problem';
import { formatDateTime } from '../../../services/localization/format';
import { useFitnessAssessment } from '../../assessment/api/assessment.queries';
import { tenantKey } from '../../fitness/api/keys';
import { useFitnessProfile, useTenantId } from '../../fitness/api/queries';
import { coachChat, generateWorkout, progressInsight } from '../api/ai.api';
import { useConversations } from '../api/queries';

export function AiCoachScreen() {
  const theme = useTheme();
  const { t, i18n } = useTranslation();
  const navigation = useNavigation<NativeStackNavigationProp<AppStackParamList>>();
  const tenantId = useTenantId();
  const client = useQueryClient();
  const profile = useFitnessProfile();
  const assessment = useFitnessAssessment();
  const features = useWorkspaceFeatures();
  const coachEnabled = isFeatureEnabled(features.data, featureCodes.aiCoach);
  const canGenerate = coachEnabled && isFeatureEnabled(features.data, featureCodes.aiWorkoutGeneration);
  const canReview = isFeatureEnabled(features.data, featureCodes.aiProgressInsights);
  const conversations = useConversations(1, coachEnabled);
  const prompts = ['planToday', 'consistency', 'reviewProgress', 'understandNutrition', 'recovery'] as const;
  const [notes, setNotes] = useState('');
  const [question, setQuestion] = useState('');
  const [conversationId, setConversationId] = useState<string | null>(null);
  const missing = profile.isError && isNotFound(profile.error);
  const assessmentReady = assessment.data?.isCompleted === true;
  const assessmentGaps = assessmentReady
    ? [
        assessment.data?.workout?.daysPerWeek ? null : t('journey.missingDays'),
        assessment.data?.workout?.equipment?.length ? null : t('journey.missingEquipment'),
        assessment.data?.workout?.location ? null : t('journey.missingLocation'),
      ].filter((item): item is string => item != null)
    : [];
  const generate = useMutation({
    mutationFn: () => generateWorkout(notes.trim() || null),
    onSuccess: async (program) => {
      client.setQueryData(tenantKey(tenantId, 'program', program.id), program);
      await client.invalidateQueries({ queryKey: tenantKey(tenantId, 'programs') });
      navigation.navigate('WorkoutPlan', { programId: program.id });
    },
  });
  const chat = useMutation({
    mutationFn: () => coachChat(question.trim(), conversationId),
    onSuccess: (response) => {
      setConversationId(response.conversationId);
      setQuestion('');
    },
  });
  const insight = useMutation({ mutationFn: () => progressInsight() });
  const profileError = profile.isError && !missing ? errorText(profile.error, t('common.offline')) : null;

  return (
    <ScreenState error={profileError} loading={profile.isLoading} onRetry={() => void profile.refetch()}>
      <KeyboardAvoidingView behavior={Platform.OS === 'ios' ? 'padding' : undefined} style={{ flex: 1 }}>
        <ScrollView contentContainerStyle={{ gap: theme.spacing.lg, padding: theme.spacing.xl }} keyboardShouldPersistTaps="handled">
          <AppText variant="title">{t('journey.coachTitle')}</AppText>
          <AppText tone="muted">{t('journey.coachBody')}</AppText>
          {assessment.isSuccess || assessment.isError ? (
            <AppCard>
              {assessmentReady ? (
                <AppText>{t('journey.assessmentReady', { id: (assessment.data?.id ?? '').slice(0, 8), date: assessment.data?.updatedAt ? formatDateTime(new Date(assessment.data.updatedAt), i18n.language) : t('journey.saved') })}</AppText>
              ) : (
                <AppText>{t('journey.assessmentMissing')}</AppText>
              )}
              {assessmentGaps.length > 0 ? <AppText tone="muted">{t('journey.assessmentGaps', { items: assessmentGaps.join(', ') })}</AppText> : null}
              <AppButton label={t('journey.assessmentUpdate')} onPress={() => navigation.navigate('FitnessAssessment')} variant="secondary" />
            </AppCard>
          ) : null}
          {missing ? (
            <AppCard>
              <AppText>{t('journey.profileEmpty')}</AppText>
              <AppButton label={t('journey.completeProfile')} onPress={() => navigation.navigate('FitnessAssessment')} />
            </AppCard>
          ) : profile.data ? (
            <AppCard>
              <AppText>{t(`journey.goalType.${profile.data.primaryGoal}`)}</AppText>
              <AppText tone="muted">{t(`journey.fitnessLevel.${profile.data.fitnessLevel}`)}</AppText>
              <AppText tone="muted">{t('journey.frequency', { count: profile.data.preferredWorkoutDays })}</AppText>
              <AppText tone="muted">{t('journey.minutes', { count: profile.data.preferredWorkoutDuration })}</AppText>
              <AppText tone="muted">{profile.data.preferredTrainingLocation || t('journey.equipment')}</AppText>
            </AppCard>
          ) : null}
          <FormField label={t('journey.notes')}>
            <AppTextInput accessibilityLabel={t('journey.notes')} onChangeText={setNotes} value={notes} />
          </FormField>
          {features.isSuccess && coachEnabled && !canGenerate ? <AppText>{t('journey.featureDisabled')}</AppText> : null}
          {canGenerate ? (
            <>
              {generate.isError ? <AppText accessibilityRole="alert" tone="danger">{errorText(generate.error, t('common.offline'))}</AppText> : null}
              <AppButton disabled={missing || !assessmentReady || generate.isPending} label={t('journey.generate')} loading={generate.isPending} onPress={() => { if (!generate.isPending) generate.mutate(); }} />
            </>
          ) : null}
          <AppText tone="muted">{t('journey.safety')}</AppText>
          <FeatureGate code={featureCodes.aiCoach}>
            <View style={{ gap: theme.spacing.sm }}>
              <FormField label={t('journey.askCoach')}>
                <AppTextInput accessibilityLabel={t('journey.askCoach')} onChangeText={setQuestion} placeholder={t('journey.askPlaceholder')} value={question} />
              </FormField>
              {chat.data ? <AppText>{chat.data.message}</AppText> : null}
              {chat.isError ? <AppText accessibilityRole="alert" tone="danger">{errorText(chat.error, t('common.offline'))}</AppText> : null}
              <AppButton disabled={question.trim().length === 0 || question.trim().length > 2000} label={t('journey.send')} loading={chat.isPending} onPress={() => chat.mutate()} variant="secondary" />
              {prompts.map((prompt) => (
                <AppButton key={prompt} label={t(`coach.prompt.${prompt}`)} onPress={() => navigation.navigate('AiConversation', { draft: t(`coach.prompt.${prompt}`) })} variant="secondary" />
              ))}
              <AppCard>
                <AppText variant="label">{t('coach.history')}</AppText>
                <AppButton label={t('coach.newConversation')} onPress={() => navigation.navigate('AiConversation', {})} variant="secondary" />
                {conversations.isLoading ? <AppText tone="muted">{t('common.loading')}</AppText> : null}
                {conversations.isError ? <AppText accessibilityRole="alert" tone="danger">{errorText(conversations.error, t('common.offline'))}</AppText> : null}
                {conversations.isSuccess && conversations.data.items.length === 0 ? <AppText tone="muted">{t('coach.historyEmpty')}</AppText> : null}
                {conversations.data?.items.map((item) => (
                  <View key={item.id}>
                    <AppButton label={item.title} onPress={() => navigation.navigate('AiConversation', { conversationId: item.id })} variant="secondary" />
                    <AppText tone="muted">{formatDateTime(new Date(item.updatedAt), i18n.language)}</AppText>
                  </View>
                ))}
              </AppCard>
            </View>
          </FeatureGate>
          {canReview ? (
            <View style={{ gap: theme.spacing.sm }}>
              {insight.data ? <AppText>{insight.data.message}</AppText> : null}
              {insight.isError ? <AppText accessibilityRole="alert" tone="danger">{errorText(insight.error, t('common.offline'))}</AppText> : null}
              <AppButton label={t('journey.reviewRecent')} loading={insight.isPending} onPress={() => insight.mutate()} variant="secondary" />
            </View>
          ) : null}
        </ScrollView>
      </KeyboardAvoidingView>
    </ScreenState>
  );
}
