import { useRoute, type RouteProp } from '@react-navigation/native';
import * as ImagePicker from 'expo-image-picker';
import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Pressable, ScrollView, TextInput, View } from 'react-native';
import type { AppStackParamList } from '../../../app/navigation/types';
import { useTheme } from '../../../app/providers/ThemeProvider';
import { AppText } from '../../../components/ui/AppText';
import { errorText } from '../../../services/api/problem';
import { useSessionStore } from '../../../store/sessionStore';
import { useChatMessages, useMarkChatRead, useSendChatAttachment, useSendChatMessage } from '../api/queries';
import { useChatRealtime } from '../realtime';

export function ChatThreadScreen() {
  const theme = useTheme();
  const { t } = useTranslation();
  const route = useRoute<RouteProp<AppStackParamList, 'ChatThread'>>();
  const userId = useSessionStore((state) => state.user?.id);
  const messages = useChatMessages(route.params.conversationId);
  const send = useSendChatMessage(route.params.conversationId);
  const attach = useSendChatAttachment(route.params.conversationId);
  const markRead = useMarkChatRead(route.params.conversationId);
  const [draft, setDraft] = useState('');
  const [typing, setTyping] = useState<string | null>(null);
  useChatRealtime(route.params.conversationId, setTyping);
  const items = [...(messages.data ?? [])].reverse();
  const latestIncoming = [...items].reverse().find((item) => item.senderUserId !== userId);

  useEffect(() => {
    if (latestIncoming) {
      markRead.mutate(latestIncoming.id);
    }
  }, [latestIncoming?.id]);

  return (
    <View style={{ flex: 1, backgroundColor: theme.colors.background }}>
      <View style={{ padding: theme.spacing.lg }}>
        <AppText variant="title">{route.params.title}</AppText>
        <AppText tone="muted">{route.params.role}</AppText>
        {typing ? <AppText tone="muted">{t('chat.typing')}</AppText> : null}
      </View>
      <ScrollView contentContainerStyle={{ gap: theme.spacing.sm, padding: theme.spacing.lg }}>
        {items.map((item) => {
          const mine = item.senderUserId === userId;
          return (
            <View key={item.id} style={{ alignSelf: mine ? 'flex-end' : 'flex-start', backgroundColor: mine ? theme.colors.accent : theme.colors.surface, borderRadius: theme.radius.md, maxWidth: '80%', padding: theme.spacing.md }}>
              <AppText>{item.body}</AppText>
              <AppText tone="muted">{new Date(item.createdAt).toLocaleTimeString()}{mine && item.isReadByOther ? ' · Read' : ''}</AppText>
            </View>
          );
        })}
      </ScrollView>
      {send.isError || attach.isError ? <AppText tone="danger">{errorText(send.error ?? attach.error, t('common.offline'))}</AppText> : null}
      <View style={{ flexDirection: 'row', gap: theme.spacing.sm, padding: theme.spacing.md }}>
        <Pressable accessibilityRole="button" onPress={() => void pickImage()} style={{ justifyContent: 'center', minHeight: theme.control.minTouch }}>
          <AppText>{t('chat.attach')}</AppText>
        </Pressable>
        <TextInput
          accessibilityLabel={t('chat.placeholder')}
          onChangeText={setDraft}
          placeholder={t('chat.placeholder')}
          placeholderTextColor={theme.colors.muted}
          style={{ borderColor: theme.colors.border, borderRadius: theme.radius.md, borderWidth: 1, color: theme.colors.text, flex: 1, minHeight: theme.control.minTouch, paddingHorizontal: theme.spacing.md }}
          value={draft}
        />
        <Pressable accessibilityRole="button" onPress={() => { const body = draft.trim(); if (!body) return; setDraft(''); send.mutate(body); }} style={{ justifyContent: 'center', minHeight: theme.control.minTouch }}>
          <AppText>{t('chat.send')}</AppText>
        </Pressable>
      </View>
    </View>
  );

  async function pickImage() {
    const result = await ImagePicker.launchImageLibraryAsync({ mediaTypes: ['images'], quality: 0.8 });
    const asset = result.assets?.[0];
    if (!asset) {
      return;
    }
    attach.mutate({ uri: asset.uri, fileName: asset.fileName ?? 'photo.jpg', contentType: asset.mimeType ?? 'image/jpeg' });
  }
}
