import { NativeModules, TurboModuleRegistry } from 'react-native';

const defaultApiUrl = 'http://localhost:8080';

export function hostFromScriptUrl(scriptUrl: string | null | undefined): string | null {
  if (!scriptUrl) {
    return null;
  }

  const match = /^[a-zA-Z][\w+.-]*:\/\/([^/:?#]+)/.exec(scriptUrl);
  return usableHost(match?.[1]);
}

export function hostFromPackagerAddress(value: string | null | undefined): string | null {
  if (!value) {
    return null;
  }

  if (value.includes('://')) {
    return hostFromScriptUrl(value);
  }

  return usableHost(value.split(':')[0]);
}

export function resolveApiBaseUrl(configured: string | undefined, packagerHost: string | null): string {
  const base = (configured?.trim() || defaultApiUrl).replace(/\/$/, '');
  if (!packagerHost || !isLoopbackUrl(base)) {
    return base;
  }

  const url = new URL(base);
  url.hostname = packagerHost;
  return url.toString().replace(/\/$/, '');
}

function usableHost(host: string | undefined): string | null {
  const value = host?.trim();
  if (!value || isLoopbackHost(value)) {
    return null;
  }

  return value;
}

function isLoopbackHost(host: string): boolean {
  return host === 'localhost' || host === '127.0.0.1' || host === '::1';
}

function isLoopbackUrl(value: string): boolean {
  try {
    return isLoopbackHost(new URL(value).hostname);
  } catch {
    return false;
  }
}

type SourceCodeModule = {
  getConstants?: () => { scriptURL?: string | null };
  scriptURL?: string;
};

export function scriptUrlFromSourceCode(source: SourceCodeModule | null | undefined): string | null {
  const fromConstants = source?.getConstants?.()?.scriptURL;
  if (typeof fromConstants === 'string' && fromConstants.length > 0) {
    return fromConstants;
  }

  return typeof source?.scriptURL === 'string' && source.scriptURL.length > 0 ? source.scriptURL : null;
}

function readExpoHost(): string | null {
  try {
    const constants = require('expo-constants').default as {
      expoConfig?: { hostUri?: string } | null;
      experienceUrl?: string;
      linkingUri?: string;
    };
    return constants.expoConfig?.hostUri ?? constants.experienceUrl ?? constants.linkingUri ?? null;
  } catch {
    return null;
  }
}

function readPackagerHost(): string | null {
  const fromExpo = hostFromPackagerAddress(readExpoHost());
  if (fromExpo) {
    return fromExpo;
  }

  try {
    const turbo = TurboModuleRegistry.get<SourceCodeModule>('SourceCode');
    const legacy = NativeModules.SourceCode as SourceCodeModule | undefined;
    return hostFromScriptUrl(scriptUrlFromSourceCode(turbo) ?? scriptUrlFromSourceCode(legacy));
  } catch {
    return null;
  }
}

export const apiBaseUrl = resolveApiBaseUrl(process.env.EXPO_PUBLIC_API_URL, readPackagerHost());

if (typeof __DEV__ !== 'undefined' && __DEV__ && process.env.NODE_ENV !== 'test') {
  console.log(`API base URL ${apiBaseUrl}`);
}

export const apiRoot = `${apiBaseUrl}/api/v1`;
