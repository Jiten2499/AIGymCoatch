import { Image, Pressable, View } from 'react-native';
import { AppText } from '../../components/ui/AppText';

const coach = require('../../../assets/journey/coach-portrait.jpg') as number;
const workout = require('../../../assets/journey/upper-body-workout.jpg') as number;
const bench = require('../../../assets/journey/bench-press.jpg') as number;
const ridge = require('../../../assets/journey/green-ridge.jpg') as number;

function photoFor(label: string): number {
  return /bench|chest|press/i.test(label) ? bench : workout;
}

export function CoachPhoto({ uri }: { uri?: string | null }) {
  return (
    <Image
      accessibilityIgnoresInvertColors
      accessibilityLabel="Athlete"
      source={uri ? { uri } : coach}
      style={{ borderRadius: 22, height: 140, width: 108 }}
    />
  );
}

export function ScenePhoto({ label, height = 190, onPress }: { label: string; height?: number; onPress?: () => void }) {
  const frame = (
    <View>
      <Image accessibilityIgnoresInvertColors resizeMode="cover" source={photoFor(label)} style={{ borderRadius: 20, height, width: '100%' }} />
      <View pointerEvents="none" style={{ alignItems: 'center', bottom: 0, justifyContent: 'center', left: 0, position: 'absolute', right: 0, top: 0 }}>
        <View style={{ alignItems: 'center', backgroundColor: '#c8f54e', borderRadius: 28, height: 56, justifyContent: 'center', width: 56 }}>
          <AppText style={{ color: '#10211a', marginLeft: 4 }}>▶</AppText>
        </View>
      </View>
    </View>
  );
  if (!onPress) {
    return frame;
  }
  return (
    <Pressable accessibilityLabel={label} accessibilityRole="imagebutton" onPress={onPress}>
      {frame}
    </Pressable>
  );
}

export function RidgeBanner({ title, subtitle }: { title: string; subtitle: string }) {
  return (
    <View style={{ borderRadius: 20, minHeight: 168, overflow: 'hidden' }}>
      <Image accessibilityIgnoresInvertColors resizeMode="cover" source={ridge} style={{ height: 168, width: '100%' }} />
      <View style={{ bottom: 16, gap: 4, left: 16, position: 'absolute', right: 16 }}>
        <AppText variant="title">{title}</AppText>
        <AppText>{subtitle}</AppText>
      </View>
    </View>
  );
}

export function ThumbPhoto({ label }: { label: string }) {
  return <Image accessibilityIgnoresInvertColors resizeMode="cover" source={photoFor(label)} style={{ borderRadius: 14, height: 72, width: 72 }} />;
}

export function AiOrb() {
  return (
    <View style={{ alignItems: 'center', height: 188, justifyContent: 'center' }}>
      <View style={{ backgroundColor: 'rgba(200,245,78,0.16)', borderRadius: 94, height: 188, position: 'absolute', width: 188 }} />
      <View style={{ backgroundColor: 'rgba(200,245,78,0.32)', borderRadius: 68, height: 136, position: 'absolute', width: 136 }} />
      <View style={{ alignItems: 'center', backgroundColor: '#10211a', borderColor: '#c8f54e', borderRadius: 52, borderWidth: 2, height: 104, justifyContent: 'center', width: 104 }}>
        <AppText tone="accent" variant="title">AI</AppText>
      </View>
    </View>
  );
}
