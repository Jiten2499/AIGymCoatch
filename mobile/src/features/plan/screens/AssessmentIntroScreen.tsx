import type { NativeStackScreenProps } from '@react-navigation/native-stack';
import { useTranslation } from 'react-i18next';
import { ScrollView, View } from 'react-native';
import type { AppStackParamList } from '../../../app/navigation/types';
import { useTheme } from '../../../app/providers/ThemeProvider';
import { ScreenState } from '../../../components/feedback/ScreenState';
import { AppButton } from '../../../components/ui/AppButton';
import { AppCard } from '../../../components/ui/AppCard';
import { AppText } from '../../../components/ui/AppText';
import { errorText } from '../../../services/api/problem';
import { useFitnessAssessmentCompletion } from '../../assessment/api/assessment.queries';

type Props = NativeStackScreenProps<AppStackParamList, 'FitnessAssessmentIntro'>;

export function AssessmentIntroScreen({ navigation }: Props) {
  const theme = useTheme();
  const { t } = useTranslation();
  const completion = useFitnessAssessmentCompletion();
  const status = completion.data?.isCompleted
    ? t('journey.introDone')
    : completion.data?.exists
      ? t('journey.introDraft')
      : null;

  return (
    <ScreenState error={completion.isError ? errorText(completion.error, t('common.offline')) : null} loading={completion.isLoading} onRetry={() => void completion.refetch()}>
      <ScrollView contentContainerStyle={{ alignItems: 'center', gap: theme.spacing.lg, padding: theme.spacing.xl }}>
        <View style={{ alignItems: 'center', backgroundColor: theme.colors.surface, borderColor: theme.colors.accent, borderRadius: 72, borderWidth: 2, height: 144, justifyContent: 'center', width: 144 }}>
          <AppText variant="display">✓</AppText>
        </View>
        <AppText variant="display">{t('journey.introTitle')}</AppText>
        <AppText tone="muted">{t('journey.introBody')}</AppText>
        <AppCard>
          <AppText variant="label">{t('journey.introTime')}</AppText>
          <AppText tone="muted">{t('journey.introUsed')}</AppText>
          {status ? <AppText>{status}</AppText> : null}
        </AppCard>
        <AppButton
          label={completion.data?.exists ? t('journey.introEdit') : t('journey.introStart')}
          onPress={() => navigation.navigate('FitnessAssessment')}
        />
      </ScrollView>
    </ScreenState>
  );
}
