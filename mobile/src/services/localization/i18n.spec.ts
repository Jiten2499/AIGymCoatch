import { describe, expect, it } from '@jest/globals';
import { formatCurrency, formatNumber } from './format';
import { i18n } from './i18n';

describe('localization', () => {
  it('falls back to English when Hindi product copy is not reviewed yet', async () => {
    await i18n.changeLanguage('hi');

    expect(i18n.t('auth.signIn')).toBe('Sign in');
    expect(i18n.t('language.hi')).toBe('हिन्दी');
    expect(i18n.t('items', { count: 2 })).toBe('2 items');
  });

  it('formats numbers and currency for the selected locale', () => {
    expect(formatNumber(1200, 'en')).toContain('1');
    expect(formatCurrency(10, 'USD', 'en')).toContain('10');
  });
});
