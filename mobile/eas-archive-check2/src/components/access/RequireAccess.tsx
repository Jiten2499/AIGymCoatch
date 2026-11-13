import type { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { AppText } from '../ui/AppText';

export function RequireAccess({ allowed, children }: { allowed: boolean; children: ReactNode }) {
  const { t } = useTranslation();
  if (!allowed) {
    return <AppText tone="muted">{t('common.forbidden')}</AppText>;
  }
  return children;
}
