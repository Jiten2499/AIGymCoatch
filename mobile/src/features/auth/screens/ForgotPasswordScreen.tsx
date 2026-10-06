import { zodResolver } from '@hookform/resolvers/zod';
import type { NativeStackScreenProps } from '@react-navigation/native-stack';
import { useState } from 'react';
import { Controller, useForm } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { Pressable, ScrollView } from 'react-native';
import type { AuthStackParamList } from '../../../app/navigation/types';
import { useTheme } from '../../../app/providers/ThemeProvider';
import { FormField } from '../../../components/forms/FormField';
import { AppButton } from '../../../components/ui/AppButton';
import { AppText } from '../../../components/ui/AppText';
import { AppTextInput } from '../../../components/ui/AppTextInput';
import { forgotPassword } from '../api/auth.api';
import { toApiError } from '../../../services/api/problem';
import { forgotSchema, type ForgotValues } from '../schemas';

type Props = NativeStackScreenProps<AuthStackParamList, 'ForgotPassword'>;

export function ForgotPasswordScreen({ navigation }: Props) {
  const theme = useTheme();
  const { t } = useTranslation();
  const [formError, setFormError] = useState<string | null>(null);
  const [sent, setSent] = useState(false);
  const { control, handleSubmit, formState } = useForm<ForgotValues>({
    resolver: zodResolver(forgotSchema),
    defaultValues: { email: '' },
  });

  const submit = handleSubmit(async (values) => {
    setFormError(null);
    try {
      await forgotPassword(values.email);
      setSent(true);
    } catch (error) {
      setFormError(toApiError(error).message);
    }
  });

  return (
    <ScrollView contentContainerStyle={{ gap: theme.spacing.lg, padding: theme.spacing.xl }}>
      <AppText variant="title">{t('auth.forgot')}</AppText>
      {sent ? <AppText>{t('auth.forgotSent')}</AppText> : null}
      <Controller
        control={control}
        name="email"
        render={({ field, fieldState }) => (
          <FormField error={fieldState.error?.message} label={t('auth.email')}>
            <AppTextInput accessibilityLabel={t('auth.email')} autoCapitalize="none" keyboardType="email-address" onBlur={field.onBlur} onChangeText={field.onChange} value={field.value} invalid={Boolean(fieldState.error)} />
          </FormField>
        )}
      />
      {formError ? <AppText accessibilityRole="alert" tone="danger">{formError}</AppText> : null}
      <AppButton label={t('auth.submit')} loading={formState.isSubmitting} onPress={submit} />
      <Pressable accessibilityRole="button" onPress={() => navigation.navigate('Login')} style={{ minHeight: theme.control.minTouch, justifyContent: 'center' }}>
        <AppText tone="accent">{t('auth.backToSignIn')}</AppText>
      </Pressable>
    </ScrollView>
  );
}
