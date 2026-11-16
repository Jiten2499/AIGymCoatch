import type { CompositeNavigationProp } from '@react-navigation/native';
import type { BottomTabNavigationProp } from '@react-navigation/bottom-tabs';
import type { NativeStackNavigationProp } from '@react-navigation/native-stack';
import { useNavigation } from '@react-navigation/native';
import { useTranslation } from 'react-i18next';
import { RefreshControl, ScrollView, View } from 'react-native';
import type { AppStackParamList, TrainerTabParamList } from '../../../app/navigation/types';
import { useTheme } from '../../../app/providers/ThemeProvider';
import { ScreenState } from '../../../components/feedback/ScreenState';
import { AppText } from '../../../components/ui/AppText';
import { MemberPhoto } from '../../../components/ui/MemberPhoto';
import { errorText } from '../../../services/api/problem';
import { useTrainerMembers } from '../api/workspace.queries';
import { TrainerIcon } from '../icons';
import { TrainerCard, useTrainerLayout } from '../ui';

type Navigation = CompositeNavigationProp<BottomTabNavigationProp<TrainerTabParamList, 'Plans'>, NativeStackNavigationProp<AppStackParamList>>;

export function TrainerPlansScreen() {
  const theme = useTheme();
  const { t } = useTranslation();
  const layout = useTrainerLayout({ topInset: true });
  const navigation = useNavigation<Navigation>();
  const members = useTrainerMembers('');

  return (
    <ScreenState error={members.isError ? errorText(members.error, t('common.offline')) : null} loading={members.isLoading} onRetry={() => void members.refetch()}>
      <ScrollView contentContainerStyle={layout.content} refreshControl={<RefreshControl onRefresh={() => void members.refetch()} refreshing={members.isRefetching} tintColor={layout.refresh} />}>
        <AppText variant="display">{t('trainer.plans')}</AppText>
        <AppText tone="muted">{t('trainer.plansBody')}</AppText>
        {(members.data?.length ?? 0) === 0 ? <AppText tone="muted">{t('trainer.noMembers')}</AppText> : members.data?.map((item) => (
          <TrainerCard key={item.id} onPress={() => navigation.navigate('TrainerMember', { memberId: item.id, tab: 'plan' })}>
            <View style={{ alignItems: 'center', flexDirection: 'row', gap: 12 }}>
              <MemberPhoto label={item.name} path={item.hasPhoto ? `/trainers/me/members/${item.id}/profile-photo` : null} size={48} />
              <View style={{ flex: 1, gap: 4 }}>
                <AppText variant="label">{item.name}</AppText>
                <AppText tone="muted">{item.goal ?? t('trainer.noGoal')}</AppText>
                <AppText tone="muted">{item.status}</AppText>
              </View>
              <TrainerIcon color={theme.colors.muted} name="chevron" size={18} />
            </View>
          </TrainerCard>
        ))}
      </ScrollView>
    </ScreenState>
  );
}
