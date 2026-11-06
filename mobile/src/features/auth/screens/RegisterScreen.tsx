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
import { toApiError } from '../../../services/api/problem';
import { signUp } from '../../../services/auth/session';
import { registerSchema, type RegisterValues } from '../schemas';

type Props = NativeStackScreenProps<AuthStackParamList, 'Register'>;

export function RegisterScreen({ navigation }: Props) {
  const theme = useTheme();
  const { t } = useTranslation();
  const [formError, setFormError] = useState<string | null>(null);
  const { control, handleSubmit, formState } = useForm<RegisterValues>({
    resolver: zodResolver(registerSchema),
    defaultValues: { email: '', password: '', firstName: '', lastName: '', phone: '' },
  });

  const submit = handleSubmit(async (values) => {
    setFormError(null);
    try {
      await signUp({ ...values, phone: values.phone || undefined });
    } catch (error) {
      setFormError(toApiError(error).message);
    }
  });

  return (
    <ScrollView contentContainerStyle={{ gap: theme.spacing.lg, padding: theme.spacing.xl }} keyboardShouldPersistTaps="handled">
      <AppText variant="title">{t('auth.createAccount')}</AppText>
      {(['firstName', 'lastName', 'email', 'phone'] as const).map((name) => (
        <Controller
          key={name}
          control={control}
          name={name}
          render={({ field, fieldState }) => (
            <FormField error={fieldState.error?.message} label={t(`auth.${name}`)}>
              <AppTextInput accessibilityLabel={t(`auth.${name}`)} autoCapitalize={name === 'email' ? 'none' : 'words'} onBlur={field.onBlur} onChangeText={field.onChange} value={field.value ?? ''} invalid={Boolean(fieldState.error)} />
            </FormField>
          )}
        />
      ))}
      <Controller
        control={control}
        name="password"
        render={({ field, fieldState }) => (
          <FormField error={fieldState.error?.message} hint={t('auth.passwordHint')} label={t('auth.password')}>
            <AppTextInput accessibilityLabel={t('auth.password')} onBlur={field.onBlur} onChangeText={field.onChange} secureTextEntry value={field.value} invalid={Boolean(fieldState.error)} />
          </FormField>
        )}
      />
      {formError ? <AppText accessibilityRole="alert" tone="danger">{formError}</AppText> : null}
      <AppButton label={t('auth.submit')} loading={formState.isSubmitting} onPress={() => { void submit(); }} />
      <Pressable accessibilityRole="button" onPress={() => navigation.navigate('Login')} style={{ minHeight: theme.control.minTouch, justifyContent: 'center' }}>
        <AppText tone="accent">{t('auth.backToSignIn')}</AppText>
      </Pressable>
    </ScrollView>
  );
}
