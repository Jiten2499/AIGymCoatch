import type { NativeStackScreenProps } from '@react-navigation/native-stack';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { ScrollView } from 'react-native';
import type { AppStackParamList } from '../../../app/navigation/types';
import { useTheme } from '../../../app/providers/ThemeProvider';
import { AppButton } from '../../../components/ui/AppButton';
import { AppText } from '../../../components/ui/AppText';
import { AppTextInput } from '../../../components/ui/AppTextInput';
import { useSaveLead } from '../api/queries';

type Props = NativeStackScreenProps<AppStackParamList, 'LeadForm'>;

export function LeadFormScreen({ navigation, route }: Props) {
  return <LeadForm leadId={route.params?.leadId} onSaved={() => navigation.replace('Leads')} />;
}

export function LeadForm({ leadId, onSaved }: { leadId?: string; onSaved: () => void }) {
  const theme = useTheme();
  const { t } = useTranslation();
  const save = useSaveLead();
  const [name, setName] = useState('');
  const [mobile, setMobile] = useState('');
  const [email, setEmail] = useState('');
  const [source, setSource] = useState('WalkIn');
  const [notes, setNotes] = useState('');

  return (
    <ScrollView contentContainerStyle={{ gap: theme.spacing.md, padding: theme.spacing.xl }}>
      <AppText variant="title">{leadId ? t('leads.edit') : t('leads.add')}</AppText>
      <AppTextInput accessibilityLabel={t('leads.name')} onChangeText={setName} placeholder={t('leads.name')} value={name} />
      <AppTextInput accessibilityLabel={t('leads.mobile')} onChangeText={setMobile} placeholder={t('leads.mobile')} value={mobile} />
      <AppTextInput accessibilityLabel={t('leads.email')} onChangeText={setEmail} placeholder={t('leads.email')} value={email} />
      <AppTextInput accessibilityLabel={t('leads.source')} onChangeText={setSource} placeholder={t('leads.source')} value={source} />
      <AppTextInput accessibilityLabel={t('leads.notes')} onChangeText={setNotes} placeholder={t('leads.notes')} value={notes} />
      <AppButton label={t('leads.save')} onPress={() => void save.mutateAsync({ id: leadId, input: { fullName: name, mobile, email, source, notes } }).then(onSaved)} />
    </ScrollView>
  );
}
