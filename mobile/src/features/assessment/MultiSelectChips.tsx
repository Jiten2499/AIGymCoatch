import { Pressable, View } from 'react-native';
import { useTheme } from '../../app/providers/ThemeProvider';
import { AppText } from '../../components/ui/AppText';

export function MultiSelectChips({
  label,
  values,
  options,
  onChange,
}: {
  label: string;
  values: string[];
  options: readonly { value: string; label: string }[];
  onChange: (values: string[]) => void;
}) {
  const theme = useTheme();
  return (
    <View style={{ gap: theme.spacing.sm }}>
      <AppText variant="label">{label}</AppText>
      <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: theme.spacing.sm }}>
        {options.map((option) => {
          const selected = values.includes(option.value);
          return (
            <Pressable
              accessibilityRole="checkbox"
              accessibilityState={{ checked: selected }}
              key={option.value}
              onPress={() => onChange(selected ? values.filter((item) => item !== option.value) : [...values, option.value])}
              style={{
                backgroundColor: selected ? theme.colors.accent : theme.colors.surface,
                borderColor: selected ? theme.colors.accent : theme.colors.border,
                borderRadius: theme.radius.pill,
                borderWidth: 1,
                justifyContent: 'center',
                minHeight: theme.control.minTouch,
                paddingHorizontal: theme.spacing.lg,
              }}
            >
              <AppText style={{ color: selected ? theme.colors.onAccent : theme.colors.text }}>{option.label}</AppText>
            </Pressable>
          );
        })}
      </View>
    </View>
  );
}
