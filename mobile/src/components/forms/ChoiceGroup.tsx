import { Pressable, View } from 'react-native';
import { useTheme } from '../../app/providers/ThemeProvider';
import { AppText } from '../ui/AppText';

type ChoiceGroupProps = {
  label: string;
  value: string;
  options: readonly { value: string; label: string }[];
  onChange: (value: string) => void;
  required?: boolean;
  error?: string;
};

export function ChoiceGroup({ label, value, options, onChange, required = false, error }: ChoiceGroupProps) {
  const theme = useTheme();
  return (
    <View accessibilityRole="radiogroup" style={{ gap: theme.spacing.sm }}>
      <AppText accessibilityLabel={required ? `${label}, required` : label} variant="label">
        {label}
        {required ? <AppText tone="danger" variant="label"> *</AppText> : null}
      </AppText>
      <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: theme.spacing.sm }}>
        {options.map((option) => {
          const selected = option.value === value;
          return (
            <Pressable
              accessibilityRole="radio"
              accessibilityState={{ selected }}
              key={option.value}
              onPress={() => onChange(option.value)}
              style={{
                backgroundColor: selected ? theme.colors.accent : theme.colors.surface,
                borderColor: selected ? theme.colors.accent : theme.colors.border,
                borderRadius: theme.radius.pill,
                borderWidth: 1,
                minHeight: theme.control.minTouch,
                justifyContent: 'center',
                paddingHorizontal: theme.spacing.lg,
              }}
            >
              <AppText variant="label" style={{ color: selected ? theme.colors.onAccent : theme.colors.text }}>{option.label}</AppText>
            </Pressable>
          );
        })}
      </View>
      {error ? <AppText accessibilityRole="alert" tone="danger" variant="caption">{error}</AppText> : null}
    </View>
  );
}
