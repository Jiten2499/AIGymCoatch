import { Pressable, View } from 'react-native';
import { useTheme } from '../../../app/providers/ThemeProvider';
import { AppText } from '../../../components/ui/AppText';

export function ChoiceList({ options, value, onChange }: { options: { value: string; label: string }[]; value: string; onChange: (value: string) => void }) {
  const theme = useTheme();
  return (
    <View style={{ gap: theme.spacing.sm }}>
      {options.map((option) => {
        const selected = option.value === value;
        return (
          <Pressable
            accessibilityRole="button"
            accessibilityState={{ selected }}
            key={option.value}
            onPress={() => onChange(option.value)}
            style={{ justifyContent: 'center', minHeight: theme.control.minTouch }}
          >
            <AppText tone={selected ? 'accent' : 'default'}>{selected ? `${option.label} ·` : option.label}</AppText>
          </Pressable>
        );
      })}
    </View>
  );
}
