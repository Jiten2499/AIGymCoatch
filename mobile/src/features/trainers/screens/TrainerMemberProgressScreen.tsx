import type { NativeStackScreenProps } from '@react-navigation/native-stack';
import { useLayoutEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Pressable, RefreshControl, ScrollView, View } from 'react-native';
import type { AppStackParamList } from '../../../app/navigation/types';
import { useTheme } from '../../../app/providers/ThemeProvider';
import { ScreenState } from '../../../components/feedback/ScreenState';
import { AppText } from '../../../components/ui/AppText';
import { MemberPhoto } from '../../../components/ui/MemberPhoto';
import { errorText } from '../../../services/api/problem';
import { useMemberProgress } from '../api/workspace.queries';
import { ProgressOverview } from '../ProgressOverview';
import { ProgressWizard } from '../ProgressWizard';
import { dayKey, useTrainerLayout } from '../ui';

type Props = NativeStackScreenProps<AppStackParamList, 'TrainerMemberProgress'>;

export function TrainerMemberProgressScreen({ route, navigation }: Props) {
  const { t } = useTranslation();
  const theme = useTheme();
  const layout = useTrainerLayout();
  const history = useMemberProgress(route.params.memberId);
  const photo = <MemberPhoto label={t('trainer.member')} path={`/trainers/me/members/${route.params.memberId}/profile-photo`} size={72} />;
  const [phase, setPhase] = useState<'overview' | 'wizard'>(route.params.assessmentId || route.params.start === 'record' ? 'wizard' : 'overview');
  const [editingId, setEditingId] = useState(route.params.assessmentId);

  useLayoutEffect(() => {
    navigation.setOptions({
      title: phase === 'wizard' ? t('trainer.recordProgress') : t('trainer.progress'),
      headerRight: () => phase === 'overview' ? (
        <Pressable accessibilityRole="button" onPress={() => { setEditingId(undefined); setPhase('wizard'); }} style={{ backgroundColor: theme.colors.accent, borderRadius: theme.radius.pill, marginRight: 8, paddingHorizontal: 12, paddingVertical: 8 }}>
          <AppText style={{ color: theme.colors.onAccent }} variant="caption">{t('trainer.recordProgress')}</AppText>
        </Pressable>
      ) : null,
    });
  }, [navigation, phase, t, theme.colors.accent, theme.colors.onAccent, theme.radius.pill]);

  if (history.isLoading) {
    return <ScreenState loading />;
  }

  if (history.isError) {
    return (
      <View style={{ flex: 1 }}>
        <View style={{ padding: theme.spacing.xl }}>{photo}</View>
        <ScreenState error={errorText(history.error, t('common.offline'))} onRetry={() => { void history.refetch(); }} />
      </View>
    );
  }

  return (
    <View style={{ backgroundColor: theme.colors.background, flex: 1 }}>
      <View style={{ paddingHorizontal: theme.spacing.xl, paddingTop: theme.spacing.xl }}>{photo}</View>
      {phase === 'overview' ? (
        <ScrollView contentContainerStyle={[layout.content, { paddingTop: theme.spacing.lg }]} refreshControl={<RefreshControl onRefresh={() => { void history.refetch(); }} refreshing={history.isRefetching} tintColor={layout.refresh} />} style={{ flex: 1 }}>
          <ProgressOverview memberId={route.params.memberId} onEdit={(assessmentId) => { setEditingId(assessmentId); setPhase('wizard'); }} records={history.data ?? []} today={dayKey(new Date())} />
        </ScrollView>
      ) : (
        <View style={{ flex: 1, paddingBottom: theme.spacing.lg, paddingHorizontal: theme.spacing.xl, paddingTop: theme.spacing.lg }}>
          <ProgressWizard
            assessmentId={editingId}
            existing={history.data?.find((item) => item.id === editingId)}
            memberId={route.params.memberId}
            onCancel={() => { if (route.params.start === 'record' && !editingId) navigation.goBack(); else setPhase('overview'); }}
            onDone={() => { setEditingId(undefined); setPhase('overview'); }}
          />
        </View>
      )}
    </View>
  );
}
