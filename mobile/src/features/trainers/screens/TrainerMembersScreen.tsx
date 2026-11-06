import type { CompositeNavigationProp } from '@react-navigation/native';
import type { BottomTabNavigationProp } from '@react-navigation/bottom-tabs';
import type { NativeStackNavigationProp } from '@react-navigation/native-stack';
import { useNavigation } from '@react-navigation/native';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { RefreshControl, ScrollView, View } from 'react-native';
import type { AppStackParamList, TrainerTabParamList } from '../../../app/navigation/types';
import { ScreenState } from '../../../components/feedback/ScreenState';
import { AppText } from '../../../components/ui/AppText';
import { MemberPhoto } from '../../../components/ui/MemberPhoto';
import { AppTextInput } from '../../../components/ui/AppTextInput';
import { errorText } from '../../../services/api/problem';
import { useTrainerMembers } from '../api/workspace.queries';
import { FilterChip, TrainerCard, useTrainerLayout } from '../ui';

type Navigation = CompositeNavigationProp<BottomTabNavigationProp<TrainerTabParamList, 'Members'>, NativeStackNavigationProp<AppStackParamList>>;
type Filter = 'all' | 'active' | 'expiring';

export function TrainerMembersScreen() {
  const { t } = useTranslation();
  const layout = useTrainerLayout();
  const navigation = useNavigation<Navigation>();
  const [search, setSearch] = useState('');
  const [filter, setFilter] = useState<Filter>('all');
  const members = useTrainerMembers(search);
  const rows = (members.data ?? []).filter((item) => filter === 'all' || (filter === 'expiring' ? item.expiring : item.status === 'Active'));

  return (
    <ScreenState error={members.isError ? errorText(members.error, t('common.offline')) : null} loading={members.isLoading} onRetry={() => void members.refetch()}>
      <ScrollView contentContainerStyle={layout.content} refreshControl={<RefreshControl onRefresh={() => void members.refetch()} refreshing={members.isRefetching} tintColor={layout.refresh} />}>
        <AppText variant="display">{t('trainer.members')}</AppText>
        <AppTextInput accessibilityLabel={t('common.search')} onChangeText={setSearch} placeholder={t('common.search')} value={search} />
        <View style={layout.row}>
          {(['all', 'active', 'expiring'] as Filter[]).map((item) => (
            <FilterChip key={item} label={t(`trainer.${item}`)} onPress={() => setFilter(item)} selected={filter === item} />
          ))}
        </View>
        {rows.length === 0 ? <AppText tone="muted">{t('trainer.noMembers')}</AppText> : rows.map((item) => (
          <TrainerCard key={item.id} onPress={() => navigation.navigate('TrainerMember', { memberId: item.id })}>
            <View style={{ alignItems: 'center', flexDirection: 'row', gap: 12 }}>
              <MemberPhoto label={item.name} path={item.hasPhoto ? `/trainers/me/members/${item.id}/profile-photo` : null} />
              <View style={{ flex: 1, gap: 4 }}>
                <AppText variant="title">{item.name}</AppText>
                <AppText tone="muted">{item.goal ?? t('trainer.noGoal')} · {item.status}</AppText>
                <AppText>{item.packageName ?? t('trainer.noPackage')}</AppText>
                {item.sessionCount !== null ? <AppText tone="muted">{t('trainer.packageUse', { used: item.sessionsUsed ?? 0, total: item.sessionCount })}</AppText> : null}
                {item.expiring ? <AppText style={{ color: layout.warning }}>{t('trainer.expiring')}</AppText> : null}
              </View>
            </View>
          </TrainerCard>
        ))}
      </ScrollView>
    </ScreenState>
  );
}
