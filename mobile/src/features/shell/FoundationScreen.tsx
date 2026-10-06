import { View } from 'react-native';
import { useTranslation } from 'react-i18next';
import { useTheme } from '../../app/providers/ThemeProvider';
import { AppText } from '../../components/ui/AppText';

export function FoundationScreen({ titleKey }: { titleKey: string }) {
  const theme = useTheme();
  const { t } = useTranslation();
  return (
    <View style={{ backgroundColor: theme.colors.background, flex: 1, gap: theme.spacing.md, padding: theme.spacing.xl }}>
      <AppText variant="title">{t(titleKey)}</AppText>
      <AppText tone="muted">{t('foundation.body')}</AppText>
    </View>
  );
}
