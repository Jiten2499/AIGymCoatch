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
import { useFinancialAccounts, useSaveFinancialAccount } from '../api/queries';

export function LedgerAccountsScreen() {
  return (
    <FeatureGate code={featureCodes.gymFinancialLedger}>
      <AccountsBody enabled />
    </FeatureGate>
  );
}

function AccountsBody({ enabled }: { enabled: boolean }) {
  const theme = useTheme();
  const { t } = useTranslation();
  const accounts = useFinancialAccounts(enabled);
  const save = useSaveFinancialAccount();
  const [name, setName] = useState('');
  const [kind, setKind] = useState('Cash');

  return (
    <ScreenState
      empty={accounts.isFetched && (accounts.data?.length ?? 0) === 0}
      emptyBody={t('ledger.noAccounts')}
      emptyTitle={t('ledger.accounts')}
      error={accounts.isError ? errorText(accounts.error, t('common.offline')) : null}
      loading={accounts.isLoading}
      onRetry={() => void accounts.refetch()}
    >
      <ScrollView contentContainerStyle={{ gap: theme.spacing.md, padding: theme.spacing.xl }}>
        <AppText variant="title">{t('ledger.accounts')}</AppText>
        {(accounts.data ?? []).map((account) => (
          <AppText key={account.id}>
            {account.name} · {account.accountKind} · {account.balance} {account.currency} · {account.isActive ? t('ledger.active') : t('ledger.inactive')}
          </AppText>
        ))}
        {(accounts.data ?? []).slice(0, 1).map((account) => (
          <AppButton
            key={`toggle-${account.id}`}
            label={account.isActive ? t('ledger.deactivate') : t('ledger.activate')}
            onPress={() => void save.mutateAsync({ id: account.id, name: account.name, active: !account.isActive })}
          />
        ))}
        <AppTextInput accessibilityLabel={t('ledger.accountName')} onChangeText={setName} placeholder={t('ledger.accountName')} value={name} />
        <AppButton label={kind} onPress={() => setKind((current) => (current === 'Cash' ? 'Bank' : current === 'Bank' ? 'Upi' : current === 'Upi' ? 'Card' : current === 'Card' ? 'Other' : 'Cash'))} />
        <AppButton label={t('ledger.saveAccount')} onPress={() => void save.mutateAsync({ name, accountKind: kind }).then(() => setName(''))} />
      </ScrollView>
    </ScreenState>
  );
}
