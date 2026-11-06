import { View } from 'react-native';

type GlyphName = 'home' | 'calendar' | 'people' | 'chart' | 'more' | 'workout' | 'nutrition' | 'progress' | 'bell' | 'chat';

export function TabGlyph({ name, color, size = 24 }: { name: GlyphName; color: string; size?: number }) {
  return (
    <View style={{ alignItems: 'center', height: size, justifyContent: 'center', width: size }}>
      {shape(name, color)}
    </View>
  );
}

export function Chevron({ color, direction }: { color: string; direction: 'left' | 'right' }) {
  return (
    <View
      style={{
        borderBottomWidth: 2,
        borderColor: color,
        borderLeftWidth: 2,
        height: 10,
        transform: [{ rotate: direction === 'left' ? '45deg' : '-135deg' }],
        width: 10,
      }}
    />
  );
}

function shape(name: GlyphName, color: string) {
  if (name === 'home') {
    return (
      <View style={{ alignItems: 'center' }}>
        <View style={{ borderBottomColor: color, borderBottomWidth: 8, borderLeftColor: 'transparent', borderLeftWidth: 9, borderRightColor: 'transparent', borderRightWidth: 9, height: 0, width: 0 }} />
        <View style={{ backgroundColor: color, height: 9, marginTop: 1, width: 12 }} />
      </View>
    );
  }
  if (name === 'calendar') {
    return (
      <View style={{ borderColor: color, borderRadius: 3, borderWidth: 2, height: 16, overflow: 'hidden', width: 16 }}>
        <View style={{ backgroundColor: color, height: 4 }} />
      </View>
    );
  }
  if (name === 'people') {
    return (
      <View style={{ alignItems: 'flex-end', flexDirection: 'row', gap: 2 }}>
        <Person color={color} scale={1} />
        <Person color={color} scale={0.8} />
      </View>
    );
  }
  if (name === 'chart' || name === 'progress') {
    const bars = name === 'progress' ? [6, 10, 15] : [14, 8, 11];
    return (
      <View style={{ alignItems: 'flex-end', flexDirection: 'row', gap: 2, height: 16 }}>
        {bars.map((height) => <View key={height} style={{ backgroundColor: color, height, width: 4 }} />)}
      </View>
    );
  }
  if (name === 'workout') {
    return (
      <View style={{ alignItems: 'center', flexDirection: 'row' }}>
        <View style={{ backgroundColor: color, borderRadius: 2, height: 10, width: 4 }} />
        <View style={{ backgroundColor: color, height: 3, width: 10 }} />
        <View style={{ backgroundColor: color, borderRadius: 2, height: 10, width: 4 }} />
      </View>
    );
  }
  if (name === 'nutrition') {
    return <View style={{ backgroundColor: color, borderRadius: 8, height: 14, width: 14 }} />;
  }
  if (name === 'chat') {
    return (
      <View style={{ borderColor: color, borderRadius: 4, borderWidth: 2, height: 12, width: 16 }}>
        <View style={{ backgroundColor: color, height: 2, left: 2, position: 'absolute', top: 10, transform: [{ rotate: '40deg' }], width: 6 }} />
      </View>
    );
  }
  if (name === 'bell') {
    return (
      <View style={{ alignItems: 'center' }}>
        <View style={{ borderColor: color, borderRadius: 8, borderWidth: 2, height: 12, width: 12 }} />
        <View style={{ backgroundColor: color, borderRadius: 1, height: 3, marginTop: -1, width: 4 }} />
        <View style={{ backgroundColor: color, borderRadius: 2, height: 3, marginTop: 1, width: 8 }} />
      </View>
    );
  }
  return (
    <View style={{ flexDirection: 'row', gap: 3 }}>
      {[0, 1, 2].map((dot) => <View key={dot} style={{ backgroundColor: color, borderRadius: 2, height: 4, width: 4 }} />)}
    </View>
  );
}

function Person({ color, scale }: { color: string; scale: number }) {
  return (
    <View style={{ alignItems: 'center' }}>
      <View style={{ backgroundColor: color, borderRadius: 6, height: 6 * scale, width: 6 * scale }} />
      <View style={{ backgroundColor: color, borderTopLeftRadius: 6, borderTopRightRadius: 6, height: 6 * scale, marginTop: 1, width: 10 * scale }} />
    </View>
  );
}
