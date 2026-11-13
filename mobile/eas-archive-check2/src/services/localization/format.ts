const intlLocale: Record<string, string> = {
  en: 'en',
  hi: 'hi',
  ne: 'ne',
};

export function intlTag(locale: string): string {
  return intlLocale[locale] ?? 'en';
}

export function formatDate(value: Date, locale: string): string {
  return new Intl.DateTimeFormat(intlTag(locale), { dateStyle: 'medium' }).format(value);
}

export function formatDateTime(value: Date, locale: string): string {
  return new Intl.DateTimeFormat(intlTag(locale), { dateStyle: 'medium', timeStyle: 'short' }).format(value);
}

export function formatNumber(value: number, locale: string): string {
  return new Intl.NumberFormat(intlTag(locale)).format(value);
}

export function formatCurrency(value: number, currency: string, locale: string): string {
  return new Intl.NumberFormat(intlTag(locale), { style: 'currency', currency }).format(value);
}
