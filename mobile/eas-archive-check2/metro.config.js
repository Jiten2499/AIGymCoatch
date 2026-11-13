const path = require('path');
const { getDefaultConfig } = require('expo/metro-config');

// Windows Metro only auto-selects the fast watcher on macOS. The slow fallback
// walk times out, Metro stays uninitialized, and the phone bundle crashes in
// DependencyGraph. Recursive fs.watch is supported on Windows.
if (process.platform === 'win32') {
  const NativeWatcher = require('@expo/metro-file-map/build/watchers/NativeWatcher').default;
  NativeWatcher.isSupported = () => true;
}

const config = getDefaultConfig(__dirname);

// @microsoft/signalr has no package "exports" map. Metro's package-exports
// resolver then fails the import on Android. Pin the React Native CJS build,
// which uses the global WebSocket and skips Node-only transports.
const signalrEntry = path.resolve(__dirname, 'node_modules/@microsoft/signalr/dist/cjs/index.js');

config.resolver.resolveRequest = (context, moduleName, platform) => {
  if (moduleName === '@microsoft/signalr') {
    return { type: 'sourceFile', filePath: signalrEntry };
  }

  return context.resolveRequest(context, moduleName, platform);
};

module.exports = config;
