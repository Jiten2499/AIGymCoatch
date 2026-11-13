import { ScrollView } from 'react-native';
import { useTranslation } from 'react-i18next';
import { useTheme } from '../../../app/providers/ThemeProvider';
import { AppText } from '../../../components/ui/AppText';

export function IntegrationsScreen() {
  const theme = useTheme();
  const { t } = useTranslation();

  return (
    <ScrollView contentContainerStyle={{ gap: theme.spacing.md, padding: theme.spacing.xl }}>
      <AppText variant="title">{t('integrations.title')}</AppText>
      <AppText>{t('integrations.googleDrive')}</AppText>
      <AppText tone="muted">{t('integrations.storageBlurb')}</AppText>
    </ScrollView>
  );
}
