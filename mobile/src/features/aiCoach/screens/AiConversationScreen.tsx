import type { NativeStackScreenProps } from '@react-navigation/native-stack';
import { useMutation } from '@tanstack/react-query';
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
import { featureCodes } from '../../../constants/access';
import { errorText } from '../../../services/api/problem';
import { coachChat } from '../api/ai.api';
import { useConversation } from '../api/queries';
import type { ConversationMessage } from '../types';

type Props = NativeStackScreenProps<AppStackParamList, 'AiConversation'>;

export function AiConversationScreen({ route }: Props) {
  const theme = useTheme();
  const { t } = useTranslation();
  const conversationId = route.params?.conversationId ?? '';
  const conversation = useConversation(conversationId, 1);
  const [draft, setDraft] = useState(route.params?.draft ?? '');
  const [sent, setSent] = useState<ConversationMessage[]>([]);
  const [activeId, setActiveId] = useState(conversationId);
  const chat = useMutation({
    mutationFn: (message: string) => coachChat(message, activeId || null),
    onSuccess: (response, message) => {
      setActiveId(response.conversationId);
      setSent((current) => [
        ...current,
        { role: 'User', content: message, createdAt: new Date().toISOString() },
        { role: 'Assistant', content: response.message, createdAt: new Date().toISOString() },
      ]);
      setDraft('');
    },
  });
  const stored = conversation.data?.messages ?? [];
  const messages = conversationId ? [...stored, ...sent] : sent;
  const loading = Boolean(conversationId) && conversation.isLoading;

  const send = () => {
    const message = draft.trim();
    if (!message || message.length > 2000 || chat.isPending) {
      return;
    }
    chat.mutate(message);
  };

  return (
    <FeatureGate code={featureCodes.aiCoach}>
      <ScreenState error={conversation.isError ? errorText(conversation.error, t('common.offline')) : null} loading={loading} onRetry={() => void conversation.refetch()}>
        <KeyboardAvoidingView behavior={Platform.OS === 'ios' ? 'padding' : undefined} style={{ flex: 1 }}>
          <ScrollView contentContainerStyle={{ gap: theme.spacing.md, padding: theme.spacing.xl }} keyboardShouldPersistTaps="handled">
            <AppText variant="title">{conversation.data?.title ?? t('coach.newConversation')}</AppText>
            <AppText tone="muted">{t('journey.safety')}</AppText>
            <AppText tone="muted">{t('journey.notMedical')}</AppText>
            {messages.length === 0 ? <AppText tone="muted">{t('coach.emptyThread')}</AppText> : null}
            {messages.map((message, index) => (
              <AppCard key={`${message.createdAt}-${index}`}>
                <AppText variant="label">{message.role === 'User' ? t('coach.you') : t('journey.aiLabel')}</AppText>
                <AppText accessibilityLabel={message.role === 'User' ? t('coach.youSaid', { message: message.content }) : t('coach.coachSaid', { message: message.content })}>
                  {message.content}
                </AppText>
              </AppCard>
            ))}
            {stored.length === 100 ? <AppText tone="muted">{t('coach.pageLimit')}</AppText> : null}
            {chat.isError ? <AppText accessibilityRole="alert" tone="danger">{errorText(chat.error, t('common.offline'))}</AppText> : null}
            <FormField label={t('journey.askCoach')}>
              <AppTextInput accessibilityLabel={t('journey.askCoach')} onChangeText={setDraft} value={draft} />
            </FormField>
            <View>
              <AppButton disabled={draft.trim().length === 0 || draft.trim().length > 2000} label={t('journey.send')} loading={chat.isPending} onPress={send} />
            </View>
          </ScrollView>
        </KeyboardAvoidingView>
      </ScreenState>
    </FeatureGate>
  );
}
