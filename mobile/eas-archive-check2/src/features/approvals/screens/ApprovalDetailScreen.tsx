import type { NativeStackScreenProps } from '@react-navigation/native-stack';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { ScrollView } from 'react-native';
import type { AppStackParamList } from '../../../app/navigation/types';
import { useTheme } from '../../../app/providers/ThemeProvider';
import { ScreenState } from '../../../components/feedback/ScreenState';
import { AppButton } from '../../../components/ui/AppButton';
import { AppText } from '../../../components/ui/AppText';
import { AppTextInput } from '../../../components/ui/AppTextInput';
import { errorText } from '../../../services/api/problem';
import { useApprovalRequest, useDecideApproval } from '../api/queries';

type Props = NativeStackScreenProps<AppStackParamList, 'ApprovalDetail'>;

export function ApprovalDetailScreen({ route }: Props) {
  return <ApprovalDetail id={route.params.requestId} />;
}

export function ApprovalDetail({ id }: { id: string }) {
  const theme = useTheme();
  const { t } = useTranslation();
  const [comment, setComment] = useState('');
  const [message, setMessage] = useState<string | null>(null);
  const request = useApprovalRequest(id, true);
  const decide = useDecideApproval(id);
  const item = request.data;

  return (
    <ScreenState
      empty={false}
      emptyBody=""
      emptyTitle=""
      error={request.isError ? errorText(request.error, t('common.offline')) : null}
      loading={request.isLoading}
      onRetry={() => void request.refetch()}
    >
      {item ? (
        <ScrollView contentContainerStyle={{ gap: theme.spacing.md, padding: theme.spacing.xl }}>
          <AppText variant="title">{item.title}</AppText>
          <AppText>{item.relatedLabel}</AppText>
          <AppText>{t('tasks.requestedBy')} {item.requestedByName}</AppText>
          <AppText>{item.requestedAt}</AppText>
          <AppText>{item.status}</AppText>
          <AppText>{item.summary}</AppText>
          {item.days ? <AppText>{t('tasks.days')} {item.days}</AppText> : null}
          {item.originalEndDate ? <AppText>{t('tasks.originalEnd')} {item.originalEndDate}</AppText> : null}
          {item.proposedEndDate ? <AppText>{t('tasks.proposedEnd')} {item.proposedEndDate}</AppText> : null}
          {item.amount != null ? <AppText>{item.currency} {item.amount}</AppText> : null}
          <AppText>{t('tasks.reason')} {item.reason || t('tasks.none')}</AppText>
          {item.history.map((entry) => <AppText key={`${entry.action}-${entry.at}`}>{entry.action} · {entry.at} · {entry.actorName}</AppText>)}
          {item.status === 'Pending' ? (
            <>
              <AppTextInput accessibilityLabel={t('tasks.comment')} onChangeText={setComment} value={comment} />
              {message ? <AppText>{message}</AppText> : null}
              {decide.isError ? <AppText>{errorText(decide.error, t('common.offline'))}</AppText> : null}
              <AppButton label={t('tasks.approve')} onPress={() => { setMessage(null); decide.mutate({ approve: true, comment }); }} />
              <AppButton label={t('tasks.reject')} onPress={() => {
                if (comment.trim().length === 0) {
                  setMessage(t('tasks.commentRequired'));
                  return;
                }
                setMessage(null);
                decide.mutate({ approve: false, comment });
              }} />
            </>
          ) : null}
        </ScrollView>
      ) : null}
    </ScreenState>
  );
}
