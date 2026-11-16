import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Pressable, View } from 'react-native';
import { useTheme } from '../../app/providers/ThemeProvider';
import { AppText } from '../../components/ui/AppText';
import { ChevronIcon } from './planIcons';

export const planLengths = [7, 10, 15, 30] as const;

export function PlanLengthField({ value, onChange, disabled = false }: { value: number; onChange: (days: number) => void; disabled?: boolean }) {
  const theme = useTheme();
  const { t } = useTranslation();
  const [open, setOpen] = useState(false);
  return (
    <View style={{ gap: theme.spacing.xs }}>
      <AppText variant="label">{t('journey.planLength')}</AppText>
      <Pressable
        accessibilityLabel={t('journey.planLength')}
        accessibilityRole="button"
        accessibilityState={{ expanded: open, disabled }}
        disabled={disabled}
        onPress={() => setOpen((current) => !current)}
        style={{ alignItems: 'center', backgroundColor: theme.colors.surface, borderColor: theme.colors.border, borderRadius: theme.radius.lg, borderWidth: 1, flexDirection: 'row', justifyContent: 'space-between', minHeight: theme.control.minTouch, paddingHorizontal: theme.spacing.md }}
      >
        <AppText>{t('journey.planLengthOption', { count: value })}</AppText>
        <ChevronIcon />
      </Pressable>
      {open ? planLengths.map((days) => {
        const selected = days === value;
        return (
          <Pressable
            accessibilityRole="button"
            accessibilityState={{ selected }}
            key={days}
            onPress={() => { onChange(days); setOpen(false); }}
            style={{ backgroundColor: selected ? theme.colors.accent : theme.colors.surface, borderColor: theme.colors.border, borderRadius: theme.radius.md, borderWidth: 1, justifyContent: 'center', minHeight: theme.control.minTouch, paddingHorizontal: theme.spacing.md }}
          >
            <AppText style={{ color: selected ? theme.colors.onAccent : theme.colors.text }}>{t('journey.planLengthOption', { count: days })}</AppText>
          </Pressable>
        );
      }) : null}
    </View>
  );
}
