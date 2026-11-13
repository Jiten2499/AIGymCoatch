import { CommonActions, useNavigation } from '@react-navigation/native';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Pressable, ScrollView, TextInput, View } from 'react-native';
import { useTheme } from '../../../app/providers/ThemeProvider';
import { FeatureGate } from '../../../components/access/FeatureGate';
import { ScreenState } from '../../../components/feedback/ScreenState';
import { AppText } from '../../../components/ui/AppText';
import { featureCodes } from '../../../constants/access';
import { errorText } from '../../../services/api/problem';
import { useChatInbox, useChatRecipients, useOpenChat } from '../api/queries';
import { useChatRealtime } from '../realtime';

const filters = ['all', 'unread', 'trainers', 'members'] as const;

export function ChatListScreen() {
  const theme = useTheme();
  const { t } = useTranslation();
  const navigation = useNavigation();
  const [filter, setFilter] = useState<(typeof filters)[number]>('all');
  const [term, setTerm] = useState('');
  const [creating, setCreating] = useState(false);
  const inbox = useChatInbox(filter === 'all' ? '' : filter, term);
  const recipients = useChatRecipients(term, creating);
  const open = useOpenChat();
  useChatRealtime();

  return (
    <FeatureGate code={featureCodes.gymChat}>
      <ScreenState
        empty={!creating && inbox.isSuccess && (inbox.data?.length ?? 0) === 0}
        emptyBody={t('chat.empty')}
        emptyTitle={t('chat.title')}
        error={inbox.isError ? errorText(inbox.error, t('common.offline')) : null}
        loading={inbox.isLoading}
        onRetry={() => void inbox.refetch()}
      >
        <ScrollView contentContainerStyle={{ gap: theme.spacing.md, padding: theme.spacing.xl }}>
          <AppText variant="title">{t('chat.title')}</AppText>
          <TextInput
            accessibilityLabel={t('chat.search')}
            onChangeText={setTerm}
            placeholder={t('chat.search')}
            placeholderTextColor={theme.colors.muted}
            style={{ borderColor: theme.colors.border, borderRadius: theme.radius.md, borderWidth: 1, color: theme.colors.text, minHeight: theme.control.minTouch, paddingHorizontal: theme.spacing.md }}
            value={term}
          />
          <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: theme.spacing.sm }}>
            {filters.map((value) => (
              <Pressable accessibilityRole="button" key={value} onPress={() => setFilter(value)} style={{ backgroundColor: filter === value ? theme.colors.accent : 'transparent', borderColor: theme.colors.border, borderRadius: theme.radius.md, borderWidth: 1, minHeight: theme.control.minTouch, justifyContent: 'center', paddingHorizontal: theme.spacing.md }}>
                <AppText>{t(`chat.${value}`)}</AppText>
              </Pressable>
            ))}
            <Pressable accessibilityRole="button" onPress={() => setCreating((current) => !current)} style={{ minHeight: theme.control.minTouch, justifyContent: 'center' }}>
              <AppText>{t('chat.newChat')}</AppText>
            </Pressable>
          </View>
          {creating ? (recipients.data ?? []).map((person) => (
            <Pressable
              accessibilityRole="button"
              key={person.userId}
              onPress={() => open.mutate(person.userId, { onSuccess: (conversation) => navigation.dispatch(CommonActions.navigate({ name: 'ChatThread', params: { conversationId: conversation.conversationId, title: conversation.displayName, role: conversation.role } })) })}
            >
              <AppText>{person.displayName}</AppText>
              <AppText tone="muted">{person.role}</AppText>
            </Pressable>
          )) : null}
          {(inbox.data ?? []).map((item) => (
            <Pressable accessibilityRole="button" key={item.conversationId} onPress={() => navigation.dispatch(CommonActions.navigate({ name: 'ChatThread', params: { conversationId: item.conversationId, title: item.displayName, role: item.role } }))}>
              <AppText variant="label">{item.displayName}{item.unreadCount > 0 ? ` (${item.unreadCount})` : ''}</AppText>
              <AppText tone="muted">{item.role}</AppText>
              <AppText numberOfLines={1}>{item.lastPreview ?? ''}</AppText>
            </Pressable>
          ))}
        </ScrollView>
      </ScreenState>
    </FeatureGate>
  );
}
