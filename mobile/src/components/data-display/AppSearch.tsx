import { useTranslation } from 'react-i18next';
import { AppTextInput } from '../ui/AppTextInput';

export function AppSearch({ value, onChangeText }: { value: string; onChangeText: (value: string) => void }) {
  const { t } = useTranslation();
  return (
    <AppTextInput
      accessibilityLabel={t('common.search')}
      autoCapitalize="none"
      autoCorrect={false}
      onChangeText={onChangeText}
      placeholder={t('common.search')}
      value={value}
    />
  );
}
