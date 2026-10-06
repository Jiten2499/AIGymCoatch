import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { ScrollView } from 'react-native';
import { useTheme } from '../../../app/providers/ThemeProvider';
import { AppButton } from '../../../components/ui/AppButton';
import { AppCard } from '../../../components/ui/AppCard';
import { AppText } from '../../../components/ui/AppText';
import { endSession } from '../../../services/auth/session';
import { useSessionStore } from '../../../store/sessionStore';
import { useWorkspaceStore } from '../../../store/workspaceStore';

export function ProfileScreen() {
  const theme = useTheme();
  const { t } = useTranslation();
  const user = useSessionStore((state) => state.user);
  const current = useSessionStore((state) => state.currentTenant);
  const workspaces = useWorkspaceStore((state) => state.workspaces);
  const [pending, setPending] = useState(false);

  return (
    <ScrollView contentContainerStyle={{ gap: theme.spacing.lg, padding: theme.spacing.xl }}>
      <AppText variant="title">{t('nav.profile')}</AppText>
      <AppCard>
        <AppText variant="label">{user ? `${user.firstName} ${user.lastName}` : t('profile.signedIn')}</AppText>
        {user ? <AppText tone="muted">{user.email}</AppText> : null}
      </AppCard>
      <AppCard>
        <AppText variant="label">{t('workspace.current')}</AppText>
        <AppText>{current?.name ?? t('profile.noWorkspace')}</AppText>
        <AppText tone="muted">{current?.type ?? ''}</AppText>
      </AppCard>
      {workspaces.length > 0 ? (
        <AppCard>
          <AppText variant="label">{t('profile.workspaces')}</AppText>
          {workspaces.map((workspace) => (
            <AppText key={workspace.id}>{workspace.name}{workspace.id === current?.id ? ` · ${t('profile.current')}` : ''}</AppText>
          ))}
        </AppCard>
      ) : null}
      <AppButton label={t('profile.logout')} loading={pending} onPress={() => { setPending(true); void endSession(); }} />
    </ScrollView>
  );
}
