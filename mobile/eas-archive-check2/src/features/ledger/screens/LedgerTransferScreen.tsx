import { useNavigation } from '@react-navigation/native';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { ScrollView } from 'react-native';
import { useTheme } from '../../../app/providers/ThemeProvider';
import { FeatureGate } from '../../../components/access/FeatureGate';
import { ScreenState } from '../../../components/feedback/ScreenState';
import { AppButton } from '../../../components/ui/AppButton';
import { AppText } from '../../../components/ui/AppText';
import { AppTextInput } from '../../../components/ui/AppTextInput';
import { featureCodes } from '../../../constants/access';
import { errorText } from '../../../services/api/problem';
import { useCreateTransfer, useFinancialAccounts } from '../api/queries';

export function LedgerTransferScreen() {
  return (
    <FeatureGate code={featureCodes.gymFinancialLedger}>
      <TransferForm enabled />
    </FeatureGate>
  );
}

function TransferForm({ enabled }: { enabled: boolean }) {
  const theme = useTheme();
  const { t } = useTranslation();
  const navigation = useNavigation();
  const accounts = useFinancialAccounts(enabled);
  const transfer = useCreateTransfer();
  const active = (accounts.data ?? []).filter((account) => account.isActive);
  const [fromAccountId, setFromAccountId] = useState('');
  const [toAccountId, setToAccountId] = useState('');
  const [amount, setAmount] = useState('');
  const [transferDate, setTransferDate] = useState(new Date().toISOString().slice(0, 10));
  const [notes, setNotes] = useState('');
  const from = fromAccountId || active[0]?.id || '';
  const to = toAccountId || active[1]?.id || active[0]?.id || '';

  return (
    <ScreenState
      empty={accounts.isFetched && active.length < 2}
      emptyBody={t('ledger.needAccounts')}
      emptyTitle={t('ledger.transfer')}
      error={accounts.isError ? errorText(accounts.error, t('common.offline')) : null}
      loading={accounts.isLoading}
      onRetry={() => void accounts.refetch()}
    >
      <ScrollView contentContainerStyle={{ gap: theme.spacing.md, padding: theme.spacing.xl }}>
        <AppText variant="title">{t('ledger.transfer')}</AppText>
        {active.map((account) => (
          <AppButton
            key={account.id}
            label={`${from === account.id ? 'From: ' : to === account.id ? 'To: ' : ''}${account.name}`}
            onPress={() => {
              if (!fromAccountId || fromAccountId === account.id) {
                setFromAccountId(account.id);
              } else {
                setToAccountId(account.id);
              }
            }}
          />
        ))}
        <AppTextInput accessibilityLabel={t('ledger.amount')} onChangeText={setAmount} placeholder={t('ledger.amount')} value={amount} />
        <AppTextInput accessibilityLabel={t('ledger.date')} onChangeText={setTransferDate} placeholder={t('ledger.date')} value={transferDate} />
        <AppTextInput accessibilityLabel={t('ledger.notes')} onChangeText={setNotes} placeholder={t('ledger.notes')} value={notes} />
        {transfer.isError ? <AppText>{errorText(transfer.error, t('common.offline'))}</AppText> : null}
        <AppButton
          label={t('ledger.saveTransfer')}
          onPress={() => void transfer.mutateAsync({ fromAccountId: from, toAccountId: to, amount: Number(amount), transferDate, notes: notes.trim() || null }).then(() => navigation.goBack())}
        />
      </ScrollView>
    </ScreenState>
  );
}
