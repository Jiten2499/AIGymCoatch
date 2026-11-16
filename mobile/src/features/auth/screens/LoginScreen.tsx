import { zodResolver } from '@hookform/resolvers/zod';
import type { NativeStackScreenProps } from '@react-navigation/native-stack';
import { useState } from 'react';
import { useForm, Controller } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { Image, Pressable, ScrollView, useWindowDimensions } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import type { AuthStackParamList } from '../../../app/navigation/types';
import { useTheme } from '../../../app/providers/ThemeProvider';
import growfitLogo from '../../../../assets/images/growfit-logo.png';
import { FormField } from '../../../components/forms/FormField';
import { AppButton } from '../../../components/ui/AppButton';
import { AppText } from '../../../components/ui/AppText';
import { AppTextInput } from '../../../components/ui/AppTextInput';
import { signIn } from '../../../services/auth/session';
import { toApiError } from '../../../services/api/problem';
import { loginSchema, type LoginValues } from '../schemas';

type Props = NativeStackScreenProps<AuthStackParamList, 'Login'>;

export function LoginScreen({ navigation }: Props) {
  const theme = useTheme();
  const insets = useSafeAreaInsets();
  const { height, width } = useWindowDimensions();
  const { t } = useTranslation();
  const logoSize = Math.min(156, Math.round(width * 0.4), Math.round(height * 0.18));
  const [formError, setFormError] = useState<string | null>(null);
  const { control, handleSubmit, formState } = useForm<LoginValues>({
    resolver: zodResolver(loginSchema),
    defaultValues: { email: '', password: '' },
  });

  const submit = handleSubmit(async (values) => {
    setFormError(null);
    try {
      await signIn(values);
    } catch (error) {
      setFormError(toApiError(error).message);
    }
  });

  return (
    <ScrollView
      contentContainerStyle={{ flexGrow: 1, gap: theme.spacing.lg, padding: theme.spacing.xl, paddingTop: insets.top + theme.spacing.xl }}
      keyboardShouldPersistTaps="handled"
      style={{ backgroundColor: theme.colors.background, flex: 1 }}
    >
      <Image
        accessibilityLabel="Growfit"
        accessibilityRole="image"
        resizeMode="contain"
        source={growfitLogo}
        style={{ alignSelf: 'center', height: logoSize, width: logoSize }}
      />
      <AppText tone="muted">{t('auth.signIn')}</AppText>
      <Controller
        control={control}
        name="email"
        render={({ field, fieldState }) => (
          <FormField error={fieldState.error?.message} label={t('auth.email')}>
            <AppTextInput accessibilityLabel={t('auth.email')} autoCapitalize="none" autoComplete="email" keyboardType="email-address" onBlur={field.onBlur} onChangeText={field.onChange} value={field.value} invalid={Boolean(fieldState.error)} />
          </FormField>
        )}
      />
      <Controller
        control={control}
        name="password"
        render={({ field, fieldState }) => (
          <FormField error={fieldState.error?.message} label={t('auth.password')}>
            <AppTextInput accessibilityLabel={t('auth.password')} autoComplete="password" onBlur={field.onBlur} onChangeText={field.onChange} secureTextEntry value={field.value} invalid={Boolean(fieldState.error)} />
          </FormField>
        )}
      />
      {formError ? <AppText accessibilityRole="alert" tone="danger">{formError}</AppText> : null}
      <AppButton label={t('auth.signIn')} loading={formState.isSubmitting} onPress={() => { void submit(); }} />
      <Pressable accessibilityLabel={t('auth.forgot')} accessibilityRole="button" onPress={() => navigation.navigate('ForgotPassword')} style={{ minHeight: theme.control.minTouch, justifyContent: 'center' }}>
        <AppText tone="accent">{t('auth.forgot')}</AppText>
      </Pressable>
      <Pressable accessibilityLabel={t('auth.createAccount')} accessibilityRole="button" onPress={() => navigation.navigate('Register')} style={{ minHeight: theme.control.minTouch, justifyContent: 'center' }} testID="create-account">
        <AppText>{t('auth.createAccount')}</AppText>
      </Pressable>
    </ScrollView>
  );
}
