import { Image, View } from 'react-native';
import { useTheme } from '../../app/providers/ThemeProvider';
import { AppText } from '../../components/ui/AppText';
import { nameInitials } from '../../components/ui/initials';

const bench = require('../../../assets/journey/bench-press.jpg') as number;
const workout = require('../../../assets/journey/upper-body-workout.jpg') as number;

export function ExerciseThumb({ label, muscle }: { label: string; muscle?: string | null }) {
  const theme = useTheme();
  const text = `${label} ${muscle ?? ''}`.toLowerCase();
  const source = /bench|chest|press/.test(text) ? bench : /squat|deadlift|row|pull|curl|raise|leg/.test(text) ? workout : null;
  if (!source) {
    return (
      <View style={{ alignItems: 'center', backgroundColor: theme.colors.surfaceMuted, borderColor: theme.colors.border, borderRadius: theme.radius.md, borderWidth: 1, height: 72, justifyContent: 'center', width: 72 }}>
        <AppText variant="label">{nameInitials(label)}</AppText>
      </View>
    );
  }
  return <Image accessibilityIgnoresInvertColors accessibilityLabel={label} resizeMode="cover" source={source} style={{ borderRadius: theme.radius.md, height: 72, width: 72 }} />;
}
