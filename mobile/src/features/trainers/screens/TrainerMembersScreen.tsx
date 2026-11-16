import type { CompositeNavigationProp } from '@react-navigation/native';
import type { BottomTabNavigationProp } from '@react-navigation/bottom-tabs';
import type { NativeStackNavigationProp } from '@react-navigation/native-stack';
import { useNavigation } from '@react-navigation/native';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { RefreshControl, ScrollView, View } from 'react-native';
import type { AppStackParamList, TrainerTabParamList } from '../../../app/navigation/types';
import { useTheme } from '../../../app/providers/ThemeProvider';
import { ScreenState } from '../../../components/feedback/ScreenState';
import { AppText } from '../../../components/ui/AppText';
import { MemberPhoto } from '../../../components/ui/MemberPhoto';
import { AppTextInput } from '../../../components/ui/AppTextInput';
import { errorText } from '../../../services/api/problem';
import { useTrainerMembers } from '../api/workspace.queries';
import { TrainerIcon } from '../icons';
import { FilterChip, TrainerCard, useTrainerLayout } from '../ui';

type Navigation = CompositeNavigationProp<BottomTabNavigationProp<TrainerTabParamList, 'Members'>, NativeStackNavigationProp<AppStackParamList>>;
type Filter = 'all' | 'active' | 'inactive';

export function TrainerMembersScreen() {
  const theme = useTheme();
  const { t } = useTranslation();
  const layout = useTrainerLayout({ topInset: true });
  const navigation = useNavigation<Navigation>();
  const [search, setSearch] = useState('');
  const [filter, setFilter] = useState<Filter>('all');
  const members = useTrainerMembers(search);
  const rows = (members.data ?? []).filter((item) => {
    if (filter === 'active') return item.status === 'Active';
    if (filter === 'inactive') return item.status !== 'Active';
    return true;
  });

  return (
    <ScreenState error={members.isError ? errorText(members.error, t('common.offline')) : null} loading={members.isLoading} onRetry={() => void members.refetch()}>
      <ScrollView contentContainerStyle={layout.content} refreshControl={<RefreshControl onRefresh={() => void members.refetch()} refreshing={members.isRefetching} tintColor={layout.refresh} />}>
        <AppText variant="display">{t('trainer.myMembers')}</AppText>
        <View style={{ alignItems: 'center', flexDirection: 'row', gap: theme.spacing.sm }}>
          <TrainerIcon color={theme.colors.muted} name="search" size={18} />
          <View style={{ flex: 1 }}>
            <AppTextInput accessibilityLabel={t('trainer.searchMember')} onChangeText={setSearch} placeholder={t('trainer.searchMember')} value={search} />
          </View>
        </View>
        <View style={layout.row}>
          {(['all', 'active', 'inactive'] as Filter[]).map((item) => (
            <FilterChip key={item} label={t(`trainer.${item}`)} onPress={() => setFilter(item)} selected={filter === item} />
          ))}
        </View>
        {rows.length === 0 ? <AppText tone="muted">{t('trainer.noMembers')}</AppText> : rows.map((item) => (
          <TrainerCard key={item.id} onPress={() => navigation.navigate('TrainerMember', { memberId: item.id })}>
            <View style={{ alignItems: 'center', flexDirection: 'row', gap: 12 }}>
              <View style={{ borderColor: theme.colors.accent, borderRadius: 28, borderWidth: 1 }}>
                <MemberPhoto label={item.name} path={item.hasPhoto ? `/trainers/me/members/${item.id}/profile-photo` : null} size={52} />
              </View>
              <View style={{ flex: 1, gap: 4 }}>
                <AppText variant="label">{item.name}</AppText>
                <AppText tone="muted">{item.status}</AppText>
                <AppText tone="muted">{item.packageName ?? item.goal ?? t('trainer.noPackage')}</AppText>
              </View>
              <TrainerIcon color={theme.colors.muted} name="chevron" size={18} />
            </View>
          </TrainerCard>
        ))}
      </ScrollView>
    </ScreenState>
  );
}
