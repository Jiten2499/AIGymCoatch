import type { NativeStackScreenProps } from '@react-navigation/native-stack';
import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Image, ScrollView } from 'react-native';
import type { AppStackParamList } from '../../../app/navigation/types';
import { useTheme } from '../../../app/providers/ThemeProvider';
import { FeatureGate } from '../../../components/access/FeatureGate';
import { ScreenState } from '../../../components/feedback/ScreenState';
import { AppButton } from '../../../components/ui/AppButton';
import { MemberPhoto } from '../../../components/ui/MemberPhoto';
import { AppText } from '../../../components/ui/AppText';
import { AppTextInput } from '../../../components/ui/AppTextInput';
import { featureCodes } from '../../../constants/access';
import { errorText } from '../../../services/api/problem';
import { uploadMemberPhoto } from '../api/members.api';
import { useMember, useMemberPhotoRemoval, useSaveMember } from '../api/queries';
import { pickMemberPhoto, type PickedMemberPhoto } from '../photo/pickMemberPhoto';

type Props = NativeStackScreenProps<AppStackParamList, 'MemberForm'>;

export function MemberFormScreen({ navigation, route }: Props) {
  const memberId = route.params?.memberId;
  return (
    <FeatureGate code={featureCodes.memberManagement}>
      <MemberForm memberId={memberId} onSaved={(id) => navigation.replace('MemberDetail', { memberId: id })} />
    </FeatureGate>
  );
}

export function MemberForm({ memberId, onSaved }: { memberId?: string; onSaved: (id: string) => void }) {
  const theme = useTheme();
  const { t } = useTranslation();
  const existing = useMember(memberId ?? '', Boolean(memberId));
  const save = useSaveMember(memberId);
  const remove = useMemberPhotoRemoval(memberId ?? '');
  const [photo, setPhoto] = useState<PickedMemberPhoto | null>(null);
  const [firstName, setFirstName] = useState('');
  const [lastName, setLastName] = useState('');
  const [phone, setPhone] = useState('');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [address, setAddress] = useState('');
  const [goal, setGoal] = useState('');
  const [emergencyName, setEmergencyName] = useState('');
  const [emergencyPhone, setEmergencyPhone] = useState('');
  const [relationship, setRelationship] = useState('');
  useEffect(() => {
    if (!existing.data) {
      return;
    }
    setFirstName(existing.data.firstName);
    setLastName(existing.data.lastName);
    setPhone(existing.data.phone ?? '');
    setEmail(existing.data.email);
    setAddress(existing.data.address ?? '');
    setGoal(existing.data.fitnessGoal ?? '');
    setEmergencyName(existing.data.emergencyContactName ?? '');
    setEmergencyPhone(existing.data.emergencyContactPhone ?? '');
    setRelationship(existing.data.emergencyContactRelationship ?? '');
  }, [existing.data]);

  async function submit() {
    const saved = await save.mutateAsync({
      address,
      email: memberId ? undefined : email,
      emergencyContactName: emergencyName,
      emergencyContactPhone: emergencyPhone,
      emergencyContactRelationship: relationship,
      firstName,
      fitnessGoal: goal || undefined,
      lastName,
      password: memberId ? undefined : password,
      joinDate: existing.data?.joinDate ?? new Date().toISOString().slice(0, 10),
      phone,
      status: existing.data?.status ?? 'Active',
    });
    if (photo) {
      await uploadMemberPhoto(saved.id, photo.uri, photo.mimeType, photo.fileName);
    }
    onSaved(saved.id);
  }

  return (
    <ScreenState error={existing.isError ? errorText(existing.error, t('common.offline')) : null} loading={Boolean(memberId) && existing.isLoading} onRetry={() => void existing.refetch()}>
      <ScrollView contentContainerStyle={{ gap: theme.spacing.md, padding: theme.spacing.xl }}>
        <AppText variant="title">{memberId ? t('members.edit') : t('members.add')}</AppText>
        {photo ? <Image accessibilityLabel={t('members.photo')} source={{ uri: photo.uri }} style={{ height: 120, width: 120 }} /> : memberId && existing.data?.hasPhoto ? <MemberPhoto label={`${existing.data.firstName} ${existing.data.lastName}`} path={`/members/${memberId}/profile-photo`} size={120} /> : null}
        <AppButton label={t('members.choosePhoto')} onPress={() => void pickMemberPhoto().then(setPhoto)} variant="secondary" />
        {memberId ? <AppButton label={t('members.removePhoto')} onPress={() => void remove.mutateAsync()} variant="secondary" /> : null}
        <Field label={t('members.firstName')} onChangeText={setFirstName} value={firstName} />
        <Field label={t('members.lastName')} onChangeText={setLastName} value={lastName} />
        {memberId ? null : <Field autoCapitalize="none" label={t('members.email')} onChangeText={setEmail} value={email} />}
        {memberId ? null : <Field autoCapitalize="none" label={t('auth.password')} onChangeText={setPassword} secure value={password} />}
        <Field keyboardType="phone-pad" label={t('members.mobile')} onChangeText={setPhone} value={phone} />
        <Field label={t('members.goal')} onChangeText={setGoal} value={goal} />
        <Field label={t('members.address')} onChangeText={setAddress} value={address} />
        <Field label={t('members.emergencyName')} onChangeText={setEmergencyName} value={emergencyName} />
        <Field keyboardType="phone-pad" label={t('members.emergencyMobile')} onChangeText={setEmergencyPhone} value={emergencyPhone} />
        <Field label={t('members.emergencyRelationship')} onChangeText={setRelationship} value={relationship} />
        {save.isError ? <AppText tone="danger">{errorText(save.error, t('common.offline'))}</AppText> : null}
        <AppButton label={t('members.save')} loading={save.isPending} onPress={() => void submit()} />
      </ScrollView>
    </ScreenState>
  );
}

function Field(props: { label: string; value: string; onChangeText: (value: string) => void; autoCapitalize?: 'none' | 'sentences'; keyboardType?: 'default' | 'phone-pad'; secure?: boolean }) {
  return (
    <>
      <AppText variant="label">{props.label}</AppText>
      <AppTextInput accessibilityLabel={props.label} autoCapitalize={props.autoCapitalize} keyboardType={props.keyboardType} onChangeText={props.onChangeText} secureTextEntry={props.secure} value={props.value} />
    </>
  );
}
