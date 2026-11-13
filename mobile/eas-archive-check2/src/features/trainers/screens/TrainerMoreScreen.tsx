import type { CompositeNavigationProp } from '@react-navigation/native';
import type { BottomTabNavigationProp } from '@react-navigation/bottom-tabs';
import type { NativeStackNavigationProp } from '@react-navigation/native-stack';
import { useNavigation } from '@react-navigation/native';
import { useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { RefreshControl, ScrollView } from 'react-native';
import type { AppStackParamList, TrainerTabParamList } from '../../../app/navigation/types';
import { useTheme } from '../../../app/providers/ThemeProvider';
import { AppAvatar } from '../../../components/ui/AppAvatar';
import { AppButton } from '../../../components/ui/AppButton';
import { AppText } from '../../../components/ui/AppText';
import { featureCodes } from '../../../constants/access';
import { useWorkspaceFeatures } from '../../licensing/api/featureQuery';
import { useMyProfilePhoto } from '../../profile/photo';
import { endSession } from '../../../services/auth/session';
import { useMyTrainerPhoto, useTrainerProfile } from '../api/workspace.queries';
import { TrainerCard, useTrainerLayout } from '../ui';

type Navigation = CompositeNavigationProp<BottomTabNavigationProp<TrainerTabParamList, 'More'>, NativeStackNavigationProp<AppStackParamList>>;
type StackLink = 'TrainerAlerts' | 'MyTargets' | 'MyLeaves' | 'ApplyLeave' | 'Profile' | 'Settings' | 'Chats';
type MoreLink = { route: StackLink | 'Schedule'; label: string };

export function TrainerMoreScreen() {
  const theme = useTheme();
  const { t } = useTranslation();
  const layout = useTrainerLayout();
  const navigation = useNavigation<Navigation>();
  const profile = useTrainerProfile();
  const features = useWorkspaceFeatures();
  const photo = useMyProfilePhoto();
  const trainerPhoto = useMyTrainerPhoto();
  const [pending, setPending] = useState(false);
  const data = profile.data;
  const leaveEnabled = features.data?.[featureCodes.staffLeave] === true;
  const chatEnabled = features.data?.[featureCodes.gymChat] === true;
  const links = useMemo((): MoreLink[] => {
    const items: MoreLink[] = [
      { route: 'TrainerAlerts', label: t('trainer.alerts') },
      { route: 'MyTargets', label: t('trainerTargets.title') },
    ];
    if (leaveEnabled) {
      items.push({ route: 'MyLeaves', label: t('nav.myLeaves') }, { route: 'ApplyLeave', label: t('nav.applyLeave') });
    }
    if (chatEnabled) {
      items.push({ route: 'Chats', label: t('nav.chat') });
    }

    items.push(
      { route: 'Profile', label: t('nav.profile') },
      { route: 'Settings', label: t('nav.settings') },
      { route: 'Schedule', label: t('trainer.availability') },
    );
    return items;
  }, [chatEnabled, leaveEnabled, t]);

  return (
    <ScrollView
      contentContainerStyle={layout.content}
      refreshControl={
        <RefreshControl
          onRefresh={() => {
            void profile.refetch();
            void features.refetch();
          }}
          refreshing={profile.isRefetching || features.isRefetching}
          tintColor={layout.refresh}
        />
      }
    >
      <AppAvatar label={data ? `${data.firstName} ${data.lastName}` : ''} uri={trainerPhoto.data ?? photo.data} />
      <AppText variant="display">{data ? `${data.firstName} ${data.lastName}` : t('nav.profile')}</AppText>
      <AppText tone="muted">{data?.specialization ?? t('trainer.coach')}</AppText>
      <TrainerCard>
        <AppText variant="label">{t('trainer.qualifications')}</AppText>
        <AppText>{data?.certification ?? t('trainer.none')}</AppText>
        <AppText tone="muted">{data?.experienceYears ? t('trainer.experience', { years: data.experienceYears }) : t('trainer.none')}</AppText>
        {data?.bio ? <AppText>{data.bio}</AppText> : null}
        {data?.joiningDate ? <AppText tone="muted">{t('trainer.joined', { date: data.joiningDate })}</AppText> : null}
      </TrainerCard>
      <TrainerCard>
        <AppText variant="label">{t('trainer.availability')}</AppText>
        <AppText tone="muted">{t('trainer.availabilityBody')}</AppText>
      </TrainerCard>
      {links.map((link) => (
        <TrainerCard
          key={link.route}
          onPress={() => (link.route === 'Schedule' ? navigation.navigate('Schedule') : navigation.navigate(link.route))}
        >
          <AppText>{link.label}</AppText>
        </TrainerCard>
      ))}
      <AppButton label={t('profile.logout')} loading={pending} onPress={() => { setPending(true); void endSession(); }} style={{ backgroundColor: theme.colors.danger }} />
    </ScrollView>
  );
}
